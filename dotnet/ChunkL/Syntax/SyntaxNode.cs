namespace ChunkL.Syntax;

/// <summary>
/// Abstract base class for all AST nodes.
/// </summary>
public abstract class SyntaxNode
{
    public SourceRange Position { get; set; }
}
