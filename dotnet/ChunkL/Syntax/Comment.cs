namespace ChunkL.Syntax;

public sealed class Comment : SyntaxNode, IBodyStatement
{
    public required string Text { get; set; }
    public CommentStyle Style { get; set; }
}
