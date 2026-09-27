# ChunkL Language Specification

ChunkL (`.chunkl`) describes the binary layout of classes. Each file defines one class, its chunks, and optional archives. Version conditions describe how fields change between versions.

---

## Table of Contents

1. [File Structure](#1-file-structure)
2. [Class Header](#2-class-header)
3. [Chunk Declarations](#3-chunk-declarations)
4. [Chunk Attributes](#4-chunk-attributes)
5. [Version Qualifiers](#5-version-qualifiers)
6. [Field Declarations](#6-field-declarations)
7. [Casted Fields](#7-casted-fields)
8. [Type Modifiers](#8-type-modifiers)
9. [Version Blocks](#9-version-blocks)
10. [Control Flow](#10-control-flow)
11. [Enum Declarations](#11-enum-declarations)
12. [Flags Declarations](#12-flags-declarations)
13. [Archive Declarations](#13-archive-declarations)
14. [Assignment and Default Values](#14-assignment-and-default-values)
15. [Comments](#15-comments)
16. [Expressions](#16-expressions)
17. [Full File Example](#17-full-file-example)

---

## 1. File Structure

A `.chunkl` file starts with a class header. The remaining declarations are:

- Optional **class attributes** (lines starting with `- `)
- **Chunk declarations** (lines starting with `0x`)
- **Archive declarations** (lines starting with `archive `)
- **Enum declarations** (lines starting with `enum `)
- **Flags declarations** (lines starting with `flags `)

Class attributes go immediately after the header. Chunks, archives, enums, and flags may then appear in any order. Blank lines and comments are allowed between declarations.

```
ClassName 0xCLASSID000 // optional class comment
- inherits: ParentClassName
- abstract

0xAAA [game_list] // chunk description
  field declarations...

archive ArchiveName
  field declarations...

enum EnumName
  Value1
  Value2

flags FlagsName
  MemberA[0]
  MemberB[1..3]
```

---

## 2. Class Header

The first non-empty line of every `.chunkl` file is the **class header**:

```
ClassName 0xCLASSID000
```

- `ClassName`: the class name, such as `CGameCtnChallenge` or `CPlugSolid2Model`.
- `0xCLASSID000`: the 32-bit class identifier in hexadecimal. The lower 12 bits are `000` at the class level. Individual chunks use these bits as their offset.

An optional inline doc comment may follow:

```
CGameCtnGhost 0x03092000 // A ghost.
CGameCtnBlock 0x03057000 // Block placed on a map.
```

### Class Attributes

Immediately after the class header, one or more lines starting with `- ` may appear. Each attribute follows the general form:

```
- attributeName: attributeValue
```

Boolean (flag) attributes with no value use the shorter form:

```
- attributeName
```

Both attributeName and attributeValue can contain spaces.

#### Examples

| Attribute | Value | Meaning |
|---|---|---|
| `inherits` | `ParentClass` | This class extends `ParentClass`. Inherits all of its chunks. |
| `abstract` | *(none)* | This class is abstract and cannot be directly instantiated. |

```
CGameCtnGhost 0x03092000
- inherits: CGameGhost

SMetaPtr 0x300E5000
- abstract
```

---

## 3. Chunk Declarations

Each chunk is declared by a **chunk header line** at the root indentation level (no leading space), followed by zero or more indented field declarations that describe the chunk's binary content.

```
0xAAA [game_list] // description
  field...
  field...
```

The chunk offset `0xAAA` is a 3-digit hex number. It forms the full chunk ID when combined with the class ID: e.g., for class `0x03043000`, chunk `0x01F` has full ID `0x0304301F`.

Chunks with a full 8-digit ID are also valid:
```
0x11001000 (base: 0x000)
```

An empty chunk body (no fields) is valid and means the chunk has no serializable data:
```
0x03A (skippable, ignore)
```

---

## 4. Chunk Attributes

Chunk attributes appear in parentheses immediately after the chunk offset, before the version list:

```
0xHHH (flag, key: value, ...) [version_list] // comment
```

Each attribute is either a **flag** (name only) or a **key-value pair** (`name: value`). Multiple attributes are comma-separated and may be freely combined.

Both key and value can contain spaces.

#### Examples

Flag Attributes:

| Attribute | Meaning |
|---|---|
| `skippable` | The chunk can be skipped by the reader (has a leading size field in the binary). |
| `ignore` | The chunk is intentionally skipped/ignored entirely. |
| `header` | The chunk is part of the GBX header (not the body). |
| `demonstration` | Documents a known but usually unused code path. Code generators omit it from production code. |

Key-value attributes:

| Attribute | Value | Meaning |
|---|---|---|
| `struct` | `StructName` | Names a struct for code generation (used with `header`). |
| `base` | `0xHHH` | This chunk inherits all fields from the referenced chunk and adds more. The keyword `base` in the body calls the parent. |

```
0x029 (skippable) [TMF, MP3, TMT, MP4, TM2020] // password
0x038 (skippable, ignore) [MP3, TMT, MP4, TM2020]
0x028 (base: 0x027) [TMU, TMF, MP3, TMT, MP4, TM2020] // old realtime thumbnail + comments
0x002 (header, struct: SHeaderTMDesc) [TM10.v3, TMF.v11, TM2020.v13] // description
0x015 (demonstration) [TM10] // flags+location, TM1.0
```

---

## 5. Version Qualifiers

A version list, enclosed in square brackets `[...]`, specifies which version contexts contain this chunk. It appears after attributes and before the inline comment.

```
0xHHH [VersionA, VersionB, VersionC]
```

Each entry is an alphanumeric identifier for a game, software version, platform, or other context in which the chunk appears. There is no fixed list of labels.

A version identifier may be followed by `.vN` to indicate the maximum chunk version observed in that context:

```
0x003 (header, struct: SHeaderCommon) [TM10.v0, TMPU.v1, TMF.v5, MP3.v11, TM2020.v11] // common
```

Chunk `0x003` exists in all listed contexts. The highest observed format version is 0 in `TM10`, 1 in `TMPU`, and so on.

---

## 6. Field Declarations

Field declarations appear inside a chunk body or archive body, indented by one additional level relative to their container (two spaces per indentation level).

```
  type FieldName
  type FieldName = default_value
  type FieldName // comment
  type FieldName # comment
  type FieldName = default_value // comment
  type // anonymous field (no name)
  type FieldName (flag, key: value, ...)
```

- **Named fields** have a name following the type.
- **Anonymous fields** have no name. Their value is read but not exposed.
- An optional **(attribute list)** may follow the field name. It contains comma-separated flags (`name`) and key-value pairs (`name: value`). Both the name and value can contain spaces.

### Special Keywords as Field Types

The following are special keywords that generate control flow rather than a data field:

| Keyword | Effect |
|---|---|
| `version` | Read/write a 32-bit chunk version integer. The version is stored and accessible as `Version` (or `v` inside archives). |
| `versionb` | Read/write a version encoded as a single byte. |
| `base` | Call the read/write method of the base chunk (when the chunk has a `base:` attribute). |
| `return` | Stop processing the current chunk/archive early (early return). |
| `throw` | Mark an unimplemented or unsupported section. Parsing will throw an exception. |
| `block` | Group fields under a custom attribute-driven block. See [Control Flow](#10-control-flow). |
| `switch` | Dispatch to a field block based on a value. See [Control Flow](#10-control-flow). |
| `skip` | Skip a number of bytes without reading their contents. See [Control Flow](#10-control-flow). |
| `assert` | Assert that a condition is true and raise an exception otherwise. See [Control Flow](#10-control-flow). |
| `loop` | Repeat a field block a number of times. See [Control Flow](#10-control-flow). |
| `while` | Repeat a field block while a condition remains true. See [Control Flow](#10-control-flow). |

Most of these support attribute lists and trailing comments. `if`, `loop`, `while`, and `switch` do not support attribute lists.

---

## 7. Casted Fields

Any type may be annotated with a cast target using angle brackets:

```
type<TargetType> FieldName
```

The underlying type is read/written as normal, then the value is cast to `TargetType`. The cast target is most commonly an enum name, but may be any compatible type.

```
byte<Direction> Dir
int<PlayMode> Mode
int<EItemType> ItemType
```

Cross-file references use a dotted path:

```
byte<CPlugSurface.MaterialId> SurfacePhysicId
```

---

## 8. Type Modifiers

### Chunk-Preference Modifier

Appending `*` directly after a class type name makes the field use chunks when the referenced class defines both chunks and a self archive. Without `*`, the self archive is used when available.

It appears before `?` when combined:

```
CGameCtnBlock* PlacedBlock
CGameCtnBlock*? OptionalBlock
```

### Nullable Types

Appending `?` to any type makes it nullable. A nullable value is preceded by a sentinel (typically `-1` for integers or a specific null-marker byte) indicating whether the value is present.

```
int? Respawns
timeint? RaceTime
bool? CarCanBeDirty
iso4? SpawnLocGround
CPlugGameSkin? Remapping (external)
```

### Array Types

Appending `[]` to a type declares a plain array. `[]` may be stacked for nested arrays:

```
float[] Xs
vec3[] Checkpoints
int3[] Coords
transquat[] U03
int[][] NestedData
```

Nullable element arrays: `[]` after `?` on the element type:
```
CMwNod?[] NadeoSkinFids (external)
```

Fixed count arrays: placing an integer expression inside `[...]` declares an array whose element count is not read from the stream. The expression uses the syntax in [Expressions](#16-expressions) and may refer to previously read fields:

```
float[4] Quaternion
byte[16] Guid
int[3] Rgb
vec3[8] BoundingCorners
int Count
int Width
int Height
int NumSamples
vec3[Count] Points
short[Count * 2] PackedSamples
int[Width * Height] IconPixels
int[NumSamples - 1] SampleOffsets
```

The count expression is evaluated once, immediately before the array is read or written. It must produce a non-negative integer. A fixed count array has no additional count prefix in the binary stream. Spaces inside the brackets are allowed; `short[Count * 2]` is one type declaration.

Fixed count and nullable elements may be combined:
```
int?[4] OptionalValues
int?[Count] OptionalValues
```

---

## 9. Version Blocks

A `version` (or `versionb`) field at the start of a chunk or archive body reads or writes a version number. Subsequent block conditions check this version to conditionally include fields.

### Version Condition Syntax

Version conditions are written at the field indentation level, followed by a block of fields at one deeper indentation level:

```
vN+      → if version >= N      (present in this version and later)
vN-      → if version <= N      (present in this version and earlier)
vN=      → if version == N      (present only in this exact version)
vN..M    → if version >= N && version <= M  (present in versions N through M inclusive)
```

A version condition may have an optional **(attribute list)** after the version marker and before the inline comment. It uses comma-separated flags (`name`) and key-value pairs (`name: value`). Both names and values can contain spaces. The tool that consumes the attribute defines its meaning:

```
vN+ (flag, key: value) // comment
```

Example:
```
0x002 [TM10.v3, TMF.v11, TM2020.v13] // description
  versionb
  v2-
    ident MapInfo = empty
    string MapName = empty
  bool NeedUnlock
  v1+
    timeint? BronzeTime
    timeint? SilverTime
    v4+
      int Cost
      v5+
        bool IsLapRace
        v6+ (new_in: TM2020)
          int<PlayMode> Mode
  v3..7
    bool HasCustomData
    string CustomDataKey
```

Version conditions nest: each block is active when **all** enclosing version conditions are satisfied simultaneously.

---

## 10. Control Flow

### `if` Statement

Conditional execution of a field block, with optional `else if` and `else` branches:

```
  if condition
    field...
  else if condition
    field...
  else
    field...
```

- The condition is an [expression](#16-expressions).
- `else if` and `else` must immediately follow the preceding branch at the same indentation level as `if`.
- Any number of `else if` branches may appear. `else` is optional.
- `if` does not support attribute lists.

Examples:
```
  if HasBadges
    SBadge Badge

  if !IsUsingGameMaterial
    id Link

  if (Flags & (1 << 15)) != 0
    id Author
    CGameCtnBlockSkin Skin

  if ItemType != EItemType::Ornament
    int SlotCost
  else if ItemType == EItemType::Character
    bool IsPlayable
  else
    int UnknownData

  if MaterialName == null || MaterialName == ""
    CPlugMaterialUserInst MaterialUserInst

  if Version >= 1
    int AuthorVersion
```

### `return`

Terminates reading/writing of the current chunk or archive early:

```
  v5+
    CGameItemPlacementParam DefaultPlacement (external)
    return
  vec3[]
```

`return` supports attribute lists.

### `throw`

Marks an incomplete, unsupported, or deliberately unimplemented section. Encountering `throw` during parsing raises an exception with an optional message:

```
  throw
  throw (type: NotSupportedException)
```

`throw` supports attribute lists.

### `skip`

Skips a number of bytes in the binary stream without reading them into a named field. The byte count is an [expression](#16-expressions):

```
  skip N
  skip CountField // comment
```

`skip` supports attribute lists.

### `assert`

Asserts that a condition holds during parsing. If the condition is false, an exception is raised:

```
  assert condition
  assert condition (type: InvalidDataException)
```

The condition is an [expression](#16-expressions). `assert` supports attribute lists.

Examples:
```
  assert Version <= 5
  assert Signature == 0xDEADBEEF
  assert Count >= 0 (type: CorruptedDataException)
```

### `block` Statement

Groups a set of fields under a customizable logic block. Unlike `if`, a `block` takes an attribute list rather than a condition:

```
  block (flag, key: value, ...) // comment
    field...
    field...
```

The block implementation defines what its attributes mean. They may specify a named scope, a custom read/write strategy, or code generation metadata. An empty attribute list is also valid.

Example:
```
  block (name: Collision)
    vec3 Position
    float Radius

  block (name: Visual, optional)
    CPlugBitmap Texture
    vec4 Color

  block
    int TestField
    float TestField2
```

### `loop` Statement

Repeats a field block a fixed number of times. The count is an integer [expression](#16-expressions), evaluated once before the first iteration:

```
  loop N // comment
    field...
    field...
```

`loop` does not support attribute lists.

Examples:
```
  loop 4
    float Value

  int Count
  loop Count
    string Name
    int Flags
    bool IsEnabled

  loop Width * Height
    int Pixel
```

### `while` Statement

Repeats a field block while a boolean [expression](#16-expressions) is true. The condition is evaluated before every iteration, including the first. Fields used by the first evaluation must already have values, and the body can update them for later evaluations:

```
  byte HasNext
  while HasNext != 0
    int ItemType
    byte[] ItemData
    byte HasNext
```

In this example, a field declaration with the same name inside the loop updates the existing value on each iteration. The terminating `HasNext` byte is consumed, but the body is skipped when it is zero. `while` may be nested inside other control flow blocks and may have a trailing comment. It does not take an attribute list.

### `switch` Statement

Dispatches to one of several field blocks based on the value of an expression:

```
  switch expression // comment
    case value
      field...
    case value
      field...
    default
      field...
```

- The `switch` expression is an [expression](#16-expressions).
- Each `case` value is an [expression](#16-expressions).
- `default` is optional and matches when no `case` value matches.
- Cases do not fall through. Each block is independent.
- `switch` does not support attribute lists.

Examples:
```
  switch ItemType
    case EItemType::Ornament
      int SlotCost
    case EItemType::Character
      bool IsPlayable
    default
      int UnknownData

  switch Version
    case 0
      string LegacyName
    case 1
      id Name
      int Flags
```

---

## 11. Enum Declarations

Enums are declared at the file root level (no indentation):

```
enum EnumName // optional comment
  ValueName1
  ValueName2
  ValueName3 = 18   // explicit integer value
```

- Enum names do not support spaces.
- Values are listed one per line, indented by two spaces.
- An explicit value `= N` may be assigned to any member. Subsequent members increment automatically unless given another explicit value.
- Inline comments on values are supported.

Examples:
```
enum EAxis
  X
  Y
  Z

enum AnimEase
  Constant
  Linear
  QuadIn
  QuadOut
  ...
  BounceOut
  BounceInOut

enum ELayerType
  Geometry
  SubdivideSmooth
  Translation
  ...
  Light = 18

enum MapKind // The map's intended use.
  EndMarker
  Campaign
  Puzzle
  ...
```

---

## 12. Flags Declarations

Flags declarations describe how the bits of an integer field are partitioned into named members. They are declared at the file root level:

```
flags FlagsName // optional comment
  MemberName[bit_range]
  MemberName[bit_range] // optional comment
```

Each member occupies a contiguous range of bits within the parent integer, specified after the name:

| Bit range syntax | Meaning |
|---|---|
| `[N]` | Single bit at position `N`. The member is a boolean. |
| `[N..M]` | Bits from position `N` to `M` (inclusive). The member is an integer of `M - N + 1` bits. |

Bit positions are zero-indexed from the least significant bit.

A flags type is referenced like an enum cast. Put the flags name in angle brackets after an integer field type:

```
int<MyFlags> Flags
uint<MyFlags> Flags
```

Examples:
```
flags EBlockFlags
  HasSkin[15]          // bit 15: skin present
  HasAuthor[16]        // bit 16: author present
  IsGhost[17]          // bit 17
  WaypointKind[18..19] // bits 18-19: 2-bit integer
  Variant[20..23]      // bits 20-23: 4-bit integer

flags EItemFlags
  IsVisible[0]
  IsCollidable[1]
  PhysicsType[2..5]
```

---

## 13. Archive Declarations

Archives are inline, value-semantic serialization structures (similar to structs). They are declared at the file root level:

```
archive ArchiveName
  field declarations...
```

### Archive Attributes

An archive declaration may include an attribute list in parentheses after the name. It uses the same flag and key-value syntax as chunk attributes:

```
archive ArchiveName (flag, key: value, ...)
```

Keys and values can contain spaces.

#### Examples

Flag Attributes:

| Attribute | Meaning |
|---|---|
| `contextual` | The archive requires access to the enclosing class node during serialization. |

Key-value attributes:

| Attribute | Value | Meaning |
|---|---|---|
| `inherits` | `BaseName` | This archive extends `BaseName`. The child body must call `base` to serialize inherited fields. `BaseName` may be another archive or an interface (e.g., `IKey`). |

```
archive DerivedArchive (inherits: BaseArchive)
  base
  int AdditionalField

archive Layer (contextual)
  int Ver
  bool CrystalEnabled
  id LayerId

archive GeometryLayer (inherits: Layer, contextual)
  base
  int GeometryVersion
  Crystal Crystal

archive Key (inherits: IKey)
  timefloat Time
  vec2 Position
  float Rotation
  vec2 Scale
  v1+
    float Opacity = 1
```

### Self Archive

An `archive` with no name optionally defines the serialization format for the class itself:

```
archive
  id Name
  byte<Direction> Direction
  byte3 Coord
  v0=
    short Flags
  v1+
    int Flags
```

---

## 14. Assignment and Default Values

### Constant Field Values

A field declaration may include a default value using `= value`. The value is an [expression](#16-expressions).

```
bool IsEnabled = true
bool Collidable = true
float Opacity = 1
float Depth = 0.5f
int DecalIntensity = 1
int3 ClipTriggerSize = (1, 1, 1)
version = 1
int = -1
ident MapInfo = empty
Material[] Materials = empty
```

### Computed Assignments

An assignment without a type keyword mutates an already-declared variable using an [expression](#16-expressions):

```
Flags = Flags & 0x1FFFF
Flags = Flags | 0x2000
MapCoordTarget = MapCoordOrigin
```

This operation conventionally does not include the field type.

### Anonymous Numeric Value Assertion

When a field has no name and an `= N` default, it asserts the read value equals `N`:

```
int = 1
version = 2
```

---

## 15. Comments

ChunkL supports two single-line comment syntaxes (no block comments):

```
// This is a comment
# This is also a comment
int FieldName // inline trailing comment
int FieldName # inline trailing comment
```

Comments may appear anywhere a trailing comment is valid: after any declaration, field, chunk header, enum value, or on a line by itself.

---

## 16. Expressions

Expressions appear in `if`/`else if` and `while` conditions, `switch`/`case` values, `assert` conditions, `skip` counts, `loop` counts, fixed array counts, field default values, and computed assignments. Every expression is parsed according to the grammar below using standard C-style operator precedence.

### Grammar

```
expression     = logical_or
logical_or     = logical_and ('||' logical_and)*
logical_and    = equality ('&&' equality)*
equality       = comparison (('==' | '!=') comparison)*
comparison     = bitwise_or (('<' | '>' | '<=' | '>=') bitwise_or)*
bitwise_or     = bitwise_xor ('|' bitwise_xor)*
bitwise_xor    = bitwise_and ('^' bitwise_and)*
bitwise_and    = shift ('&' shift)*
shift          = additive (('<<' | '>>') additive)*
additive       = multiplicative (('+' | '-') multiplicative)*
multiplicative = unary (('*' | '/') unary)*
unary          = ('!' | '~' | '-') unary | primary
primary        = grouped | tuple | literal | scoped_identifier | identifier

grouped        = '(' expression ')'
tuple          = '(' expression ',' expression (',' expression)* ')'
```

A primary expression is one of:

| Form | Examples | Notes |
|------|----------|-------|
| Integer literal | `0`, `1`, `-1`, `42` | Decimal integer (the unary minus is parsed as a unary operator) |
| Hex literal | `0xDEADBEEF`, `0x1FFFF` | Case-insensitive hex digits after `0x` |
| Float literal | `0.5f`, `1.0` | Optional `f` suffix |
| String literal | `""`, `"hello"` | Double-quoted, backslash escaping |
| Boolean literal | `true`, `false` | |
| Null literal | `null` | |
| Empty literal | `empty` | Represents an empty/default collection |
| Identifier | `Version`, `Flags`, `Count` | Any alphanumeric name (field or variable reference) |
| Scoped identifier | `EItemType::Ornament` | `Qualifier::Member` for enum/flags values |
| Grouped | `(Flags & (1 << 15))` | Parenthesized sub-expression |
| Tuple | `(1, 1, 1)` | Comma-separated values in parentheses |

### Operators

Operators are listed from lowest to highest precedence:

| Precedence | Operators | Associativity | Meaning |
|---|---|---|---|
| 1 | `\|\|` | Left | Logical OR |
| 2 | `&&` | Left | Logical AND |
| 3 | `==` `!=` | Left | Equality |
| 4 | `<` `>` `<=` `>=` | Left | Comparison |
| 5 | `\|` | Left | Bitwise OR |
| 6 | `^` | Left | Bitwise XOR |
| 7 | `&` | Left | Bitwise AND |
| 8 | `<<` `>>` | Left | Bit shift |
| 9 | `+` `-` | Left | Addition, subtraction |
| 10 | `*` `/` | Left | Multiplication, division |
| 11 | `!` `~` `-` | Right (unary) | Logical NOT, bitwise NOT, negation |

### Examples

```
Version >= 1
(Flags & (1 << 15)) != 0
MaterialName == null || MaterialName == ""
ItemType != EItemType::Ornament
Flags & 0x1FFFF
(1, 1, 1)
```

---

## 17. Full File Example

The following illustrates a typical `.chunkl` file combining most language features:

```
CGameCtnBlock 0x03057000 // Block placed on a map.

0x002 [TM10]
  ident BlockModel
  byte<Direction> Direction // Facing direction of the block.
  byte3 Coord              // Position in block coordinates.
  int Flags

archive
  version
  id Name
  byte<Direction> Direction
  byte3 Coord
  v0=
    short Flags
  v1+
    int Flags
  if (Flags & (1 << 15)) != 0
    id Author
    CGameCtnBlockSkin Skin
  v2+
    if (Flags & (1 << 19)) != 0
      CPlugCharPhySpecialProperty PhyCharSpecialProperty
    if (Flags & (1 << 20)) != 0
      CGameWaypointSpecialProperty WaypointSpecialProperty
    if (Flags & (1 << 17)) != 0
      id DecalId
      int DecalIntensity = 1
      int DecalVariant = -1

archive SSquareCardEventIds
  int
  int
  ident[]

enum Direction
  North
  East
  South
  West
```

Another example showing version blocks, enums, and list fields:

```
CGameCtnMediaClip 0x03079000

0x00D [MP4.v0, TM2020.v1] // MP tracks
  version
  CGameCtnMediaTrack[] Tracks (deprec)
  string Name
  bool StopWhenLeave
  bool
  bool StopWhenRespawn
  string
  float
  int LocalPlayerClipEntIndex = -1

0x00E (skippable) [TM2020]
  version
  int
```

Another example showing archive inheritance and contextual archives:

```
CPlugCrystal 0x09003000
- inherits: CPlugTreeGenerator

0x003 [MP4.v2, TM2020.v2] // materials
  version
  Material[] Materials = empty

enum ELayerType
  Geometry
  SubdivideSmooth
  Translation
  Rotation
  Scale
  Mirror
  Light = 18

enum EAxis
  X
  Y
  Z

archive Material
  string MaterialName
  if MaterialName == null || MaterialName == ""
    CPlugMaterialUserInst MaterialUserInst

archive Layer (contextual)
  int Ver
  bool CrystalEnabled
  id LayerId
  string LayerName
  if Ver >= 1
    bool IsEnabled = true

archive GeometryLayer (inherits: Layer, contextual)
  base
  int GeometryVersion
  Crystal Crystal
  int[] U02
  if GeometryVersion >= 1
    bool IsVisible = true
    bool Collidable = true

archive TranslationLayer (inherits: ModifierLayer, contextual)
  base
  int TranslationVersion
  vec3 Translation

archive MirrorLayer (inherits: ModifierLayer, contextual)
  base
  int MirrorVersion
  int<EAxis> Axis
  float Distance
  bool Independently
```
