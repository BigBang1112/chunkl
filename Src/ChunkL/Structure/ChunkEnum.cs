using System.Text;

namespace ChunkL.Structure;

public sealed class ChunkEnum : ChunkProperty
{
    public required string EnumType { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder(Type.ToString());
        sb.Append('<');
        sb.Append(EnumType);
        sb.Append('>');

        if (IsNullable)
        {
            sb.Append('?');
        }

        sb.Append(' ');
        sb.Append(Name);

        if (!string.IsNullOrEmpty(Description))
        {
            sb.Append(" // ");
            sb.Append(Description);
        }

        return sb.ToString();
    }
}
