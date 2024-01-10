using System.Text;

namespace ChunkL.Structure;

public class ChunkProperty : IChunkMember
{
    public required string Type { get; init; }
    public required bool IsNullable { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string DefaultValue { get; init; }

    public bool IsLocal => Name.Length > 0 && char.IsLower(Name[0]);

    public override string ToString()
    {
        var sb = new StringBuilder(Type);

        if (IsNullable)
        {
            sb.Append('?');
        }

        if (!string.IsNullOrEmpty(Name))
        {
            sb.Append(' ');
            sb.Append(Name);
        }

        if (!string.IsNullOrEmpty(DefaultValue))
        {
            sb.Append(" = ");
            sb.Append(DefaultValue);
        }

        if (!string.IsNullOrEmpty(Description))
        {
            sb.Append(" // ");
            sb.Append(Description);
        }

        return sb.ToString();
    }
}
