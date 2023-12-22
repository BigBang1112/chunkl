using ChunkL.Serialization;

namespace ChunkL;

public static class ChunkLSerializer
{
    public static ChunkLDataModel Deserialize(TextReader reader)
    {
        var header = new HeaderReader(reader).Read();
        var body = new BodyReader(reader).Read();

        return new ChunkLDataModel(header, body);
    }
}
