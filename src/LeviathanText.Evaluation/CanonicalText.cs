using System.Globalization;
using System.Text;
using LeviathanText.Core.Unicode;

namespace LeviathanText.Evaluation;

/// <summary>
/// Canonical text handling for scoring (examples.md §1): canonical-equivalence
/// normalization only (NFC), and scalar/grapheme iteration. No compatibility
/// folding, case folding, sigma substitution or accent stripping is ever applied.
///
/// Scalar enumeration routes through <see cref="ScalarText"/> so malformed text
/// (dangling surrogate halves) is handled exactly as Core handles it — a lone
/// surrogate half is its own scalar position, matching Core's contract and resolving
/// A-REV-EVAL-1.
/// </summary>
public static class CanonicalText
{
    public static string Nfc(string text) => text.Normalize(NormalizationForm.FormC);

    public static string Nfd(string text) => text.Normalize(NormalizationForm.FormD);

    /// <summary>
    /// Unicode scalars (code points) of <paramref name="text"/>, in order, using Core's
    /// scalar segmentation. A dangling surrogate half contributes its own scalar
    /// (its UTF-16 code-unit value); a valid surrogate pair contributes one scalar.
    /// </summary>
    public static List<int> ToScalars(string text)
    {
        int length = ScalarText.ScalarLength(text);
        var scalars = new List<int>(length);
        for (int i = 0; i < length; i++)
        {
            var slice = ScalarText.Slice(text, i, i + 1);
            scalars.Add(slice.Length == 1 ? slice[0] : char.ConvertToUtf32(slice, 0));
        }
        return scalars;
    }

    /// <summary>Inverse of <see cref="ToScalars"/>, tolerant of dangling-surrogate scalars.</summary>
    public static string FromScalars(IEnumerable<int> scalars)
    {
        var sb = new StringBuilder();
        foreach (int cp in scalars)
        {
            if (cp is >= 0xD800 and <= 0xDFFF) sb.Append((char)cp); // lone surrogate half
            else sb.Append(char.ConvertFromUtf32(cp));
        }
        return sb.ToString();
    }

    /// <summary>Unicode scalar length (code points, not UTF-16 units).</summary>
    public static int ScalarLength(string text) => ScalarText.ScalarLength(text);

    /// <summary>
    /// True when the scalar is a combining mark (any combining class category).
    /// Operates on NFD text so precomposed polytonic vowels are seen as base + mark.
    /// </summary>
    public static bool IsCombiningMark(int scalar)
    {
        if (scalar is < 0 or > 0x10FFFF) return false;
        var category = CharUnicodeInfo.GetUnicodeCategory(scalar);
        return category is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;
    }
}
