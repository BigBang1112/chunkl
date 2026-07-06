namespace ChunkL.Syntax;

public sealed class CastType : SyntaxNode
{
    public required string Name { get; set; }
    public string? QualifyingType { get; set; }
}
