# ChunkL for Visual Studio Code

The VS Code extension adds syntax highlighting and completions for [ChunkL](../README.md) (`.chunkl`) files.

See the [language specification](../SPECIFICATION.md) for the full syntax.

## Features

- A TextMate grammar highlights class headers, chunk offsets, attributes, version qualifiers, field types, control flow, and comments.
- Completions suggest declarations, control flow keywords, attribute names, and literal values (`true`, `false`, `null`, and `empty`).
- Local enum and flags names are suggested in casts and expressions. Values are suggested as `Enum::Value`, or by member name after `Enum::`. Expressions also suggest previously declared fields in the current chunk or archive.
- Basic keywords such as `archive`, `enum`, `if`, `while`, and `version` complete as plain text. Entries labeled `(snippet)` insert a declaration or block with editable placeholders.
- Language configuration sets `//` as the line comment and closes brackets and quotes automatically. The grammar also highlights `#` comments.

Completions use the current line and a lightweight scan of the open file. The extension does not report diagnostics.

The grammar and snippets support expression based fixed array counts and `while` blocks.

## Debugging

1. Open the repository root in VS Code and run `npm ci` in `vscode-chunkl` once.
2. Press **F5** and choose **Debug ChunkL extension**. The launch task compiles the extension, opens an Extension Development Host with the `examples` folder, and attaches to its inspector on `127.0.0.1:9333`.
3. Open `debug.chunkl`, place a breakpoint in `src/completionProvider.ts`, and request completions with **Ctrl+Space**. Breakpoints map to TypeScript through source maps.

Try completions after `wh` in a chunk body, `if `, `byte<`, or `Direction::`.

After changing TypeScript, restart the debug session to recompile. After changing the grammar or language configuration, run **Developer: Reload Window** in the Extension Development Host.

The explicit IPv4 address avoids a VS Code debugger connection failure on machines where `localhost` resolves to IPv6 (`::1`). If port 9333 is already in use, close the previous Extension Development Host before pressing **F5** again.

## Building and packaging

```sh
npm ci
npm run compile
npm test
npm run package
```

`npm run package` creates a `.vsix` file. Install it with **Extensions: Install from VSIX...** in VS Code.

The [publish workflow](../.github/workflows/publish.yml) publishes the extension to Visual Studio Marketplace in parallel with the NuGet and Cargo packages, then attaches the `.vsix` to the GitHub release. It uses the version in `package.json` and the `BigBang1112` publisher. Configure the repository secret `VSCE_PAT` with a token that can publish under that publisher, following the [VS Code publishing guide](https://code.visualstudio.com/api/working-with-extensions/publishing-extension#get-a-personal-access-token).

## License

Licensed under the [MIT License](../LICENSE.txt).
