
namespace ChunkL.Structure;

public sealed class BodyModel
{
    public List<ChunkDefinition> ChunkDefinitions { get; init; } = [];

    public override string ToString()
    {
        return $"BodyModel ({ChunkDefinitions.Count} chunks)";
    }
}
