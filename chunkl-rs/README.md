# chunkl

Rust parser, public syntax tree, lexer, and canonical writer for the ChunkL language.

```rust
use chunkl::{parse_file, write};

let result = parse_file("CGameCtnBlock.chunkl")?;
if result.success() {
    let file = result.file.unwrap();
    println!("{} ({})", file.header.class_name, file.header.class_id);
    println!("{}", write(&file));
}
# Ok::<(), std::io::Error>(())
```

Use `parse_source` for strings and `parse_reader` for any `std::io::Read`. The crate has no runtime dependencies.

Run the tests from this directory with:

```text
cargo test
```
