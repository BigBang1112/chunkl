# ChunkL

ChunkL (`.chunkl`) is a domain-specific language to describe the binary serialization structure of classes and their chunks. Each `.chunkl` file corresponds to one engine class and declares how its chunks and archive types are read from or written to a binary stream, in a way that stays backwards compatible across game versions.

For the full language reference, see [SPECIFICATION.md](SPECIFICATION.md).

## What ChunkL looks like

```
CGameCtnBlock 0x03057000 // Block placed on a map.

0x002 [TM10]
  ident BlockModel
  byte<Direction> Direction // Facing direction of the block.
  byte3 Coord // Position in block coordinates.
  int Flags

archive
  id Name
  byte<Direction> Direction
  byte3 Coord
  v0=
    short Flags
  v1+
    int Flags

enum Direction
  North
  East
  South
  West
```

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

## VS Code extension

The [vscode-chunkl](vscode-chunkl/) extension adds `.chunkl` language support to VS Code: syntax highlighting (via a TextMate grammar) and editor completions. See that folder for build/packaging instructions.

## Building and testing

```
dotnet build
dotnet test
```

## License

Licensed under the [MIT License](LICENSE.txt).
