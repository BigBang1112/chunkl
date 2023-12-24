
using System.Text;

namespace ChunkL.Structure;

public sealed class ChunkIfStatement : IChunkMember, IChunkMemberBlock
{
    public required string Left { get; init; }
    public required string Operator { get; init; }
    public required string Right { get; init; }
    public required string Description { get; init; }
    public List<IChunkMember> Members { get; init; } = [];

    public override string ToString()
    {
        var sb = new StringBuilder("if ");
        sb.Append(Left);

        if (!string.IsNullOrEmpty(Operator))
        {
            sb.Append(' ');
            sb.Append(Operator);
            sb.Append(' ');
            sb.Append(Right);
        }

        if (!string.IsNullOrEmpty(Description))
        {
            sb.Append(" // ");
            sb.Append(Description);
        }

        return sb.ToString();
    }
}