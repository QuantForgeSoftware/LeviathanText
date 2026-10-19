import { scalarCount, scalarOffsetFromUtf16, utf16OffsetFromScalar } from './text-utils';

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
