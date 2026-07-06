namespace ChunkL.Syntax;

public sealed class ChunkOffset : SyntaxNode
{
    public required string HexValue { get; set; }
    public bool IsFullId { get; set; }
}
