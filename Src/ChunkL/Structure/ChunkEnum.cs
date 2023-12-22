namespace ChunkL.Structure;

public sealed class ChunkEnum : ChunkProperty
{
    public required string EnumType { get; init; }

    public override string ToString()
    {
        return $"{Type}<{EnumType}>{(IsNullable ? "?" : "")} {Name} // {Description}";
    }
}
