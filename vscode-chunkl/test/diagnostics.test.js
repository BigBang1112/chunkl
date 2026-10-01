const assert = require("node:assert/strict");
const { readFileSync, readdirSync } = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const { getDiagnostics } = require("../out/diagnostics");

const header = "TestClass 0x01000000\n";

test("valid project fixtures have no editor diagnostics", () => {
  const fixtures = path.join(__dirname, "../../dotnet/ChunkL.Tests/Fixtures");
  for (const name of readdirSync(fixtures).filter((entry) => entry.endsWith(".chunkl"))) {
    assert.deepEqual(getDiagnostics(readFileSync(path.join(fixtures, name), "utf8")), [], name);
  }
  const example = path.join(__dirname, "../examples/debug.chunkl");
  assert.deepEqual(getDiagnostics(readFileSync(example, "utf8")), []);
});

test("reports precise diagnostics for old member access, strings, and delimiters", () => {
  const source = header + "0x001\n  int[Header::Count] Data\n  string Name = \"open\n  if (Ready]\n    int Data\n";
  const diagnostics = getDiagnostics(source);
  assert.deepEqual(diagnostics.find((problem) => problem.message.includes("Use '.'") ), {
    line: 2, start: 12, end: 14, message: "Use '.' for member access",
  });
  assert.ok(diagnostics.some((problem) => problem.line === 3 && problem.message === "Unterminated string literal"));
  assert.ok(diagnostics.some((problem) => problem.line === 4 && problem.message === "Unexpected ']'"));
  assert.ok(diagnostics.some((problem) => problem.line === 4 && problem.message === "Unclosed '('"));
  assert.deepEqual(getDiagnostics(header + "0x001\n  string Text = \"A::B [\" // )\n"), []);
});

test("reports malformed headers, declarations, indentation, and missing expressions", () => {
  assert.match(getDiagnostics("")[0].message, /class header/);
  assert.match(getDiagnostics("TestClass 123\n")[0].message, /class header/);
  const diagnostics = getDiagnostics(header + "garbage\n  int Value\nconstructor\n  Value =\nconstructor\nproperty bool Flag\n  get =\n0x001\n  if\n    int Data\n");
  for (const message of [
    "Expected a chunk, archive, enum, flags, property, or constructor declaration",
    "Expected a top-level declaration before this body",
    "Expected 'FieldName = expression'",
    "At most one constructor is allowed",
    "Expected 'get = expression' or 'set'",
    "Expected an expression",
  ]) assert.ok(diagnostics.some((problem) => problem.message === message), message);
  assert.ok(getDiagnostics(header + "0x001\n   int Value\n").some((problem) => /indentation/.test(problem.message)));
});

test("checks enum and flags members without rejecting enum ellipses", () => {
  assert.deepEqual(getDiagnostics(header + "enum Direction\n  North\n  ...\nflags Options\n  Mode[1..3]\n"), []);
  const diagnostics = getDiagnostics(header + "enum Direction\n  7North\nflags Options\n  Mode[3..1]\n  Other[]\n");
  assert.ok(diagnostics.some((problem) => problem.message === "Expected an enum member"));
  assert.ok(diagnostics.some((problem) => problem.message.includes("end bit")));
  assert.ok(diagnostics.some((problem) => problem.message.includes("Expected a flags member")));
});

test("extension refreshes diagnostics on open and edit, then clears them on close", () => {
  const Module = require("node:module");
  const originalLoad = Module._load;
  const handlers = {};
  const stored = new Map();
  let triggers;
  const collection = {
    set(uri, problems) { stored.set(uri, problems); },
    delete(uri) { stored.delete(uri); },
    dispose() {},
  };
  const vscode = {
    languages: {
      registerCompletionItemProvider(_selector, _provider, ...items) {
        triggers = items;
        return { dispose() {} };
      },
      createDiagnosticCollection(name) {
        assert.equal(name, "chunkl");
        return collection;
      },
    },
    workspace: {
      textDocuments: [],
      onDidOpenTextDocument(handler) { handlers.open = handler; return { dispose() {} }; },
      onDidChangeTextDocument(handler) { handlers.change = handler; return { dispose() {} }; },
      onDidCloseTextDocument(handler) { handlers.close = handler; return { dispose() {} }; },
    },
    Range: class { constructor(line, start, _endLine, end) { this.line = line; this.start = start; this.end = end; } },
    Diagnostic: class { constructor(range, message, severity) { this.range = range; this.message = message; this.severity = severity; } },
    DiagnosticSeverity: { Error: 0 },
  };
  Module._load = function (request, parent, isMain) {
    return request === "vscode" ? vscode : originalLoad.call(this, request, parent, isMain);
  };
  let activate;
  try {
    ({ activate } = require("../out/extension"));
  } finally {
    Module._load = originalLoad;
  }
  const document = { languageId: "chunkl", uri: "file:///sample.chunkl", getText: () => header + "0x001\n  if\n" };
  vscode.workspace.textDocuments.push(document);
  const context = { subscriptions: [] };
  activate(context);
  assert.ok(triggers.includes("."));
  assert.ok(stored.get(document.uri).some((problem) => problem.message === "Expected an expression"));
  const opened = { languageId: "chunkl", uri: "file:///opened.chunkl", getText: () => "" };
  handlers.open(opened);
  assert.ok(stored.get(opened.uri).some((problem) => problem.message.includes("class header")));
  handlers.open({ languageId: "plaintext", uri: "file:///notes.txt", getText: () => "" });
  assert.equal(stored.has("file:///notes.txt"), false);
  document.getText = () => header + "0x001\n  if true\n    int Data\n";
  handlers.change({ document });
  assert.deepEqual(stored.get(document.uri), []);
  handlers.close(document);
  assert.equal(stored.has(document.uri), false);
  handlers.close(opened);
  assert.equal(stored.has(opened.uri), false);
  assert.equal(context.subscriptions.length, 5);
});
