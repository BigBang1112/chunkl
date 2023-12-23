using System.Text;

namespace ChunkL.Structure;

public class ChunkProperty : IChunkMember
{
    public required string Type { get; init; }
    public required bool IsNullable { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder(Type);

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
