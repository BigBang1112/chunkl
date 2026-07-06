namespace ChunkL.Syntax;

public sealed class BitRange : SyntaxNode
{
    public required int Start { get; set; }
    public int? End { get; set; }
    public bool IsSingleBit => End is null;
}
