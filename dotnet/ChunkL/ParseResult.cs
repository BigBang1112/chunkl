using ChunkL.Diagnostics;
using ChunkL.Syntax;

namespace ChunkL;

/// <summary>
/// Result of parsing a .chunkl file.
/// </summary>
public sealed class ParseResult
{
    public ChunkLFile? File { get; }
    public Diagnostic[] Diagnostics { get; }
    public bool Success => File != null && !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    public ParseResult(ChunkLFile? file, Diagnostic[] diagnostics)
    {
        File = file;
        Diagnostics = diagnostics;
    }
}
