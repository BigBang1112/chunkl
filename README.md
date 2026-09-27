# ChunkL

ChunkL (`.chunkl`) describes the binary layout of classes. Each file defines one class, its chunks, and optional archives. Version conditions describe how fields change between versions.

See the [language specification](SPECIFICATION.md) for the full syntax.

## What ChunkL looks like

```text
CGameCtnBlock 0x03057000 // Block placed on a map.

0x002 [TM10]
  ident BlockModel
  byte<Direction> Direction // Facing direction of the block.
  byte3 Coord // Position in block coordinates.
  int Flags

archive
  version
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

## .NET library

The [.NET library](dotnet/README.md) parses `.chunkl` files into a syntax tree, reports diagnostics, and writes the tree back to ChunkL. Its README covers installation and usage.

## VS Code extension

The [VS Code extension](vscode-chunkl/README.md) adds syntax highlighting and completions for `.chunkl` files.

## Rust library

The [Rust crate](chunkl-rs/README.md) provides a lexer, public syntax tree, parser, diagnostics, and writer. It has no runtime dependencies.

## License

Licensed under the [MIT License](LICENSE.txt).
