const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const { getCompletionContext, getCompletionScope } = require("../out/completionContext");
const { SNIPPETS } = require("../out/completionData");
const Module = require("node:module");

const originalLoad = Module._load;
Module._load = function (request, parent, isMain) {
  if (request === "vscode") {
    return {
      CompletionItem: class {
        constructor(label, kind) {
          this.label = label;
          this.kind = kind;
        }
      },
      SnippetString: class {
        constructor(value) {
          this.value = value;
        }
      },
      CompletionItemKind: {
        Property: 1, Keyword: 2, TypeParameter: 3, Class: 4,
        Snippet: 5, Field: 6, Enum: 7, EnumMember: 8, Value: 9, Operator: 10,
      },
    };
  }
  return originalLoad.call(this, request, parent, isMain);
};
const { ChunkLCompletionProvider } = require("../out/completionProvider");
Module._load = originalLoad;

function completionItemsAt(source, lineText) {
  const lines = source.split(/\r?\n/);
  const line = lines.indexOf(lineText);
  assert.notEqual(line, -1, `Line not found: ${lineText}`);
  const document = { getText: () => source, lineAt: (position) => ({ text: lines[position.line] }) };
  return new ChunkLCompletionProvider()
    .provideCompletionItems(document, { line, character: lineText.length });
}

function completionsAt(source, lineText) {
  return completionItemsAt(source, lineText).map((item) => item.label);
}

test("offers plain declaration and control keywords alongside separate snippets", () => {
  const root = completionItemsAt("en", "en");
  for (const label of ["archive", "enum", "flags", "constructor", "property"]) {
    const item = root.find((item) => item.label === label);
    assert.equal(item.kind, 2); // Keyword
    assert.equal(item.insertText, undefined); // Insert the keyword itself.
  }
  assert.ok(!root.some((item) => item.label === "if"));

  const body = completionItemsAt("Test 0x01000000\n0x001\n  wh", "  wh");
  for (const label of ["version", "versionb", "base", "return", "throw", "if", "else",
    "else if", "loop", "while", "switch", "case", "default", "block", "skip", "assert"]) {
    const item = body.find((item) => item.label === label);
    assert.equal(item.kind, 2);
    assert.equal(item.insertText, undefined);
  }
  const snippet = body.find((item) => item.label === "while (snippet)");
  assert.equal(snippet.filterText, "while");
  assert.match(snippet.insertText.value, /^while .*\n/);
  assert.deepEqual(completionsAt("  // wh", "  // wh"), []);
  assert.deepEqual(completionsAt('  string Name = "wh', '  string Name = "wh'), []);
});

test("suggests attributes only inside attribute lists", () => {
  assert.equal(getCompletionScope("0x001 (skippable, "), "attribute");
  assert.equal(getCompletionScope("  v1= (new_in: "), "attribute");
  assert.equal(getCompletionScope("  int[Count * 2] Values ("), "attribute");
  assert.equal(getCompletionScope("  short[(Count + 1) * 2] Samples ("), "attribute");
  assert.equal(getCompletionScope("  int Flags = 0 ("), "attribute");
  assert.equal(getCompletionScope("  int Flags = ("), "field");
  assert.equal(getCompletionScope("  if (Flags & 1) != 0"), "field");
  assert.equal(getCompletionScope("  while (HasNext != 0)"), "field");
  assert.equal(getCompletionScope("  int[(Count + 1) * 2] Values"), "field");
});

test("distinguishes root and indented lines", () => {
  assert.equal(getCompletionScope(""), "root");
  assert.equal(getCompletionScope("archive"), "root");
  assert.equal(getCompletionScope("  "), "field");
  assert.equal(getCompletionScope("  while "), "field");
});

test("recognizes expression and type completion positions", () => {
  assert.deepEqual(getCompletionContext("  int[Width * "), { kind: "expression" });
  assert.deepEqual(getCompletionContext("  while HasNext != "), { kind: "expression" });
  assert.deepEqual(getCompletionContext("  byte<Dire"), { kind: "cast" });
  assert.deepEqual(getCompletionContext("  if Direction::N"), { kind: "enum-member", typeName: "Direction" });
  assert.deepEqual(getCompletionContext("  if Name == \"North"), { kind: "none" });
  assert.deepEqual(getCompletionContext("  int Value // ("), { kind: "none" });
});

