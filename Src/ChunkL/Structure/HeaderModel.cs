namespace ChunkL.Structure;

public sealed class HeaderModel
{
    public required string Name { get; init; }
    public required uint Id { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, string> Features { get; init; } = [];
}
