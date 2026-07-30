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

## .NET library

The [dotnet](dotnet/) folder contains the .NET implementation, published as a NuGet package. See that folder for installation, usage, and build/test instructions.

## VS Code extension

The [vscode-chunkl](vscode-chunkl/) extension adds `.chunkl` language support to VS Code: syntax highlighting (via a TextMate grammar) and editor completions. See that folder for build/packaging instructions.

## Rust library

The dependency-free Rust implementation is in [chunkl-rs](chunkl-rs/). It provides the lexer,
public syntax tree, parser, diagnostics, and canonical writer.

```sh
cd chunkl-rs
cargo test
```

## License

Licensed under the [MIT License](LICENSE.txt).
