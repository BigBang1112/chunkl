namespace ChunkL.Syntax;

public sealed class AttributeEntry : SyntaxNode
{
    public required string Name { get; set; }
    public string? Value { get; set; }
}
