namespace ChunkL.Syntax;

public sealed class ElseIfClause : SyntaxNode
{
    public required string Condition { get; set; }
    public List<IBodyStatement> Body { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
