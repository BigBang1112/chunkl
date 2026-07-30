namespace ChunkL.Syntax;

public sealed class AttributeList : SyntaxNode
{
    public List<AttributeEntry> Entries { get; set; } = [];
}
