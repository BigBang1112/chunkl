namespace ChunkL.Parsing;

public sealed class ParserException : Exception
{
    public SourcePosition Position { get; }

    public ParserException(string message, SourcePosition position)
        : base($"{position}: {message}")
    {
        Position = position;
    }

    public ParserException(string message, SourcePosition position, Exception innerException)
        : base($"{position}: {message}", innerException)
    {
        Position = position;
    }
}
