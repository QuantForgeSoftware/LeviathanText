using LeviathanText.Core.Unicode;

namespace LeviathanText.Core.Tests;

public class ScalarTextTests
{
    private const string PrecomposedAlpha = "\u03AC";            // ά precomposed
    private const string DecomposedAlpha = "\u03B1\u0301";       // α + combining acute
    private const string NomenSacrum = "\u039A\u0305\u03F2\u0305"; // Κ + overline, lunate sigma + overline
    private const string AstralA = "\uD835\uDD38";               // 𝔸, 1 scalar / 2 UTF-16 units
    private const string Charmaritos = "\u03C7" + DecomposedAlpha + "\u03C1\u03B9\u03C4\u03BF\u03C2"; // χ+α+́+ρ+ι+τ+ο+ς

    [Theory]
    [InlineData("\u03AC", 1)]      // precomposed: 1 scalar
    [InlineData("\u03B1\u0301", 2)] // decomposed: 2 scalars
    [InlineData("\u039A\u0305\u03F2\u0305", 4)]
    [InlineData("\uD835\uDD38", 1)]
    [InlineData("", 0)]
    public void ScalarLength_counts_scalars(string text, int expected) =>
        Assert.Equal(expected, ScalarText.ScalarLength(text));

    [Fact]
    public void Utf16ToScalarMap_covers_all_boundaries()
    {
        var text = AstralA + "α"; // 3 UTF-16 units, 2 scalars
        var map = ScalarText.Utf16ToScalarMap(text);
        Assert.Equal(4, map.Length);
        Assert.Equal(0, map[0]); // start of high surrogate
        Assert.Equal(0, map[1]); // low half: same scalar
        Assert.Equal(1, map[2]); // α
        Assert.Equal(2, map[3]); // end
    }

    [Fact]
    public void Utf16_and_scalar_conversion_roundtrip()
    {
        var text = Charmaritos + " " + AstralA;
        var map = ScalarText.Utf16ToScalarMap(text);
        for (int utf16 = 0; utf16 <= text.Length; utf16++)
        {
            // Roundtrip holds at valid boundaries; an index strictly inside a surrogate
            // pair is not a scalar boundary, so it is skipped.
            bool insidePair = utf16 >= 2
                && char.IsHighSurrogate(text[utf16 - 1])
                && char.IsLowSurrogate(text[utf16]);
            if (insidePair) continue;

            var scalar = ScalarText.Utf16IndexToScalar(text, utf16);
            Assert.Equal(map[utf16], scalar); // conversion agrees with the map
            Assert.Equal(utf16, ScalarText.ScalarToUtf16Index(text, scalar));
        }
    }

    [Fact]
    public void Slice_extracts_by_scalar_range()
    {
        // Charmaritos: χ(0) α(1) ́(2) ρ(3) ι(4) τ(5) ο(6) ς(7) — slice scalars [1,4) = α, ́, ρ
        var sliced = ScalarText.Slice(Charmaritos, 1, 4);
        Assert.Equal(DecomposedAlpha + "ρ", sliced);
        Assert.Equal(3, ScalarText.ScalarLength(sliced));
    }

    [Fact]
    public void ScalarToUtf16Index_out_of_range_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScalarText.ScalarToUtf16Index("αβ", 3));
    }

    [Fact]
    public void Precomposed_and_decomposed_have_different_utf16_but_same_canonical_text()
    {
        Assert.Equal(2, ScalarText.ScalarLength(DecomposedAlpha));
        Assert.Equal(1, ScalarText.ScalarLength(PrecomposedAlpha));
        Assert.NotEqual(DecomposedAlpha, PrecomposedAlpha);
        Assert.Equal(PrecomposedAlpha, DecomposedAlpha.Normalize(System.Text.NormalizationForm.FormC));
    }
}
