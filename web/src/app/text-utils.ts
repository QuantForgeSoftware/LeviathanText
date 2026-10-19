/**
 * Scalar-offset helpers (greek-transcription.md §4): the wire contract stores Unicode
 * scalar offsets; the browser works in UTF-16 code units. Conversions are explicit and
 * tested, never implicit.
 */

/** Unicode scalars (code points) in a string. */
export function scalarCount(text: string): number {
  let count = 0;
  for (const _ of text) count++;
  return count;
}

/** Scalar offset of a UTF-16 index (clamped; an index inside a surrogate pair rounds down). */
export function scalarOffsetFromUtf16(text: string, utf16Index: number): number {
  let count = 0;
  let i = 0;
  while (i < utf16Index && i < text.length) {
    const code = text.codePointAt(i)!;
    i += code > 0xffff ? 2 : 1;
    count++;
  }
  return count;
}

/** UTF-16 index of a scalar offset (clamped to the string). */
export function utf16OffsetFromScalar(text: string, scalarOffset: number): number {
  let count = 0;
  let i = 0;
  while (i < text.length && count < scalarOffset) {
    const code = text.codePointAt(i)!;
    i += code > 0xffff ? 2 : 1;
    count++;
  }
  return i;
}

/**
 * Replace a scalar range with new text. Used when an editor accepts a source-linked
 * alternative reading: the result is an L2 editorial composition, never L1 evidence.
 */
export function applyScalarEdit(text: string, start: number, end: number, replacement: string): string {
  const scalars = Array.from(text);
  const from = Math.max(0, Math.min(start, scalars.length));
  const to = Math.max(from, Math.min(end, scalars.length));
  return scalars.slice(0, from).join('') + replacement + scalars.slice(to).join('');
}
