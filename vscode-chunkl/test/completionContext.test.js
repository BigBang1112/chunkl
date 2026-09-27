const assert = require("node:assert/strict");
const { readFileSync } = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const { getCompletionScope } = require("../out/completionContext");
const { SNIPPETS } = require("../out/completionData");

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
