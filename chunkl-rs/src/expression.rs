use crate::Diagnostic;
use crate::ast::*;
use crate::lexer::{Lexer, Token, TokenKind};

pub fn parse_expression(text: &str) -> Expression {
    parse_expression_checked(text).0
}

/// Parses an expression and reports malformed or unconsumed tokens.
pub fn parse_expression_checked(text: &str) -> (Expression, Vec<Diagnostic>) {
    let tokens = Lexer::new(text).tokenize();
    let mut parser = ExprParser::new(&tokens);
    let expr = parser.parse_expr();
    if !parser.at_end() {
        parser.error("Unexpected token after expression");
    }
    (expr, parser.diagnostics)
}

struct ExprParser<'a> {
    tokens: &'a [Token],
    pos: usize,
    diagnostics: Vec<Diagnostic>,
}

impl<'a> ExprParser<'a> {
    fn new(tokens: &'a [Token]) -> Self {
        Self {
            tokens,
            pos: 0,
            diagnostics: Vec::new(),
        }
    }

    fn current(&self) -> &Token {
        &self.tokens[self.pos.min(self.tokens.len() - 1)]
    }

    fn kind(&self) -> TokenKind {
        self.current().kind
    }

    fn advance(&mut self) -> String {
        let text = self.tokens[self.pos.min(self.tokens.len() - 1)]
            .text
            .clone();
        if self.pos < self.tokens.len() {
            self.pos += 1;
        }
        text
    }

    fn at_end(&self) -> bool {
        matches!(
            self.kind(),
            TokenKind::EndOfFile | TokenKind::Newline | TokenKind::Comment
        )
    }

    fn parse_expr(&mut self) -> Expression {
        self.parse_logical_or()
    }

    fn parse_logical_or(&mut self) -> Expression {
        let mut left = self.parse_logical_and();
        while !self.at_end() && (self.kind() == TokenKind::PipePipe || self.keyword("or")) {
            self.advance();
            let right = self.parse_logical_and();
            left = Expression::Binary {
                left: Box::new(left),
                operator: BinaryOp::LogicalOr,
                right: Box::new(right),
            };
        }
        left
    }

    fn parse_logical_and(&mut self) -> Expression {
        let mut left = self.parse_equality();
        while !self.at_end()
            && (self.kind() == TokenKind::AmpersandAmpersand || self.keyword("and"))
        {
            self.advance();
            let right = self.parse_equality();
            left = Expression::Binary {
                left: Box::new(left),
                operator: BinaryOp::LogicalAnd,
                right: Box::new(right),
            };
        }
        left
    }

    fn parse_equality(&mut self) -> Expression {
        let mut left = self.parse_comparison();
        while !self.at_end() {
            let op = match self.kind() {
                TokenKind::EqualsEquals => BinaryOp::Equal,
                TokenKind::BangEquals => BinaryOp::NotEqual,
                _ => break,
            };
            self.advance();
            let right = self.parse_comparison();
            left = Expression::Binary {
                left: Box::new(left),
                operator: op,
                right: Box::new(right),
            };
        }
        left
    }

    fn parse_comparison(&mut self) -> Expression {
        let mut left = self.parse_bitwise_or();
        while !self.at_end() {
            if self.keyword("is") {
                self.advance();
                left = Expression::PatternTest {
                    value: Box::new(left),
                    pattern: self.parse_pattern_or(),
                };
                continue;
            }
            let op = match self.kind() {
                TokenKind::LessThan => BinaryOp::LessThan,
                TokenKind::GreaterThan => BinaryOp::GreaterThan,
                TokenKind::LessThanEquals => BinaryOp::LessOrEqual,
                TokenKind::GreaterThanEquals => BinaryOp::GreaterOrEqual,
                _ => break,
            };
            self.advance();
            let right = self.parse_bitwise_or();
            left = Expression::Binary {
                left: Box::new(left),
                operator: op,
                right: Box::new(right),
            };
        }
        left
    }

    fn keyword(&self, text: &str) -> bool {
        self.kind() == TokenKind::Identifier && self.current().text == text
    }

