import * as vscode from "vscode";
import { ChunkLCompletionProvider } from "./completionProvider";
import { getDiagnostics } from "./diagnostics";

export function activate(context: vscode.ExtensionContext): void {
  const provider = vscode.languages.registerCompletionItemProvider(
    { language: "chunkl" },
    new ChunkLCompletionProvider(),
    "(",
    "<",
    "[",
    ":",
    ".",
    " "
  );
  const diagnostics = vscode.languages.createDiagnosticCollection("chunkl");
  const updateDiagnostics = (document: vscode.TextDocument): void => {
    if (document.languageId !== "chunkl") {
      diagnostics.delete(document.uri);
      return;
    }
    diagnostics.set(document.uri, getDiagnostics(document.getText()).map((problem) => {
      const range = new vscode.Range(problem.line, problem.start, problem.line, problem.end);
      const diagnostic = new vscode.Diagnostic(range, problem.message, vscode.DiagnosticSeverity.Error);
      diagnostic.source = "ChunkL";
      return diagnostic;
    }));
  };
  context.subscriptions.push(
    provider,
    diagnostics,
    vscode.workspace.onDidOpenTextDocument(updateDiagnostics),
    vscode.workspace.onDidChangeTextDocument((event) => updateDiagnostics(event.document)),
    vscode.workspace.onDidCloseTextDocument((document) => diagnostics.delete(document.uri))
  );
  for (const document of vscode.workspace.textDocuments) { updateDiagnostics(document); }
}

export function deactivate(): void {
  // no-op
}
