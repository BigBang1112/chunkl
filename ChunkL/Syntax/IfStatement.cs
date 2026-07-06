namespace ChunkL.Syntax;

public sealed class IfStatement : SyntaxNode, IBodyStatement
{
    public required string Condition { get; set; }
    public List<IBodyStatement> Body { get; set; } = [];
    public List<ElseIfClause> ElseIfs { get; set; } = [];
    public ElseClause? Else { get; set; }
    public Comment? TrailingComment { get; set; }
}
