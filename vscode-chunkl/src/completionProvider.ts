import * as vscode from "vscode";
import {
  PRIMITIVE_TYPES,
  CONTROL_KEYWORDS,
  ROOT_KEYWORDS,
  ATTRIBUTE_KEYWORDS,
  CLASS_TYPES,
  SNIPPETS,
} from "./completionData";

export class ChunkLCompletionProvider implements vscode.CompletionItemProvider {
  provideCompletionItems(
    document: vscode.TextDocument,
    position: vscode.Position,
    _token: vscode.CancellationToken,
    _context: vscode.CompletionContext
  ): vscode.CompletionItem[] {
    const line = document.lineAt(position).text;
    const textBefore = line.substring(0, position.character);

    // Inside parentheses: offer attribute keywords
    if (this.isInsideParens(textBefore)) {
      return this.getAttributeCompletions();
    }

    // Root level (no indentation)
    if (/^\S/.test(line) || line.length === 0) {
      return this.getRootCompletions();
    }

    // Indented (field level)
    return this.getFieldCompletions();
  }

  private isInsideParens(textBefore: string): boolean {
    let depth = 0;
    for (const ch of textBefore) {
      if (ch === "(") {
        depth++;
      } else if (ch === ")") {
        depth--;
      }
    }
    return depth > 0;
  }

  private getAttributeCompletions(): vscode.CompletionItem[] {
    return ATTRIBUTE_KEYWORDS.map((kw) => {
      const item = new vscode.CompletionItem(
        kw,
        vscode.CompletionItemKind.Property
      );
      if (kw.endsWith(":")) {
        item.insertText = new vscode.SnippetString(`${kw} \${1}`);
      }
      item.detail = "ChunkL attribute";
      return item;
    });
  }

  private getRootCompletions(): vscode.CompletionItem[] {
    const items: vscode.CompletionItem[] = [];

    for (const kw of ROOT_KEYWORDS) {
      const item = new vscode.CompletionItem(
        kw,
        vscode.CompletionItemKind.Keyword
      );
      item.detail = "ChunkL root keyword";
      items.push(item);
    }

    // Root-level snippets: chunk, archive, enum, flags
    for (const snip of SNIPPETS) {
      if (
        ["chunk", "archive", "archive (self)", "enum", "flags"].includes(
          snip.label
        )
      ) {
        const item = new vscode.CompletionItem(
          snip.label,
          vscode.CompletionItemKind.Snippet
        );
        item.insertText = new vscode.SnippetString(snip.insertText);
        item.detail = snip.detail;
        items.push(item);
      }
    }

    return items;
  }

  private getFieldCompletions(): vscode.CompletionItem[] {
    const items: vscode.CompletionItem[] = [];

    // Control keywords
    for (const kw of CONTROL_KEYWORDS) {
      const item = new vscode.CompletionItem(
        kw,
        vscode.CompletionItemKind.Keyword
      );
      item.detail = "ChunkL keyword";
      items.push(item);
    }

    // Primitive types
    for (const t of PRIMITIVE_TYPES) {
      const item = new vscode.CompletionItem(
        t,
        vscode.CompletionItemKind.TypeParameter
      );
      item.detail = "Primitive type";
      items.push(item);
    }

    // Class types
    for (const c of CLASS_TYPES) {
      const item = new vscode.CompletionItem(
        c,
        vscode.CompletionItemKind.Class
      );
      item.detail = "Class type";
      items.push(item);
    }

    // Field-level snippets
    for (const snip of SNIPPETS) {
      if (
        !["chunk", "archive", "archive (self)", "enum", "flags"].includes(
          snip.label
        )
      ) {
        const item = new vscode.CompletionItem(
          snip.label,
          vscode.CompletionItemKind.Snippet
        );
        item.insertText = new vscode.SnippetString(snip.insertText);
        item.detail = snip.detail;
        items.push(item);
      }
    }

    return items;
  }
}
