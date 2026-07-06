namespace ChunkL.Syntax;

public sealed class ElseClause : SyntaxNode
{
    public List<IBodyStatement> Body { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
