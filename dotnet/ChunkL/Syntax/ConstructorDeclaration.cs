namespace ChunkL.Syntax;

public sealed class ConstructorDeclaration : SyntaxNode
{
    public List<IBodyStatement> Body { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
