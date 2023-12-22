namespace ChunkL.Structure;

public sealed class ChunkDefinition
{
    public required uint Id { get; init; }
    public required string Properties { get; init; }
    public required string Description { get; init; }
    public List<IChunkMember> Members { get; init; } = [];

    public override string ToString()
    {
        return $"0x{Id:X3} ({Properties}) // {Description}";
    }
}
