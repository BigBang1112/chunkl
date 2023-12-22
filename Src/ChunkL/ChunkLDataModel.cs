using ChunkL.Structure;

namespace ChunkL;

public sealed class ChunkLDataModel(HeaderModel header)
{
    public HeaderModel Header { get; } = header;
}
