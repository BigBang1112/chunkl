namespace ChunkL.Syntax;

public sealed class EnumMember : SyntaxNode
{
    public required string Name { get; set; }
    public string? ExplicitValue { get; set; }
    public Comment? TrailingComment { get; set; }
}
