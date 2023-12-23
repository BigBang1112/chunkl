using System.Text;

namespace ChunkL.Structure;

public sealed class ChunkVersion : IChunkMember, IChunkMemberBlock
{
    public required int Number { get; init; }
    public required string Operator { get; init; }
    public required string Description { get; init; }
    public List<IChunkMember> Members { get; init; } = [];

    public override string ToString()
    {
        var sb = new StringBuilder("v");
        sb.Append(Number);
        sb.Append(Operator);

        if (!string.IsNullOrEmpty(Description))
        {
            sb.Append(" // ");
            sb.Append(Description);
        }

        return sb.ToString();
    }
}
