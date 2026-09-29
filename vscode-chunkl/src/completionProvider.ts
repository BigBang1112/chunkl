import * as vscode from "vscode";
import { getCompletionContext } from "./completionContext";
import { collectLocalEnums, collectVisibleFields, getBodyContext } from "./documentSymbols";
import {
  ATTRIBUTE_KEYWORDS,
  CONTROL_KEYWORDS,
  ROOT_KEYWORDS,
  SNIPPETS,
  SnippetDef,
} from "./completionData";

export class ChunkLCompletionProvider implements vscode.CompletionItemProvider {
  provideCompletionItems(
    document: vscode.TextDocument,
    position: vscode.Position
  ): vscode.CompletionItem[] {
    const textBeforeCursor = document.lineAt(position).text.slice(0, position.character);
    const context = getCompletionContext(textBeforeCursor);

    if (context.kind === "none") {
      return [];
    }
    if (context.kind === "attribute") {
      return ATTRIBUTE_KEYWORDS.map(attributeCompletion);
    }
    if (context.kind === "enum-member") {
      const localEnum = collectLocalEnums(document.getText())
        .find((type) => type.name === context.typeName);
      return localEnum?.members.map((member) =>
        completionItem(member, vscode.CompletionItemKind.EnumMember, `${localEnum.name} member`)
      ) ?? [];
    }
    if (context.kind === "cast") {
      return collectLocalEnums(document.getText()).map((type) =>
        completionItem(type.name, vscode.CompletionItemKind.Enum, `Local ${type.kind}`)
      );
    }
    if (context.kind === "expression") {
      const source = document.getText();
      return [
        ...collectVisibleFields(source, position.line).map((field) =>
          completionItem(field.name, field.kind === "property" ? vscode.CompletionItemKind.Property : vscode.CompletionItemKind.Field,
            `${field.type} ${field.kind ?? "field"}`)
        ),
        ...collectLocalEnums(source).flatMap((type) => [
          completionItem(type.name, vscode.CompletionItemKind.Enum, `Local ${type.kind}`),
          ...type.members.map((member) =>
            completionItem(`${type.name}::${member}`, vscode.CompletionItemKind.EnumMember, `${type.kind} member`)
          ),
        ]),
        ...["true", "false", "null", "empty"].map((value) =>
          completionItem(value, vscode.CompletionItemKind.Value, "ChunkL value")
        ),
        ...["is", "and", "or", ...(/\bis\b/.test(textBeforeCursor) ? ["not"] : [])].map((keyword) =>
          keywordCompletion(keyword, "ChunkL expression operator")
        ),
        completionItem("!", vscode.CompletionItemKind.Operator, "Negate a boolean expression"),
      ];
    }

    const body = getBodyContext(document.getText(), position.line);
    if (context.kind === "field" && body.kind === "property" && /^ {2}\S*$|^ {2}$/.test(textBeforeCursor)) {
      return [
        ...["get", "set"].map((keyword) => keywordCompletion(keyword, "Property accessor")),
        ...SNIPPETS.filter((snippet) => snippet.scope === "accessor").map(snippetCompletion),
      ];
    }
    if (context.kind === "field" && (body.kind === "constructor" || (body.kind === "property" && body.inSetter))) {
      const targets = collectVisibleFields(document.getText(), position.line, "write").map((field) =>
        completionItem(field.name, field.kind === "property" ? vscode.CompletionItemKind.Property : vscode.CompletionItemKind.Field,
          `${field.type} assignment target`)
      );
      if (body.kind === "constructor") { return targets; }
      const keywords = ["if", "else", "else if", "switch", "case", "default"];
      return [
        ...targets,
        ...keywords.map((keyword) => keywordCompletion(keyword, "Property setter control flow")),
        ...SNIPPETS.filter((snippet) => snippet.scope === "field" && ["if", "if-else", "else if", "switch"].includes(snippet.label))
          .map(snippetCompletion),
      ];
    }

    const snippets = SNIPPETS.filter((snippet) => snippet.scope === context.kind);
    const items = snippets.map(snippetCompletion);
    if (context.kind === "root") {
      return [
        ...ROOT_KEYWORDS.map((keyword) => keywordCompletion(keyword, "ChunkL declaration")),
        ...items,
      ];
    }

    items.push(
      ...CONTROL_KEYWORDS.map((keyword) =>
        keywordCompletion(keyword, "ChunkL keyword")
      )
    );
    return items;
  }
}

function attributeCompletion(keyword: string): vscode.CompletionItem {
  const item = completionItem(keyword, vscode.CompletionItemKind.Property, "ChunkL attribute");
  if (keyword.endsWith(":")) {
    item.insertText = new vscode.SnippetString(`${keyword} \${1}`);
  }
  return item;
}

function keywordCompletion(keyword: string, detail: string): vscode.CompletionItem {
  return completionItem(keyword, vscode.CompletionItemKind.Keyword, detail);
}

function completionItem(
  label: string,
  kind: vscode.CompletionItemKind,
  detail: string
): vscode.CompletionItem {
  const item = new vscode.CompletionItem(label, kind);
  item.detail = detail;
  return item;
}

function snippetCompletion(snippet: SnippetDef): vscode.CompletionItem {
  const item = new vscode.CompletionItem(`${snippet.label} (snippet)`, vscode.CompletionItemKind.Snippet);
  item.filterText = snippet.label;
  item.insertText = new vscode.SnippetString(snippet.insertText);
  item.detail = snippet.detail;
  return item;
}
