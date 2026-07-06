# ChunkL Language Specification

ChunkL (`.chunkl`) is a domain-specific language used in GBX.NET to describe the binary serialization structure of Gbx (GameBox) classes and their chunks. Each `.chunkl` file corresponds to one engine class and declares how its chunks and archive types are read from or written to a Gbx binary stream.

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
16. [Full File Example](#16-full-file-example)

---

## 1. File Structure

A `.chunkl` file consists of, in order:

1. A **class header** (first line of the file)
2. Optional **class modifiers** (lines starting with `- `)
3. One or more **chunk declarations** (lines starting with `0x`)
4. Optional **archive declarations** (lines starting with `archive `)
5. Optional **enum declarations** (lines starting with `enum `)
6. Optional **flags declarations** (lines starting with `flags `)

There is no required ordering constraint between archive and enum declarations, but they typically appear after all chunk declarations.

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

- `ClassName` — The GBX engine class name (e.g., `CGameCtnChallenge`, `CPlugSolid2Model`).
- `0xCLASSID000` — The 32-bit class identifier in hexadecimal. The lower 12 bits are always `000` at the class level; individual chunks use the lower 12 bits as the chunk offset.

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

Each attribute is either a **flag** (name only) or a **key–value pair** (`name: value`). Multiple attributes are comma-separated and may be freely combined.

Both key and value can contain spaces.

#### Examples

Flag Attributes:

| Attribute | Meaning |
|---|---|
| `skippable` | The chunk can be skipped by the reader (has a leading size field in the binary). |
| `ignore` | The chunk is intentionally skipped/ignored entirely. |
| `header` | The chunk is part of the GBX header (not the body). |
| `demonstration` | Documents a known but usually unused code path; not generated into production code. |

Key–Value Attributes:

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

Each entry is a free-form identifier — typically a short label representing a software version, game title, platform, or other context in which the chunk is known to appear. There is no fixed set of allowed identifiers; any alphanumeric label is valid.

A version identifier may be followed by `.vN` to indicate the maximum chunk version observed in that context:

```
0x003 (header, struct: SHeaderCommon) [TM10.v0, TMPU.v1, TMF.v5, MP3.v11, TM2020.v11] // common
```

This means: chunk `0x003` exists in all listed contexts; in `TM10` the chunk format goes up to version 0, in `TMPU` up to version 1, etc.

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
- **Anonymous fields** have no name; their value is read but not exposed.
- An optional **(attribute list)** may follow the field name, using the same syntax as class and chunk attributes: flags (`name`) and key–value pairs (`name: value`), comma-separated. Both `name` and `value` can contain spaces.

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
| `assert` | Assert a condition is true; raise an exception otherwise. See [Control Flow](#10-control-flow). |
| `loop` | Repeat a field block a number of times. See [Control Flow](#10-control-flow). |

Most of these support attribute lists and trailing comments. Exceptions: `if`, `loop`, and `switch` do not support attribute lists.

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

Appending `*` directly after a class type name marks the field as preferring chunks over the self-archive when the referenced class defines both. Without `*`, the self-archive is used when available; with `*`, chunks are used instead.

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

Fixed count arrays: placing an integer literal or a previously-read integer field name inside `[...]` declares an array whose element count is not read from the stream:

```
float[4] Quaternion
byte[16] Guid
int[3] Rgb
vec3[8] BoundingCorners
int Count
vec3[Count] Points
```

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
        v6+
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

- The condition is a free-form expression spanning from `if`/`else if` to the end of the line (or to a trailing comment).
- `else if` and `else` must immediately follow the last field of the preceding branch (at the same indentation level as `if`).
- Any number of `else if` branches may appear; `else` is optional.
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

Skips a number of bytes in the binary stream without reading them into a named field. The byte count is a free-form expression:

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

The condition is a free-form expression, identical in form to an `if` condition. `assert` supports attribute lists.

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

The meaning of the attributes is defined by the block's implementation — they describe the kind of blocking logic to apply (e.g., a named scope, a custom read/write strategy, metadata for code generation). An empty attribute list is also valid.

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

Repeats a field block a fixed number of times. The count is either a literal integer or the name of a previously-read integer field:

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
```

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

- The `switch` expression is a free-form expression, identical in form to an `if` condition.
- Each `case` value is a free-form expression.
- `default` is optional and matches when no `case` value matches.
- Cases do not fall through; each block is independent.
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
- An explicit value `= N` may be assigned to any member; subsequent members auto-increment unless also given explicit values.
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

A flags type is referenced the same way as an enum cast — by annotating an integer field with the flags name in angle brackets:

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
  WaypointKind[18..19] // bits 18–19: 2-bit integer
  Variant[20..23]      // bits 20–23: 4-bit integer

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

An archive declaration may include an attribute list in parentheses after the name, using the same flag / key–value syntax as class and chunk attributes:

```
archive ArchiveName (flag, key: value, ...)
```

Keys and values can contain spaces.

#### Examples

Flag Attributes:

| Attribute | Meaning |
|---|---|
| `contextual` | The archive requires access to the enclosing class node during serialization. |

Key–Value Attributes:

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

A field declaration may include a default value using `= value`. The value is a free-form literal expression spanning from `=` to the end of the line, stopping before an attribute list `(...)` or a trailing comment.

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

An assignment without a type keyword mutates an already-declared variable using an expression:

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

## 16. Full File Example

The following illustrates a typical `.chunkl` file combining most language features:

```
CGameCtnBlock 0x03057000 // Block placed on a map.

0x002 [TM10]
  ident BlockModel
  byte<Direction> Direction // Facing direction of the block.
  byte3 Coord              // Position in block coordinates.
  int Flags

archive
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