test("provider suggests local enums and fields without class or other type names", () => {
  const source = [
    "Test 0x01000000", "0x001", "  int OldField", "0x002", "  int Width",
    "  CPlugMaterial Material", "  byte<", "  if Direction::", "  if Options::", "  while ",
    "  int[Width * ", "enum Direction", "  North", "  South",
    "flags Options", "  Enabled[0]", "",
  ].join("\n");
  assert.deepEqual(completionsAt(source, "  byte<"), ["Direction", "Options"]);
  assert.deepEqual(completionsAt(source, "  if Direction::"), ["North", "South"]);
  assert.deepEqual(completionsAt(source, "  if Options::"), ["Enabled"]);
  for (const line of ["  while ", "  int[Width * "]) {
    const labels = completionsAt(source, line);
    for (const name of ["Width", "OldField", "Material", "Direction", "Options", "Direction::North", "Options::Enabled", "true", "false", "null", "empty"]) {
      assert.ok(labels.includes(name), `${name} missing at ${line}`);
    }
    for (const name of ["int", "vec3", "CMwNod", "CPlugMaterial"]) {
      assert.ok(!labels.includes(name), `${name} should not be suggested at ${line}`);
    }
  }
  assert.ok(completionsAt(source, "  int Width").includes("while"));
  const body = completionItemsAt(source, "  int Width");
  assert.ok(body.every((item) => [1, 2, 5].includes(item.kind))); // Attributes, keywords, snippets only.
  assert.ok(!body.some((item) => ["int", "vec3", "CMwNod", "CGameCtnBlock", "Direction", "Options", "Width"]
    .includes(item.label)));
});

test("class fields include forward declarations while named archives keep their own scope", () => {
  const source = [
    "Test 0x01000000", "archive", "  version = 1", "  int Count",
    "  if Count > 0", "    int Count", "  if ", "  int Later",
    "archive Other", "  if true", "enum Direction", "  North = 1 // explicit value",
    "  South # comment", "# comment between members", "  East", "  North", "flags Options",
    "  Enabled[0]", "  Mode[1..3]", "  Mode[1..3]", "",
  ].join("\n");
  const expression = completionsAt(source, "  if ");
  assert.equal(expression.filter((name) => name === "Count").length, 1);
  assert.ok(expression.includes("v"));
  assert.ok(!expression.includes("Version"));
  assert.ok(expression.includes("Later"));
  assert.ok(expression.includes("Direction::East"));
  assert.ok(expression.includes("Options::Mode"));
  assert.ok(!completionsAt(source, "  if true").includes("Count"));
  assert.ok(!completionsAt(source, "  if true").includes("Version"));
});

test("constructor and accessor completions follow the new declaration rules", () => {
  const source = [
    "Test 0x01000000", "constructor", "  F", "  Flags = ",
    "property bool IsGhost", "  get = Flags != 0", "  set", "    F", "    if ",
    "property bool HasSkin", "  get = ", "property int WriteOnly", "  set", "    Flags = value",
    "0x001", "  short Flags", "archive", "  int Flags", "archive Other", "  string Hidden",
  ].join("\n");
  const constructorTargets = completionsAt(source, "  F");
  assert.ok(constructorTargets.includes("Flags"));
  assert.ok(constructorTargets.includes("IsGhost"));
  assert.ok(constructorTargets.includes("WriteOnly"));
  for (const invalid of ["HasSkin", "Hidden", "version", "if", "value"]) {
    assert.ok(!constructorTargets.includes(invalid), invalid);
  }
  const setterTargets = completionsAt(source, "    F");
  assert.ok(setterTargets.includes("Flags"));
  assert.ok(setterTargets.includes("if"));
  for (const invalid of ["IsGhost", "HasSkin", "version", "loop", "while", "return", "value"]) {
    assert.ok(!setterTargets.includes(invalid), invalid);
  }
  const setterExpression = completionItemsAt(source, "    if ");
  assert.equal(setterExpression.find((item) => item.label === "value").detail, "bool parameter");
  assert.equal(setterExpression.find((item) => item.label === "Flags").detail, "int field");
  assert.equal(setterExpression.filter((item) => item.label === "Flags").length, 1);
  assert.ok(setterExpression.some((item) => item.label === "HasSkin"));
  assert.ok(!setterExpression.some((item) => item.label === "WriteOnly"));
  const getterExpression = completionsAt(source, "  get = ");
  for (const invalid of ["value", "HasSkin", "WriteOnly", "Hidden"]) {
    assert.ok(!getterExpression.includes(invalid), invalid);
  }
  assert.ok(getterExpression.includes("Flags"));
  const accessors = completionsAt("Test 0x01000000\nproperty bool Flag\n  ", "  ");
  assert.deepEqual(accessors, ["get", "set", "get (snippet)", "set (snippet)"]);
});

