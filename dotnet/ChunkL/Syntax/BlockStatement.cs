namespace ChunkL.Syntax;

public sealed class BlockStatement : SyntaxNode, IBodyStatement
{
    public AttributeList? Attributes { get; set; }
    public List<IBodyStatement> Body { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
