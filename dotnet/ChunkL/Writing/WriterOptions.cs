namespace ChunkL.Writing;

public sealed class WriterOptions
{
    public string IndentString { get; set; } = "  ";
    public string NewLine { get; set; } = "\n";
    public bool PreserveComments { get; set; } = true;
}
