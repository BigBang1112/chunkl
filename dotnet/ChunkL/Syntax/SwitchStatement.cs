namespace ChunkL.Syntax;

public sealed class SwitchStatement : SyntaxNode, IBodyStatement
{
    public required Expression Expression { get; set; }
    public List<SwitchCase> Cases { get; set; } = [];
    public SwitchDefault? Default { get; set; }
    public Comment? TrailingComment { get; set; }
}
