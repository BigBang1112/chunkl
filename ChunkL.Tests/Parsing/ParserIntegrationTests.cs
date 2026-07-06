using ChunkL.Syntax;
using Xunit;

namespace ChunkL.Tests.Parsing;

public class ParserIntegrationTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Parse_Minimal_File()
    {
        var result = ChunkLParser.Parse(FixturePath("minimal.chunkl"));

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
        Assert.NotNull(result.File);
        Assert.Equal("CGameMinimal", result.File!.Header.ClassName);
        Assert.Equal("0x03000000", result.File.Header.ClassId);
        Assert.Single(result.File.Chunks);
        Assert.Equal("0x001", result.File.Chunks[0].Offset.HexValue);
        Assert.Single(result.File.Chunks[0].Body);

        var field = Assert.IsType<FieldDeclaration>(result.File.Chunks[0].Body[0]);
        Assert.Equal("int", field.Type.Name);
        Assert.Equal("Value", field.Name);
    }

    [Fact]
    public void Parse_Minimal_Stream()
    {
        using var stream = File.OpenRead(FixturePath("minimal.chunkl"));
        var result = ChunkLParser.Parse(stream);

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
        Assert.NotNull(result.File);
        Assert.Equal("CGameMinimal", result.File!.Header.ClassName);
        Assert.Equal("0x03000000", result.File.Header.ClassId);
    }

    [Fact]
    public void Parse_FullExample_File()
    {
        var result = ChunkLParser.Parse(FixturePath("full_example.chunkl"));

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
        var file = result.File!;

        Assert.Equal("CGameCtnBlock", file.Header.ClassName);
        Assert.Equal("0x03057000", file.Header.ClassId);
        Assert.NotNull(file.Header.TrailingComment);
        Assert.Equal("Block placed on a map.", file.Header.TrailingComment!.Text);

        // Chunk 0x002
        Assert.Single(file.Chunks);
        var chunk = file.Chunks[0];
        Assert.Equal("0x002", chunk.Offset.HexValue);
        Assert.Single(chunk.VersionQualifiers);
        Assert.Equal("TM10", chunk.VersionQualifiers[0].Label);
        Assert.Equal(4, chunk.Body.Count);

        // First field: ident BlockModel
        var field1 = Assert.IsType<FieldDeclaration>(chunk.Body[0]);
        Assert.Equal("ident", field1.Type.Name);
        Assert.Equal("BlockModel", field1.Name);

        // Second field: byte<Direction> Direction
        var field2 = Assert.IsType<FieldDeclaration>(chunk.Body[1]);
        Assert.Equal("byte", field2.Type.Name);
        Assert.NotNull(field2.Type.CastTarget);
        Assert.Equal("Direction", field2.Type.CastTarget!.Name);
        Assert.Equal("Direction", field2.Name);

        // Archives
        Assert.Equal(2, file.Archives.Count);

        // Self archive (no name)
        Assert.Null(file.Archives[0].Name);
        Assert.NotEmpty(file.Archives[0].Body);

        // Named archive
        Assert.Equal("SSquareCardEventIds", file.Archives[1].Name);

        // Enum
        Assert.Single(file.Enums);
        Assert.Equal("Direction", file.Enums[0].Name);
        Assert.Equal(4, file.Enums[0].Members.Count);
        Assert.Equal("North", file.Enums[0].Members[0].Name);
        Assert.Equal("West", file.Enums[0].Members[3].Name);
    }

    [Fact]
    public void Parse_ControlFlow_File()
    {
        var result = ChunkLParser.Parse(FixturePath("control_flow.chunkl"));

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
        var file = result.File!;

        Assert.Equal("CGameCtnMediaClip", file.Header.ClassName);
        Assert.Equal(3, file.Chunks.Count);

        // First chunk 0x00D
        var chunk1 = file.Chunks[0];
        Assert.Equal("0x00D", chunk1.Offset.HexValue);
        Assert.Equal(2, chunk1.VersionQualifiers.Count);
        Assert.Equal("MP4", chunk1.VersionQualifiers[0].Label);
        Assert.Equal(0, chunk1.VersionQualifiers[0].MaxVersion);
        Assert.Equal("TM2020", chunk1.VersionQualifiers[1].Label);
        Assert.Equal(1, chunk1.VersionQualifiers[1].MaxVersion);

        // Field with attribute list
        var tracks = Assert.IsType<FieldDeclaration>(chunk1.Body[1]);
        Assert.Equal("CGameCtnMediaTrack", tracks.Type.Name);
        Assert.Equal(1, tracks.Type.ArrayDimensions);
        Assert.Equal("Tracks", tracks.Name);
        Assert.NotNull(tracks.Attributes);
        Assert.Single(tracks.Attributes!.Entries);
        Assert.Equal("deprec", tracks.Attributes.Entries[0].Name);

        // Default value
        var localPlayer = chunk1.Body.OfType<FieldDeclaration>()
            .First(f => f.Name == "LocalPlayerClipEntIndex");
        Assert.Equal("-1", localPlayer.DefaultValue);

        // Second chunk (skippable)
        var chunk2 = file.Chunks[1];
        Assert.NotNull(chunk2.Attributes);
        Assert.Contains(chunk2.Attributes!.Entries, e => e.Name == "skippable");

        // Third chunk with if, loop, switch
        var chunk3 = file.Chunks[2];
        Assert.NotEmpty(chunk3.Body);

        // version field
        Assert.Equal("version", Assert.IsType<FieldDeclaration>(chunk3.Body[0]).Type.Name);

        // if statement
        var ifStmt = Assert.IsType<IfStatement>(chunk3.Body[1]);
        Assert.Contains("Version >= 1", ifStmt.Condition);

        // loop statement
        var loopStmt = Assert.IsType<LoopStatement>(chunk3.Body[2]);
        Assert.Equal("4", loopStmt.CountExpression);
        Assert.Single(loopStmt.Body);

        // switch statement
        var switchStmt = Assert.IsType<SwitchStatement>(chunk3.Body[3]);
        Assert.Equal("Version", switchStmt.Expression);
        Assert.Equal(2, switchStmt.Cases.Count);
        Assert.NotNull(switchStmt.Default);
    }

    [Fact]
    public void Parse_EnumsFlags_File()
    {
        var result = ChunkLParser.Parse(FixturePath("enums_flags.chunkl"));

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
        var file = result.File!;

        // Class attributes
        Assert.Single(file.ClassAttributes);
        Assert.Equal("inherits", file.ClassAttributes[0].Name);
        Assert.Equal("CPlugTreeGenerator", file.ClassAttributes[0].Value);

        // Enums
        Assert.Equal(2, file.Enums.Count);

        var layerType = file.Enums[0];
        Assert.Equal("ELayerType", layerType.Name);
        Assert.Equal(7, layerType.Members.Count);
        Assert.Equal("Light", layerType.Members[6].Name);
        Assert.Equal("18", layerType.Members[6].ExplicitValue);

        var axis = file.Enums[1];
        Assert.Equal("EAxis", axis.Name);
        Assert.Equal(3, axis.Members.Count);

        // Flags
        Assert.Single(file.Flags);
        var blockFlags = file.Flags[0];
        Assert.Equal("EBlockFlags", blockFlags.Name);
        Assert.Equal(5, blockFlags.Members.Count);
        Assert.Equal("HasSkin", blockFlags.Members[0].Name);
        Assert.Equal(15, blockFlags.Members[0].Bits.Start);
        Assert.True(blockFlags.Members[0].Bits.IsSingleBit);
        Assert.Equal("WaypointKind", blockFlags.Members[3].Name);
        Assert.Equal(18, blockFlags.Members[3].Bits.Start);
        Assert.Equal(19, blockFlags.Members[3].Bits.End);
        Assert.False(blockFlags.Members[3].Bits.IsSingleBit);
    }

    [Fact]
    public void Parse_Archives_File()
    {
        var result = ChunkLParser.Parse(FixturePath("archives.chunkl"));

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
        var file = result.File!;

        Assert.Equal(4, file.Archives.Count);

        // Material archive
        var material = file.Archives[0];
        Assert.Equal("Material", material.Name);
        Assert.Equal(2, material.Body.Count);
        Assert.IsType<IfStatement>(material.Body[1]);

        // Layer archive with contextual attribute
        var layer = file.Archives[1];
        Assert.Equal("Layer", layer.Name);
        Assert.NotNull(layer.Attributes);
        Assert.Contains(layer.Attributes!.Entries, e => e.Name == "contextual");

        // GeometryLayer with multiple attributes
        var geoLayer = file.Archives[2];
        Assert.Equal("GeometryLayer", geoLayer.Name);
        Assert.NotNull(geoLayer.Attributes);
        Assert.Equal(2, geoLayer.Attributes!.Entries.Count);
        Assert.Equal("inherits", geoLayer.Attributes.Entries[0].Name);
        Assert.Equal("Layer", geoLayer.Attributes.Entries[0].Value);
        Assert.Equal("contextual", geoLayer.Attributes.Entries[1].Name);

        // Key archive with version block
        var key = file.Archives[3];
        Assert.Equal("Key", key.Name);
        Assert.NotNull(key.Attributes);
        Assert.Equal("inherits", key.Attributes!.Entries[0].Name);
        Assert.Equal("IKey", key.Attributes.Entries[0].Value);

        // Check version block in Key
        var versionBlock = key.Body.OfType<VersionCondition>().FirstOrDefault();
        Assert.NotNull(versionBlock);
        Assert.Equal(VersionConditionKind.GreaterOrEqual, versionBlock!.Kind);
        Assert.Equal(1, versionBlock.Version);
    }

    [Fact]
    public void Parse_VersionConditions()
    {
        var source = """
            TestClass 0x01000000

            0x001
              version
              v0=
                short Flags
              v1+
                int Flags
              v3..7
                bool HasData
            """;

        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));

        var body = result.File!.Chunks[0].Body;

        // version field
        Assert.Equal("version", Assert.IsType<FieldDeclaration>(body[0]).Type.Name);

        // v0=
        var v0 = Assert.IsType<VersionCondition>(body[1]);
        Assert.Equal(VersionConditionKind.Exact, v0.Kind);
        Assert.Equal(0, v0.Version);

        // v1+
        var v1 = Assert.IsType<VersionCondition>(body[2]);
        Assert.Equal(VersionConditionKind.GreaterOrEqual, v1.Kind);
        Assert.Equal(1, v1.Version);

        // v3..7
        var v3 = Assert.IsType<VersionCondition>(body[3]);
        Assert.Equal(VersionConditionKind.Range, v3.Kind);
        Assert.Equal(3, v3.Version);
        Assert.Equal(7, v3.VersionEnd);
    }

    [Fact]
    public void Parse_IfElseIfElse()
    {
        var source = """
            TestClass 0x01000000

            0x001
              if ItemType != EItemType::Ornament
                int SlotCost
              else if ItemType == EItemType::Character
                bool IsPlayable
              else
                int UnknownData
            """;

        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));

        var ifStmt = Assert.IsType<IfStatement>(result.File!.Chunks[0].Body[0]);
        Assert.Contains("ItemType != EItemType::Ornament", ifStmt.Condition);
        Assert.Single(ifStmt.Body);
        Assert.Single(ifStmt.ElseIfs);
        Assert.Contains("ItemType == EItemType::Character", ifStmt.ElseIfs[0].Condition);
        Assert.NotNull(ifStmt.Else);
        Assert.Single(ifStmt.Else!.Body);
    }

    [Fact]
    public void Parse_TypeModifiers()
    {
        var source = """
            TestClass 0x01000000

            0x001
              int? Respawns
              CGameCtnBlock* PlacedBlock
              CGameCtnBlock*? OptionalBlock
              float[] Xs
              int[][] NestedData
              CMwNod?[] NadeoSkinFids (external)
            """;

        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));

        var body = result.File!.Chunks[0].Body;

        // int? Respawns
        var f0 = ((FieldDeclaration)body[0]).Type;
        Assert.True(f0.IsNullable);
        Assert.Equal("int", f0.Name);

        // CGameCtnBlock* PlacedBlock
        var f1 = ((FieldDeclaration)body[1]).Type;
        Assert.True(f1.ChunkPreference);
        Assert.Equal("CGameCtnBlock", f1.Name);

        // CGameCtnBlock*? OptionalBlock
        var f2 = ((FieldDeclaration)body[2]).Type;
        Assert.True(f2.ChunkPreference);
        Assert.True(f2.IsNullable);

        // float[] Xs
        var f3 = ((FieldDeclaration)body[3]).Type;
        Assert.Equal(1, f3.ArrayDimensions);

        // int[][] NestedData
        var f4 = ((FieldDeclaration)body[4]).Type;
        Assert.Equal(2, f4.ArrayDimensions);

        // CMwNod?[] NadeoSkinFids
        var f5 = ((FieldDeclaration)body[5]).Type;
        Assert.True(f5.IsNullable);
        Assert.Equal(1, f5.ArrayDimensions);
    }

    [Fact]
    public void Parse_ComputedAssignment()
    {
        var source = """
            TestClass 0x01000000

            0x001
              int Flags
              Flags = Flags & 0x1FFFF
            """;

        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));

        var body = result.File!.Chunks[0].Body;
        Assert.Equal("Flags", Assert.IsType<ComputedAssignment>(body[1]).TargetName);
        Assert.Contains("Flags & 0x1FFFF", Assert.IsType<ComputedAssignment>(body[1]).Expression);
    }

    [Fact]
    public void Parse_SpecialKeywords_AreFieldDeclarations()
    {
        var source = """
            TestClass 0x01000000

            0x001
              version
              base
              return
              throw
            """;

        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));

        var body = result.File!.Chunks[0].Body;
        Assert.True(Assert.IsType<FieldDeclaration>(body[0]).IsSpecialKeyword);
        Assert.Equal("version", Assert.IsType<FieldDeclaration>(body[0]).Type.Name);
        Assert.True(Assert.IsType<FieldDeclaration>(body[1]).IsSpecialKeyword);
        Assert.Equal("base", Assert.IsType<FieldDeclaration>(body[1]).Type.Name);
        Assert.IsType<ReturnStatement>(body[2]);
        Assert.IsType<ThrowStatement>(body[3]);
    }

    [Fact]
    public void Parse_SkipAndAssert()
    {
        var source = """
            TestClass 0x01000000

            0x001
              skip 8
              assert Version <= 5
              assert Signature == 0xDEADBEEF (type: CorruptedDataException)
            """;

        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));

        var body = result.File!.Chunks[0].Body;
        var skip = Assert.IsType<SkipStatement>(body[0]);
        Assert.Equal("8", skip.Expression);

        var assert1 = Assert.IsType<AssertStatement>(body[1]);
        Assert.Contains("Version <= 5", assert1.Condition);

        var assert2 = Assert.IsType<AssertStatement>(body[2]);
        Assert.Contains("Signature == 0xDEADBEEF", assert2.Condition);
        Assert.NotNull(assert2.Attributes);
    }

    [Fact]
    public void Parse_BlockStatement()
    {
        var source = """
            TestClass 0x01000000

            0x001
              block (name: Collision)
                vec3 Position
                float Radius
            """;

        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));

        var block = Assert.IsType<BlockStatement>(result.File!.Chunks[0].Body[0]);
        Assert.NotNull(block.Attributes);
        Assert.Equal("name", block.Attributes!.Entries[0].Name);
        Assert.Equal("Collision", block.Attributes.Entries[0].Value);
        Assert.Equal(2, block.Body.Count);
    }

    [Fact]
    public void Parse_CrossFileTypeCast()
    {
        var source = """
            TestClass 0x01000000

            0x001
              byte<CPlugSurface.MaterialId> SurfacePhysicId
            """;

        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));

        var field = Assert.IsType<FieldDeclaration>(result.File!.Chunks[0].Body[0]);
        Assert.NotNull(field.Type.CastTarget);
        Assert.Equal("CPlugSurface", field.Type.CastTarget!.QualifyingType);
        Assert.Equal("MaterialId", field.Type.CastTarget.Name);
    }

    [Fact]
    public void Parse_FixedCountArrays()
    {
        var source = """
            TestClass 0x01000000

            0x001
              float[4] Quaternion
              vec3[Count] Points
              int?[4] OptionalValues
              byte[16] Hash
            """;

        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));

        var body = result.File!.Chunks[0].Body;

        // float[4] Quaternion
        var f0 = ((FieldDeclaration)body[0]).Type;
        Assert.Equal("float", f0.Name);
        Assert.Equal(1, f0.ArrayDimensions);
        Assert.Equal("4", f0.FixedArrayCount);
        Assert.Equal("Quaternion", ((FieldDeclaration)body[0]).Name);

        // vec3[Count] Points
        var f1 = ((FieldDeclaration)body[1]).Type;
        Assert.Equal("vec3", f1.Name);
        Assert.Equal(1, f1.ArrayDimensions);
        Assert.Equal("Count", f1.FixedArrayCount);
        Assert.Equal("Points", ((FieldDeclaration)body[1]).Name);

        // int?[4] OptionalValues
        var f2 = ((FieldDeclaration)body[2]).Type;
        Assert.Equal("int", f2.Name);
        Assert.True(f2.IsNullable);
        Assert.Equal(1, f2.ArrayDimensions);
        Assert.Equal("4", f2.FixedArrayCount);
        Assert.Equal("OptionalValues", ((FieldDeclaration)body[2]).Name);

        // byte[16] Hash
        var f3 = ((FieldDeclaration)body[3]).Type;
        Assert.Equal("byte", f3.Name);
        Assert.Equal(1, f3.ArrayDimensions);
        Assert.Equal("16", f3.FixedArrayCount);
        Assert.Equal("Hash", ((FieldDeclaration)body[3]).Name);
    }

    [Fact]
    public void Parse_ChunkAttributes_File()
    {
        var result = ChunkLParser.Parse(FixturePath("chunk_attributes.chunkl"));

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
        var file = result.File!;

        Assert.Equal("CGameCtnChallenge", file.Header.ClassName);
        Assert.Equal(2, file.ClassAttributes.Count);
        Assert.Equal("inherits", file.ClassAttributes[0].Name);
        Assert.Equal("CGameCtnCollector", file.ClassAttributes[0].Value);
        Assert.Equal("abstract", file.ClassAttributes[1].Name);

        Assert.Equal(8, file.Chunks.Count);

        // header + struct chunk with .vN version qualifiers
        var headerChunk = file.Chunks[0];
        Assert.Equal("0x00D", headerChunk.Offset.HexValue);
        Assert.NotNull(headerChunk.Attributes);
        Assert.Contains(headerChunk.Attributes!.Entries, e => e.Name == "header");
        Assert.Contains(headerChunk.Attributes.Entries, e => e.Name == "struct" && e.Value == "SHeaderCommon");
        Assert.Equal(5, headerChunk.VersionQualifiers.Count);
        Assert.Equal("TM10", headerChunk.VersionQualifiers[0].Label);
        Assert.Equal(0, headerChunk.VersionQualifiers[0].MaxVersion);
        Assert.Equal("TM2020", headerChunk.VersionQualifiers[4].Label);
        Assert.Equal(11, headerChunk.VersionQualifiers[4].MaxVersion);

        // base: reference chunk
        var baseChunk = file.Chunks.Single(c => c.Offset.HexValue == "0x028");
        Assert.NotNull(baseChunk.Attributes);
        Assert.Contains(baseChunk.Attributes!.Entries, e => e.Name == "base" && e.Value == "0x027");
        var baseField = Assert.IsType<FieldDeclaration>(baseChunk.Body[0]);
        Assert.True(baseField.IsSpecialKeyword);
        Assert.Equal("base", baseField.Type.Name);

        // skippable + ignore, empty body
        var ignoredChunk = file.Chunks.Single(c => c.Offset.HexValue == "0x038");
        Assert.Contains(ignoredChunk.Attributes!.Entries, e => e.Name == "skippable");
        Assert.Contains(ignoredChunk.Attributes.Entries, e => e.Name == "ignore");
        Assert.Empty(ignoredChunk.Body);

        // demonstration flag
        var demoChunk = file.Chunks.Single(c => c.Offset.HexValue == "0x015");
        Assert.Contains(demoChunk.Attributes!.Entries, e => e.Name == "demonstration");

        // full 8-digit chunk id with base attribute
        var fullIdChunk = file.Chunks.Last();
        Assert.Equal("0x11001000", fullIdChunk.Offset.HexValue);
        Assert.True(fullIdChunk.Offset.IsFullId);
        Assert.Contains(fullIdChunk.Attributes!.Entries, e => e.Name == "base" && e.Value == "0x000");
    }

    [Fact]
    public void Parse_ControlFlowAdvanced_File()
    {
        var result = ChunkLParser.Parse(FixturePath("control_flow_advanced.chunkl"));

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
        var file = result.File!;

        Assert.Equal(5, file.Chunks.Count);

        // block statements
        var blockChunk = file.Chunks[0];
        Assert.Equal(3, blockChunk.Body.Count);
        var block1 = Assert.IsType<BlockStatement>(blockChunk.Body[0]);
        Assert.Equal("name", block1.Attributes!.Entries[0].Name);
        Assert.Equal("Collision", block1.Attributes.Entries[0].Value);
        Assert.Equal(2, block1.Body.Count);
        var block2 = Assert.IsType<BlockStatement>(blockChunk.Body[1]);
        Assert.Equal(2, block2.Attributes!.Entries.Count);
        var block3 = Assert.IsType<BlockStatement>(blockChunk.Body[2]);
        Assert.Null(block3.Attributes);

        // skip + assert
        var skipAssertChunk = file.Chunks[1];
        var skip1 = Assert.IsType<SkipStatement>(skipAssertChunk.Body[0]);
        Assert.Equal("8", skip1.Expression);
        var skip2 = Assert.IsType<SkipStatement>(skipAssertChunk.Body[2]);
        Assert.Equal("Count", skip2.Expression);
        var assert1 = Assert.IsType<AssertStatement>(skipAssertChunk.Body[3]);
        Assert.Contains("Version <= 5", assert1.Condition);
        var assert2 = Assert.IsType<AssertStatement>(skipAssertChunk.Body[4]);
        Assert.NotNull(assert2.Attributes);

        // return inside version block
        var returnChunk = file.Chunks[2];
        var versionBlock = Assert.IsType<VersionCondition>(returnChunk.Body[0]);
        Assert.IsType<ReturnStatement>(versionBlock.Body[1]);

        // throw statements
        var throwChunk = file.Chunks[3];
        Assert.IsType<ThrowStatement>(throwChunk.Body[0]);
        var throwWithAttr = Assert.IsType<ThrowStatement>(throwChunk.Body[1]);
        Assert.NotNull(throwWithAttr.Attributes);

        // computed assignments
        var computedChunk = file.Chunks[4];
        var computed1 = Assert.IsType<ComputedAssignment>(computedChunk.Body[1]);
        Assert.Equal("Flags", computed1.TargetName);
        Assert.Contains("Flags & 0x1FFFF", computed1.Expression);
        var computed2 = Assert.IsType<ComputedAssignment>(computedChunk.Body[2]);
        Assert.Contains("Flags | 0x2000", computed2.Expression);
    }

    [Fact]
    public void Parse_TypeModifiers_File()
    {
        var result = ChunkLParser.Parse(FixturePath("type_modifiers.chunkl"));

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
        var body = result.File!.Chunks[0].Body;

        var fields = body.OfType<FieldDeclaration>().ToList();

        var surfacePhysicId = fields.Single(f => f.Name == "SurfacePhysicId");
        Assert.NotNull(surfacePhysicId.Type.CastTarget);
        Assert.Equal("CPlugSurface", surfacePhysicId.Type.CastTarget!.QualifyingType);
        Assert.Equal("MaterialId", surfacePhysicId.Type.CastTarget.Name);

        var respawns = fields.Single(f => f.Name == "Respawns");
        Assert.True(respawns.Type.IsNullable);

        var remapping = fields.Single(f => f.Name == "Remapping");
        Assert.NotNull(remapping.Attributes);
        Assert.Contains(remapping.Attributes!.Entries, e => e.Name == "external");

        var optionalCounted = fields.Single(f => f.Name == "OptionalCounted");
        Assert.True(optionalCounted.Type.IsNullable);
        Assert.Equal("Count", optionalCounted.Type.FixedArrayCount);

        // anonymous numeric assertion: `version = 2`
        var versionAssert = fields.Single(f => f.Type.Name == "version");
        Assert.Equal("2", versionAssert.DefaultValue);
        Assert.True(versionAssert.IsSpecialKeyword);

        // trailing anonymous, unnamed field `float`
        var lastField = fields.Last();
        Assert.Null(lastField.Name);
        Assert.Equal("float", lastField.Type.Name);
    }

    [Fact]
    public void Parse_DefaultValueWithFieldName()
    {
        var source = """
            TestClass 0x01000000

            0x001
              bool IsEnabled = true
              float Opacity = 0.5f
              int DecalIntensity = 1
              ident MapInfo = empty
            """;

        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));

        var body = result.File!.Chunks[0].Body;

        Assert.Equal("true", ((FieldDeclaration)body[0]).DefaultValue);
        Assert.Equal("0.5f", ((FieldDeclaration)body[1]).DefaultValue);
        Assert.Equal("1", ((FieldDeclaration)body[2]).DefaultValue);
        Assert.Equal("empty", ((FieldDeclaration)body[3]).DefaultValue);
    }
}
