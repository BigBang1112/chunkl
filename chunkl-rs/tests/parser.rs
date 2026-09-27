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
    let BodyStatement::Field(pixels) = &body[2] else {
        panic!()
    };
    assert_eq!(
        pixels.ty.fixed_array_count.as_deref(),
        Some("Width * Height")
    );
    assert_eq!(pixels.name.as_deref(), Some("Pixels"));
    let BodyStatement::Field(samples) = &body[3] else {
        panic!()
    };
    assert_eq!(
        samples.ty.fixed_array_count.as_deref(),
        Some("(Width + 1) * 2")
    );
    let BodyStatement::While(statement) = &body[5] else {
        panic!()
    };
    assert_eq!(statement.body.len(), 2);
    assert_eq!(
        statement.trailing_comment.as_ref().map(|c| c.text.as_str()),
        Some("sentinel")
    );

    let generated = write(&file);
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
