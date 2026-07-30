namespace ChunkL.Syntax;

public sealed class ComputedAssignment : SyntaxNode, IBodyStatement
{
    public required string TargetName { get; set; }
    public required Expression Expression { get; set; }
    public Comment? TrailingComment { get; set; }
}
