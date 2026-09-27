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

## Build and test

```sh
dotnet build
dotnet test
```

## License

Licensed under the [MIT License](../LICENSE.txt).
