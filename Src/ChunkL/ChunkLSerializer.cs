using ChunkL.Serialization;

namespace ChunkL;

public static class ChunkLSerializer
{
    public static ChunkLDataModel Deserialize(TextReader reader)
    {
        var header = new HeaderReader(reader).Read();

        return new ChunkLDataModel(header);
    }
}
