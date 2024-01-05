namespace ChunkL.Dynamic;

public static class ChunkLDynamicSerializer
{
    public static DynamicClass Deserialize(Stream stream, ChunkLDataModel dataModel)
    {
        return new DynamicClass
        {
            Name = dataModel.Header.Name,
            Id = dataModel.Header.Id
        };
    }
}
