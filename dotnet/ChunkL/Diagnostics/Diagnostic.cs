namespace ChunkL.Diagnostics;

/// <summary>
/// A single diagnostic message produced during parsing.
/// </summary>
public sealed class Diagnostic
{
    public DiagnosticSeverity Severity { get; }
    public string Message { get; }
    public SourcePosition Position { get; }
    public string? Code { get; }

    public Diagnostic(DiagnosticSeverity severity, string message, SourcePosition position, string? code = null)
    {
        Severity = severity;
        Message = message;
        Position = position;
        Code = code;
    }

    public override string ToString() =>
        $"{Severity} {Position}: {Message}" + (Code is not null ? $" [{Code}]" : "");
}
