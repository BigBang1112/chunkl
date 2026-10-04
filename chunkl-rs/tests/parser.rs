use std::path::PathBuf;

use chunkl::{parse_file, parse_source, write, BodyStatement, VersionConditionKind};

fn fixture(name: &str) -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("..")
        .join("dotnet")
        .join("ChunkL.Tests")
        .join("Fixtures")
        .join(name)
}

#[test]
fn parses_minimal_file() {
    let result = parse_file(fixture("minimal.chunkl")).unwrap();
    assert!(result.success(), "{:?}", result.diagnostics);
    let file = result.file.unwrap();
    assert_eq!(file.header.class_name, "CGameMinimal");
    assert_eq!(file.header.class_id, "0x03000000");
    assert_eq!(file.chunks.len(), 1);
    let BodyStatement::Field(field) = &file.chunks[0].body[0] else {
        panic!()
    };
    assert_eq!(field.ty.name, "int");
    assert_eq!(field.name.as_deref(), Some("Value"));
}

#[test]
fn parses_nested_control_flow_and_type_modifiers() {
    let source = r#"TestClass 0x01000000

0x001
  version
  v3..7
    int?[Count] Values
  if Kind == 1
    loop 4
      byte<EKind> Value
  else
    return
"#;
    let result = parse_source(source);
    assert!(result.success(), "{:?}", result.diagnostics);
    let body = &result.file.unwrap().chunks[0].body;
    let BodyStatement::VersionCondition(condition) = &body[1] else {
        panic!()
    };
    assert_eq!(condition.kind, VersionConditionKind::Range);
    assert_eq!(condition.version_end, Some(7));
    let BodyStatement::Field(field) = &condition.body[0] else {
        panic!()
    };
    assert!(field.ty.is_nullable);
    assert_eq!(field.ty.fixed_array_count.as_deref(), Some("Count"));
    let BodyStatement::If(statement) = &body[2] else {
        panic!()
    };
    assert!(statement.else_clause.is_some());
}

#[test]
fn parses_expression_array_counts_and_while() {
    let source = r#"TestClass 0x01000000

0x001
  int Width
  int Height
  int[Header.Count] FromHeader
  int[Width * Height] Pixels
  short[(Width + 1) * 2] Samples
  byte HasNext
  while HasNext != 0 // sentinel
    int ItemType
    byte HasNext
"#;
    let result = parse_source(source);
    assert!(result.success(), "{:?}", result.diagnostics);
    let file = result.file.unwrap();
    let body = &file.chunks[0].body;
    let BodyStatement::Field(from_header) = &body[2] else {
        panic!()
    };
    assert_eq!(
        from_header.ty.fixed_array_count.as_deref(),
        Some("Header.Count")
    );
    let BodyStatement::Field(pixels) = &body[3] else {
        panic!()
    };
    assert_eq!(
        pixels.ty.fixed_array_count.as_deref(),
        Some("Width * Height")
    );
    assert_eq!(pixels.name.as_deref(), Some("Pixels"));
    let BodyStatement::Field(samples) = &body[4] else {
        panic!()
    };
    assert_eq!(
        samples.ty.fixed_array_count.as_deref(),
        Some("(Width + 1) * 2")
    );
    let BodyStatement::While(statement) = &body[6] else {
        panic!()
    };
    assert_eq!(statement.body.len(), 2);
    assert_eq!(
        statement.trailing_comment.as_ref().map(|c| c.text.as_str()),
        Some("sentinel")
    );

    let generated = write(&file);
    assert!(generated.contains("int[Header.Count] FromHeader"));
    assert!(generated.contains("int[Width * Height] Pixels"));
    assert!(generated.contains("while HasNext != 0 // sentinel"));
    let reparsed = parse_source(&generated);
    assert!(reparsed.success(), "{:?}", reparsed.diagnostics);
    assert_eq!(write(&reparsed.file.unwrap()), generated);
}

#[test]
fn parses_version_condition_attributes() {
    let source = r#"TestClass 0x01000000

0x001
  version
  v5+ (new_in: TM2020)
    int Value
"#;
    let result = parse_source(source);
    assert!(result.success(), "{:?}", result.diagnostics);
    let body = &result.file.unwrap().chunks[0].body;
    let BodyStatement::VersionCondition(condition) = &body[1] else {
        panic!()
    };
    let attributes = condition.attributes.as_ref().expect("attributes");
    assert_eq!(attributes.entries[0].name, "new_in");
    assert_eq!(attributes.entries[0].value.as_deref(), Some("TM2020"));
}

