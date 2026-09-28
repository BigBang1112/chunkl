namespace ChunkL.Syntax;

public sealed class PropertyDeclaration : SyntaxNode
{
    public required TypeReference Type { get; set; }
    public required string Name { get; set; }
    public List<SyntaxNode> Accessors { get; set; } = [];
    public Comment? TrailingComment { get; set; }
    public GetterAccessor? Getter => Accessors.OfType<GetterAccessor>().FirstOrDefault();
    public SetterAccessor? Setter => Accessors.OfType<SetterAccessor>().FirstOrDefault();
}

public sealed class GetterAccessor : SyntaxNode
{
    public required Expression Expression { get; set; }
    public Comment? TrailingComment { get; set; }
}

public sealed class SetterAccessor : SyntaxNode
{
    public List<IBodyStatement> Body { get; set; } = [];
    public Comment? TrailingComment { get; set; }
}
