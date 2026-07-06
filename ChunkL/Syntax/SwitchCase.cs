namespace ChunkL.Syntax;

public sealed class SwitchCase : SyntaxNode
{
    public required string Value { get; set; }
    public List<IBodyStatement> Body { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
