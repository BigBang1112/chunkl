namespace ChunkL.Structure;

public sealed class ChunkMemberAssign : IChunkMember
{
    public required string Left { get; init; }
    public required string Right { get; init; }
    public required string Description { get; init; }
}
