using System.Text;

namespace ChunkL.Structure;

public sealed class ChunkIfStatement : IChunkMember, IChunkMemberBlock
{
    public List<string> Condition { get; init; } = [];
    public required string Description { get; init; }
    public List<IChunkMember> Members { get; init; } = [];

    public override string ToString()
    {
        var sb = new StringBuilder("if ");

        var first = true;
        var prevCondition = string.Empty;

        foreach (var condition in Condition)
        {
            if (!first && condition is not ")" && prevCondition is not "(")
            {
                sb.Append(' ');
            }

            sb.Append(condition);

            first = false;
            prevCondition = condition;
        }

        if (!string.IsNullOrEmpty(Description))
        {
            sb.Append(" // ");
            sb.Append(Description);
        }

        return sb.ToString();
    }
}