namespace ChunkL.Structure;

public sealed class ChunkVersion : IChunkMember
{
    public required int Number { get; init; }
    public required string Operator { get; init; }
    public required string Description { get; init; }
    public List<IChunkMember> Members { get; init; } = [];

    public override string ToString()
    {
        return $"v{Number}{Operator} // {Description}";
    }
}
