using System.Text;

namespace ChunkL.Structure;

public sealed class ChunkThrow : IChunkMember
{
    public required string Exception { get; init; }
    public required string Message { get; init; }
    public required string Description { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder("throw");

        if (!string.IsNullOrEmpty(Exception))
        {
            sb.Append(' ');
            sb.Append(Exception);
        }

        if (!string.IsNullOrEmpty(Message))
        {
            sb.Append("(\"");
            sb.Append(Message);
            sb.Append("\")");
        }

        if (!string.IsNullOrEmpty(Description))
        {
            sb.Append(" // ");
            sb.Append(Description);
        }

        return sb.ToString();
    }
}
