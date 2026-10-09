# ChunkL (.NET)

The .NET library parses [ChunkL](../README.md) (`.chunkl`) files into a syntax tree, reports diagnostics, and writes the tree back to ChunkL.

## Install

Add the [`ChunkL` NuGet package](https://www.nuget.org/packages/ChunkL):

```sh
dotnet add package ChunkL
```

## Usage

```csharp
using System;
using ChunkL;

// Parse a .chunkl file from disk.
var result = ChunkLParser.Parse("CGameCtnBlock.chunkl");

if (!result.Success)
{
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.Error.WriteLine(diagnostic);
    }
    return;
}

var file = result.File!;
Console.WriteLine($"{file.Header.ClassName} ({file.Header.ClassId})");
var source = ChunkLParser.Write(file);
```

Use `ChunkLParser.ParseSource(string)` for source text or `ChunkLParser.Parse(Stream)` for a stream. Check `Success` and `Diagnostics` before using `File`.

The syntax tree includes constructors, properties with ordered accessors, and `is` patterns. Boolean `and` and `or` parse as logical operators and are written as `&&` and `||`; pattern operators retain their keyword spelling. The writer preserves declaration order so writing a file does not change inline default initialization order.

## Semantic analysis

Use `ChunkLParser.Analyze(file)` after parsing to resolve shared fields and check local semantic rules:

```csharp
var analysis = ChunkLParser.Analyze(file);
foreach (var diagnostic in analysis.Diagnostics)
{
    Console.Error.WriteLine(diagnostic);
}

foreach (var field in analysis.Class.InlineDefaults)
{
    Console.WriteLine($"{field.Name}: {ChunkLParser.WriteExpression(field.DefaultValue!)}");
}
```

Check `analysis.Success` before using the model. `Class.Fields` contains one member per name, its resolved storage type, and all declarations with their original wire types. `Class.InlineDefaults` lists defaults once in source order and omits fields directly assigned by the constructor. `ConstructorAssignments` lists the subsequent assignments, including assignments to properties. Named archives have their own field scopes. `Archives` also reports `RequiresExternalVersion`, including self archives and versions read through a local base archive.

Analysis reports conflicting declarations or defaults, invalid property reads and writes, recursive accessors, incompatible known primitive results, and chunk version blocks without a version source. Parsing and semantic analysis report diagnostics separately so tools can also work with incomplete layouts.

The library provides syntax and initialization metadata. Consumers apply type defaults, evaluate expressions and property accessors, create fresh `empty` values, and serialize binary data. External type compatibility, constructors, inherited class members, and caller-supplied archive versions require the consuming tool's context. `GetDeclarationsInSourceOrder()` exposes the order used by the writer and analyzer. `TypeReference.ArrayCounts` preserves each array dimension's optional count; `ArrayDimensions` and `FixedArrayCount` remain available.

Fields support game-specific defaults such as `int Count = 0 [TMSX = 5, TM2020 = 8]`. Pass a game label to `ChunkLParser.Analyze(file, "TMSX")` to select `StoredField.DefaultValue` and order `InlineDefaults` by each selected expression's first declaration. `FallbackDefaultDeclaration` and `GameDefaults` retain the alternatives. Omitting the game selects fallback defaults. `FieldDeclaration.GetDefaultValue("TMSX")` also selects a single declaration's default, including anonymous and version fields.

## Build and test

```sh
dotnet build
dotnet test
```

## License

Licensed under the [MIT License](../LICENSE.txt).
