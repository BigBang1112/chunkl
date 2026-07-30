namespace ChunkL.Syntax;

public sealed class ClassAttribute : SyntaxNode
{
    public required string Name { get; set; }
    public string? Value { get; set; }
    public Comment? TrailingComment { get; set; }
}
