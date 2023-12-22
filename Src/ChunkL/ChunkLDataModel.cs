using ChunkL.Structure;

namespace ChunkL;

public sealed class ChunkLDataModel(HeaderModel header, BodyModel body)
{
    public HeaderModel Header { get; } = header;
    public BodyModel Body { get; } = body;
}
