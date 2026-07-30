namespace ChunkL.Syntax;

public sealed class ThrowStatement : SyntaxNode, IBodyStatement
{
    public AttributeList? Attributes { get; set; }
    public Comment? TrailingComment { get; set; }
}
