namespace ChunkL.Syntax;

public sealed class FlagsMember : SyntaxNode
{
    public required string Name { get; set; }
    public required BitRange Bits { get; set; }
    public Comment? TrailingComment { get; set; }
}
