use chunkl::{
    BinaryOp, BodyStatement, Expression, Pattern, UnaryOp, analyze, parse_source, write,
    write_expression,
};

fn parse(source: &str) -> chunkl::ChunkLFile {
    let result = parse_source(source);
    assert!(result.success(), "{:?}", result.diagnostics);
    result.file.unwrap()
}

#[test]
fn new_syntax_preserves_source_order_and_semantics() {
    let file = parse(include_str!(
        "../../dotnet/ChunkL.Tests/Fixtures/spec_additions.chunkl"
    ));
    assert!(file.constructor.is_some());
    assert_eq!(file.properties.len(), 3);
    assert_eq!(file.properties[0].accessors.len(), 3);
    assert!(matches!(
        file.properties[0].setter().unwrap().body[0],
        BodyStatement::If(_)
    ));
    assert!(matches!(
        file.properties[2].setter().unwrap().body[0],
        BodyStatement::Switch(_)
    ));
    let BodyStatement::If(branch) = &file.archives[0].body[4] else {
        panic!()
    };
    let Expression::Binary {
        operator: BinaryOp::LogicalAnd,
        left,
        ..
    } = &branch.condition
    else {
        panic!()
    };
    let Expression::Parenthesized(inner) = left.as_ref() else {
        panic!()
    };
    assert!(matches!(
        inner.as_ref(),
        Expression::PatternTest {
            pattern: Pattern::Not(_),
            ..
        }
    ));
    let model = analyze(&file);
    assert!(model.success(), "{:?}", model.diagnostics);
    let flags = model
        .class
        .fields
        .iter()
        .find(|f| f.name == "Flags")
        .unwrap();
    assert_eq!(flags.ty.name, "int");
    assert_eq!(
        flags
            .declarations
            .iter()
            .map(|d| d.ty.name.as_str())
            .collect::<Vec<_>>(),
        ["short", "int"]
    );
    assert!(
        model
            .class
            .fields
            .iter()
            .find(|f| f.name == "Count")
            .unwrap()
            .is_default_skipped
    );
    let default_names = model
        .class
        .inline_defaults
        .iter()
        .map(|i| model.class.fields[*i].name)
        .collect::<Vec<_>>();
    assert_eq!(default_names, ["First", "Name", "Flags", "Later"]);
    assert_eq!(
        model
            .constructor_assignments
            .iter()
            .map(|a| a.target_name.as_str())
            .collect::<Vec<_>>(),
        ["IsGhost", "Count"]
    );
    assert!(
        model
            .archives
            .iter()
            .find(|a| a.declaration.name.as_deref() == Some("Key"))
            .unwrap()
            .requires_external_version
    );
    assert!(
        !model
            .archives
            .iter()
            .find(|a| a.declaration.name.as_deref() == Some("Versioned"))
            .unwrap()
            .requires_external_version
    );
    assert!(
        !model
            .archives
            .iter()
            .find(|a| a.declaration.name.as_deref() == Some("Derived"))
            .unwrap()
            .requires_external_version
    );

    let written = write(&file);
    assert!(written.find("archive\n").unwrap() < written.find("0x001").unwrap());
    assert!(written.contains("int[][3] Matrix"));
    let second = parse(&written);
    assert_eq!(write(&second), written);
    let model2 = analyze(&second);
    assert!(model2.success(), "{:?}", model2.diagnostics);
    assert_eq!(
        default_names,
        model2
            .class
            .inline_defaults
            .iter()
            .map(|i| model2.class.fields[*i].name)
            .collect::<Vec<_>>()
    );
    assert!(matches!(
        second.chunks[0].body.last(),
        Some(BodyStatement::Field(_))
    ));
}

#[test]
fn patterns_and_boolean_keywords_retain_full_meaning() {
    for (expression, expected) in [
        ("Name is null or empty", "Name is null or empty"),
        (
            "Name is not null and not empty",
            "Name is not null and not empty",
        ),
        ("Name is null or \"\"", "Name is null or \"\""),
        (
            "(Name is null or empty) and IsEnabled",
            "(Name is null or empty) && IsEnabled",
        ),
        (
            "IsEnabled or ForceReload and HasData",
            "IsEnabled || ForceReload && HasData",
        ),
        ("!true", "!true"),
        ("!!IsEnabled", "!!IsEnabled"),
        ("!IsEnabled or IsGhost", "!IsEnabled || IsGhost"),
        ("!(IsEnabled and HasData)", "!(IsEnabled && HasData)"),
        ("!(Name is null)", "!(Name is null)"),
        (
            "Name is null or empty and not null",
            "Name is null or empty and not null",
        ),
    ] {
        let file = parse(&format!(
            "Test 0x01000000\n0x001\n  if {expression}\n    int Data\n"
        ));
        let BodyStatement::If(branch) = &file.chunks[0].body[0] else {
            panic!()
        };
        assert_eq!(write_expression(&branch.condition), expected);
        assert_eq!(write(&parse(&write(&file))), write(&file));
    }
}

