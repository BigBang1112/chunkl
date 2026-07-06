namespace ChunkL.Syntax;

public sealed class EnumDeclaration : SyntaxNode
{
    public required string Name { get; set; }
    public List<EnumMember> Members { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
