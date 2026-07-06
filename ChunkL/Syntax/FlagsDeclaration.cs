namespace ChunkL.Syntax;

public sealed class FlagsDeclaration : SyntaxNode
{
    public required string Name { get; set; }
    public List<FlagsMember> Members { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
