import { CONTROL_KEYWORDS } from "./completionData";

export interface LocalEnum {
  kind: "enum" | "flags";
  name: string;
  members: string[];
}

export interface FieldSymbol {
  name: string;
  type: string;
  kind?: "field" | "property" | "parameter" | "version";
  readable?: boolean;
  writable?: boolean;
}

export interface BodyContext {
  kind: "root" | "chunk" | "archive" | "constructor" | "property" | "enum" | "flags";
  start: number;
  name?: string;
  type?: string;
  inSetter?: boolean;
}

const MEMBER_DECLARATION = /^\s*([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*(?:<[^>]+>)?\*?\??(?:\[[^\]]*\])*)\s+([A-Za-z_]\w*)\b/;

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

export function getBodyContext(source: string, cursorLine: number): BodyContext {
  const lines = source.split(/\r?\n/);
  let context: BodyContext = { kind: "root", start: -1 };
  for (let line = 0; line <= Math.min(cursorLine, lines.length - 1); line++) {
    const text = lines[line];
    if (!text.trim() || /^\s*(?:\/\/|#)/.test(text)) {
      continue;
    }
    if (/^\S/.test(text)) {
      if (/^0[xX][\da-fA-F]+\b/.test(text)) {
        context = { kind: "chunk", start: line };
      } else if (/^archive\b/.test(text)) {
        context = { kind: "archive", start: line, name: /^archive\s+([A-Za-z_]\w*)/.exec(text)?.[1] };
      } else if (/^constructor\b/.test(text)) {
        context = { kind: "constructor", start: line };
      } else if (/^property\b/.test(text)) {
        const property = MEMBER_DECLARATION.exec(text.replace(/^property\s+/, ""));
        context = { kind: "property", start: line, type: property?.[1], name: property?.[2] };
      } else if (/^(enum|flags)\b/.test(text)) {
        context = { kind: text.startsWith("enum") ? "enum" : "flags", start: line };
      } else {
        context = { kind: "root", start: line };
      }
    } else if (context.kind === "property" && /^ {2}(?:get|set)\b/.test(text)) {
      context.inSetter = /^ {2}set\b/.test(text);
    }
  }
  return context;
}

export function collectVisibleFields(source: string, cursorLine: number, access: "read" | "write" = "read"): FieldSymbol[] {
  const lines = source.split(/\r?\n/);
  const context = getBodyContext(source, cursorLine);
  if (!["chunk", "archive", "constructor", "property"].includes(context.kind)) {
    return [];
  }
  const fields = new Map<string, FieldSymbol>();
  const namedArchive = context.kind === "archive" && context.name !== undefined;
  let body: BodyContext = { kind: "root", start: -1 };
  let property: FieldSymbol | undefined;
  for (let index = 0; index < lines.length; index++) {
    const text = lines[index];
    if (/^\s*(?:\/\/|#)/.test(text) || !text.trim()) {
      continue;
    }
    if (/^\S/.test(text)) {
      body = getHeaderContext(text, index);
      property = undefined;
      if (!namedArchive && body.kind === "property" && body.name && body.type) {
        property = { name: body.name, type: body.type, kind: "property", readable: false, writable: false };
        fields.set(property.name, property);
      }
      continue;
    }
    if (property) {
      if (/^ {2}get\b/.test(text)) { property.readable = true; }
      if (/^ {2}set\b/.test(text)) { property.writable = true; }
      continue;
    }
    const eligible = namedArchive ? body.start === context.start
      : body.kind === "chunk" || (body.kind === "archive" && body.name === undefined);
    if (!eligible) { continue; }
    const declaration = MEMBER_DECLARATION.exec(text);
    if (declaration && !BODY_KEYWORDS.has(declaration[1])) {
      const field: FieldSymbol = { name: declaration[2], type: declaration[1], kind: "field", readable: true, writable: true };
      const previous = fields.get(field.name);
      if (previous) {
        previous.type = commonIntegerType(previous.type, field.type) ?? previous.type;
      } else {
        fields.set(field.name, field);
      }
    }
  }
  if (context.kind === "archive" && access === "read") {
    fields.set("v", { name: "v", type: "version", kind: "version" });
  } else if (context.kind === "chunk" && access === "read" && lines.slice(context.start + 1, cursorLine)
    .some((text) => /^\s+versionb?\b/.test(text))) {
    fields.set("Version", { name: "Version", type: "version", kind: "version" });
  }
  if (context.kind === "property" && context.inSetter && access === "read") {
    fields.set("value", { name: "value", type: context.type ?? "", kind: "parameter", readable: true });
  }
  return [...fields.values()].filter((field) => {
    if (context.kind === "property" && field.kind === "property" && field.name === context.name
      && (access === "write" || !context.inSetter)) { return false; }
    return access === "write" ? field.writable : field.readable !== false;
  });
}

function getHeaderContext(text: string, start: number): BodyContext {
  return { ...getBodyContext(text, 0), start };
}

function commonIntegerType(a: string, b: string): string | undefined {
  const integerTypes: Record<string, [number, boolean]> = {
    sbyte: [8, true], byte: [8, false], short: [16, true], ushort: [16, false],
    int: [32, true], uint: [32, false], long: [64, true], ulong: [64, false],
  };
  if (!integerTypes[a] || !integerTypes[b]) { return undefined; }
  const contains = (candidate: string, source: string): boolean => {
    const [width, signed] = integerTypes[candidate];
    const [sourceWidth, sourceSigned] = integerTypes[source];
    return signed === sourceSigned ? width >= sourceWidth : signed && width > sourceWidth;
  };
  return [a, b].find((candidate) => contains(candidate, a) && contains(candidate, b));
}