#[test]
fn logical_not_works_on_boolean_fields_and_conditions() {
    let file = parse(
        "Test 0x01000000\n0x001\n  bool IsEnabled = !false\n  if !IsEnabled\n    bool WasDisabled = !!IsEnabled\n  else if !(IsEnabled && true)\n    bool MaybeDisabled\n  assert !IsEnabled\n",
    );
    let BodyStatement::If(branch) = &file.chunks[0].body[1] else {
        panic!()
    };
    assert!(matches!(
        branch.condition,
        Expression::Unary {
            operator: UnaryOp::Not,
            ..
        }
    ));
    let model = analyze(&file);
    assert!(model.success(), "{:?}", model.diagnostics);
    let written = write(&file);
    assert_eq!(written, write(&parse(&written)));
}

#[test]
fn malformed_new_syntax_reports_diagnostics() {
    for declarations in [
        "constructor\nconstructor\n",
        "constructor (x)\n",
        "constructor\n  int Flags\n",
        "constructor\n  if true\n    Flags = 1\n",
        "property bool Flag = true\n  get = true\n",
        "property bool Flag (x)\n  get = true\n",
        "property bool Flag\n",
        "property bool Flag\n  get = true\n  get = false\n",
        "property bool Flag\n  get = true\n  set\n",
        "property bool Flag\n  set\n    // Only a comment.\n",
        "property bool Flag\n  set\n    loop 4\n      Flags = 1\n",
        "property bool Flag\n  set\n    version\n",
        "property bool Flag\n    get = true\n",
        "property bool Flag\n  get = Flags is not\n",
        "0x001\n  if Flags is Missing\n    int Data\n",
        "0x001\n  if (true\n    int Data\n",
        "0x001\n  if true nonsense\n    int Data\n",
        "property 42 Flag\n  get = true\n",
        "property string Name\n  get = \"unterminated\n",
        "0x001\n  if Kind is Direction::\n    int Data\n",
    ] {
        let result = parse_source(&format!("Test 0x01000000\n{declarations}"));
        assert!(!result.success(), "Accepted invalid source: {declarations}");
        assert!(!result.diagnostics.is_empty());
    }
}

#[test]
fn repeated_fields_resolve_or_report_conflicts() {
    for (fields, success, stored_type) in [
        ("short X\n  int X", true, "int"),
        ("uint X\n  long X", true, "long"),
        ("int X\n  uint X", false, "int"),
        ("int X = 1\n  int X = 2", false, "int"),
        ("int X = 1 + 2\n  int X = 1+2", true, "int"),
        ("string X = \"a b\"\n  string X = \"ab\"", false, "string"),
        ("int X\n  int? X", false, "int"),
        ("byte<Kind> X\n  int<Kind> X", false, "byte"),
        ("int[Count][] X\n  int[][Count] X", false, "int"),
    ] {
        let file = parse(&format!("Test 0x01000000\n0x001\n  {fields}\n"));
        let model = analyze(&file);
        assert_eq!(
            model.success(),
            success,
            "{fields}: {:?}",
            model.diagnostics
        );
        assert_eq!(model.class.fields.len(), 1);
        assert_eq!(model.class.fields[0].ty.name, stored_type);
    }
}

#[test]
fn archive_defaults_are_independent_of_class_constructor() {
    let file = parse(
        "Test 0x01000000\nconstructor\n  X = 10\narchive\n  int X = 1\narchive Named\n  string X = empty\n",
    );
    let model = analyze(&file);
    assert!(model.success(), "{:?}", model.diagnostics);
    assert!(model.class.inline_defaults.is_empty());
    let archive = model
        .archives
        .iter()
        .find(|a| a.declaration.name.as_deref() == Some("Named"))
        .unwrap();
    assert_eq!(archive.fields.inline_defaults.len(), 1);
    assert_eq!(
        archive.fields.fields[archive.fields.inline_defaults[0]]
            .ty
            .name,
        "string"
    );
}