    fn error(&mut self, message: &str) {
        self.diagnostics
            .push(Diagnostic::error(message, self.current().position));
    }

    fn can_start_pattern(&self, offset: usize) -> bool {
        let Some(token) = self.tokens.get(self.pos + offset) else {
            return false;
        };
        match token.kind {
            TokenKind::IntLiteral | TokenKind::HexLiteral | TokenKind::StringLiteral => true,
            TokenKind::OpenParen => self.can_start_pattern(offset + 1),
            TokenKind::Identifier if token.text == "not" => self.can_start_pattern(offset + 1),
            TokenKind::Identifier => {
                matches!(token.text.as_str(), "true" | "false" | "null" | "empty")
                    || self
                        .tokens
                        .get(self.pos + offset + 1)
                        .is_some_and(|t| t.kind == TokenKind::ColonColon)
            }
            _ => false,
        }
    }

    fn parse_pattern_or(&mut self) -> Pattern {
        let mut left = self.parse_pattern_and();
        while self.keyword("or") && self.can_start_pattern(1) {
            self.advance();
            left = Pattern::Or(Box::new(left), Box::new(self.parse_pattern_and()));
        }
        left
    }

    fn parse_pattern_and(&mut self) -> Pattern {
        let mut left = self.parse_pattern_not();
        while self.keyword("and") && self.can_start_pattern(1) {
            self.advance();
            left = Pattern::And(Box::new(left), Box::new(self.parse_pattern_not()));
        }
        left
    }

    fn parse_pattern_not(&mut self) -> Pattern {
        if self.keyword("not") {
            self.advance();
            return Pattern::Not(Box::new(self.parse_pattern_not()));
        }
        if self.kind() == TokenKind::OpenParen {
            self.advance();
            let inner = self.parse_pattern_or();
            self.close_paren();
            return Pattern::Parenthesized(Box::new(inner));
        }
        if !self.can_start_pattern(0) {
            self.error("Expected a literal or scoped identifier pattern");
        }
        Pattern::Constant(Box::new(self.parse_primary()))
    }

    fn close_paren(&mut self) {
        if self.kind() == TokenKind::CloseParen {
            self.advance();
        } else {
            self.error("Expected ')'");
        }
    }

    fn parse_bitwise_or(&mut self) -> Expression {
        let mut left = self.parse_bitwise_xor();
        while !self.at_end() && self.kind() == TokenKind::Pipe {
            self.advance();
            let right = self.parse_bitwise_xor();
            left = Expression::Binary {
                left: Box::new(left),
                operator: BinaryOp::BitwiseOr,
                right: Box::new(right),
            };
        }
        left
    }

    fn parse_bitwise_xor(&mut self) -> Expression {
        let mut left = self.parse_bitwise_and();
        while !self.at_end() && self.kind() == TokenKind::Caret {
            self.advance();
            let right = self.parse_bitwise_and();
            left = Expression::Binary {
                left: Box::new(left),
                operator: BinaryOp::BitwiseXor,
                right: Box::new(right),
            };
        }
        left
    }

    fn parse_bitwise_and(&mut self) -> Expression {
        let mut left = self.parse_shift();
        while !self.at_end() && self.kind() == TokenKind::Ampersand {
            self.advance();
            let right = self.parse_shift();
            left = Expression::Binary {
                left: Box::new(left),
                operator: BinaryOp::BitwiseAnd,
                right: Box::new(right),
            };
        }
        left
    }

    fn parse_shift(&mut self) -> Expression {
        let mut left = self.parse_additive();
        while !self.at_end() {
            let op = match self.kind() {
                TokenKind::LessLess => BinaryOp::ShiftLeft,
                TokenKind::GreaterGreater => BinaryOp::ShiftRight,
                _ => break,
            };
            self.advance();
            let right = self.parse_additive();
            left = Expression::Binary {
                left: Box::new(left),
                operator: op,
                right: Box::new(right),
            };
        }
        left
    }

