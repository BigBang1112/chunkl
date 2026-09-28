import { CONTROL_KEYWORDS } from "./completionData";

export interface LocalEnum {
  kind: "enum" | "flags";
  name: string;
  members: string[];
}

export interface FieldSymbol {
  name: string;
  type: string;
}

const BODY_KEYWORDS = new Set(CONTROL_KEYWORDS);

export function collectLocalEnums(source: string): LocalEnum[] {
  const enums: LocalEnum[] = [];
  let current: LocalEnum | undefined;
  for (const line of source.split(/\r?\n/)) {
    if (/^\S/.test(line) && !/^(?:\/\/|#)/.test(line)) {
      const declaration = /^(enum|flags)\s+([A-Za-z_]\w*)\b/.exec(line);
      current = declaration
        ? { kind: declaration[1] as LocalEnum["kind"], name: declaration[2], members: [] }
        : undefined;
      if (current) {
        enums.push(current);
      }
      continue;
    }
    if (!current) {
      continue;
    }
    const member = current.kind === "enum"
      ? /^\s+([A-Za-z_]\w*)(?=\s*(?:=|\/\/|#|$))/.exec(line)
      : /^\s+([A-Za-z_]\w*)(?=\s*\[)/.exec(line);
    if (member && !current.members.includes(member[1])) {
      current.members.push(member[1]);
    }
  }
  return enums;
}

export function collectVisibleFields(source: string, cursorLine: number): FieldSymbol[] {
  const lines = source.split(/\r?\n/);
  let bodyStart = -1;
  for (let line = 0; line < Math.min(cursorLine, lines.length); line++) {
    if (/^\S/.test(lines[line]) && !/^(?:\/\/|#)/.test(lines[line])) {
      bodyStart = /^(?:0x[\da-fA-F]+|archive\b)/.test(lines[line]) ? line : -1;
    }
  }
  if (bodyStart < 0) {
    return [];
  }
  const fields: FieldSymbol[] = [];
  const seen = new Set<string>();
  for (let line = Math.min(cursorLine, lines.length) - 1; line > bodyStart; line--) {
    const text = lines[line];
    if (/^\s+versionb?(?:\s*=\s*\S+)?\s*(?:(?:\/\/|#).*)?$/.test(text) && !seen.has("Version")) {
      fields.push({ name: "Version", type: "version" });
      seen.add("Version");
      continue;
    }
    const declaration = /^\s+([A-Za-z_][\w.]*(?:<[^>]+>)?[*?]?(?:\[[^\]]*\])*)\s+([A-Za-z_]\w*)\b/.exec(text);
    if (declaration && !BODY_KEYWORDS.has(declaration[1]) && !seen.has(declaration[2])) {
      fields.push({ name: declaration[2], type: declaration[1] });
      seen.add(declaration[2]);
    }
  }
  return fields;
}
