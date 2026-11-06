import { mergeText } from './text-merge';

describe('mergeText (C# ThreeWayMerge scalar contract)', () => {
  // Shared expected fixtures from Core.Tests/ThreeWayMergeTests.cs.
  const cleanCases: [string, string, string, string][] = [
    ['αβγδε', 'Αβγδε', 'αβγδΕ', 'ΑβγδΕ'],
    ['αβγ', 'αΒγ', 'αΒγ', 'αΒγ'],
    ['αβ', 'αβ', 'αβγδ', 'αβγδ'],
    ['αβ', 'αβγδ', 'αβ', 'αβγδ'],
    ['🙂αβγδε🙂', '🙂Αβγδε🙂', '🙂αβγδΕ🙂', '🙂ΑβγδΕ🙂'],
    ['αβ', 'Αβ', 'αXβ', 'ΑXβ'],
  ];
  cleanCases.forEach(([base, local, remote, expected], index) => {
    it(`matches clean C# fixture ${index + 1}`, () => {
      expect(mergeText(base, local, remote)).toEqual({ success: true, text: expected });
      expect(mergeText(base, remote, local)).toEqual({ success: true, text: expected });
    });
  });

  it('rebases later typing onto the acknowledged merged save without dropping the remote prefix', () => {
    expect(mergeText('αγ', 'αγε', 'δαγ')).toEqual({ success: true, text: 'δαγε' });
  });

  it('refuses conflicting replacements and preserves the base instead of choosing a reading', () => {
    expect(mergeText('αβγ', 'αΒγ', 'αXγ')).toEqual({ success: false, text: 'αβγ' });
  });

  it('refuses different insertions at the same scalar boundary', () => {
    expect(mergeText('αβ', 'αXβ', 'αYβ')).toEqual({ success: false, text: 'αβ' });
    expect(mergeText('', 'α', 'β')).toEqual({ success: false, text: '' });
  });

  it('merges identical hunks once while retaining other disjoint edits', () => {
    expect(mergeText('αβγδε', 'ΑβγδεΖ', 'ΑβγΔε')).toEqual({ success: true, text: 'ΑβγΔεΖ' });
  });

  it('keeps combining marks and astral characters as distinct intact scalars without normalization', () => {
    expect(mergeText('🙂α\u0301β', '🙂α\u0300β', '𝔸α\u0301β')).toEqual({ success: true, text: '𝔸α\u0300β' });
    expect(mergeText('🙂α\u0301β', '🙂α\u0300β', '🙂α\u0308β')).toEqual({ success: false, text: '🙂α\u0301β' });
  });

  it('allows insertions at both edges of a replacement and adjacent replacements', () => {
    expect(mergeText('αβγ', 'αΒγ', 'αXβYγ')).toEqual({ success: true, text: 'αXΒYγ' });
    expect(mergeText('αβγ', 'Αβγ', 'αΒγ')).toEqual({ success: true, text: 'ΑΒγ' });
  });

  it('rejects an insertion strictly inside a removed span', () => {
    expect(mergeText('αβγδ', 'αδ', 'αβXγδ')).toEqual({ success: false, text: 'αβγδ' });
  });

  it('uses the same deletion-first LCS tie break for repeated text', () => {
    expect(mergeText('ααβ', 'αβ', 'ααΒ')).toEqual({ success: true, text: 'αΒ' });
  });

  it('merges disjoint large-text prefix/suffix hunks without allocating an exact table', () => {
    const base = 'α'.repeat(2_001);
    expect(mergeText(base, 'β' + base.slice(1), base.slice(0, -1) + 'γ'))
      .toEqual({ success: true, text: 'β' + 'α'.repeat(1_999) + 'γ' });
  });

  it('conservatively refuses overlapping large single-hunk approximations', () => {
    const base = 'α'.repeat(2_001);
    const local = 'β' + base.slice(1, -1) + 'γ';
    const remote = base.slice(0, 1_000) + 'δ' + base.slice(1_001);
    expect(mergeText(base, local, remote)).toEqual({ success: false, text: base });
  });

  it('normalizes lone surrogates as .NET rune enumeration does when diffing', () => {
    expect(mergeText('\ud800αβ', '\ud800Αβ', '\ud800αΒ')).toEqual({ success: true, text: '\ufffdΑΒ' });
  });
});
