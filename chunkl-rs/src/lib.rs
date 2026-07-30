//! Parser, syntax tree, lexer, and writer for the ChunkL language.

pub mod ast;
pub mod diagnostic;
pub mod lexer;
mod parser;
mod writer;

pub use ast::*;
pub use diagnostic::{Diagnostic, DiagnosticSeverity, SourcePosition, SourceRange};
pub use lexer::{Lexer, Token, TokenKind};
pub use parser::{parse_file, parse_reader, parse_source};
pub use writer::{write, write_with_options, WriterOptions};

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
