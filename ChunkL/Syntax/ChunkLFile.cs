namespace ChunkL.Syntax;

/// <summary>
/// Root node representing an entire .chunkl file.
/// </summary>
public sealed class ChunkLFile : SyntaxNode
{
    public required ClassHeader Header { get; set; }
    public List<ClassAttribute> ClassAttributes { get; set; } = [];
    public List<ChunkDeclaration> Chunks { get; set; } = [];
    public List<ArchiveDeclaration> Archives { get; set; } = [];
    public List<EnumDeclaration> Enums { get; set; } = [];
    public List<FlagsDeclaration> Flags { get; set; } = [];
    public List<Comment> TopLevelComments { get; set; } = [];
}
