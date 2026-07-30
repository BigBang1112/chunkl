namespace ChunkL.Syntax;

public sealed class SkipStatement : SyntaxNode, IBodyStatement
{
    public required string Expression { get; set; }
    public AttributeList? Attributes { get; set; }
    public Comment? TrailingComment { get; set; }
}
