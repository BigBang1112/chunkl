using System.Text.Json.Serialization;

namespace ChunkL.Json;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ChunkLDataModel))]
public partial class ChunkLJsonSerializerContext : JsonSerializerContext
{
}
