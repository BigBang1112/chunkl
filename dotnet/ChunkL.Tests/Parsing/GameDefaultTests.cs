using ChunkL.Syntax;
using Xunit;

namespace ChunkL.Tests.Parsing;

public class GameDefaultTests
{
    private static ChunkLFile Parse(string source)
    {
        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
        return result.File!;
    }

    [Fact]
    public void GameDefaultsRoundTripAndSelectAcrossFieldScopes()
    {
        var file = Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "game_defaults.chunkl")));
        var written = ChunkLParser.Write(file);
        Assert.Contains("version = 1 (optional) [TMSX = 5]", written);
        Assert.Contains("int Shared = 1 (optional, name: Shared) [TMSX = 5, tmsx = 6]", written);
        Assert.Contains("int (optional) [TMSX = 3]", written);
        foreach (var current in new[] { file, Parse(ChunkLParser.Write(file)) })
        {
            var fields = current.Chunks[0].Body.OfType<FieldDeclaration>().ToArray();
            Assert.Equal("5", ChunkLParser.WriteExpression(fields[0].GameDefaults[0].Value));
            Assert.Equal("5", ChunkLParser.WriteExpression(fields[0].GetDefaultValue("TMSX")!));
            Assert.Equal("1", ChunkLParser.WriteExpression(fields[0].GetDefaultValue()!));
            Assert.Equal("3", ChunkLParser.WriteExpression(fields[^1].GetDefaultValue("TMSX")!));
            Assert.Null(fields[^1].GetDefaultValue("Other"));
            Assert.Null(fields[^1].Name);
            Assert.Equal(0, fields[^1].Type.ArrayDimensions);
            Assert.IsType<TupleExpression>(fields[6].GameDefaults[0].Value);
            Assert.Equal("Count", fields[8].Type.FixedArrayCount);
            Assert.Equal("comment", fields[5].TrailingComment!.Text);

            var fallback = ChunkLParser.Analyze(current);
            var game = ChunkLParser.Analyze(current, "TMSX");
            Assert.True(game.Success, string.Join("; ", game.Diagnostics.Select(d => d.Message)));
            Assert.Equal("1", ChunkLParser.WriteExpression(fallback.Class.Fields.Single(f => f.Name == "Shared").DefaultValue!));
            Assert.DoesNotContain(fallback.Class.InlineDefaults, f => f.Name == "Only");
            Assert.Equal("5", ChunkLParser.WriteExpression(game.Class.Fields.Single(f => f.Name == "Shared").DefaultValue!));
            Assert.Equal("12", ChunkLParser.WriteExpression(game.Archives[0].Fields.Single(f => f.Name == "Shared").DefaultValue!));
            Assert.DoesNotContain(game.Class.InlineDefaults, f => f.Name == "Skipped");
            Assert.Equal("int", game.Class.Fields.Single(f => f.Name == "Shared").Type.Name);
            var modern = ChunkLParser.Analyze(current, "TM2020");
            Assert.True(modern.Class.InlineDefaults.FindIndex(f => f.Name == "First") < modern.Class.InlineDefaults.FindIndex(f => f.Name == "Shared"));
            Assert.Equal("8", ChunkLParser.WriteExpression(modern.Class.Fields.Single(f => f.Name == "Shared").DefaultValue!));
            Assert.Equal("6", ChunkLParser.WriteExpression(ChunkLParser.Analyze(current, "tmsx").Class.Fields.Single(f => f.Name == "Shared").DefaultValue!));
            Assert.Equal("1", ChunkLParser.WriteExpression(ChunkLParser.Analyze(current, "Other").Class.Fields.Single(f => f.Name == "Shared").DefaultValue!));
        }
    }

    [Theory]
    [InlineData("int Value []")]
    [InlineData("int Value [TMSX]")]
    [InlineData("int Value [TMSX =]")]
    [InlineData("int Value [TMSX = 5,]")]
    [InlineData("int Value [TMSX = 5, TMSX = 6]")]
    [InlineData("int Value [TM.v1 = 5]")]
    [InlineData("int Value [TMSX = 5")]
    [InlineData("int Value [TMSX = 5] (flag)")]
    [InlineData("int Value = 0 [TMSX = 5] (optional, name: Value)")]
    [InlineData("version = 1 [TMSX = 5] (optional)")]
    [InlineData("int [TMSX = 5] (optional)")]
    [InlineData("int Value [TMSX = 5] [TM2020 = 6]")]
    [InlineData("Value = 1 [TMSX = 5]")]
    [InlineData("return [TMSX = 5]")]
    [InlineData("throw [TMSX = 5]")]
    [InlineData("block [TMSX = 5]")]
    [InlineData("v1+ [TMSX = 5]")]
    public void RejectsMalformedLists(string declaration)
    {
        Assert.False(ChunkLParser.ParseSource($"Test 0x01000000\n0x001\n  {declaration}\n").Success);
    }

    [Fact]
    public void RepeatedDefaultsCompareSyntaxAndRespectAstEdits()
    {
        var file = Parse("Test 0x01000000\n0x001\n  int Value [TMSX = (1 + 2)]\n0x002\n  int Value [TMSX = (1+2), TM2020 = 9]\n");
        Assert.True(ChunkLParser.Analyze(file, "TMSX").Success);
        var entry = ((FieldDeclaration)file.Chunks[1].Body[0]).GameDefaults[0];
        entry.Value = new LiteralExpression { Kind = LiteralKind.Integer, Value = "4" };
        Assert.Contains(ChunkLParser.Analyze(file).Diagnostics, d => d.Message.Contains("Conflicting game defaults"));
    }

    [Fact]
    public void GameExpressionsReceiveSemanticValidation()
    {
        var file = Parse("Test 0x01000000\n0x001\n  bool Value [TMSX = 1 and true]\n");
        Assert.Contains(ChunkLParser.Analyze(file).Diagnostics, d => d.Message.Contains("boolean operands"));
    }

    [Fact]
    public void GameLabelsAreOpenAndDefaultsCanUseAllExpressionForms()
    {
        var file = Parse("Test 0x01000000\n0x001\n  int? Value = null [2020_Custom = -5, Other = Count + 1]\n  int Count = 1\n  string Name [Custom = empty]\n");
        Assert.True(ChunkLParser.Analyze(file, "2020_Custom").Success);
        Assert.Equal("-5", ChunkLParser.WriteExpression(((FieldDeclaration)file.Chunks[0].Body[0]).GetDefaultValue("2020_Custom")!));
    }
}
