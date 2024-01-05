using System.Text;

namespace ChunkL.Structure;

public sealed class ChunkMemberAssign : IChunkMember
{
    public required string Left { get; init; }
    public required string Right { get; init; }
    public required string Description { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder(Left);
        sb.Append(" = ");
        sb.Append(Right);

        if (!string.IsNullOrEmpty(Description))
        {
            sb.Append(" // ");
            sb.Append(Description);
        }

        return sb.ToString();
    }
}
