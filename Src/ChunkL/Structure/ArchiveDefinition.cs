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

        if (Properties.Count > 0)
        {
            sb.Append(" (");

            var first = true;

            foreach (var pair in Properties)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                sb.Append(pair.Key);

                if (!string.IsNullOrEmpty(pair.Value))
                {
                    sb.Append(": ");
                    sb.Append(pair.Value);
                }

                first = false;
            }

            sb.Append(')');
        }

        if (!string.IsNullOrEmpty(Description))
        {
            sb.Append(" // ");
            sb.Append(Description);
        }

        return sb.ToString();
    }
}
