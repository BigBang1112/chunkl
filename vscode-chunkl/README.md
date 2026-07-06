# ChunkL for Visual Studio Code

Language support for [ChunkL](https://github.com/BigBang1112/chunkl) (`.chunkl`) files.

For the full language reference, see [SPECIFICATION.md](../SPECIFICATION.md).

## Features

- **Syntax highlighting** for `.chunkl` files via a TextMate grammar (class headers, chunk offsets, attributes, version qualifiers, field types and modifiers, control flow keywords, comments, and more).
- **Editor completions** for:
  - Root-level keywords (`archive`, `enum`, `flags`) and class attributes
  - Field-level primitive types, control-flow keywords (`if`, `switch`, `loop`, `block`, `skip`, `assert`, `return`, `throw`, ...), and common class types
  - Attribute keys/flags inside `(...)` lists (e.g. `skippable`, `header`, `base:`, `inherits:`)
- **Language configuration** for comments (`//`, `#`) and bracket/auto-closing behavior.

## Building and packaging

```
npm install
npm run compile
npm run package
```

`npm run package` produces a `.vsix` file (via `vsce`) that can be installed manually through the **Extensions: Install from VSIX...** command in VS Code.

## License

Licensed under the [MIT License](../LICENSE.txt).
