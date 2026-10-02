export interface ChunkLDiagnostic {
  line: number;
  start: number;
  end: number;
  message: string;
}

type Section = "chunk" | "archive" | "enum" | "flags" | "property" | "constructor";

interface LineScan {
  code: string;
  problems: { start: number; end: number; message: string }[];
}

/** Fast syntax checks for incomplete documents. The libraries remain the source of full validation. */
export function getDiagnostics(source: string): ChunkLDiagnostic[] {
  const diagnostics: ChunkLDiagnostic[] = [];
  const lines = source.split(/\r?\n/);
  let hasHeader = false;
  let section: Section | undefined;
  let previousIndent = 0;
  let previousCanNest = false;
  let constructors = 0;
  let declarationsStarted = false;

  for (let line = 0; line < lines.length; line++) {
    const original = lines[line];
    const scan = scanLine(original);
    for (const problem of scan.problems) {
      diagnostics.push({ line, ...problem });
    }
    const code = scan.code.trimEnd();
    const text = code.trim();
    if (!text) { continue; }
    const indent = code.length - code.trimStart().length;
    const report = (message: string, start = indent, end = code.length): void => {
      diagnostics.push({ line, start, end: Math.max(start + 1, end), message });
    };

    if (!hasHeader) {
      hasHeader = true;
      if (!/^[A-Za-z_]\w*\s+0[xX][0-9a-fA-F]{8}$/.test(text) || indent !== 0) {
        report("Expected a class header: ClassName 0x00000000");
      }
      continue;
    }

    if (indent === 0) {
      previousIndent = 0;
      previousCanNest = false;
      if (text.startsWith("- ") && !declarationsStarted) { continue; }
      declarationsStarted = true;
      if (/^0x/i.test(text)) {
        section = "chunk";
        if (!/^0[xX](?:[0-9a-fA-F]{3}|[0-9a-fA-F]{8})(?=\s|$)/.test(text)) {
          report("Expected a 3-digit chunk offset or 8-digit chunk ID");
        }
      } else if (/^archive\b/.test(text)) {
        section = "archive";
        if (!/^archive(?:\s+[A-Za-z_]\w*)?(?:\s*\([^)]*\))?$/.test(text)) {
          report("Expected 'archive', optionally followed by a name and attributes");
        }
      } else if (/^enum\b/.test(text)) {
        section = "enum";
        if (!/^enum\s+[A-Za-z_]\w*$/.test(text)) { report("Expected 'enum Name'"); }
      } else if (/^flags\b/.test(text)) {
        section = "flags";
        if (!/^flags\s+[A-Za-z_]\w*$/.test(text)) { report("Expected 'flags Name'"); }
      } else if (/^property\b/.test(text)) {
        section = "property";
        if (!/^property\s+.+\s+[A-Za-z_]\w*$/.test(text)) {
          report("Expected 'property Type Name'");
        }
      } else if (/^constructor\b/.test(text)) {
        section = "constructor";
        constructors++;
        if (text !== "constructor") { report("The constructor header takes no arguments"); }
        if (constructors > 1) { report("At most one constructor is allowed"); }
      } else {
        section = undefined;
        report("Expected a chunk, archive, enum, flags, property, or constructor declaration");
      }
      previousCanNest = section !== undefined;
      continue;
    }

    if (!section) {
      report("Expected a top-level declaration before this body");
      continue;
    }
    if (/\t/.test(original.slice(0, indent)) || indent % 2 !== 0) {
      report("Use multiples of two spaces for indentation", 0, indent);
    }
    if (indent > previousIndent + 2 || (indent > previousIndent && !previousCanNest)) {
      report(`Unexpected indentation; expected at most ${previousIndent + (previousCanNest ? 2 : 0)} spaces`, 0, indent);
    }
    previousIndent = indent;
    previousCanNest = false;

    if (section === "enum") {
      if (indent !== 2) { report("Enum members use two spaces of indentation", 0, indent); }
      if (text !== "..." && !/^[A-Za-z_]\w*(?:\s*=\s*.+)?$/.test(text)) {
        report("Expected an enum member");
      }
      continue;
    }
    if (section === "flags") {
      if (indent !== 2) { report("Flags members use two spaces of indentation", 0, indent); }
      const match = /^([A-Za-z_]\w*)\[(\d+)(?:\.\.(\d+))?\]$/.exec(text);
      if (!match) {
        report("Expected a flags member such as Name[0] or Name[1..3]");
      } else if (match[3] !== undefined && Number(match[3]) < Number(match[2])) {
        report("The end bit must be greater than or equal to the start bit");
      }
      continue;
    }
    if (section === "constructor") {
      if (indent !== 2) { report("Constructor assignments use two spaces of indentation", 0, indent); }
      if (!/^[A-Za-z_]\w*\s*=\s*.+$/.test(text)) { report("Expected 'FieldName = expression'"); }
      continue;
    }
    if (section === "property" && indent === 2) {
      if (text === "set") {
        previousCanNest = true;
      } else if (!/^get\s*=\s*.+$/.test(text)) {
        report("Expected 'get = expression' or 'set'");
      }
      continue;
    }

    const block = /^(?:if|else(?:\s+if)?|switch|case|default|loop|while|block)\b/.test(text)
      || /^v\d+(?:[+\-=]|\.\.\d+)(?=\s|\(|$)/.test(text);
    previousCanNest = block;
    if (/^(?:if|else\s+if|switch|case|loop|while|skip|assert)\s*$/.test(text)) {
      report("Expected an expression");
    } else if (!/^v\d+=$/.test(text) && /(?:^|[^=<>!])=\s*$/.test(text)) {
      report("Expected an expression after '='");
    }
  }

  if (!hasHeader) {
    diagnostics.push({ line: 0, start: 0, end: 1, message: "Expected a class header: ClassName 0x00000000" });
  }
  return diagnostics;
}

function scanLine(line: string): LineScan {
  const problems: LineScan["problems"] = [];
  const open: { char: string; index: number }[] = [];
  let inString = false;
  let stringStart = 0;
  let index = 0;
  for (; index < line.length; index++) {
    const char = line[index];
    if (inString) {
      if (char === "\\") { index++; }
      else if (char === '"') { inString = false; }
      continue;
    }
    if (char === '"') { inString = true; stringStart = index; continue; }
    if (char === "#" || (char === "/" && line[index + 1] === "/")) { break; }
    if (char === ":" && line[index + 1] === ":") {
      problems.push({ start: index, end: index + 2, message: "Use '.' for member access" });
      index++;
      continue;
    }
    if (char === "(" || char === "[") {
      open.push({ char, index });
    } else if (char === ")" || char === "]") {
      const expected = char === ")" ? "(" : "[";
      if (open[open.length - 1]?.char === expected) { open.pop(); }
      else { problems.push({ start: index, end: index + 1, message: `Unexpected '${char}'` }); }
    }
  }
  if (inString) {
    problems.push({ start: stringStart, end: line.length, message: "Unterminated string literal" });
  }
  for (const bracket of open) {
    problems.push({ start: bracket.index, end: bracket.index + 1, message: `Unclosed '${bracket.char}'` });
  }
  return { code: line.slice(0, index), problems };
}
