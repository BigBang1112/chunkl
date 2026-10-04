const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const path = require("node:path");
const { before, test } = require("node:test");
const { Registry, parseRawGrammar } = require("vscode-textmate");
const { loadWASM, OnigScanner, OnigString } = require("vscode-oniguruma");

let grammar;
before(async () => {
  await loadWASM(readFileSync(require.resolve("vscode-oniguruma/release/onig.wasm")));
  const registry = new Registry({
    onigLib: Promise.resolve({
      createOnigScanner: (patterns) => new OnigScanner(patterns),
      createOnigString: (text) => new OnigString(text),
    }),
    loadGrammar: async () => {
      const file = path.join(__dirname, "../syntaxes/chunkl.tmLanguage.json");
      return parseRawGrammar(readFileSync(file, "utf8"), file);
    },
  });
  grammar = await registry.loadGrammar("source.chunkl");
});

function scopesAt(result, line, text) {
  const index = line.indexOf(text);
  assert.notEqual(index, -1, text);
  return result.tokens.find((token) => token.startIndex <= index && index < token.endIndex).scopes;
}

const attributes = String.raw`(name: "value, with ) and // # and \"quote\"", enabled, raw: plain value)`;
for (const prefix of ["0x001 ", "archive Key ", "  int Value ", "  byte<Options> Value ", "  v1+ ", "  block "]) {
  test(`quoted attributes preserve highlighting after ${prefix.trim()}`, () => {
    const line = prefix + attributes + " // trailing comment";
    const result = grammar.tokenizeLine(line);
    for (const text of ['"value', ", with", ") and", "// #", "quote"]) {
      assert.ok(scopesAt(result, line, text).some((scope) => scope.startsWith("string.quoted.double")), text);
    }
    assert.ok(scopesAt(result, line, String.raw`\"quote`).includes("constant.character.escape.chunkl"));
    assert.ok(scopesAt(result, line, "enabled").includes("entity.other.attribute-name.flag.chunkl"));
    assert.ok(scopesAt(result, line, "raw").includes("entity.other.attribute-name.chunkl"));
    assert.ok(scopesAt(result, line, "plain value").includes("string.unquoted.attribute-value.chunkl"));
    assert.ok(scopesAt(result, line, "// trailing").includes("comment.line.double-slash.chunkl"));
    const nextLine = "  int Next";
    const next = grammar.tokenizeLine(nextLine, result.ruleStack);
    assert.deepEqual(next.tokens, grammar.tokenizeLine(nextLine).tokens);
    assert.ok(!scopesAt(next, nextLine, "Next").some((scope) => scope.startsWith("string.")));
  });
}

test("quoted class attributes protect comment markers and escaped quotes", () => {
  const line = String.raw`- description: "URL // # and \"quote\"" // trailing comment`;
  const result = grammar.tokenizeLine(line);
  assert.ok(scopesAt(result, line, "description").includes("entity.other.attribute-name.chunkl"));
  for (const text of ['"URL', "// #", "quote"]) {
    assert.ok(scopesAt(result, line, text).some((scope) => scope.startsWith("string.quoted.double")), text);
  }
  assert.ok(scopesAt(result, line, String.raw`\"quote`).includes("constant.character.escape.chunkl"));
  assert.ok(scopesAt(result, line, "// trailing").includes("comment.line.double-slash.chunkl"));
});

test("unquoted values and valueless flags retain their scopes", () => {
  for (const line of ["- description: plain value // comment", "- abstract"]) {
    const result = grammar.tokenizeLine(line);
    assert.ok(scopesAt(result, line, line.includes("description") ? "description" : "abstract")
      .includes("entity.other.attribute-name.chunkl"));
    if (line.includes("description")) {
      assert.ok(scopesAt(result, line, "plain value").includes("string.unquoted.attribute-value.chunkl"));
      assert.ok(scopesAt(result, line, "// comment").includes("comment.line.double-slash.chunkl"));
    }
  }
});

test("an unfinished quoted attribute does not color the following line as a string", () => {
  const result = grammar.tokenizeLine('0x001 (name: "unfinished');
  const line = "0x002 (enabled)";
  const next = grammar.tokenizeLine(line, result.ruleStack);
  assert.ok(scopesAt(next, line, "0x002").includes("constant.numeric.hex.chunk-id.chunkl"));
  assert.ok(scopesAt(next, line, "enabled").includes("entity.other.attribute-name.flag.chunkl"));
});

test("nested field types keep their complete path and modifier scopes", () => {
  for (const [type, scope] of [
    ["CGameCtnMacroBlockInfo.BlockSpawn", "support.class.chunkl"],
    ["Outer._Inner.Leaf2", "support.class.chunkl"],
    ["outer.Inner.Leaf", "storage.type.primitive.chunkl"],
  ]) {
    for (const suffix of ["", "?[]", "[Count]"]) {
      const line = "  " + type + suffix + " Spawns (external) // nested";
      const result = grammar.tokenizeLine(line);
      for (const segment of type.split(".")) {
        assert.ok(scopesAt(result, line, segment).includes(scope), segment);
      }
      assert.ok(scopesAt(result, line, "Spawns").includes("variable.other.field.chunkl"));
      assert.ok(scopesAt(result, line, "external").includes("entity.other.attribute-name.flag.chunkl"));
      assert.ok(scopesAt(result, line, "// nested").includes("comment.line.double-slash.chunkl"));
      if (suffix.includes("?")) {
        assert.ok(scopesAt(result, line, "?").includes("storage.modifier.nullable.chunkl"));
      }
      if (suffix.includes("[")) {
        assert.ok(scopesAt(result, line, "[").includes("punctuation.definition.array.begin.chunkl"));
      }
    }
  }
  const line = "  Outer.Inner.Leaf*?[][] NestedItems";
  const result = grammar.tokenizeLine(line);
  assert.ok(scopesAt(result, line, "Leaf").includes("support.class.chunkl"));
  assert.ok(scopesAt(result, line, "*").includes("storage.modifier.chunk-preference.chunkl"));
  assert.ok(scopesAt(result, line, "?").includes("storage.modifier.nullable.chunkl"));
  assert.ok(scopesAt(result, line, "NestedItems").includes("variable.other.field.chunkl"));
  const anonymous = "  CGameCtnMacroBlockInfo.BlockSpawn // anonymous";
  assert.ok(scopesAt(grammar.tokenizeLine(anonymous), anonymous, "BlockSpawn").includes("support.class.chunkl"));
});

test("nested types support casts and property highlighting", () => {
  for (const [type, scope] of [
    ["Outer.Inner.Leaf", "support.class.chunkl"],
    ["outer.Inner.Leaf", "storage.type.primitive.chunkl"],
  ]) {
    const line = "  " + type + "<Other.Inner.Target>?[] Items";
    const result = grammar.tokenizeLine(line);
    assert.ok(scopesAt(result, line, "Leaf").includes(scope));
    assert.ok(scopesAt(result, line, "Target").includes("entity.name.type.cast.chunkl"));
    assert.ok(scopesAt(result, line, "Items").includes("variable.other.field.chunkl"));
  }
  const line = "property CGameCtnMacroBlockInfo.BlockSpawn?[] CurrentSpawns";
  const result = grammar.tokenizeLine(line);
  assert.ok(scopesAt(result, line, "BlockSpawn").includes("storage.type.property.chunkl"));
  assert.ok(scopesAt(result, line, "CurrentSpawns").includes("variable.other.property.chunkl"));
});
