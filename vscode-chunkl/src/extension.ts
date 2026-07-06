import * as vscode from "vscode";
import { ChunkLCompletionProvider } from "./completionProvider";

export function activate(context: vscode.ExtensionContext): void {
  const provider = vscode.languages.registerCompletionItemProvider(
    { language: "chunkl", scheme: "file" },
    new ChunkLCompletionProvider(),
    "(",
    "<",
    " "
  );
  context.subscriptions.push(provider);
}

export function deactivate(): void {
  // no-op
}
