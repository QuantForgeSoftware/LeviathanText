namespace LeviathanText.Core.Unicode;

/// <summary>
/// Scalar-offset arithmetic over logical-order text (greek-transcription.md §4).
/// Scalar = Unicode code point; UTF-16 code units are a conversion target only.
/// All methods are pure; malformed surrogate halves are treated as their own
/// scalar position so offsets never silently shift.
/// </summary>
public static class ScalarText
{
    /// <summary>Number of Unicode scalars (code points) in <paramref name="text"/>.</summary>
    public static int ScalarLength(ReadOnlySpan<char> text)
    {
        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            count++;
            if (IsSurrogatePairAt(text, i)) i++;
        }
        return count;
    }

    /// <summary>
    /// Map from UTF-16 code-unit index to scalar index. Length = text.Length + 1 so both
    /// ends of every UTF-16 boundary are addressable; the low half of a surrogate pair
    /// maps to the same scalar as the high half.
    /// </summary>
    public static int[] Utf16ToScalarMap(ReadOnlySpan<char> text)
    {
        var map = new int[text.Length + 1];
        int scalar = 0;
        for (int i = 0; i < text.Length; i++)
        {
            map[i] = scalar;
            if (IsSurrogatePairAt(text, i))
            {
                map[i + 1] = scalar;
                scalar++;
                i++;
            }
            else
            {
                scalar++;
            }
        }
        map[text.Length] = scalar;
        return map;
    }

    /// <summary>Scalar length of the prefix up to UTF-16 index <paramref name="utf16Index"/>.
    /// An index strictly inside a surrogate pair maps to the pair's scalar, consistent with <see cref="Utf16ToScalarMap"/>.</summary>
    public static int Utf16IndexToScalar(ReadOnlySpan<char> text, int utf16Index)
    {
        if (utf16Index < 0 || utf16Index > text.Length) throw new ArgumentOutOfRangeException(nameof(utf16Index));
        return Utf16ToScalarMap(text)[utf16Index];
    }

    /// <summary>UTF-16 index of scalar offset <paramref name="scalarIndex"/>.</summary>
    public static int ScalarToUtf16Index(ReadOnlySpan<char> text, int scalarIndex)
    {
        if (scalarIndex < 0) throw new ArgumentOutOfRangeException(nameof(scalarIndex));
        int scalar = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (scalar == scalarIndex) return i;
            if (i == text.Length) break;
            i += IsSurrogatePairAt(text, i) ? 1 : 0;
            scalar++;
        }
        throw new ArgumentOutOfRangeException(nameof(scalarIndex), $"Scalar offset {scalarIndex} exceeds scalar length {ScalarLength(text)}.");
    }

    /// <summary>Extract the substring between scalar offsets [start, end).</summary>
    public static string Slice(ReadOnlySpan<char> text, int start, int end)
    {
        int utf16Start = ScalarToUtf16Index(text, start);
        int utf16End = ScalarToUtf16Index(text, end);
        return text[utf16Start..utf16End].ToString();
    }

    private static bool IsSurrogatePairAt(ReadOnlySpan<char> text, int i) =>
        char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
}
