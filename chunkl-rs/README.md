# ChunkL (Rust)

The Rust crate parses [ChunkL](../README.md) (`.chunkl`) files into a public syntax tree and writes the tree back to ChunkL. It also exposes the lexer and diagnostics. The crate has no runtime dependencies and requires Rust 1.85 or later.

## Usage

```rust
use chunkl::{parse_file, write};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let result = parse_file("CGameCtnBlock.chunkl")?;
    if !result.success() {
        for diagnostic in result.diagnostics {
            eprintln!("{diagnostic:?}");
        }
        return Ok(());
    }

    let file = result.file.unwrap();
    println!("{} ({})", file.header.class_name, file.header.class_id);
    println!("{}", write(&file));
    Ok(())
}
```

Use `parse_source` for strings and `parse_reader` for any `std::io::Read`. Check `ParseResult::success()` before using `file`.

The public syntax tree includes constructors, properties with ordered accessors, and `is` patterns. Boolean `and` and `or` parse as logical operators and are written as `&&` and `||`; pattern operators retain their keyword spelling. The writer preserves declaration order so writing a file does not change inline default initialization order. `parse_expression_checked` returns an expression and its diagnostics; `parse_expression` returns only the expression.

## Semantic analysis

Use `analyze(&file)` after parsing to resolve shared fields and check local semantic rules:

```rust
let analysis = chunkl::analyze(&file);
for diagnostic in &analysis.diagnostics {
    eprintln!("{diagnostic}");
}

for index in &analysis.class.inline_defaults {
    let field = &analysis.class.fields[*index];
    println!("{}: {:?}", field.name, field.default_declaration);
}
```

Check `analysis.success()` before using the model. `class.fields` contains one member per name, its resolved storage type, and all declarations with their original wire types. `class.inline_defaults` holds member indices once in source order and omits fields directly assigned by the constructor. `constructor_assignments` lists the subsequent assignments, including assignments to properties. Named archives have their own field scopes. `archives` also reports `requires_external_version`, including self archives and versions read through a local base archive.

Analysis reports conflicting declarations or defaults, invalid property reads and writes, recursive accessors, incompatible known primitive results, and chunk version blocks without a version source. Parsing and semantic analysis report diagnostics separately so tools can also work with incomplete layouts.

The library provides syntax and initialization metadata. Consumers apply type defaults, evaluate expressions and property accessors, create fresh `empty` values, and serialize binary data. External type compatibility, constructors, inherited class members, and caller-supplied archive versions require the consuming tool's context. `declarations_in_source_order()` exposes the order used by the writer and analyzer. `TypeReference::array_counts` preserves each array dimension's optional count; `array_dimensions` and `fixed_array_count` remain available.

## Build and test

```sh
cargo build --locked
cargo test --locked
```

After changing the crate version or dependencies in `Cargo.toml`, update and commit `Cargo.lock` with the manifest. For a version-only change, run `cargo update --workspace --offline`, then `cargo test --locked`. CI and publishing use `--locked` to catch a missing or stale lockfile.

## License

Licensed under the [MIT License](../LICENSE.txt).
