use chunkl::{
    BodyStatement, Expression, analyze, analyze_for_game, parse_source, write, write_expression,
};

fn parse(source: &str) -> chunkl::ChunkLFile {
    let result = parse_source(source);
    assert!(result.success(), "{:?}", result.diagnostics);
    result.file.unwrap()
}

#[test]
fn defaults_round_trip_and_select_across_scopes() {
    let file = parse(include_str!(
        "../../dotnet/ChunkL.Tests/Fixtures/game_defaults.chunkl"
    ));
    let written = write(&file);
    assert!(written.contains("version = 1 (optional) [TMSX = 5]"));
    assert!(written.contains("int Shared = 1 (optional, name: Shared) [TMSX = 5, tmsx = 6]"));
    assert!(written.contains("int (optional) [TMSX = 3]"));
    let second = parse(&written);
    for current in [&file, &second] {
        let fields: Vec<_> = current.chunks[0]
            .body
            .iter()
            .filter_map(|s| match s {
                BodyStatement::Field(f) => Some(f),
                _ => None,
            })
            .collect();
        assert_eq!(write_expression(&fields[0].game_defaults[0].value), "5");
        assert_eq!(
            write_expression(fields[0].default_for_game(Some("TMSX")).unwrap()),
            "5"
        );
        assert_eq!(
            write_expression(fields[0].default_for_game(None).unwrap()),
            "1"
        );
        assert_eq!(
            write_expression(
                fields
                    .last()
                    .unwrap()
                    .default_for_game(Some("TMSX"))
                    .unwrap()
            ),
            "3"
        );
        assert!(
            fields
                .last()
                .unwrap()
                .default_for_game(Some("Other"))
                .is_none()
        );
        assert!(fields.last().unwrap().name.is_none());
        assert_eq!(fields.last().unwrap().ty.array_dimensions, 0);
        assert!(matches!(
            fields[6].game_defaults[0].value,
            Expression::Tuple(_)
        ));
        assert_eq!(fields[8].ty.fixed_array_count.as_deref(), Some("Count"));
        assert_eq!(fields[5].trailing_comment.as_ref().unwrap().text, "comment");

        let fallback = analyze(current);
        let game = analyze_for_game(current, "TMSX");
        assert!(game.success(), "{:?}", game.diagnostics);
        let value = |model: &chunkl::SemanticModel<'_>, name: &str| {
            model
                .class
                .fields
                .iter()
                .find(|f| f.name == name)
                .unwrap()
                .default_value
                .map(write_expression)
        };
        assert_eq!(value(&fallback, "Shared").as_deref(), Some("1"));
        assert!(
            !fallback
                .class
                .inline_defaults
                .iter()
                .any(|i| fallback.class.fields[*i].name == "Only")
        );
        assert_eq!(value(&game, "Shared").as_deref(), Some("5"));
        assert_eq!(
            write_expression(
                game.archives[0]
                    .fields
                    .fields
                    .iter()
                    .find(|f| f.name == "Shared")
                    .unwrap()
                    .default_value
                    .unwrap()
            ),
            "12"
        );
        assert!(
            !game
                .class
                .inline_defaults
                .iter()
                .any(|i| game.class.fields[*i].name == "Skipped")
        );
        assert_eq!(
            game.class
                .fields
                .iter()
                .find(|f| f.name == "Shared")
                .unwrap()
                .ty
                .name,
            "int"
        );
        let modern = analyze_for_game(current, "TM2020");
        let names: Vec<_> = modern
            .class
            .inline_defaults
            .iter()
            .map(|i| modern.class.fields[*i].name)
            .collect();
        assert!(
            names.iter().position(|n| *n == "First") < names.iter().position(|n| *n == "Shared")
        );
        assert_eq!(value(&modern, "Shared").as_deref(), Some("8"));
        assert_eq!(
            value(&analyze_for_game(current, "tmsx"), "Shared").as_deref(),
            Some("6")
        );
        assert_eq!(
            value(&analyze_for_game(current, "Other"), "Shared").as_deref(),
            Some("1")
        );
    }
}

#[test]
fn rejects_malformed_game_defaults() {
    for declaration in [
        "int Value []",
        "int Value [TMSX]",
        "int Value [TMSX =]",
        "int Value [TMSX = 5,]",
        "int Value [TMSX = 5, TMSX = 6]",
        "int Value [TM.v1 = 5]",
        "int Value [TMSX = 5",
        "int Value [TMSX = 5] (flag)",
        "int Value = 0 [TMSX = 5] (optional, name: Value)",
        "version = 1 [TMSX = 5] (optional)",
        "int [TMSX = 5] (optional)",
        "int Value [TMSX = 5] [TM2020 = 6]",
        "Value = 1 [TMSX = 5]",
        "return [TMSX = 5]",
        "throw [TMSX = 5]",
        "block [TMSX = 5]",
        "v1+ [TMSX = 5]",
    ] {
        let result = parse_source(&format!("Test 0x01000000\n0x001\n  {declaration}\n"));
        assert!(!result.success(), "{declaration}");
    }
}

#[test]
fn repeated_defaults_compare_syntax_and_respect_edits() {
    let mut file = parse(
        "Test 0x01000000\n0x001\n  int Value [TMSX = (1 + 2)]\n0x002\n  int Value [TMSX = (1+2), TM2020 = 9]\n",
    );
    assert!(analyze_for_game(&file, "TMSX").success());
    let BodyStatement::Field(field) = &mut file.chunks[1].body[0] else {
        panic!()
    };
    field.game_defaults[0].value = Expression::Literal(chunkl::Literal::Integer("4".to_owned()));
    assert!(
        analyze(&file)
            .diagnostics
            .iter()
            .any(|d| d.message.contains("Conflicting game defaults"))
    );
}

#[test]
fn game_expressions_receive_semantic_validation() {
    let file = parse("Test 0x01000000\n0x001\n  bool Value [TMSX = 1 and true]\n");
    assert!(
        analyze(&file)
            .diagnostics
            .iter()
            .any(|d| d.message.contains("boolean operands"))
    );
}

#[test]
fn game_labels_are_open_and_defaults_use_all_expression_forms() {
    let file = parse(
        "Test 0x01000000\n0x001\n  int? Value = null [2020_Custom = -5, Other = Count + 1]\n  int Count = 1\n  string Name [Custom = empty]\n",
    );
    assert!(analyze_for_game(&file, "2020_Custom").success());
    let BodyStatement::Field(field) = &file.chunks[0].body[0] else {
        panic!()
    };
    assert_eq!(
        write_expression(field.default_for_game(Some("2020_Custom")).unwrap()),
        "-5"
    );
}
