namespace ChunkL.Tests;

public class ChunkLSerializerTests
{
    [Theory]
    [InlineData("CGameCtnChallenge")]
    [InlineData("CGameCtnBlock")]
    [InlineData("CPlugVehicleCarPhyTuning")]
    public void Deserialize(string className)
    {
        var reader = new StreamReader($"Files/{className}.chunkl");
        var dataModel = ChunkLSerializer.Deserialize(reader);
    }
}
