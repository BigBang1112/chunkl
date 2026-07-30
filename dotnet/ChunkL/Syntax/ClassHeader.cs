namespace ChunkL.Syntax;

public sealed class ClassHeader : SyntaxNode
{
    public required string ClassName { get; set; }
    public required string ClassId { get; set; }
    public Comment? TrailingComment { get; set; }
}
