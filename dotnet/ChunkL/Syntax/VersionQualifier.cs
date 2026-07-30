namespace ChunkL.Syntax;

public sealed class VersionQualifier : SyntaxNode
{
    public required string Label { get; set; }
    public int? MaxVersion { get; set; }
}
