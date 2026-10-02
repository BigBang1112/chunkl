# ChunkL for Visual Studio Code

Syntax highlighting, completions, and snippets for [ChunkL](https://github.com/BigBang1112/chunkl) (`.chunkl`) files.

ChunkL describes the binary layout of classes through chunks, archives, and version conditions. This extension helps you write and edit those definitions in VS Code.

## Features

- **Syntax highlighting** for class headers, chunks, archives, field types, attributes, version conditions, constructors, properties, control flow, and comments.
- **Context-aware completions** for keywords, types, attributes, operators, and literal values, including `is`, `not`, `and`, `or`, and `!` in expressions.
- **Field and property suggestions** from declarations in the current file, including declarations that appear later. Named archives have their own field scope, and assignment suggestions include writable members.
- **Enum and flags completions** for type names and dotted values such as `Direction.North`. Typing `Direction.` in an expression suggests its members.
- **Snippets** for chunks, archives, enums, flags, constructors, properties, loops, conditionals, and version blocks, with editable placeholders.
- **Editing support** for line comments, automatic bracket and quote closing, and indentation.
- **Syntax diagnostics** for common errors such as invalid headers, indentation, missing expressions, unterminated strings, unmatched brackets, and incorrect member access.

Requires VS Code **1.85 or later**.

## Example

```chunkl
CGameCtnBlock 0x03057000 // Block placed on a map.

constructor
  Direction = Direction.North

0x002 [TM10]
  ident BlockModel
  byte<Direction> Direction
  byte3 Coord
  int Flags

archive
  version
  id Name
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

See the [language specification](https://github.com/BigBang1112/chunkl/blob/main/SPECIFICATION.md) for the full syntax.

## Support

Completions and syntax checks use the current file. For full parsing and semantic validation, see the [ChunkL libraries](https://github.com/BigBang1112/chunkl#net-library).

Report bugs or request features on [GitHub Issues](https://github.com/BigBang1112/chunkl/issues). The source code is available in the [ChunkL repository](https://github.com/BigBang1112/chunkl/tree/main/vscode-chunkl).

## License

Licensed under the [MIT License](https://github.com/BigBang1112/chunkl/blob/main/LICENSE.txt).
