using System.Text;

namespace ChunkL.Structure;

public class PropertyType
{
    public required string PrimaryType { get; init; }
    public string GenericType { get; init; } = "";
    public string PrimaryTypeMarker { get; init; } = "";
    public string GenericTypeMarker { get; init; } = "";
    public bool IsArray { get; init; }
    public string ArrayLength { get; init; } = "";
    public bool IsDeprec { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder(PrimaryType);

        if (!string.IsNullOrEmpty(GenericType))
        {
            sb.Append('<');
            sb.Append(GenericType);

            if (!string.IsNullOrEmpty(GenericTypeMarker))
            {
                sb.Append(GenericTypeMarker);
            }

            sb.Append('>');
        }

        if (!string.IsNullOrEmpty(PrimaryTypeMarker))
        {
            sb.Append(PrimaryTypeMarker);
        }

        if (IsArray)
        {
            sb.Append('[');
            sb.Append(ArrayLength);
            sb.Append(']');
        }

        if (IsDeprec)
        {
            sb.Append("_deprec");
        }

        return sb.ToString();
    }
}
