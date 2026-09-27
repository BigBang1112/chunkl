import * as vscode from "vscode";
import { getCompletionScope } from "./completionContext";
import {
  ATTRIBUTE_KEYWORDS,
  CLASS_TYPES,
  CONTROL_KEYWORDS,
  PRIMITIVE_TYPES,
  SNIPPETS,
  SnippetDef,
} from "./completionData";

export class ChunkLCompletionProvider implements vscode.CompletionItemProvider {
  provideCompletionItems(
    document: vscode.TextDocument,
    position: vscode.Position
  ): vscode.CompletionItem[] {
    const textBeforeCursor = document.lineAt(position).text.slice(0, position.character);
    const scope = getCompletionScope(textBeforeCursor);

    if (scope === "attribute") {
      return ATTRIBUTE_KEYWORDS.map(attributeCompletion);
    }

    const snippets = SNIPPETS.filter((snippet) => snippet.scope === scope);
    const items = snippets.map(snippetCompletion);
    if (scope === "root") {
      return items;
    }

    const snippetLabels = new Set(snippets.map((snippet) => snippet.label));
    items.push(
      ...CONTROL_KEYWORDS.filter((keyword) => !snippetLabels.has(keyword)).map((keyword) =>
        keywordCompletion(keyword, "ChunkL keyword")
      ),
      ...PRIMITIVE_TYPES.map((type) => typeCompletion(type, vscode.CompletionItemKind.TypeParameter, "Primitive type")),
      ...CLASS_TYPES.map((type) => typeCompletion(type, vscode.CompletionItemKind.Class, "Class type"))
    );
    return items;
  }
}

function attributeCompletion(keyword: string): vscode.CompletionItem {
  const item = typeCompletion(keyword, vscode.CompletionItemKind.Property, "ChunkL attribute");
  if (keyword.endsWith(":")) {
    item.insertText = new vscode.SnippetString(`${keyword} \${1}`);
  }
  return item;
}

function keywordCompletion(keyword: string, detail: string): vscode.CompletionItem {
  return typeCompletion(keyword, vscode.CompletionItemKind.Keyword, detail);
}

function typeCompletion(
  label: string,
  kind: vscode.CompletionItemKind,
  detail: string
): vscode.CompletionItem {
  const item = new vscode.CompletionItem(label, kind);
  item.detail = detail;
  return item;
}

function snippetCompletion(snippet: SnippetDef): vscode.CompletionItem {
  const item = new vscode.CompletionItem(snippet.label, vscode.CompletionItemKind.Snippet);
  item.insertText = new vscode.SnippetString(snippet.insertText);
  item.detail = snippet.detail;
  return item;
}