    fn parse_additive(&mut self) -> Expression {
        let mut left = self.parse_multiplicative();
        while !self.at_end() {
            let op = match self.kind() {
                TokenKind::Plus => BinaryOp::Add,
                TokenKind::Minus => BinaryOp::Subtract,
                _ => break,
            };
            self.advance();
            let right = self.parse_multiplicative();
            left = Expression::Binary {
                left: Box::new(left),
                operator: op,
                right: Box::new(right),
            };
        }
        left
    }

    fn parse_multiplicative(&mut self) -> Expression {
        let mut left = self.parse_unary();
        while !self.at_end() {
            let op = match self.kind() {
                TokenKind::Star => BinaryOp::Multiply,
                TokenKind::Slash => BinaryOp::Divide,
                _ => break,
            };
            self.advance();
            let right = self.parse_unary();
            left = Expression::Binary {
                left: Box::new(left),
                operator: op,
                right: Box::new(right),
            };
        }
        left
    }

    fn parse_unary(&mut self) -> Expression {
        if self.at_end() {
            return self.parse_primary();
        }
        let op = match self.kind() {
            TokenKind::Bang => Some(UnaryOp::Not),
            TokenKind::Tilde => Some(UnaryOp::BitwiseNot),
            TokenKind::Minus => Some(UnaryOp::Negate),
            _ => None,
        };
        if let Some(op) = op {
            self.advance();
            let operand = self.parse_unary();
            Expression::Unary {
                operator: op,
                operand: Box::new(operand),
            }
        } else {
            self.parse_primary()
        }
    }

    fn parse_primary(&mut self) -> Expression {
        match self.kind() {
            TokenKind::OpenParen => {
                self.advance(); // (
                let first = self.parse_expr();
                if self.kind() == TokenKind::Comma {
                    // Tuple
                    let mut elements = vec![first];
                    while self.kind() == TokenKind::Comma {
                        self.advance();
                        elements.push(self.parse_expr());
                    }
                    self.close_paren();
                    Expression::Tuple(elements)
                } else {
                    self.close_paren();
                    Expression::Parenthesized(Box::new(first))
                }
            }
            TokenKind::IntLiteral => {
                let text = self.advance();
                if text.contains('.') || text.ends_with('f') || text.ends_with('F') {
                    Expression::Literal(Literal::Float(text))
                } else {
                    Expression::Literal(Literal::Integer(text))
                }
            }
            TokenKind::HexLiteral => {
                let text = self.advance();
                Expression::Literal(Literal::Hex(text))
            }
            TokenKind::StringLiteral => {
                let token = self.current();
                let slash_count = token
                    .text
                    .as_bytes()
                    .iter()
                    .rev()
                    .skip(1)
                    .take_while(|b| **b == b'\\')
                    .count();
                if token.text.len() < 2 || !token.text.ends_with('"') || slash_count % 2 != 0 {
                    self.error("Unterminated string literal");
                }
                let text = self.advance();
                Expression::Literal(Literal::String(text))
            }
            TokenKind::Identifier => {
                let text = self.advance();
                match text.as_str() {
                    "true" => Expression::Literal(Literal::Bool(true)),
                    "false" => Expression::Literal(Literal::Bool(false)),
                    "null" => Expression::Literal(Literal::Null),
                    "empty" => Expression::Literal(Literal::Empty),
                    _ => {
                        // Check for scoped identifier: Ident::Ident
                        if self.kind() == TokenKind::ColonColon {
                            self.advance(); // ::
                            let member = if self.kind() == TokenKind::Identifier {
                                self.advance()
                            } else {
                                self.error("Expected an enum or flags member after '::'");
                                String::new()
                            };
                            Expression::ScopedIdentifier {
                                qualifier: text,
                                name: member,
                            }
                        } else {
                            Expression::Identifier(text)
                        }
                    }
                }
            }
            _ => {
                self.error("Expected an expression");
                let text = if self.at_end() {
                    String::new()
                } else {
                    self.advance()
                };
                Expression::Identifier(text)
            }
        }
    }
}
