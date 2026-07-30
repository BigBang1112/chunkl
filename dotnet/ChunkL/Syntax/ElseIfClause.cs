namespace ChunkL.Syntax;

public sealed class ElseIfClause : SyntaxNode
{
    public required Expression Condition { get; set; }
    public List<IBodyStatement> Body { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
