namespace ChunkL.Syntax;

public sealed class ArchiveDeclaration : SyntaxNode
{
    public string? Name { get; set; }
    public AttributeList? Attributes { get; set; }
    public List<IBodyStatement> Body { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
