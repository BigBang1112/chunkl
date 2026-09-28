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
    public List<PropertyDeclaration> Properties { get; set; } = [];
    public ConstructorDeclaration? Constructor { get; set; }
    public List<Comment> TopLevelComments { get; set; } = [];
    public List<SyntaxNode> DeclarationOrder { get; set; } = [];

    /// <summary>
    /// Enumerates declarations in source order, appending newly added declarations.
    /// </summary>
    public IEnumerable<SyntaxNode> GetDeclarationsInSourceOrder()
    {
        var current = Chunks.Cast<SyntaxNode>().Concat(Archives).Concat(Enums)
            .Concat(Flags).Concat(Properties).Concat(TopLevelComments).ToList();
        if (Constructor != null)
            current.Add(Constructor);
        var remaining = new HashSet<SyntaxNode>(current);
        foreach (var declaration in DeclarationOrder.Concat(current))
            if (remaining.Remove(declaration))
                yield return declaration;
    }
}
