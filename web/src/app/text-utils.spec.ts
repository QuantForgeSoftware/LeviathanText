import { applyScalarEdit, markSegments, scalarCount, scalarDiff, scalarOffsetFromUtf16, utf16OffsetFromScalar } from './text-utils';

describe('text-utils (scalar offsets)', () => {
  const decomposed = '\u03B1\u0301\u03BD\u03B8\u03C1\u03C9\u03C0\u03BF\u03C2'; // α + acute + 7 letters
  const astral = '\uD835\uDD38'; // 𝔸: 1 scalar, 2 UTF-16 units

  it('counts scalars, not code units', () => {
    expect(scalarCount(decomposed)).toBe(9);
    expect(scalarCount(astral)).toBe(1);
    expect(scalarCount('')).toBe(0);
    expect(decomposed.length).toBe(9); // sanity: BMP text, units == scalars here
  });

  it('converts UTF-16 to scalar offsets at boundaries', () => {
    expect(scalarOffsetFromUtf16(astral + 'x', 0)).toBe(0);
    expect(scalarOffsetFromUtf16(astral + 'x', 2)).toBe(1); // after the astral char
    expect(scalarOffsetFromUtf16(astral + 'x', 3)).toBe(2);
  });

  it('round-trips at valid boundaries', () => {
    for (let scalar = 0; scalar <= scalarCount(decomposed + astral); scalar++) {
      const utf16 = utf16OffsetFromScalar(decomposed + astral, scalar);
      expect(scalarOffsetFromUtf16(decomposed + astral, utf16)).toBe(scalar);
    }
  });

  it('clamps out-of-range requests', () => {
    expect(scalarOffsetFromUtf16('ab', 99)).toBe(2);
    expect(utf16OffsetFromScalar('ab', 99)).toBe(2);
    expect(utf16OffsetFromScalar('', 1)).toBe(0);
  });
});

describe('applyScalarEdit', () => {
  it('replaces scalar ranges safely around astral characters', () => {
    const text = 'a' + '\uD835\uDD38' + 'b'; // a, 𝔸, b
    expect(applyScalarEdit(text, 1, 2, 'x')).toBe('axb');
    expect(applyScalarEdit(text, 0, 1, '')).toBe('\uD835\uDD38b');
  });

  it('clamps out-of-range edits', () => {
    expect(applyScalarEdit('abc', 2, 99, 'Z')).toBe('abZ');
    expect(applyScalarEdit('abc', 9, 10, 'Z')).toBe('abcZ');
    expect(applyScalarEdit('', 0, 0, 'x')).toBe('x');
  });
});

describe('markSegments', () => {
  it('splits scalar ranges into styled segments', () => {
    const segments = markSegments('αβγδ', [
      { start: 1, end: 3, kind: 'Unclear' },
      { start: 3, end: 4, kind: 'Lost' },
    ]);
    expect(segments).toEqual([
      { text: 'α', kind: '' },
      { text: 'βγ', kind: 'unclear' },
      { text: 'δ', kind: 'lost' },
    ]);
  });

  it('is scalar-safe with astral characters and clamps ranges', () => {
    const segments = markSegments('a\uD835\uDD38b', [{ start: 1, end: 99, kind: 'SuppliedEditorial' }]);
    expect(segments).toEqual([
      { text: 'a', kind: '' },
      { text: '\uD835\uDD38b', kind: 'supplied' },
    ]);
  });
});

describe('scalarDiff', () => {
  it('reports the changed middle and common edges', () => {
    expect(scalarDiff('αβγδ', 'αβXδ')).toEqual({
      equal: false, prefix: 'αβ', aMiddle: 'γ', bMiddle: 'X', suffix: 'δ',
    });
  });

  it('handles equality, insertion and astral scalars', () => {
    expect(scalarDiff('abc', 'abc').equal).toBe(true);
    expect(scalarDiff('ac', 'abc')).toEqual({ equal: false, prefix: 'a', aMiddle: '', bMiddle: 'b', suffix: 'c' });
    const astral = '\uD835\uDD38';
    expect(scalarDiff('a' + astral, 'a' + astral + 'b')).toEqual({
      equal: false, prefix: 'a' + astral, aMiddle: '', bMiddle: 'b', suffix: '',
    });
  });
});
