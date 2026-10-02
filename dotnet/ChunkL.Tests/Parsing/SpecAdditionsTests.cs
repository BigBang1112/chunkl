using ChunkL.Syntax;
using Xunit;

namespace ChunkL.Tests.Parsing;

public class SpecAdditionsTests
{
    private static ChunkLFile Parse(string source)
    {
        var result = ChunkLParser.ParseSource(source);
        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
        return result.File!;
    }

    [Fact]
    public void NewSyntaxPreservesSourceOrderAndSemanticsAcrossRoundTrip()
    {
        var file = Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "spec_additions.chunkl")));
        Assert.NotNull(file.Constructor);
        Assert.Equal(3, file.Properties.Count);
        Assert.Equal(3, file.Properties[0].Accessors.Count);
        Assert.IsType<IfStatement>(file.Properties[0].Setter!.Body[0]);
        Assert.IsType<SwitchStatement>(file.Properties[2].Setter!.Body[0]);
        var condition = Assert.IsType<BinaryExpression>(Assert.IsType<IfStatement>(file.Archives[0].Body[4]).Condition);
        Assert.Equal(BinaryOperator.LogicalAnd, condition.Operator);
        var grouped = Assert.IsType<ParenthesizedExpression>(condition.Left);
        Assert.IsType<NotPattern>(Assert.IsType<PatternTestExpression>(grouped.Inner).Pattern);

        var model = ChunkLParser.Analyze(file);
        Assert.True(model.Success, string.Join("; ", model.Diagnostics.Select(d => d.ToString())));
        var flags = model.Class.Fields.Single(f => f.Name == "Flags");
        Assert.Equal("int", flags.Type.Name);
        Assert.Equal(new[] { "short", "int" }, flags.Declarations.Select(d => d.Type.Name));
        Assert.Single(model.Class.InlineDefaults, f => f.Name == "Flags");
        Assert.True(model.Class.Fields.Single(f => f.Name == "Count").IsDefaultSkipped);
        Assert.DoesNotContain(model.Class.InlineDefaults, f => f.Name == "Count");
        Assert.Equal(new[] { "First", "Name", "Flags", "Later" }, model.Class.InlineDefaults.Select(f => f.Name));
        Assert.Equal(new[] { "IsGhost", "Count" }, model.ConstructorAssignments.Select(a => a.TargetName));
        Assert.True(model.Archives.Single(a => a.Declaration.Name == "Key").RequiresExternalVersion);
        Assert.False(model.Archives.Single(a => a.Declaration.Name == "Versioned").RequiresExternalVersion);
        Assert.False(model.Archives.Single(a => a.Declaration.Name == "Derived").RequiresExternalVersion);

        var written = ChunkLParser.Write(file);
        Assert.True(written.IndexOf("archive\n", StringComparison.Ordinal) < written.IndexOf("0x001", StringComparison.Ordinal));
        Assert.Contains("int[][3] Matrix", written);
        var second = Parse(written);
        Assert.Equal(written, ChunkLParser.Write(second));
        Assert.Equal(model.Class.InlineDefaults.Select(f => f.Name), ChunkLParser.Analyze(second).Class.InlineDefaults.Select(f => f.Name));
        Assert.IsType<FieldDeclaration>(second.Chunks[0].Body.Last());
    }

    [Theory]
    [InlineData("Name is null or empty", "Name is null or empty")]
    [InlineData("Name is not null and not empty", "Name is not null and not empty")]
    [InlineData("Name is null or \"\"", "Name is null or \"\"")]
    [InlineData("(Name is null or empty) and IsEnabled", "(Name is null or empty) && IsEnabled")]
    [InlineData("IsEnabled or ForceReload and HasData", "IsEnabled || ForceReload && HasData")]
    [InlineData("!true", "!true")]
    [InlineData("!!IsEnabled", "!!IsEnabled")]
    [InlineData("!IsEnabled or IsGhost", "!IsEnabled || IsGhost")]
    [InlineData("!(IsEnabled and HasData)", "!(IsEnabled && HasData)")]
    [InlineData("!(Name is null)", "!(Name is null)")]
    [InlineData("Name is null or empty and not null", "Name is null or empty and not null")]
    [InlineData("ItemType == EItemType.Ornament", "ItemType == EItemType.Ornament")]
    [InlineData("ItemType is EItemType.Ornament", "ItemType is EItemType.Ornament")]
    [InlineData("Header.Size.Count > 0", "Header.Size.Count > 0")]
    public void ExpressionsRetainTheirFullMeaning(string expression, string expected)
    {
        var file = Parse($"Test 0x01000000\n0x001\n  if {expression}\n    int Data\n");
        var condition = Assert.IsType<IfStatement>(file.Chunks[0].Body[0]).Condition;
        Assert.Equal(expected, ChunkLParser.WriteExpression(condition));
        Assert.Equal(ChunkLParser.Write(file), ChunkLParser.Write(Parse(ChunkLParser.Write(file))));
    }

    [Fact]
    public void LogicalNotWorksOnBooleanFieldsAndConditions()
    {
        var file = Parse("""
            Test 0x01000000
            0x001
              bool IsEnabled = !false
              if !IsEnabled
                bool WasDisabled = !!IsEnabled
              else if !(IsEnabled && true)
                bool MaybeDisabled
              assert !IsEnabled
            """);
        var branch = Assert.IsType<IfStatement>(file.Chunks[0].Body[1]);
        Assert.Equal(UnaryOperator.Not, Assert.IsType<UnaryExpression>(branch.Condition).Operator);
        Assert.True(ChunkLParser.Analyze(file).Success);
        var written = ChunkLParser.Write(file);
        Assert.Equal(written, ChunkLParser.Write(Parse(written)));
    }

    [Theory]
    [InlineData("constructor\nconstructor\n")]
    [InlineData("constructor (x)\n")]
    [InlineData("constructor\n  int Flags\n")]
    [InlineData("constructor\n  if true\n    Flags = 1\n")]
    [InlineData("property bool Flag = true\n  get = true\n")]
    [InlineData("property bool Flag (x)\n  get = true\n")]
    [InlineData("property bool Flag\n")]
    [InlineData("property bool Flag\n  get = true\n  get = false\n")]
    [InlineData("property bool Flag\n  get = true\n  set\n")]
    [InlineData("property bool Flag\n  set\n    // Only a comment.\n")]
    [InlineData("property bool Flag\n  set\n    loop 4\n      Flags = 1\n")]
    [InlineData("property bool Flag\n  set\n    version\n")]
    [InlineData("property bool Flag\n    get = true\n")]
    [InlineData("property bool Flag\n  get = Flags is not\n")]
    [InlineData("0x001\n  if Flags is Missing\n    int Data\n")]
    [InlineData("0x001\n  if (true\n    int Data\n")]
    [InlineData("0x001\n  if true nonsense\n    int Data\n")]
    [InlineData("property 42 Flag\n  get = true\n")]
    [InlineData("property string Name\n  get = \"unterminated\n")]
    [InlineData("0x001\n  if Kind is Direction.\n    int Data\n")]
    [InlineData("0x001\n  if Kind is Direction::North\n    int Data\n")]
    [InlineData("0x001\n  int[Header::Count] Data\n")]
    public void InvalidNewSyntaxReportsDiagnostics(string declarations)
    {
        var result = ChunkLParser.ParseSource("Test 0x01000000\n" + declarations);
        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Theory]
    [InlineData("short X\n  int X", true, "int")]
    [InlineData("uint X\n  long X", true, "long")]
    [InlineData("int X\n  uint X", false, "int")]
    [InlineData("int X = 1\n  int X = 2", false, "int")]
    [InlineData("int X = 1 + 2\n  int X = 1+2", true, "int")]
    [InlineData("string X = \"a b\"\n  string X = \"ab\"", false, "string")]
    [InlineData("int X\n  int? X", false, "int")]
    [InlineData("byte<Kind> X\n  int<Kind> X", false, "byte")]
    [InlineData("int[Count][] X\n  int[][Count] X", false, "int")]
    public void RepeatedFieldsResolveOrReportConflicts(string fields, bool success, string storedType)
    {
        var model = ChunkLParser.Analyze(Parse("Test 0x01000000\n0x001\n  " + fields + "\n"));
        Assert.Equal(success, model.Success);
        Assert.Equal(storedType, Assert.Single(model.Class.Fields).Type.Name);
    }

    [Fact]
    public void NamedArchiveFieldsHaveIndependentDefaults()
    {
        var model = ChunkLParser.Analyze(Parse("""
            Test 0x01000000
            constructor
              X = 10
            archive
              int X = 1
            archive Named
              string X = empty
            """));
        Assert.True(model.Success);
        Assert.Empty(model.Class.InlineDefaults);
        Assert.Equal("string", Assert.Single(model.Archives.Single(a => a.Declaration.Name == "Named").InlineDefaults).Type.Name);
    }

    [Theory]
    [InlineData("property bool A\n  get = B\nproperty bool B\n  get = A\n", "Recursive")]
    [InlineData("property int A\n  set\n    B = value\nproperty int B\n  set\n    A = value\n", "Recursive")]
    [InlineData("property int A\n  get = 1\nconstructor\n  A = 2\n", "read-only")]
    [InlineData("property int A\n  set\n    Flags = value\nproperty int B\n  get = A\n0x001\n  int Flags\n", "write-only")]
    [InlineData("property int Flags\n  get = 1\n0x001\n  int Flags\n", "Duplicate")]
    [InlineData("constructor\n  Missing = 1\n", "Unknown")]
    [InlineData("0x001\n  int Flags\n  if Flags is empty\n    int Data\n", "empty pattern")]
    [InlineData("0x001 [Context.v13]\n  v1+\n    int Data\n", "preceding version")]
    [InlineData("property bool Flag\n  get = 42\n", "incompatible")]
    [InlineData("property bool Flag\n  get = Missing\n", "Unknown")]
    [InlineData("property int P\n  set\n    if value is empty\n      Flags = 0\n0x001\n  int Flags\n", "empty pattern")]
    [InlineData("0x001\n  int Flags\n  if Flags or true\n    int Data\n", "boolean operands")]
    [InlineData("0x001\n  int Flags\n  if !Flags\n    int Data\n", "'!' requires a boolean operand")]
    [InlineData("0x001\n  int[] Items\n  if Items is \"\"\n    int Data\n", "requires a string")]
    [InlineData("archive Named\n  int Flags\n  if Flags is empty\n    int Data\n", "empty pattern")]
    [InlineData("property int P\n  get = 1\narchive Named\n  if P == 1\n    int Data\n", "Class properties")]
    [InlineData("property int P\n  get = 1\narchive Named\n  P = 2\n", "Class properties")]
    public void LocalSemanticErrorsAreReportedSeparatelyFromSyntax(string declarations, string message)
    {
        var model = ChunkLParser.Analyze(Parse("Test 0x01000000\n" + declarations));
        Assert.False(model.Success);
        Assert.Contains(model.Diagnostics, d => d.Message.Contains(message, StringComparison.Ordinal));
    }

    [Fact]
    public void ArchiveVersionRequirementsRespectBranchesAndEarlyReturns()
    {
        var model = ChunkLParser.Analyze(Parse("""
            Test 0x01000000
            archive Complete
              if Enabled
                version
              else
                versionb
              v1+
                int Data
            archive Incomplete
              if Enabled
                version
              v1+
                int Data
            archive ReturnBranch
              if Enabled
                return
              else
                version
              v1+
                int Data
            """));
        Assert.True(model.Success);
        Assert.Equal(new[] { false, true, false }, model.Archives.Select(a => a.RequiresExternalVersion));
    }

    [Fact]
    public void AnalysisUsesEditedDefaultExpressions()
    {
        var file = Parse("Test 0x01000000\n0x001\n  int X = 1\n  int X = 1\n");
        Assert.True(ChunkLParser.Analyze(file).Success);
        Assert.IsType<FieldDeclaration>(file.Chunks[0].Body[1]).DefaultValue = new LiteralExpression { Kind = LiteralKind.Integer, Value = "2" };
        Assert.False(ChunkLParser.Analyze(file).Success);
    }

    [Fact]
    public void NamedArchiveFieldsShadowClassProperties()
    {
        var model = ChunkLParser.Analyze(Parse("Test 0x01000000\nproperty int P\n  get = 1\narchive Named\n  string P\n  P = \"text\"\n  if P is empty\n    int Data\n"));
        Assert.True(model.Success, string.Join("; ", model.Diagnostics.Select(d => d.ToString())));
    }
}
