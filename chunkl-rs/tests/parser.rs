use std::path::PathBuf;

use chunkl::{parse_file, parse_source, write, BodyStatement, VersionConditionKind};

fn fixture(name: &str) -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("..")
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
