namespace ChunkL.Structure;

public class ChunkProperty : IChunkMember
{
    public required string Type { get; init; }
    public required bool IsNullable { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    public override string ToString()
    {
        return $"{Type}{(IsNullable ? "?" : "")} {Name} // {Description}";
    }
}
