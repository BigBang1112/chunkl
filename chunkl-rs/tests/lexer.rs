use chunkl::{Lexer, TokenKind};

#[test]
fn tokenizes_literals_comments_and_positions() {
    let tokens = Lexer::new("  byte<Direction> Value = 0x2A // value\n").tokenize();
    assert_eq!(tokens[0].kind, TokenKind::Identifier);
    assert_eq!(tokens[0].text, "byte");
    assert_eq!(tokens[0].leading_spaces, 2);
    assert_eq!(tokens[0].source_offset, 2);
    assert_eq!(tokens[1].kind, TokenKind::LessThan);
    assert!(tokens
        .iter()
        .any(|token| token.kind == TokenKind::HexLiteral));
    assert!(tokens.iter().any(|token| token.kind == TokenKind::Comment));
    assert_eq!(tokens.last().unwrap().kind, TokenKind::EndOfFile);
}

#[test]
fn tokenizes_all_multi_character_operators() {
    let kinds: Vec<_> = Lexer::new("== != <= >= << >> :: .. && ||")
        .tokenize()
        .into_iter()
        .map(|token| token.kind)
        .collect();
    assert_eq!(
        &kinds[..10],
        &[
            TokenKind::EqualsEquals,
            TokenKind::BangEquals,
            TokenKind::LessThanEquals,
            TokenKind::GreaterThanEquals,
            TokenKind::LessLess,
            TokenKind::GreaterGreater,
            TokenKind::ColonColon,
            TokenKind::DotDot,
            TokenKind::AmpersandAmpersand,
            TokenKind::PipePipe,
        ]
    );
}
