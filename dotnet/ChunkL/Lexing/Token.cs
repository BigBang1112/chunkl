namespace ChunkL.Lexing;

/// <summary>
/// A single lexical token from ChunkL source.
/// </summary>
public readonly record struct Token(
    TokenKind Kind,
    string Text,
    SourcePosition Position,
    int LeadingSpaces,
    int SourceOffset,
    int SourceLength)
{
    public override string ToString() => $"{Kind}({Text}) at {Position}";
}
