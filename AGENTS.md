# Repository guidance

For every new or changed ChunkL language feature, update `SPECIFICATION.md` and carry the change through all code projects: `dotnet/`, `chunkl-rs/`, and `vscode-chunkl/`. Update relevant tests and fixtures in each project so the specification and implementations stay in sync.

For package version or dependency changes, update the corresponding lockfiles in the same change. After changing `chunkl-rs/Cargo.toml`, refresh `chunkl-rs/Cargo.lock` (use `cargo generate-lockfile --offline` for version-only changes) and run `cargo test --locked`. Keep the root package versions in `vscode-chunkl/package-lock.json` in sync with `vscode-chunkl/package.json`. Preserve `--locked` in Rust CI and publishing commands.
