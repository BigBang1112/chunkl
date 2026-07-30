use std::fmt;

#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub struct SourcePosition {
    pub line: usize,
    pub column: usize,
}

impl SourcePosition {
    pub const fn new(line: usize, column: usize) -> Self {
        Self { line, column }
    }
}

#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub struct SourceRange {
    pub start: SourcePosition,
    pub end: SourcePosition,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DiagnosticSeverity {
    Info,
    Warning,
    Error,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Diagnostic {
    pub severity: DiagnosticSeverity,
    pub message: String,
    pub position: SourcePosition,
    pub code: Option<String>,
}

impl Diagnostic {
    pub fn error(message: impl Into<String>, position: SourcePosition) -> Self {
        Self {
            severity: DiagnosticSeverity::Error,
            message: message.into(),
            position,
            code: None,
        }
    }
}

impl fmt::Display for Diagnostic {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            formatter,
            "{:?} ({},{}): {}",
            self.severity, self.position.line, self.position.column, self.message
        )?;
        if let Some(code) = &self.code {
            write!(formatter, " [{code}]")?;
        }
        Ok(())
    }
}