test("archive and chunk version suggestions use their own context", () => {
  const source = ["Test 0x01000000", "0x001", "  version", "  if ", "archive Named", "  if v "].join("\n");
  const chunk = completionsAt(source, "  if ");
  assert.ok(chunk.includes("Version"));
  assert.ok(!chunk.includes("v"));
  const archive = completionsAt(source, "  if v ");
  assert.ok(archive.includes("v"));
  assert.ok(!archive.includes("Version"));
});

test("properties offer casts and pattern operators in expressions", () => {
  assert.deepEqual(getCompletionContext("property byte<Dire"), { kind: "cast" });
  const source = ["Test 0x01000000", "property byte<", "enum Direction", "  North"].join("\n");
  assert.deepEqual(completionsAt(source, "property byte<"), ["Direction"]);
  const expression = "Test 0x01000000\n0x001\n  string Name\n  if Name is ";
  for (const keyword of ["is", "not", "and", "or", "null", "empty"]) {
    assert.ok(completionsAt(expression, "  if Name is ").includes(keyword));
  }
  assert.ok(!completionsAt("Test 0x01000000\n0x001\n  if ", "  if ").includes("not"));
  const negate = completionItemsAt("Test 0x01000000\n0x001\n  bool IsEnabled\n  if ", "  if ");
  assert.equal(negate.find((item) => item.label === "!").kind, 10);
  assert.ok(completionsAt("Test 0x01000000\n0x001\n  bool IsEnabled\n  if !", "  if !").includes("IsEnabled"));
  assert.ok(SNIPPETS.some((snippet) => snippet.label === "constructor" && snippet.scope === "root"));
  assert.ok(SNIPPETS.some((snippet) => snippet.label === "property" && snippet.scope === "root"));
});

test("grammar highlights properties, accessors, constructors, and pattern operators", () => {
  const grammar = JSON.parse(readFileSync(path.join(__dirname, "../syntaxes/chunkl.tmLanguage.json"), "utf8"));
  assert.match("constructor // defaults", new RegExp(grammar.repository["constructor-declaration"].match));
  assert.match("property int[Count * 2] Values", new RegExp(grammar.repository["property-declaration"].begin));
  assert.match("property CGameCtnBlock*? Block", new RegExp(grammar.repository["property-declaration"].begin));
  const accessors = grammar.repository["property-accessor"].patterns;
  assert.match("  get = Name is not (null or empty)", new RegExp(accessors[0].begin));
  assert.match("  set // incoming value", new RegExp(accessors[1].begin));
  const words = grammar.repository.expression.patterns.find((pattern) => pattern.name === "keyword.operator.word.chunkl");
  for (const word of ["is", "not", "and", "or"]) {
    assert.match(word, new RegExp(words.match));
  }
  assert.doesNotMatch("ordinary", new RegExp(words.match));
  const operators = grammar.repository.expression.patterns.find((pattern) => pattern.name === "keyword.operator.chunkl");
  assert.match("!IsEnabled", new RegExp(operators.match));
  assert.equal(grammar.injections["L:meta.setter.chunkl - comment - string"].patterns[0].name, "variable.parameter.setter.chunkl");
});

test("new syntax has field snippets", () => {
  const whileSnippet = SNIPPETS.find((snippet) => snippet.label === "while");
  const arraySnippet = SNIPPETS.find((snippet) => snippet.label === "fixed array");
  assert.equal(whileSnippet.scope, "field");
  assert.match(whileSnippet.insertText, /^while /);
  assert.equal(arraySnippet.scope, "field");
  assert.match(arraySnippet.insertText, /\[\$\{2:Count \* 2\}\]/);
});

test("grammar recognizes arithmetic array counts and while expressions", () => {
  const grammar = JSON.parse(readFileSync(path.join(__dirname, "../syntaxes/chunkl.tmLanguage.json"), "utf8"));
  const primitive = new RegExp(grammar.repository["field-primitive"].match);
  const classField = new RegExp(grammar.repository["field-class"].match);
  const castArray = new RegExp(grammar.repository["field-primitive-with-cast"].patterns[0].match);
  const repeat = grammar.repository["control-flow"].patterns.find((pattern) => pattern.match?.includes("loop|while"));

  assert.match("  int[Width * Height] Pixels", primitive);
  assert.match("  short[(Count + 1) * 2] Samples", primitive);
  assert.match("  CPlugMaterial[Count - 1] Materials", classField);
  assert.match("Direction>[Count * 2]", castArray);
  assert.match("  while HasNext != 0 // sentinel", new RegExp(repeat.match));
  assert.match("  loop Width * Height", new RegExp(repeat.match));
});
