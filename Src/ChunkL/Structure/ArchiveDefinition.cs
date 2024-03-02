using System.Text;

namespace ChunkL.Structure;

public sealed class ArchiveDefinition : IChunkMemberBlock
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public Dictionary<string, string> Properties { get; init; } = [];
    public List<IChunkMember> Members { get; init; } = [];

    public override string ToString()
    {
        var sb = new StringBuilder("archive");

        if (!string.IsNullOrEmpty(Name))
        {
            sb.Append(' ');
            sb.Append(Name);
        }

        if (!string.IsNullOrEmpty(Description))
        {
            sb.Append(" // ");
            sb.Append(Description);
        }

        return sb.ToString();
    }
}
