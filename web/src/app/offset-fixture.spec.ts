import fixtures from './fixtures/offset-fixtures.json';
import { scalarCount, scalarOffsetFromUtf16, utf16OffsetFromScalar } from './text-utils';

describe('shared offset fixture', () => {
  for (const testCase of fixtures.cases) {
    it(`matches scalar/UTF-16 boundaries for ${testCase.name}`, () => {
      expect(scalarCount(testCase.text)).toBe(testCase.scalarOffsets[testCase.scalarOffsets.length - 1]);
      for (let i = 0; i < testCase.utf16Offsets.length; i++) {
        expect(scalarOffsetFromUtf16(testCase.text, testCase.utf16Offsets[i])).toBe(testCase.scalarOffsets[i]);
        expect(utf16OffsetFromScalar(testCase.text, testCase.scalarOffsets[i])).toBe(testCase.utf16Offsets[i]);
      }
    });
  }
});
