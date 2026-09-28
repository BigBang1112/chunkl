export const ROOT_KEYWORDS: string[] = ["archive", "enum", "flags"];

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
  "while",
  "switch",
  "case",
  "default",
  "block",
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

export interface SnippetDef {
  label: string;
  insertText: string;
  detail: string;
  scope: "root" | "field";
}

export const SNIPPETS: SnippetDef[] = [
  {
    label: "chunk",
    insertText: "0x${1:000} (${2:skippable}) [${3:TM2020}]\n  version\n  $0",
    detail: "New chunk declaration",
    scope: "root",
  },
  {
    label: "archive",
    insertText: "archive ${1:Name}\n  $0",
    detail: "New archive declaration",
    scope: "root",
  },
  {
    label: "archive (self)",
    insertText: "archive\n  $0",
    detail: "Self archive declaration",
    scope: "root",
  },
  {
    label: "enum",
    insertText: "enum ${1:Name}\n  ${2:Value1}\n  ${3:Value2}\n  $0",
    detail: "New enum declaration",
    scope: "root",
  },
  {
    label: "flags",
    insertText: "flags ${1:Name}\n  ${2:Member}[${3:0}]\n  $0",
    detail: "New flags declaration",
    scope: "root",
  },
  {
    label: "if",
    insertText: "if ${1:condition}\n  $0",
    detail: "If statement",
    scope: "field",
  },
  {
    label: "if-else",
    insertText: "if ${1:condition}\n  $2\nelse\n  $0",
    detail: "If-else statement",
    scope: "field",
  },
  {
    label: "loop",
    insertText: "loop ${1:Count}\n  $0",
    detail: "Loop statement",
    scope: "field",
  },
  {
    label: "while",
    insertText: "while ${1:HasNext != 0}\n  $0",
    detail: "While statement",
    scope: "field",
  },
  {
    label: "fixed array",
    insertText: "${1:Type}[${2:Count * 2}] ${3:Values}",
    detail: "Array with an expression count",
    scope: "field",
  },
  {
    label: "switch",
    insertText: "switch ${1:Expression}\n  case ${2:Value}\n    $0",
    detail: "Switch statement",
    scope: "field",
  },
  {
    label: "version block",
    insertText: "v${1:1}+\n  $0",
    detail: "Version condition block (v1+)",
    scope: "field",
  },
  {
    label: "else if",
    insertText: "else if ${1:condition}\n  $0",
    detail: "Else-if statement",
    scope: "field",
  },
  {
    label: "block",
    insertText: "block\n  $0",
    detail: "Block statement",
    scope: "field",
  },
  {
    label: "skip",
    insertText: "skip ${1:expression}",
    detail: "Skip statement",
    scope: "field",
  },
  {
    label: "assert",
    insertText: "assert ${1:condition}",
    detail: "Assert statement",
    scope: "field",
  },
];
