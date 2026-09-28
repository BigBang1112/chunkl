export type CompletionScope = "root" | "field" | "attribute";

export type CompletionContext =
  | { kind: "none" | "root" | "field" | "attribute" | "expression" | "cast" }
  | { kind: "enum-member"; typeName: string };

export function getCompletionContext(textBeforeCursor: string): CompletionContext {
  if (isInsideCommentOrString(textBeforeCursor)) {
    return { kind: "none" };
  }

  const scope = getCompletionScope(textBeforeCursor);
  if (scope !== "field") {
    return { kind: scope };
  }

  const member = /\b([A-Za-z_]\w*)::(?:[A-Za-z_]\w*)?$/.exec(textBeforeCursor);
  if (member) {
    return { kind: "enum-member", typeName: member[1] };
  }
  if (/^\s+[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*<[\w.]*$/.test(textBeforeCursor)) {
    return { kind: "cast" };
  }
  if (isInsideArrayCount(textBeforeCursor)
    || /^\s*(?:if|else\s+if|while|loop|switch|case|skip|assert)\s+/.test(textBeforeCursor)
    || /^\s+.*(?<![=!<>])=(?!=)/.test(textBeforeCursor)) {
    return { kind: "expression" };
  }
  return { kind: "field" };
}

export function getCompletionScope(textBeforeCursor: string): CompletionScope {
  if (isInsideAttributeList(textBeforeCursor)) {
    return "attribute";
  }
  return /^\S|^$/.test(textBeforeCursor) ? "root" : "field";
}

function isInsideAttributeList(text: string): boolean {
  const openParens: number[] = [];
  let inString = false;

  for (let index = 0; index < text.length; index++) {
    const char = text[index];
    if (char === '"' && text[index - 1] !== "\\") {
      inString = !inString;
    } else if (!inString && char === "(") {
      openParens.push(index);
    } else if (!inString && char === ")") {
      openParens.pop();
    }
  }

  const open = openParens[openParens.length - 1];
  if (open === undefined) {
    return false;
  }

  const prefix = text.slice(0, open).trim();
  if (/^(?:0x[\da-fA-F]+|archive(?:\s+\w+)?|v\d+(?:[+\-=]|\.\.\d+)|block|return|throw)$/.test(prefix)) {
    return true;
  }
  if (/^(?:if|else|switch|case|while|loop)\b/.test(prefix)) {
    return false;
  }
  if (prefix.includes("=")) {
    return /^\w[\w.<>*?\[\]()\s+\-\/*]*\s+\w+\s*=\s*.+$/.test(prefix);
  }
  return /^(?:skip|assert)\s+.+$/.test(prefix)
    || /^\w[\w.<>*?\[\]()\s+\-\/*]*\s+\w+$/.test(prefix);
}

function isInsideArrayCount(text: string): boolean {
  const open = text.lastIndexOf("[");
  return open > text.lastIndexOf("]")
    && /^\s+[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*(?:<[^>]+>)?[*?]?(?:\[[^\]]*\])*\[$/.test(text.slice(0, open + 1));
}

function isInsideCommentOrString(text: string): boolean {
  let inString = false;
  for (let index = 0; index < text.length; index++) {
    const char = text[index];
    if (char === '"' && text[index - 1] !== "\\") {
      inString = !inString;
    } else if (!inString && (char === "#" || (char === "/" && text[index + 1] === "/"))) {
      return true;
    }
  }
  return inString;
}
