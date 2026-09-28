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
7. [Property Declarations](#7-property-declarations)
8. [Casted Fields](#8-casted-fields)
9. [Type Modifiers](#9-type-modifiers)
10. [Version Blocks](#10-version-blocks)
11. [Control Flow](#11-control-flow)
12. [Enum Declarations](#12-enum-declarations)
13. [Flags Declarations](#13-flags-declarations)
14. [Archive Declarations](#14-archive-declarations)
15. [Constructor Declaration](#15-constructor-declaration)
16. [Assignment and Default Values](#16-assignment-and-default-values)
17. [Comments](#17-comments)
18. [Expressions](#18-expressions)
19. [Full File Example](#19-full-file-example)

---

## 1. File Structure

A `.chunkl` file starts with a class header. The remaining declarations are:

- Optional **class attributes** (lines starting with `- `)
- **Chunk declarations** (lines starting with `0x`)
- **Archive declarations** (lines starting with `archive `)
- **Enum declarations** (lines starting with `enum `)
- **Flags declarations** (lines starting with `flags `)
- **Property declarations** (lines starting with `property `)
- An optional **constructor declaration** (a line containing `constructor`)

Class attributes go immediately after the header. Chunks, archives, enums, flags, properties, and the constructor may then appear in any order. At most one constructor declaration is allowed per class. Blank lines and comments are allowed between declarations.

```
ClassName 0xCLASSID000 // optional class comment
- inherits: ParentClassName
- abstract

constructor
  FieldName = default_value

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

property type PropertyName
  get = expression
```

---

## 2. Class Header

The first non-empty line is the class header:

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

Class attributes go immediately after the header. Each attribute starts with `- `:

```
- attributeName: attributeValue
```

Boolean (flag) attributes with no value use the shorter form:

```
- attributeName
```

Both `attributeName` and `attributeValue` can contain spaces.

#### Examples

| Attribute | Value | Meaning |
|---|---|---|
| `inherits` | `ParentClass` | The class extends `ParentClass` and inherits all of its chunks. |
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

Flag attributes:

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

Each entry is an alphanumeric label for a game, software version, platform, or other context. The language has no fixed list of labels.

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

### Repeated Named Fields

Named fields in chunks and the self archive belong to the class. Repeating a name in these bodies refers to one stored member, including declarations inside version blocks, branches, and loops. Each declaration still reads or writes a value at its own position in the binary layout. Reading it replaces the member's current value; writing it uses that value.

Each named archive has its own field scope. Its fields do not share storage with class fields or fields in other named archives. Repeating a name within that scope follows the same rules.

Repeated declarations must agree on the type, cast target, nullability, chunk preference, and array shape. Uncast scalar integer fields may use different wire types: the stored member uses the widest declared integer type that can represent the full range of every declaration. If none of the declared types can do so, the declarations are invalid. For example, `short Flags` and `int Flags` share an `int` member. Each occurrence keeps its declared wire type, and writing a value outside that type's range is an error.

A shared member has one inline default. Multiple declarations may repeat that default only when their expressions have identical syntax, ignoring whitespace outside string literals. Different defaults are invalid. The default is evaluated once, at the position of the first declaration that supplies it, according to the [initialization rules](#initialization-order). Version conditions and other control flow govern serialization, not which default applies. A constructor assignment to that member skips its inline default across all declarations.

### Special Keywords as Field Types

The following are special keywords that generate control flow rather than a data field:

| Keyword | Effect |
|---|---|
| `version` | Read/write a 32-bit chunk version integer. The version is stored and accessible as `Version` (or `v` inside archives). |
| `versionb` | Read/write a version encoded as a single byte. |
| `base` | Call the read/write method of the base chunk (when the chunk has a `base:` attribute). |
| `return` | Stop processing the current chunk/archive early (early return). |
| `throw` | Mark an unimplemented or unsupported section. Parsing will throw an exception. |
| `block` | Group fields under a custom attribute-driven block. See [Control Flow](#11-control-flow). |
| `switch` | Dispatch to a field block based on a value. See [Control Flow](#11-control-flow). |
| `skip` | Skip a number of bytes without reading their contents. See [Control Flow](#11-control-flow). |
| `assert` | Assert that a condition is true and raise an exception otherwise. See [Control Flow](#11-control-flow). |
| `loop` | Repeat a field block a number of times. See [Control Flow](#11-control-flow). |
| `while` | Repeat a field block while a condition remains true. See [Control Flow](#11-control-flow). |

Most of these support attribute lists and trailing comments. `if`, `loop`, `while`, and `switch` do not support attribute lists.

---

## 7. Property Declarations

A property exposes existing class fields through a getter, a setter, or both. It has no independent stored value and contributes no bytes to the binary layout. Properties are declared at the file root level:

```
property type PropertyName // optional comment
  get = expression
  set
    FieldName = value
```

The header uses the same type syntax as a field. The property name must be unique within the class, including its named fields. The header cannot have an inline default, attributes, or version qualifiers.

Indent accessors by two spaces. A property needs at least one accessor, with at most one `get` and one `set`. Either order is valid.

- `get = expression` evaluates an [expression](#18-expressions) each time you read the property. The result must be compatible with the property type. The getter does not change state or cache its result.
- `set` takes a non-empty block indented by four spaces. Inside this block, `value` is reserved for the incoming value and has the property type.
- A property with only `get` is read-only. Assigning to it is invalid. A property with only `set` is write-only. Using it in an expression is invalid.

A setter supports computed assignments, `if`/`else if`/`else`, and `switch`/`case`/`default`. Use the existing syntax and indentation rules. Assignments run in source order and target class fields or properties with setters. Accessors cannot contain field declarations, binary read/write operations, version blocks, or other control flow statements.

Accessors can refer to class fields and other properties declared anywhere in the file. Reading a property calls its getter. Assigning to it calls its setter. An accessor must not call itself, directly or through another property.

You can use properties in expressions and computed assignments inside chunks and the self archive. Property declarations belong at the file root level and cannot go inside a chunk or archive body. Trailing comments and standalone comment lines are allowed in declarations and setter bodies.

To give a property an initial value, assign to it in the [constructor](#15-constructor-declaration). The assignment calls its setter at that point in the constructor, after the applicable inline field defaults have been applied.

This example exposes two bits of `Flags`, with a setter for `IsGhost`:

```
CGameCtnBlock 0x03057000

constructor
  IsGhost = true

0x002
  int Flags = 0

property bool IsGhost
  get = (Flags & (1 << 17)) != 0
  set
    if value
      Flags = Flags | (1 << 17)
    else
      Flags = Flags & ~(1 << 17)

property bool HasSkin
  get = (Flags & (1 << 15)) != 0
```

Setting `IsGhost` updates bit 17 of `Flags` and leaves the other bits unchanged. The constructor sets that bit initially. Reading `Flags` from the stream later replaces the stored value, so both getters reflect the flags just read. Only `Flags` is serialized.

---

## 8. Casted Fields

Any type may be annotated with a cast target using angle brackets:

```
type<TargetType> FieldName
```

The underlying type is read or written normally, then the value is cast to `TargetType`. The target is usually an enum, but any compatible type is allowed.

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

## 9. Type Modifiers

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

For nullable elements, put `[]` after the element type's `?`:
```
CMwNod?[] NadeoSkinFids (external)
```

Put an integer expression inside `[...]` to declare a fixed count array. Its element count is not read from the stream. The count uses the [expression syntax](#18-expressions) and can refer to fields already read:

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

## 10. Version Blocks

A `version` or `versionb` field reads or writes the current serialization version. It normally appears at the start of a chunk or archive body. Version blocks use the current version to decide which fields to include.

### Version Source and Scope

Each chunk has its own version context. A chunk must read or write `version` or `versionb` before reaching a version block. A version read in another chunk does not supply this context.

Each archive invocation has its own version context, exposed as `v`. Its caller may explicitly supply a version without adding a version field to the archive's binary layout. A `version` or `versionb` declaration in the archive reads or writes a version field and replaces the current context. Calling a nested archive does not implicitly pass the enclosing version, the caller must supply it explicitly when needed. A nested archive's version does not change its caller's context.

The `base` statement serializes inherited archive fields in the current archive's version context. If the base archive reads a version field, that version remains available to subsequent blocks in the derived archive. Inheriting an interface alone does not provide a version.

Reaching a version block without a version established by a version field or explicitly supplied to the archive is invalid. There is no implicit version zero. Chunk qualifiers such as `[TM2020.v13]` describe observed versions and do not initialize the current serialization version.

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

## 11. Control Flow

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

- The condition is an [expression](#18-expressions).
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

Skips a number of bytes in the binary stream without reading them into a named field. The byte count is an [expression](#18-expressions):

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

The condition is an [expression](#18-expressions). `assert` supports attribute lists.

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

The block implementation defines what each attribute means. Attributes can specify a named scope, a custom read/write strategy, or code generation metadata. An empty attribute list is valid.

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

Repeats a field block a fixed number of times. The count is an integer [expression](#18-expressions), evaluated once before the first iteration:

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

Repeats a field block while a boolean [expression](#18-expressions) is true. The condition is evaluated before every iteration, including the first. Fields used by the first evaluation must already have values, and the body can update them for later evaluations:

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

- The `switch` expression is an [expression](#18-expressions).
- Each `case` value is an [expression](#18-expressions).
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

## 12. Enum Declarations

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

## 13. Flags Declarations

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

## 14. Archive Declarations

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
| `inherits` | `BaseName` | The archive extends `BaseName`. Its body must call `base` to serialize inherited fields. `BaseName` can be another archive or an interface such as `IKey`. |

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

`Key` uses `v` explicitly supplied by its caller for the `v1+` block. Its `IKey` interface does not establish that version.

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

This self archive also uses `v` explicitly supplied by its caller. Its binary layout starts with `Name`. A self archive can instead declare `version` or `versionb` when the binary layout includes a version field, as in the [full file example](#19-full-file-example).

---

## 15. Constructor Declaration

`constructor` sets field defaults when a new class instance is created. It appears at the file root level, with assignments indented by two spaces:

```
constructor // optional comment
  FieldName = default_value
  OtherField = expression // optional comment
```

- A class can have at most one constructor. It is optional.
- The header is just `constructor`, with an optional comment. It takes no name, parameters, attributes, or version qualifiers.
- Write assignments as `FieldName = expression`, without a type. A target must be a named class field from a chunk or the self archive, or a [property](#7-property-declarations) with a setter. It can be declared before or after the constructor. Anonymous fields and fields in named archives cannot be targets.
- Values use the [expression syntax](#18-expressions). Assignments run in source order and can use field values already initialized.
- The body allows only assignments and comments. Field declarations, version blocks, and control flow statements are invalid. An empty body is valid.

Constructor assignments run once when the instance is created. If a constructor assignment targets a field, that field's inline default is skipped entirely. Its default expression is not evaluated or applied, even if the field declaration appears before the constructor.

Apply inline defaults to the remaining fields in [initialization order](#initialization-order), then run constructor assignments in source order. Fields omitted from the constructor keep their defaults.

The constructor does not declare fields or read or write binary data. Reading a chunk or self archive later can replace the initial values with values from the stream.

Example:

```
CGameCtnMediaClip 0x03079000

constructor
  Name = ""
  StopWhenLeave = true
  StopWhenRespawn = true
  LocalPlayerClipEntIndex = -1

0x00D [MP4.v0, TM2020.v1]
  version
  string Name
  bool StopWhenLeave = false
  bool StopWhenRespawn
  int LocalPlayerClipEntIndex
```

`StopWhenLeave` starts as `true`, as assigned by the constructor. Its inline `false` default is skipped.

---

## 16. Assignment and Default Values

### Constant Field Values

Add `= value` to a field declaration to give it a default. The value is an [expression](#18-expressions).

For a named class field, this default applies only if the [constructor](#15-constructor-declaration) does not assign to that field. If it does, skip the default expression entirely and use the constructor assignment.

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
string Name = empty
CPlugMaterialUserInst MaterialUserInst = empty
```

### Initialization Order

When a class instance is created:

1. Initialize all stored members to their type defaults: zero for numeric and enum values, `false` for booleans, `null` for nullable and reference types, and member-wise type defaults for non-nullable composite value types.
2. Evaluate and apply inline defaults in file source order, skipping members directly targeted by constructor assignments. For a [repeated field](#repeated-named-fields), use the position of the first declaration that supplies its default and evaluate it only once. Defaults inside serialization conditions still apply at this stage.
3. Run constructor assignments in source order, including calls to property setters.

An inline default reads the current values of any fields or properties it references. Forward references are allowed, but a later default or constructor assignment has not run yet. Reading a field does not evaluate its default on demand. Cyclic field dependencies do not cause recursive evaluation; each default runs once at its position.

For example, these defaults leave both `A` and `B` equal to `1`: `A` reads the initial zero value of `B` before `B`'s inline default runs.

```
0x002
  int A = B + 1
  int B = 1
```

Named archives apply the same type-default and inline-default steps to their own members when created. The class constructor does not initialize members of a named archive.

### Computed Assignments

An assignment updates a declared variable or calls a property's setter. Its value is an [expression](#18-expressions):

```
Flags = Flags & 0x1FFFF
Flags = Flags | 0x2000
MapCoordTarget = MapCoordOrigin
```

Write the assignment without the field type.

### Anonymous Numeric Value Assertion

An anonymous field with `= N` asserts that the value read equals `N`:

```
int = 1
version = 2
```

---

## 17. Comments

ChunkL supports single-line comments with `//` or `#`. It has no block comments:

```
// This is a comment
# This is also a comment
int FieldName // inline trailing comment
int FieldName # inline trailing comment
```

Comments can follow declarations, fields, chunk headers, and enum values. They can also occupy a line of their own.

---

## 18. Expressions

ChunkL uses expressions for conditions, `switch`/`case` values, byte and repetition counts, fixed array counts, defaults, assignments, and property accessors. This includes `if`/`else if`, `while`, `assert`, `skip`, and `loop`. The grammar and operator precedence are defined below.

### Grammar

```
expression     = logical_or
logical_or     = logical_and (('||' | 'or') logical_and)*
logical_and    = equality (('&&' | 'and') equality)*
equality       = comparison (('==' | '!=') comparison)*
comparison     = bitwise_or (comparison_op bitwise_or | 'is' pattern)*
comparison_op  = '<' | '>' | '<=' | '>='
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

pattern        = pattern_or
pattern_or     = pattern_and ('or' pattern_and)*
pattern_and    = pattern_not ('and' pattern_not)*
pattern_not    = 'not' pattern_not | pattern_primary
pattern_primary = literal | scoped_identifier | '(' pattern ')'
```

After `is`, an `and` or `or` that can continue the pattern belongs to that pattern. To combine the result with boolean `and` or `or`, put the complete test in parentheses: `(Name is null or empty) and IsEnabled`.

Primary expressions use these forms:

| Form | Examples | Notes |
|------|----------|-------|
| Integer literal | `0`, `1`, `-1`, `42` | Decimal integer (the unary minus is parsed as a unary operator) |
| Hex literal | `0xDEADBEEF`, `0x1FFFF` | Case-insensitive hex digits after `0x` |
| Float literal | `0.5f`, `1.0` | Optional `f` suffix |
| String literal | `""`, `"hello"` | Double-quoted, backslash escaping |
| Boolean literal | `true`, `false` | |
| Null literal | `null` | |
| Empty literal | `empty` | Produces a non-null empty value or parameterless instance of the expected type. See [Empty Values](#empty-values). |
| Identifier | `Version`, `Flags`, `Count`, `IsGhost`, `value` | An alphanumeric field, variable, or readable property name. Inside a setter, `value` is the incoming value. |
| Scoped identifier | `EItemType::Ornament` | `Qualifier::Member` for enum/flags values |
| Grouped | `(Flags & (1 << 15))` | Parenthesized sub-expression |
| Tuple | `(1, 1, 1)` | Comma-separated values in parentheses |

### Operators

Operators are listed from lowest to highest precedence:

| Precedence | Operators | Associativity | Meaning |
|---|---|---|---|
| 1 | `\|\|` `or` | Left | Logical OR |
| 2 | `&&` `and` | Left | Logical AND |
| 3 | `==` `!=` | Left | Equality |
| 4 | `<` `>` `<=` `>=` `is` | Left | Comparison or pattern test |
| 5 | `\|` | Left | Bitwise OR |
| 6 | `^` | Left | Bitwise XOR |
| 7 | `&` | Left | Bitwise AND |
| 8 | `<<` `>>` | Left | Bit shift |
| 9 | `+` `-` | Left | Addition, subtraction |
| 10 | `*` `/` | Left | Multiplication, division |
| 11 | `!` `~` `-` | Right (unary) | Logical NOT, bitwise NOT, negation |

Boolean `and` and `or` have the same meaning and precedence as `&&` and `||`. Both require boolean operands and short-circuit. `and` skips the right operand when the left is false. `or` skips it when the left is true.

Within a pattern, `not` binds more tightly than `and`, and `and` binds more tightly than `or`. Use parentheses to group patterns. The keywords `is`, `not`, `and`, and `or` are lowercase and must be separate tokens.

### Null and Empty Patterns

`is` tests a value against a pattern and returns a boolean. It evaluates the value once. Literal patterns compare values. Scoped identifiers match the corresponding enum or flags value.

Pattern `and` and `or` test the same value and short-circuit. `not` negates the following pattern.

| Expression | Meaning |
|---|---|
| `Name is null` | `Name` is null. |
| `Name is not null` | `Name` is non-null. |
| `Name is empty` | `Name` is a non-null string of length zero. |
| `Name is null or empty` | `Name` is null or a string of length zero. |
| `Name is null or ""` | The same string test as `Name is null or empty`. |
| `Items is empty` | `Items` is a non-null array of length zero. |
| `Items is null or empty` | `Items` is null or an array of length zero. |
| `Name is not (null or empty)` | `Name` is a non-null string with at least one character. |
| `Name is not null and not empty` | The same test as `Name is not (null or empty)`. |

The `empty` pattern checks the length of a non-null string or array. It never matches null and does not construct a value. Whitespace counts as string content. For arrays, only the length matters. Other types cannot use this pattern, even if they support `empty` as a value. The `""` pattern applies only to strings.

`not` applies to the pattern immediately after it. `Name is not null or empty` means `Name is (not null) or empty`, so it matches every non-null string. To exclude both null and empty strings, use `Name is not (null or empty)`.

Examples:

```
  if MaterialName is null or empty
    CPlugMaterialUserInst MaterialUserInst

  if (Name is not null and not empty) and IsEnabled
    string Description

  if (Items is null) or ForceReload
    int ReloadVersion
```

### Empty Values

As a value, `empty` creates a non-null value of the expected type:

| Expected type | Value |
|---|---|
| String | An empty string, equivalent to `""`. |
| Array | An array with zero elements. |
| Other concrete type with a parameterless constructor | A fresh instance created by that constructor, equivalent to parameterless `new()` in the target language. |

The field declaration, assignment target, or property's return type supplies the expected type. For a nullable type, `empty` creates a non-null value of the underlying type.

`empty` is invalid if the expected type is unknown or has neither a supported empty value nor an accessible parameterless constructor. A fixed count array must still match its declared count, so it can use `empty` only when that count is zero.

Each object created by `empty` is a fresh instance with its constructor defaults. Assigning `empty` replaces the previous value and does not share a mutable default instance.

```
string Name = empty
string? OptionalName = empty // Non-null empty string.
Material[] Materials = empty
CPlugMaterialUserInst MaterialUserInst = empty
```

In a pattern, `Materials is empty` checks the array's length. It does not allocate an array or compare against a new instance.

### Examples

```
Version >= 1
(Flags & (1 << 15)) != 0
MaterialName == null || MaterialName == ""
MaterialName is null or empty
MaterialName is null or ""
(MaterialName is not null) and IsEnabled
IsEnabled or ForceReload
ItemType != EItemType::Ornament
Flags & 0x1FFFF
(1, 1, 1)
```

---

## 19. Full File Example

This `.chunkl` file combines chunks, a constructor, archives, version blocks, conditions, and an enum:

```
CGameCtnBlock 0x03057000 // Block placed on a map.

constructor
  Direction = Direction::North
  DecalIntensity = 1
  DecalVariant = -1

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

This example includes array fields and versioned chunks:

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

This example includes archive inheritance and contextual archives:

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
  if MaterialName is null or empty
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
