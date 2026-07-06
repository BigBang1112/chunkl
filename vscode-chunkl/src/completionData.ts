import * as vscode from "vscode";

export const PRIMITIVE_TYPES: string[] = [
  "int",
  "uint",
  "float",
  "bool",
  "string",
  "byte",
  "short",
  "id",
  "ident",
  "vec2",
  "vec3",
  "vec4",
  "iso4",
  "timeint",
  "timefloat",
  "byte3",
  "int3",
  "transquat",
];

export const CONTROL_KEYWORDS: string[] = [
  "version",
  "versionb",
  "base",
  "return",
  "throw",
  "skip",
  "assert",
  "if",
  "else",
  "else if",
  "loop",
  "switch",
  "case",
  "default",
  "block",
];

export const ROOT_KEYWORDS: string[] = [
  "archive",
  "enum",
  "flags",
];

export const ATTRIBUTE_KEYWORDS: string[] = [
  "skippable",
  "ignore",
  "header",
  "demonstration",
  "struct:",
  "base:",
  "external",
  "contextual",
  "inherits:",
  "deprec",
  "name:",
  "optional",
  "type:",
];

export const CLASS_TYPES: string[] = [
  "CMwNod",
  "CGameCtnBlock",
  "CGameCtnBlockSkin",
  "CGameCtnChallenge",
  "CGameCtnGhost",
  "CGameCtnMediaTrack",
  "CGameCtnMediaClip",
  "CGameItemPlacementParam",
  "CGameWaypointSpecialProperty",
  "CPlugBitmap",
  "CPlugCharPhySpecialProperty",
  "CPlugCrystal",
  "CPlugGameSkin",
  "CPlugMaterialUserInst",
  "CPlugSolid2Model",
  "CPlugSurface",
  "CPlugTreeGenerator",
];

export interface SnippetDef {
  label: string;
  insertText: string;
  detail: string;
}

export const SNIPPETS: SnippetDef[] = [
  {
    label: "chunk",
    insertText: "0x${1:000} (${2:skippable}) [${3:TM2020}]\n  version\n  $0",
    detail: "New chunk declaration",
  },
  {
    label: "archive",
    insertText: "archive ${1:Name}\n  $0",
    detail: "New archive declaration",
  },
  {
    label: "archive (self)",
    insertText: "archive\n  $0",
    detail: "Self archive declaration",
  },
  {
    label: "enum",
    insertText: "enum ${1:Name}\n  ${2:Value1}\n  ${3:Value2}\n  $0",
    detail: "New enum declaration",
  },
  {
    label: "flags",
    insertText: "flags ${1:Name}\n  ${2:Member}[${3:0}]\n  $0",
    detail: "New flags declaration",
  },
  {
    label: "if",
    insertText: "if ${1:condition}\n  $0",
    detail: "If statement",
  },
  {
    label: "if-else",
    insertText: "if ${1:condition}\n  $2\nelse\n  $0",
    detail: "If-else statement",
  },
  {
    label: "loop",
    insertText: "loop ${1:Count}\n  $0",
    detail: "Loop statement",
  },
  {
    label: "switch",
    insertText: "switch ${1:Expression}\n  case ${2:Value}\n    $0",
    detail: "Switch statement",
  },
  {
    label: "version block",
    insertText: "v${1:1}+\n  $0",
    detail: "Version condition block (v1+)",
  },
  {
    label: "else if",
    insertText: "else if ${1:condition}\n  $0",
    detail: "Else-if statement",
  },
  {
    label: "block",
    insertText: "block\n  $0",
    detail: "Block statement",
  },
  {
    label: "skip",
    insertText: "skip ${1:expression}",
    detail: "Skip statement",
  },
  {
    label: "assert",
    insertText: "assert ${1:condition}",
    detail: "Assert statement",
  },
];
