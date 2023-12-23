using System.Text;

namespace ChunkL.Structure;

public sealed class HeaderModel
{
    public required string Name { get; init; }
    public required uint Id { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, string> Features { get; init; } = [];

    public override string ToString()
    {
        var sb = new StringBuilder("0x");
        sb.Append(Id.ToString("X8"));
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
