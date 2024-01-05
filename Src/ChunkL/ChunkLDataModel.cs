using ChunkL.Structure;

namespace ChunkL;

public sealed class ChunkLDataModel(HeaderModel header, BodyModel body)
{
    public HeaderModel Header { get; init; } = header;
    public BodyModel Body { get; init; } = body;
}
