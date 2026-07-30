namespace ChunkL;

/// <summary>
/// A range of source positions from Start to End (inclusive).
/// </summary>
public readonly record struct SourceRange(SourcePosition Start, SourcePosition End)
{
    public override string ToString() => $"{Start}-{End}";
}
