using ChunkL.Serialization;

namespace ChunkL;

public static class ChunkLSerializer
{
    public static ChunkLDataModel Deserialize(TextReader reader)
    {
        var header = new HeaderSerializer(reader).Read();

        return new ChunkLDataModel(header);
    }
}
