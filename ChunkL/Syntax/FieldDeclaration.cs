namespace ChunkL.Syntax;

public sealed class FieldDeclaration : SyntaxNode, IBodyStatement
{
    public required TypeReference Type { get; set; }
    public string? Name { get; set; }
    public string? DefaultValue { get; set; }
    public AttributeList? Attributes { get; set; }
    public Comment? TrailingComment { get; set; }
    public bool IsSpecialKeyword { get; set; }
}
