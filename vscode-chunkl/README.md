# ChunkL for Visual Studio Code

The VS Code extension adds syntax highlighting and completions for [ChunkL](../README.md) (`.chunkl`) files.

See the [language specification](../SPECIFICATION.md) for the full syntax.

## Features

- A TextMate grammar highlights class headers, chunk offsets, attributes, version qualifiers, field types, control flow, and comments.
- Completions suggest declarations, field types, control flow keywords, common class types, and attribute names.
- Language configuration sets `//` as the line comment and closes brackets and quotes automatically. The grammar also highlights `#` comments.

Completions come from fixed lists. The extension does not parse files or report diagnostics.

## Building and packaging

```sh
npm ci
npm run compile
npm run package
```

`npm run package` creates a `.vsix` file. Install it with **Extensions: Install from VSIX...** in VS Code.

## License

Licensed under the [MIT License](../LICENSE.txt).
