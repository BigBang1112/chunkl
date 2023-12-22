namespace ChunkL.Tests;

public class ChunkLSerializerTests
{
    [Fact]
    public void Deserialize()
    {
        var reader = new StreamReader("Files/CGameCtnChallenge.chunkl");
        var dataModel = ChunkLSerializer.Deserialize(reader);
    }
}
