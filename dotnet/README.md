# ChunkL (.NET)

.NET implementation of the [ChunkL](../README.md) parser and writer.

## Installing the library

The library is published as a NuGet package:

```
dotnet add package ChunkL
```

## Using the library

```csharp
using ChunkL;

// Parse a .chunkl file from disk
var result = ChunkLParser.Parse("CGameCtnBlock.chunkl");

// Or parse ChunkL source text directly
var result2 = ChunkLParser.ParseSource(sourceText);

// Or parse from a stream (an embedded resource or network stream)
using var stream = File.OpenRead("CGameCtnBlock.chunkl");
var result3 = ChunkLParser.Parse(stream);

if (!result.Success)
{
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.WriteLine(diagnostic); // Severity, position, message, and optional code
    }
    return;
}

var file = result.File!;
Console.WriteLine($"{file.Header.ClassName} ({file.Header.ClassId})");

foreach (var chunk in file.Chunks)
{
    Console.WriteLine($"Chunk {chunk.Offset.HexValue}: {chunk.Body.Count} statements");
}

// Write the parsed AST back to ChunkL source
var written = ChunkLParser.Write(file);
```

## Building and testing

```
dotnet build
dotnet test
```

## License

Licensed under the [MIT License](../LICENSE.txt).
