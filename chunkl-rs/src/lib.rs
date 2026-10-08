//! Parser, syntax tree, lexer, and writer for the ChunkL language.

pub mod ast;
pub mod diagnostic;
pub mod expression;
pub mod lexer;
mod parser;
pub mod semantics;
mod writer;

pub use ast::*;
pub use diagnostic::{Diagnostic, DiagnosticSeverity, SourcePosition, SourceRange};
pub use expression::{parse_expression, parse_expression_checked};
pub use lexer::{Lexer, Token, TokenKind};
pub use parser::{parse_file, parse_reader, parse_source};
pub use semantics::{
    ArchiveScope, FieldScope, GameDefaultDefinition, SemanticModel, StoredField, analyze,
    analyze_for_game,
};
pub use writer::{WriterOptions, write, write_expression, write_with_options};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ParseResult {
    pub file: Option<ChunkLFile>,
    pub diagnostics: Vec<Diagnostic>,
}

impl ParseResult {
    pub fn success(&self) -> bool {
        self.file.is_some()
            && !self
                .diagnostics
                .iter()
                .any(|diagnostic| diagnostic.severity == DiagnosticSeverity::Error)
    }
}
