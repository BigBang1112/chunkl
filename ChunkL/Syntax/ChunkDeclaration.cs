namespace ChunkL.Syntax;

public sealed class ChunkDeclaration : SyntaxNode
{
    public required ChunkOffset Offset { get; set; }
    public AttributeList? Attributes { get; set; }
    public List<VersionQualifier> VersionQualifiers { get; set; } = [];
    public List<IBodyStatement> Body { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
