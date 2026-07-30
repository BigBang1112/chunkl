namespace ChunkL.Syntax;

public sealed class SwitchDefault : SyntaxNode
{
    public List<IBodyStatement> Body { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
