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

## Build and test

```sh
cargo build
cargo test
```

## License

Licensed under the [MIT License](../LICENSE.txt).
