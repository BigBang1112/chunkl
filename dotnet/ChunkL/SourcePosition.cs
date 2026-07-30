namespace ChunkL;

/// <summary>
/// A 1-based line/column position in source text.
/// </summary>
public readonly record struct SourcePosition(int Line, int Column)
{
    public override string ToString() => $"({Line},{Column})";
}
