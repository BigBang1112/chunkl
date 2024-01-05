namespace ChunkL.Dynamic;

public static class ChunkLDynamicSerializer
{
    public static DynamicClass Deserialize(Stream stream, ChunkLDataModel dataModel)
    {
        var header = new HeaderReader(reader).Read();
        var body = new BodyReader(reader).Read();

        return new ChunkLDataModel(header, body);
    }
}
