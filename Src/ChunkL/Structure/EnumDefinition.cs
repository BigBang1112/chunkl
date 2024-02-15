using System.Text;

namespace ChunkL.Structure;

public sealed class EnumDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public List<EnumValue> Values { get; init; } = [];

    public override string ToString()
    {
        var sb = new StringBuilder("enum ");

        sb.Append(Name);

        if (!string.IsNullOrEmpty(Description))
        {
            sb.Append(" // ");
            sb.Append(Description);
        }

        return sb.ToString();
    }
}
