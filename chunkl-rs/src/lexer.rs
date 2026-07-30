use crate::SourcePosition;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum TokenKind {
    HexLiteral,
    IntLiteral,
    Identifier,
    StringLiteral,
    Dot,
    DotDot,
    Comma,
    Colon,
    ColonColon,
    Equals,
    EqualsEquals,
    BangEquals,
    LessThan,
    GreaterThan,
    LessThanEquals,
    GreaterThanEquals,
    LessLess,
    GreaterGreater,
    OpenParen,
    CloseParen,
    OpenBracket,
    CloseBracket,
    Plus,
    Minus,
    Star,
    Slash,
    Ampersand,
    AmpersandAmpersand,
    Pipe,
    PipePipe,
    Bang,
    Tilde,
    Caret,
    Question,
    Comment,
    Newline,
    EndOfFile,
    Unknown,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Token {
    pub kind: TokenKind,
    pub text: String,
    pub position: SourcePosition,
    pub leading_spaces: usize,
    pub source_offset: usize,
    pub source_length: usize,
}

pub struct Lexer<'a> {
    source: &'a str,
    offset: usize,
    line: usize,
    column: usize,
    leading_spaces: usize,
    at_line_start: bool,
}

impl<'a> Lexer<'a> {
    pub fn new(source: &'a str) -> Self {
        Self {
            source,
            offset: 0,
            line: 1,
            column: 1,
            leading_spaces: 0,
            at_line_start: true,
        }
    }

    pub fn tokenize(mut self) -> Vec<Token> {
        let mut tokens = Vec::new();
        while self.offset < self.source.len() {
            self.skip_horizontal_whitespace();
            if self.offset >= self.source.len() {
                break;
            }

            let start = self.offset;
            let position = SourcePosition::new(self.line, self.column);
            let indent = self.leading_spaces;
            let byte = self.current_byte();

            let (kind, end) = if byte == b'\r' || byte == b'\n' {
                let end = self.consume_newline();
                (TokenKind::Newline, end)
            } else if self.rest().starts_with("//") || byte == b'#' {
                (TokenKind::Comment, self.scan_to_newline())
            } else if byte == b'"' {
                (TokenKind::StringLiteral, self.scan_string())
            } else if self.rest().starts_with("0x") || self.rest().starts_with("0X") {
                (TokenKind::HexLiteral, self.scan_hex())
            } else if byte.is_ascii_digit() {
                (TokenKind::IntLiteral, self.scan_number())
            } else if is_identifier_start(byte) {
                (TokenKind::Identifier, self.scan_identifier())
            } else if let Some((kind, length)) = multi_char_token(self.rest()) {
                self.advance_bytes(length);
                (kind, self.offset)
            } else {
                let kind = single_char_token(byte).unwrap_or(TokenKind::Unknown);
                self.advance_bytes(1);
                (kind, self.offset)
            };

            tokens.push(Token {
                kind,
                text: self.source[start..end].to_owned(),
                position,
                leading_spaces: indent,
                source_offset: start,
                source_length: end - start,
            });
            if kind != TokenKind::Newline {
                self.at_line_start = false;
            }
        }

        tokens.push(Token {
            kind: TokenKind::EndOfFile,
            text: String::new(),
            position: SourcePosition::new(self.line, self.column),
            leading_spaces: self.leading_spaces,
            source_offset: self.offset,
            source_length: 0,
        });
        tokens
    }

    fn rest(&self) -> &str {
        &self.source[self.offset..]
    }

    fn current_byte(&self) -> u8 {
        self.source.as_bytes()[self.offset]
    }

    fn skip_horizontal_whitespace(&mut self) {
        while self.offset < self.source.len() {
            match self.current_byte() {
                b' ' => {
                    if self.at_line_start {
                        self.leading_spaces += 1;
                    }
                    self.advance_bytes(1);
                }
                b'\t' => {
                    if self.at_line_start {
                        self.leading_spaces += 4;
                    }
                    self.advance_bytes(1);
                }
                _ => break,
            }
        }
    }

