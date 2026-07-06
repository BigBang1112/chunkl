namespace ChunkL.Syntax;

public sealed class LoopStatement : SyntaxNode, IBodyStatement
{
    public required string CountExpression { get; set; }
    public List<IBodyStatement> Body { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
