namespace ChunkL.Syntax;

public sealed class ReturnStatement : SyntaxNode, IBodyStatement
{
    public AttributeList? Attributes { get; set; }
    public Comment? TrailingComment { get; set; }
}
