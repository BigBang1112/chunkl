
namespace ChunkL.Structure;

public sealed class BodyModel
{
    public List<ChunkDefinition> ChunkDefinitions { get; init; } = [];
    public List<ArchiveDefinition> ArchiveDefinitions { get; init; } = [];
    public List<EnumDefinition> EnumDefinitions { get; init; } = [];

    public override string ToString()
    {
        return $"BodyModel ({ChunkDefinitions.Count} chunks, {ArchiveDefinitions.Count} archives, {EnumDefinitions.Count} enums)";
    }
}