    fn advance_bytes(&mut self, count: usize) {
        self.offset += count;
        self.column += count;
    }

    fn consume_newline(&mut self) -> usize {
        if self.rest().starts_with("\r\n") {
            self.offset += 2;
        } else {
            self.offset += 1;
        }
        self.line += 1;
        self.column = 1;
        self.leading_spaces = 0;
        self.at_line_start = true;
        self.offset
    }

    fn scan_to_newline(&mut self) -> usize {
        while self.offset < self.source.len() && !matches!(self.current_byte(), b'\r' | b'\n') {
            self.advance_bytes(1);
        }
        self.offset
    }

    fn scan_string(&mut self) -> usize {
        self.advance_bytes(1);
        let mut escaped = false;
        while self.offset < self.source.len() {
            let byte = self.current_byte();
            if matches!(byte, b'\r' | b'\n') {
                break;
            }
            self.advance_bytes(1);
            if byte == b'"' && !escaped {
                break;
            }
            escaped = byte == b'\\' && !escaped;
            if byte != b'\\' {
                escaped = false;
            }
        }
        self.offset
    }

    fn scan_hex(&mut self) -> usize {
        self.advance_bytes(2);
        while self.offset < self.source.len() && self.current_byte().is_ascii_hexdigit() {
            self.advance_bytes(1);
        }
        self.offset
    }

    fn scan_number(&mut self) -> usize {
        while self.offset < self.source.len()
            && (self.current_byte().is_ascii_digit()
                || matches!(self.current_byte(), b'.' | b'f' | b'F'))
        {
            if self.rest().starts_with("..") {
                break;
            }
            self.advance_bytes(1);
        }
        self.offset
    }

    fn scan_identifier(&mut self) -> usize {
        while self.offset < self.source.len() && is_identifier_continue(self.current_byte()) {
            self.advance_bytes(1);
        }
        self.offset
    }
}

fn is_identifier_start(byte: u8) -> bool {
    byte.is_ascii_alphabetic() || byte == b'_'
}

fn is_identifier_continue(byte: u8) -> bool {
    byte.is_ascii_alphanumeric() || byte == b'_'
}

fn multi_char_token(source: &str) -> Option<(TokenKind, usize)> {
    [
        ("==", TokenKind::EqualsEquals),
        ("!=", TokenKind::BangEquals),
        ("<=", TokenKind::LessThanEquals),
        (">=", TokenKind::GreaterThanEquals),
        ("<<", TokenKind::LessLess),
        (">>", TokenKind::GreaterGreater),
        ("::", TokenKind::ColonColon),
        ("..", TokenKind::DotDot),
        ("&&", TokenKind::AmpersandAmpersand),
        ("||", TokenKind::PipePipe),
    ]
    .into_iter()
    .find_map(|(text, kind)| source.starts_with(text).then_some((kind, text.len())))
}

fn single_char_token(byte: u8) -> Option<TokenKind> {
    Some(match byte {
        b'.' => TokenKind::Dot,
        b',' => TokenKind::Comma,
        b':' => TokenKind::Colon,
        b'=' => TokenKind::Equals,
        b'<' => TokenKind::LessThan,
        b'>' => TokenKind::GreaterThan,
        b'(' => TokenKind::OpenParen,
        b')' => TokenKind::CloseParen,
        b'[' => TokenKind::OpenBracket,
        b']' => TokenKind::CloseBracket,
        b'+' => TokenKind::Plus,
        b'-' => TokenKind::Minus,
        b'*' => TokenKind::Star,
        b'/' => TokenKind::Slash,
        b'&' => TokenKind::Ampersand,
        b'|' => TokenKind::Pipe,
        b'!' => TokenKind::Bang,
        b'~' => TokenKind::Tilde,
        b'^' => TokenKind::Caret,
        b'?' => TokenKind::Question,
        _ => return None,
    })
}
