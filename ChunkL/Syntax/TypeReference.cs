namespace ChunkL.Syntax;

public sealed class TypeReference : SyntaxNode
{
    public required string Name { get; set; }
    public CastType? CastTarget { get; set; }
    public bool ChunkPreference { get; set; }
    public bool IsNullable { get; set; }
    public int ArrayDimensions { get; set; }
    public string? FixedArrayCount { get; set; }
}
