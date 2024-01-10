using System.Text;

namespace ChunkL.Structure;

public sealed class ChunkDefinition : IChunkMemberBlock
{
    private bool versionMemberChecked;
    private ChunkProperty? versionMember;

    public required uint Id { get; init; }
    public required string Description { get; init; }
    public Dictionary<string, string> Properties { get; init; } = [];
    public Dictionary<string, int?> Versions { get; init; } = [];
    public List<IChunkMember> Members { get; init; } = [];

    public ChunkProperty? VersionMember
    {
        get
        {
            if (versionMemberChecked) return versionMember;
            versionMember = Members.OfType<ChunkProperty>().FirstOrDefault(p => p.Type is "version" or "versionb");
            versionMemberChecked = true;
            return versionMember;
        }
    }

    public bool IsVersionable => VersionMember is not null;

    public override string ToString()
    {
        var sb = new StringBuilder("0x");
        sb.Append(Id.ToString("X3"));

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

        if (Versions.Count > 0)
        {
            sb.Append(" [");

            var first = true;

            foreach (var pair in Versions)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                sb.Append(pair.Key);

                if (pair.Value.HasValue)
                {
                    sb.Append(".v");
                    sb.Append(pair.Value.Value);
                }

                first = false;
            }

            sb.Append(']');
        }

        if (!string.IsNullOrEmpty(Description))
        {
            sb.Append(" // ");
            sb.Append(Description);
        }

        return sb.ToString();
    }
}
