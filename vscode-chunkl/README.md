# ChunkL for Visual Studio Code

The VS Code extension adds syntax highlighting and completions for [ChunkL](../README.md) (`.chunkl`) files.

See the [language specification](../SPECIFICATION.md) for the full syntax.

## Features

- A TextMate grammar highlights class headers, chunk offsets, attributes, version qualifiers, field types, control flow, and comments.
- Completions suggest declarations, field types, control flow keywords, common class types, and attribute names.
- Language configuration sets `//` as the line comment and closes brackets and quotes automatically. The grammar also highlights `#` comments.

Completions come from fixed lists. The extension does not parse files or report diagnostics.

The grammar and snippets support expression based fixed array counts and `while` blocks.

## Debugging

1. Open the repository root in VS Code and run `npm ci` in `vscode-chunkl` once.
2. Press **F5** and choose **Debug ChunkL extension**. The launch task compiles the extension and opens an Extension Development Host with `examples/debug.chunkl` available.
3. Open `debug.chunkl`, place a breakpoint in `src/completionProvider.ts`, and request completions with **Ctrl+Space**. Breakpoints map to TypeScript through source maps.

After changing TypeScript, restart the debug session to recompile. After changing the grammar or language configuration, run **Developer: Reload Window** in the Extension Development Host.

## Building and packaging

```sh
npm ci
npm run compile
npm test
npm run package
```

`npm run package` creates a `.vsix` file. Install it with **Extensions: Install from VSIX...** in VS Code.

## License

Licensed under the [MIT License](../LICENSE.txt).
