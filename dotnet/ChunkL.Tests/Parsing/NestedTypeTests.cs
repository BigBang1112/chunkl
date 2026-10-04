using ChunkL.Syntax;
using Xunit;

namespace ChunkL.Tests.Parsing;

public class NestedTypeTests
{
    [Fact]
    public void ParseAndWrite_NestedTypes()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "nested_types.chunkl"));
        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var file = result.File!;
        var fields = file.Chunks[0].Body.OfType<FieldDeclaration>().ToList();

        var spawn = fields.Single(f => f.Name == "Spawn");
        Assert.Equal("CGameCtnMacroBlockInfo.BlockSpawn", spawn.Type.Name);
        Assert.Equal("empty", ChunkLParser.WriteExpression(spawn.DefaultValue!));
        Assert.NotNull(spawn.TrailingComment);

        var optional = fields.Single(f => f.Name == "OptionalSpawns");
        Assert.Equal(spawn.Type.Name, optional.Type.Name);
        Assert.True(optional.Type.IsNullable);
        Assert.Equal(1, optional.Type.ArrayDimensions);
        Assert.Contains(optional.Attributes!.Entries, e => e.Name == "external");

        var counted = fields.Single(f => f.Name == "Spawns");
        Assert.Equal(spawn.Type.Name, counted.Type.Name);
        Assert.Equal("Count", counted.Type.FixedArrayCount);

        var nested = fields.Single(f => f.Name == "NestedItems");
        Assert.Equal("Outer.Inner.Leaf", nested.Type.Name);
        Assert.True(nested.Type.ChunkPreference);
        Assert.True(nested.Type.IsNullable);
        Assert.Equal(2, nested.Type.ArrayDimensions);

        var cast = fields.Single(f => f.Name == "CastItems");
        Assert.Equal("Outer.Inner.Leaf", cast.Type.Name);
        Assert.Equal("Other.Inner", cast.Type.CastTarget!.QualifyingType);
        Assert.Equal("Target", cast.Type.CastTarget.Name);
        Assert.True(cast.Type.ChunkPreference);
        Assert.True(cast.Type.IsNullable);
        Assert.Equal(1, cast.Type.ArrayDimensions);
        var direction = fields.Single(f => f.Name == "Direction");
        Assert.Equal("Outer.Inner", direction.Type.CastTarget!.QualifyingType);
        Assert.Equal("Direction", direction.Type.CastTarget.Name);

        Assert.Null(fields.Last().Name);
        Assert.Equal(spawn.Type.Name, fields.Last().Type.Name);
        var archived = file.Archives[0].Body.OfType<FieldDeclaration>().ToList();
        Assert.Equal(spawn.Type.Name, archived[0].Type.Name);
        Assert.Equal("Outer._Inner.Leaf2", archived[1].Type.Name);
        Assert.Equal(spawn.Type.Name, file.Properties[0].Type.Name);
        Assert.Equal(optional.Type.Name, file.Properties[1].Type.Name);
        Assert.True(file.Properties[1].Type.IsNullable);
        Assert.Equal(1, file.Properties[1].Type.ArrayDimensions);

        var written = ChunkLParser.Write(file);
        Assert.Contains("CGameCtnMacroBlockInfo.BlockSpawn Spawn = empty", written);
        Assert.Contains("Outer.Inner.Leaf<Other.Inner.Target>*?[] CastItems", written);
        Assert.Contains("property CGameCtnMacroBlockInfo.BlockSpawn CurrentSpawn", written);
        var reparsed = ChunkLParser.ParseSource(written);
        Assert.True(reparsed.Success, string.Join("; ", reparsed.Diagnostics));
        Assert.Equal(written, ChunkLParser.Write(reparsed.File!));
    }

    [Theory]
    [InlineData(".Outer")]
    [InlineData("Outer.")]
    [InlineData("Outer..Inner")]
    [InlineData("Outer.123")]
    [InlineData("Outer. Inner")]
    [InlineData("Outer .Inner")]
    public void Parse_RejectsInvalidTypePaths(string type)
    {
        foreach (var declaration in new[]
        {
            $"0x001\n  {type} Value\n",
            $"property {type} Value\n  get = empty\n"
        })
        {
            var result = ChunkLParser.ParseSource("TestClass 0x01000000\n" + declaration);
            Assert.False(result.Success);
        }
    }
}