#[test]
fn local_semantic_errors_are_separate_from_syntax() {
    for (declarations, message) in [
        (
            "property bool A\n  get = B\nproperty bool B\n  get = A\n",
            "Recursive",
        ),
        (
            "property int A\n  set\n    B = value\nproperty int B\n  set\n    A = value\n",
            "Recursive",
        ),
        (
            "property int A\n  get = 1\nconstructor\n  A = 2\n",
            "read-only",
        ),
        (
            "property int A\n  set\n    Flags = value\nproperty int B\n  get = A\n0x001\n  int Flags\n",
            "write-only",
        ),
        (
            "property int Flags\n  get = 1\n0x001\n  int Flags\n",
            "Duplicate",
        ),
        ("constructor\n  Missing = 1\n", "Unknown"),
        (
            "0x001\n  int Flags\n  if Flags is empty\n    int Data\n",
            "empty pattern",
        ),
        (
            "0x001 [Context.v13]\n  v1+\n    int Data\n",
            "preceding version",
        ),
        ("property bool Flag\n  get = 42\n", "incompatible"),
        ("property bool Flag\n  get = Missing\n", "Unknown"),
        (
            "property int P\n  set\n    if value is empty\n      Flags = 0\n0x001\n  int Flags\n",
            "empty pattern",
        ),
        (
            "0x001\n  int Flags\n  if Flags or true\n    int Data\n",
            "boolean operands",
        ),
        (
            "0x001\n  int Flags\n  if !Flags\n    int Data\n",
            "'!' requires a boolean operand",
        ),
        (
            "0x001\n  int[] Items\n  if Items is \"\"\n    int Data\n",
            "requires a string",
        ),
        (
            "archive Named\n  int Flags\n  if Flags is empty\n    int Data\n",
            "empty pattern",
        ),
        (
            "property int P\n  get = 1\narchive Named\n  if P == 1\n    int Data\n",
            "Class properties",
        ),
        (
            "property int P\n  get = 1\narchive Named\n  P = 2\n",
            "Class properties",
        ),
    ] {
        let file = parse(&format!("Test 0x01000000\n{declarations}"));
        let model = analyze(&file);
        assert!(!model.success(), "{declarations}");
        assert!(
            model
                .diagnostics
                .iter()
                .any(|d| d.message.contains(message)),
            "{:?}",
            model.diagnostics
        );
    }
}

#[test]
fn archive_version_requirements_respect_branches_and_returns() {
    let file = parse(
        "Test 0x01000000\narchive Complete\n  if Enabled\n    version\n  else\n    versionb\n  v1+\n    int Data\narchive Incomplete\n  if Enabled\n    version\n  v1+\n    int Data\narchive ReturnBranch\n  if Enabled\n    return\n  else\n    version\n  v1+\n    int Data\n",
    );
    let model = analyze(&file);
    assert!(model.success(), "{:?}", model.diagnostics);
    assert_eq!(
        model
            .archives
            .iter()
            .map(|a| a.requires_external_version)
            .collect::<Vec<_>>(),
        [false, true, false]
    );
}

#[test]
fn named_archive_fields_shadow_class_properties() {
    let file = parse(
        "Test 0x01000000\nproperty int P\n  get = 1\narchive Named\n  string P\n  P = \"text\"\n  if P is empty\n    int Data\n",
    );
    let model = analyze(&file);
    assert!(model.success(), "{:?}", model.diagnostics);
}

#[test]
fn grouped_defaults_and_escaped_strings_are_not_attributes_or_comments() {
    let file = parse(
        "Test 0x01000000\n0x001\n  int A = (B)\n  int B = 2\n  int C = (1, 2, 3)\n  string Path = \"C:\\\\\" // path\n  skip (A)\nproperty int P\n  get=(A)\n",
    );
    let BodyStatement::Field(a) = &file.chunks[0].body[0] else {
        panic!()
    };
    assert!(a.attributes.is_none());
    assert!(matches!(
        a.default_value,
        Some(Expression::Parenthesized(_))
    ));
    let BodyStatement::Field(c) = &file.chunks[0].body[2] else {
        panic!()
    };
    assert!(matches!(c.default_value, Some(Expression::Tuple(_))));
    let BodyStatement::Field(path) = &file.chunks[0].body[3] else {
        panic!()
    };
    assert_eq!(path.trailing_comment.as_ref().unwrap().text, "path");
    assert_eq!(write(&parse(&write(&file))), write(&file));
}

#[test]
fn analysis_uses_edited_default_expressions() {
    let mut file = parse("Test 0x01000000\n0x001\n  int X = 1\n  int X = 1\n");
    assert!(analyze(&file).success());
    let BodyStatement::Field(field) = &mut file.chunks[0].body[1] else {
        panic!()
    };
    field.default_value = Some(Expression::Literal(chunkl::Literal::Integer(
        "2".to_owned(),
    )));
    assert!(!analyze(&file).success());
}