#[test]
fn all_reference_fixtures_round_trip_to_the_same_ast() {
    for name in [
        "minimal.chunkl",
        "full_example.chunkl",
        "control_flow.chunkl",
        "control_flow_advanced.chunkl",
        "enums_flags.chunkl",
        "archives.chunkl",
        "chunk_attributes.chunkl",
        "type_modifiers.chunkl",
        "nested_types.chunkl",
    ] {
        let first = parse_file(fixture(name)).unwrap();
        assert!(first.success(), "{name}: {:?}", first.diagnostics);
        let first_file = first.file.unwrap();
        let generated = write(&first_file);
        let second = parse_source(&generated);
        assert!(
            second.success(),
            "{name}: {:?}\n{generated}",
            second.diagnostics
        );
        assert_eq!(write(&second.file.unwrap()), generated, "fixture {name}");
    }
}

#[test]
fn malformed_header_reports_an_error() {
    let result = parse_source("NotAHeader\n");
    assert!(!result.success());
    assert_eq!(result.diagnostics.len(), 1);
}

#[test]
fn parses_nested_types_in_fields_archives_and_properties() {
    let result = parse_file(fixture("nested_types.chunkl")).unwrap();
    assert!(result.success(), "{:?}", result.diagnostics);
    let file = result.file.unwrap();
    let fields: Vec<_> = file.chunks[0]
        .body
        .iter()
        .filter_map(|statement| match statement {
            BodyStatement::Field(field) => Some(field),
            _ => None,
        })
        .collect();
    let spawn = fields[1];
    assert_eq!(spawn.ty.name, "CGameCtnMacroBlockInfo.BlockSpawn");
    assert_eq!(spawn.name.as_deref(), Some("Spawn"));
    assert!(spawn.default_value.is_some());
    assert!(spawn.trailing_comment.is_some());
    let optional = fields[2];
    assert_eq!(optional.ty.name, spawn.ty.name);
    assert!(optional.ty.is_nullable);
    assert_eq!(optional.ty.array_dimensions, 1);
    assert_eq!(
        optional.attributes.as_ref().unwrap().entries[0].name,
        "external"
    );
    assert_eq!(fields[3].ty.name, spawn.ty.name);
    assert_eq!(fields[3].ty.fixed_array_count.as_deref(), Some("Count"));
    let nested = fields[4];
    assert_eq!(nested.ty.name, "Outer.Inner.Leaf");
    assert!(nested.ty.chunk_preference);
    assert!(nested.ty.is_nullable);
    assert_eq!(nested.ty.array_dimensions, 2);
    let cast = fields[5];
    assert_eq!(cast.ty.name, "Outer.Inner.Leaf");
    assert_eq!(
        cast.ty
            .cast_target
            .as_ref()
            .unwrap()
            .qualifying_type
            .as_deref(),
        Some("Other.Inner")
    );
    assert_eq!(cast.ty.cast_target.as_ref().unwrap().name, "Target");
    assert!(cast.ty.chunk_preference);
    assert!(cast.ty.is_nullable);
    assert_eq!(cast.ty.array_dimensions, 1);
    assert_eq!(
        fields[6]
            .ty
            .cast_target
            .as_ref()
            .unwrap()
            .qualifying_type
            .as_deref(),
        Some("Outer.Inner")
    );
    assert_eq!(fields[6].ty.cast_target.as_ref().unwrap().name, "Direction");
    assert_eq!(fields[7].ty.name, spawn.ty.name);
    assert!(fields[7].name.is_none());
    let BodyStatement::Field(archived) = &file.archives[0].body[0] else {
        panic!()
    };
    assert_eq!(archived.ty.name, spawn.ty.name);
    let BodyStatement::Field(leaf) = &file.archives[0].body[1] else {
        panic!()
    };
    assert_eq!(leaf.ty.name, "Outer._Inner.Leaf2");
    assert_eq!(file.properties[0].ty.name, spawn.ty.name);
    assert_eq!(file.properties[1].ty.name, optional.ty.name);
    assert!(file.properties[1].ty.is_nullable);
    assert_eq!(file.properties[1].ty.array_dimensions, 1);

    let written = write(&file);
    assert!(written.contains("CGameCtnMacroBlockInfo.BlockSpawn Spawn = empty"));
    assert!(written.contains("Outer.Inner.Leaf<Other.Inner.Target>*?[] CastItems"));
    assert!(written.contains("property CGameCtnMacroBlockInfo.BlockSpawn CurrentSpawn"));
    let reparsed = parse_source(&written);
    assert!(reparsed.success(), "{:?}", reparsed.diagnostics);
    assert_eq!(write(&reparsed.file.unwrap()), written);
}

#[test]
fn rejects_invalid_nested_type_paths() {
    for ty in [
        ".Outer",
        "Outer.",
        "Outer..Inner",
        "Outer.123",
        "Outer. Inner",
        "Outer .Inner",
    ] {
        for declaration in [
            format!("0x001\n  {ty} Value\n"),
            format!("property {ty} Value\n  get = empty\n"),
        ] {
            let result = parse_source(&format!("TestClass 0x01000000\n{declaration}"));
            assert!(!result.success(), "{ty} should be rejected");
        }
    }
}
