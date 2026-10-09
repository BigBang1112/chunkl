export interface GameDefaultList {
  start: number;
  end?: number;
  entries: string[];
}

/** Locate a field's trailing list without confusing type arrays, tuples, or strings. */
export function findGameDefaults(text: string): GameDefaultList | undefined {
  let inString = false;
  let depth = 0;
  let start: number | undefined;
  let entryStart = 0;
  const entries: string[] = [];
  for (let index = 0; index < text.length; index++) {
    const char = text[index];
    if (inString) {
      if (char === "\\") { index++; }
      else if (char === '"') { inString = false; }
      continue;
    }
    if (char === '"') { inString = true; continue; }
    if (char === "#" || (char === "/" && text[index + 1] === "/")) { break; }
    if (start === undefined && depth === 0 && char === "[") {
      const prefix = text.slice(0, index).trim();
      const field = /^[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*(?:<[^>]+>)?\*?\??(?:\[[^\]]*\])*(?:\s+([A-Za-z_]\w*))?(?:\s*(=).*)?(?:\s*\([^)]*\))?$/.exec(prefix);
      const hasNameOrDefault = Boolean(field?.[1] || field?.[2]);
      const hasGameEntry = /^\s*[A-Za-z0-9_]+\s*=(?!=)/.test(text.slice(index + 1));
      if (field && (hasNameOrDefault || hasGameEntry || /^(version|versionb)$/.test(prefix))) {
        start = index;
        entryStart = index + 1;
        continue;
      }
    }
    if (start !== undefined && depth === 0 && (char === "," || char === "]")) {
      entries.push(text.slice(entryStart, index));
      if (char === "]") { return { start, end: index + 1, entries }; }
      entryStart = index + 1;
    } else if (char === "(" || char === "[") { depth++; }
    else if (char === ")" || char === "]") { depth--; }
  }
  if (start !== undefined) {
    entries.push(text.slice(entryStart));
    return { start, entries };
  }
  return undefined;
}

export function collectGameLabels(source: string): string[] {
  const labels = new Set<string>();
  for (const line of source.split(/\r?\n/)) {
    if (/^0x/i.test(line)) {
      const list = /\[([^\]]*)\]/.exec(line)?.[1];
      for (const entry of list?.split(",") ?? []) {
        const label = /^\s*([A-Za-z0-9_]+)(?:\.v\d+)?\s*$/.exec(entry)?.[1];
        if (label) { labels.add(label); }
      }
    } else if (/^\s+/.test(line)) {
      for (const entry of findGameDefaults(line)?.entries ?? []) {
        const label = /^\s*([A-Za-z0-9_]+)\s*=/.exec(entry)?.[1];
        if (label) { labels.add(label); }
      }
    }
  }
  return [...labels];
}
