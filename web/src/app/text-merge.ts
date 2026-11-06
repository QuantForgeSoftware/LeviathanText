/** Conservative scalar merge matching Core/Text/ThreeWayMerge.cs.
 * Non-overlapping or identical edits merge; another overlap keeps the base text
 * and reports failure. Combining marks remain separate scalars, with no normalization.
 */
export function mergeText(baseText: string, localText: string, remoteText: string): { success: boolean; text: string } {
  if (localText === remoteText) return { success: true, text: localText };
  if (localText === baseText) return { success: true, text: remoteText };
  if (remoteText === baseText) return { success: true, text: localText };

  const base = scalars(baseText);
  const candidates = [...diff(base, scalars(localText)), ...diff(base, scalars(remoteText))]
    .sort((a, b) => a.start - b.start || a.end - b.end);
  const accepted: Hunk[] = [];
  for (const candidate of candidates) {
    const clash = accepted.find(existing => overlaps(existing, candidate));
    if (!clash) accepted.push(candidate);
    else if (clash.start !== candidate.start || clash.end !== candidate.end || clash.replacement !== candidate.replacement)
      return { success: false, text: baseText };
  }
  const result: string[] = [];
  let cursor = 0;
  for (const hunk of accepted) {
    if (hunk.start > cursor) result.push(base.slice(cursor, hunk.start).join(''));
    result.push(hunk.replacement);
    cursor = hunk.end;
  }
  result.push(base.slice(cursor).join(''));
  return { success: true, text: result.join('') };
}

interface Hunk { start: number; end: number; replacement: string }
const maxExactDiffCells = 4_000_000;

function scalars(text: string): string[] {
  // .NET EnumerateRunes replaces unpaired UTF-16 surrogates with U+FFFD.
  return Array.from(text, value => {
    const codePoint = value.codePointAt(0)!;
    return codePoint >= 0xd800 && codePoint <= 0xdfff ? '\ufffd' : value;
  });
}

function overlaps(a: Hunk, b: Hunk): boolean {
  if (a.start === a.end && b.start === b.end) return a.start === b.start;
  if (a.start === a.end) return b.start < a.start && a.start < b.end;
  if (b.start === b.end) return a.start < b.start && b.start < a.end;
  return a.start < b.end && b.start < a.end;
}

function diff(base: string[], variant: string[]): Hunk[] {
  const n = base.length, m = variant.length;
  const cells = (n + 1) * (m + 1);
  if (cells > maxExactDiffCells) {
    let prefix = 0, suffix = 0;
    while (prefix < n && prefix < m && base[prefix] === variant[prefix]) prefix++;
    while (suffix < n - prefix && suffix < m - prefix && base[n - 1 - suffix] === variant[m - 1 - suffix]) suffix++;
    return [{ start: prefix, end: n - suffix, replacement: variant.slice(prefix, m - suffix).join('') }];
  }

  const width = m + 1;
  const table = new Int32Array(cells);
  for (let i = n - 1; i >= 0; i--)
    for (let j = m - 1; j >= 0; j--)
      table[i * width + j] = base[i] === variant[j]
        ? table[(i + 1) * width + j + 1] + 1
        : Math.max(table[(i + 1) * width + j], table[i * width + j + 1]);

  const hunks: Hunk[] = [];
  let x = 0, y = 0;
  while (x < n || y < m) {
    if (x < n && y < m && base[x] === variant[y]) { x++; y++; continue; }
    const start = x;
    const replacement: string[] = [];
    while (x < n || y < m) {
      if (x < n && y < m && base[x] === variant[y]) break;
      // Match the C# deletion-first tie break for repeated/ambiguous text.
      if (x < n && (y >= m || table[(x + 1) * width + y] >= table[x * width + y + 1])) x++;
      else replacement.push(variant[y++]);
    }
    hunks.push({ start, end: x, replacement: replacement.join('') });
  }
  return hunks;
}
