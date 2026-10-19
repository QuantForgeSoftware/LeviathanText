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

export interface MarkRange {
  start: number;
  end: number;
  kind: string;
}

export interface MarkSegment {
  text: string;
  kind: string; // '' for plain, otherwise a mark class suffix
}

/**
 * Split text into segments by scalar ranges so uncertainty/gap/supply marks can be rendered.
 * Overlapping ranges resolve to the last annotation in the supplied order (documented
 * simplification: the renderer shows one style per scalar).
 */
export function markSegments(text: string, ranges: MarkRange[]): MarkSegment[] {
  const scalars = Array.from(text);
  const kindAt: string[] = new Array(scalars.length).fill('');
  for (const range of ranges) {
    const kind = markClass(range.kind);
    if (!kind) continue;
    const from = Math.max(0, Math.min(range.start, scalars.length));
    const to = Math.max(from, Math.min(range.end, scalars.length));
    for (let i = from; i < to; i++) kindAt[i] = kind;
  }

  const segments: MarkSegment[] = [];
  for (let i = 0; i < scalars.length; i++) {
    if (segments.length > 0 && segments[segments.length - 1].kind === kindAt[i]) {
      segments[segments.length - 1].text += scalars[i];
    } else {
      segments.push({ text: scalars[i], kind: kindAt[i] });
    }
  }
  return segments;
}

function markClass(kind: string): string {
  switch (kind) {
    case 'Unclear':
    case 'Illegible': return 'unclear';
    case 'Lost': return 'lost';
    case 'SuppliedEditorial': return 'supplied';
    case 'AlternativeReading': return 'alternative';
    case 'Erased':
    case 'Overwritten': return 'erased';
    default: return '';
  }
}

export interface ScalarDiff {
  equal: boolean;
  prefix: string;
  aMiddle: string;
  bMiddle: string;
  suffix: string;
}

/**
 * Longest common prefix/suffix on scalars. This is a display aid: it shows the changed
 * middle, not a word-level or edit-distance alignment (documented limitation).
 */
export function scalarDiff(a: string, b: string): ScalarDiff {
  const left = Array.from(a);
  const right = Array.from(b);
  let prefix = 0;
  while (prefix < left.length && prefix < right.length && left[prefix] === right[prefix]) prefix++;
  let suffix = 0;
  while (
    suffix < left.length - prefix &&
    suffix < right.length - prefix &&
    left[left.length - 1 - suffix] === right[right.length - 1 - suffix]
  ) suffix++;

  const equal = prefix === left.length && prefix === right.length;
  return {
    equal,
    prefix: left.slice(0, prefix).join(''),
    aMiddle: left.slice(prefix, left.length - suffix).join(''),
    bMiddle: right.slice(prefix, right.length - suffix).join(''),
    suffix: suffix === 0 ? '' : left.slice(left.length - suffix).join(''),
  };
}
