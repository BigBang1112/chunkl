using Xunit;

namespace ChunkL.Tests.Writing;

public class WriterRoundTripTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static void AssertRoundTrip(string source)
    {
        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success,
            $"Parse failed: {string.Join("; ", result.Diagnostics.Select(d => d.ToString()))}");

        var written = ChunkLParser.Write(result.File!);

        // Re-parse the written output to verify it produces an equivalent AST
        var result2 = ChunkLParser.ParseSource(written);
        Assert.True(result2.Success,
            $"Re-parse failed: {string.Join("; ", result2.Diagnostics.Select(d => d.ToString()))}");

        // Compare the written outputs for structural equivalence
        var written2 = ChunkLParser.Write(result2.File!);
        Assert.Equal(written, written2);
    }

    [Fact]
    public void RoundTrip_Minimal()
    {
        var source = File.ReadAllText(FixturePath("minimal.chunkl"));
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_FullExample()
    {
        var source = File.ReadAllText(FixturePath("full_example.chunkl"));
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_ControlFlow()
    {
        var source = File.ReadAllText(FixturePath("control_flow.chunkl"));
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_EnumsFlags()
    {
        var source = File.ReadAllText(FixturePath("enums_flags.chunkl"));
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_Archives()
    {
        var source = File.ReadAllText(FixturePath("archives.chunkl"));
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_ChunkAttributes()
    {
        var source = File.ReadAllText(FixturePath("chunk_attributes.chunkl"));
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_ControlFlowAdvanced()
    {
        var source = File.ReadAllText(FixturePath("control_flow_advanced.chunkl"));
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_TypeModifiers()
    {
        var source = File.ReadAllText(FixturePath("type_modifiers.chunkl"));
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_InlineSource_Simple()
    {
        var source = """
            CGameMinimal 0x03000000

            0x001
              int Value
              string Name
            """;
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_InlineSource_VersionBlocks()
    {
        var source = """
            TestClass 0x01000000

            0x001
              version
              v0=
                short Flags
              v1+
                int Flags
            """;
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_InlineSource_IfElse()
    {
        var source = """
            TestClass 0x01000000

            0x001
              if HasBadges
                int BadgeCount
              else
                int DefaultCount
            """;
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_InlineSource_Enum()
    {
        var source = """
            TestClass 0x01000000

            enum Direction
              North
              East
              South
              West
            """;
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_InlineSource_Flags()
    {
        var source = """
            TestClass 0x01000000

            flags EBlockFlags
              HasSkin[15]
              WaypointKind[18..19]
            """;
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_InlineSource_Switch()
    {
        var source = """
            TestClass 0x01000000

            0x001
              switch Version
                case 0
                  string LegacyName
                case 1
                  id Name
                default
                  int Unknown
            """;
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_InlineSource_Loop()
    {
        var source = """
            TestClass 0x01000000

            0x001
              loop 4
                float Value
            """;
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_InlineSource_Archive()
    {
        var source = """
            TestClass 0x01000000

            archive MyArchive (contextual)
              int Version
              string Name
            """;
        AssertRoundTrip(source);
    }

    [Fact]
    public void RoundTrip_InlineSource_FixedCountArrays()
    {
        var source = """
            TestClass 0x01000000

            0x001
              float[4] Quaternion
              vec3[Count] Points
              int?[4] OptionalValues
              byte[16] Hash
            """;
        AssertRoundTrip(source);
    }

    [Fact]
    public void Write_PreservesCommentStyle()
    {
        var source = "TestClass 0x01000000 // a class\n";
        var result = ChunkLParser.ParseSource(source);
        var written = ChunkLParser.Write(result.File!);
        Assert.Contains("// a class", written);
    }

    [Fact]
    public void Write_Produces_ValidOutput()
    {
        var source = """
            CGameCtnBlock 0x03057000 // Block placed on a map.

            0x002 [TM10]
              ident BlockModel
              byte<Direction> Direction
              int Flags

            enum Direction
              North
              East
              South
              West
            """;

        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success);

        var written = ChunkLParser.Write(result.File!);
        Assert.Contains("CGameCtnBlock 0x03057000", written);
        Assert.Contains("0x002 [TM10]", written);
        Assert.Contains("ident BlockModel", written);
        Assert.Contains("byte<Direction> Direction", written);
        Assert.Contains("enum Direction", written);
        Assert.Contains("  North", written);
    }
}
