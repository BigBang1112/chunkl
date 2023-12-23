using System.Text;

namespace ChunkL.Structure;

public sealed class ChunkDefinition : IChunkMemberBlock
{
    public required uint Id { get; init; }
    public required string Properties { get; init; }
    public required string Description { get; init; }
    public List<IChunkMember> Members { get; init; } = [];
    public bool IsVersionable => Members.Count > 0 && Members[0] is ChunkVersion;

    public override string ToString()
    {
        var sb = new StringBuilder("0x");
        sb.Append(Id.ToString("X3"));
        sb.Append(" (");
        sb.Append(Properties);
        sb.Append(")");

        if (!string.IsNullOrEmpty(Description))
        {
            sb.Append(" // ");
            sb.Append(Description);
        }

        return sb.ToString();
    }
}
