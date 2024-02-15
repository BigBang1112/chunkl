using System.Text;

namespace ChunkL.Structure;

public sealed class EnumValue
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ExplicitValue { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder(Name);

        if (!string.IsNullOrEmpty(ExplicitValue))
        {
            sb.Append(" = ");
            sb.Append(ExplicitValue);
        }

        if (!string.IsNullOrEmpty(Description))
        {
            sb.Append(" // ");
            sb.Append(Description);
        }

        return sb.ToString();
    }
}
