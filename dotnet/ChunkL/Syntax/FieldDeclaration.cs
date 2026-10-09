namespace ChunkL.Syntax;

public sealed class FieldDeclaration : SyntaxNode, IBodyStatement
{
    public required TypeReference Type { get; set; }
    public string? Name { get; set; }
    public Expression? DefaultValue { get; set; }
    public string? DefaultValueSource { get; set; }
    public List<GameDefault> GameDefaults { get; set; } = [];
    public AttributeList? Attributes { get; set; }
    public Comment? TrailingComment { get; set; }
    public bool IsSpecialKeyword { get; set; }

    public Expression? GetDefaultValue(string? game = null) =>
        GameDefaults.FirstOrDefault(d => d.Game == game)?.Value ?? DefaultValue;
}

public sealed class GameDefault : SyntaxNode
{
    public required string Game { get; set; }
    public required Expression Value { get; set; }
    public string? ValueSource { get; set; }
}
