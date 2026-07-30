using System.Collections;

namespace ChunkL.Diagnostics;

/// <summary>
/// Accumulates diagnostics during parsing.
/// </summary>
public sealed class DiagnosticBag : IEnumerable<Diagnostic>
{
    private readonly List<Diagnostic> _diagnostics = [];

    public bool HasErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    public int Count => _diagnostics.Count;

    public void ReportError(string message, SourcePosition position, string? code = null)
    {
        _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, message, position, code));
    }

    public void ReportWarning(string message, SourcePosition position, string? code = null)
    {
        _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, message, position, code));
    }

    public void ReportInfo(string message, SourcePosition position, string? code = null)
    {
        _diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, message, position, code));
    }

    public Diagnostic[] ToArray() => [.. _diagnostics];

    public IEnumerator<Diagnostic> GetEnumerator() => _diagnostics.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
