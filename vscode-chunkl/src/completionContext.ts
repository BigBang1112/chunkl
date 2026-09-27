export type CompletionScope = "root" | "field" | "attribute";

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
