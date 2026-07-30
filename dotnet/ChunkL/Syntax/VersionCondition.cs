namespace ChunkL.Syntax;

public sealed class VersionCondition : SyntaxNode, IBodyStatement
{
    public VersionConditionKind Kind { get; set; }
    public required int Version { get; set; }
    public int? VersionEnd { get; set; }
    public List<IBodyStatement> Body { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
