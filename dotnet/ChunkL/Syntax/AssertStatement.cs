namespace ChunkL.Syntax;

public sealed class AssertStatement : SyntaxNode, IBodyStatement
{
    public required Expression Condition { get; set; }
    public AttributeList? Attributes { get; set; }
    public Comment? TrailingComment { get; set; }
}
