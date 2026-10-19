using System.Globalization;
using System.Text;

namespace LeviathanText.Core.Unicode;

/// <summary>
/// Bounded Greek transcription-profile inspection (todo §2: versioned profiles; do not infer
/// accents, do not fold or strip marks). This is a data-QA aid, not a normalizer: it
/// classifies scalars, flags characters outside the pilot profile and flags combining marks
/// with no base. Nothing is rewritten.
/// </summary>
public static class GreekTextProfile
{
    public const string ProfileId = "greek-diplomatic-pilot";
    public const string Version = "1.0";

    public enum Category
    {
        GreekLetter,
        CombiningMark,
        Punctuation,
        NumeralSign,
        EditorialBracket,
        Space,
        LatinLetter,
        Digit,
        Other,
        UnpairedMark,
    }

    public sealed record Issue(int ScalarOffset, Category Category, string Scalar);

    public sealed record Report(
        int ScalarCount,
        IReadOnlyDictionary<Category, int> Counts,
        IReadOnlyList<Issue> Issues)
    {
        public bool IsClean => Issues.Count == 0;
    }

    private static readonly HashSet<int> Punctuation =
    [
        0x00B7, // middle dot
        0x0387, // greek ano teleia
        0x002E, 0x002C, 0x003B, 0x003A, 0x0021, 0x003F,
        0x2013, 0x2014, 0x2026, 0x00AB, 0x00BB,
        0x0027, 0x2019,
    ];

    private static readonly HashSet<int> Brackets =
    [
        0x0028, 0x0029, 0x005B, 0x005D, 0x007B, 0x007D,
        0x2329, 0x232A, // angle brackets
        0x27E8, 0x27E9, // mathematical angle brackets
        0x27E6, 0x27E7, // white square brackets (editorial)
        0x2E22, 0x2E23, // top half brackets (editorial)
        0x2E24, 0x2E25, // bottom half brackets
    ];

    private static readonly HashSet<int> NumeralSigns =
    [
        0x02B9, // modifier prime (Greek numeral)
        0x2032, // prime
        0x2033, // double prime
        0x0374, // greek numeral sign
        0x0375, // greek lower numeral sign
        0x002F, // solidus in fractions
        0x2044, // fraction slash
    ];

    public static Report Inspect(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var counts = new Dictionary<Category, int>();
        var issues = new List<Issue>();
        int scalarIndex = 0;
        bool haveBase = false;

        foreach (var rune in text.EnumerateRunes())
        {
            var category = Classify(rune);
            if (category == Category.CombiningMark && !haveBase)
            {
                category = Category.UnpairedMark;
                issues.Add(new Issue(scalarIndex, category, rune.ToString()));
            }

            counts[category] = counts.GetValueOrDefault(category) + 1;

            if (category is Category.GreekLetter or Category.LatinLetter or Category.Digit)
                haveBase = true;
            else if (category is not Category.CombiningMark)
                haveBase = false;

            if (category is Category.Other or Category.LatinLetter)
                issues.Add(new Issue(scalarIndex, category, rune.ToString()));

            scalarIndex++;
        }

        return new Report(scalarIndex, counts, issues);
    }

    public static Category Classify(Rune rune)
    {
        int value = rune.Value;
        // Numeral and punctuation signs checked before the Greek block: U+0374/U+0375
        // (numeral signs) live inside the Greek range but are not letters.
        if (NumeralSigns.Contains(value)) return Category.NumeralSign;
        if (Brackets.Contains(value)) return Category.EditorialBracket;
        if (Punctuation.Contains(value)) return Category.Punctuation;
        if (value is >= 0x0370 and <= 0x03FF or >= 0x1F00 and <= 0x1FFF) return Category.GreekLetter;
        if (value is >= 0x0041 and <= 0x005A or >= 0x0061 and <= 0x007A) return Category.LatinLetter;
        if (value is >= 0x0030 and <= 0x0039) return Category.Digit;
        if (value is 0x0020 or 0x0009 or 0x000A or 0x000D or 0x00A0)
            return Category.Space;

        var unicodeCategory = Rune.GetUnicodeCategory(rune);
        return unicodeCategory switch
        {
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark
                => Category.CombiningMark,
            _ => Category.Other,
        };
    }
}
