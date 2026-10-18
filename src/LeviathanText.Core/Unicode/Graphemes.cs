using System.Globalization;

namespace LeviathanText.Core.Unicode;

/// <summary>
/// Extended grapheme cluster access (UAX #29), the unit for editing, alignment and
/// character-level scoring (greek-transcription.md §4). .NET's <see cref="StringInfo"/>
/// implements extended grapheme clusters, including combining sequences and ZWJ runs.
/// </summary>
public static class Graphemes
{
    /// <summary>Enumerate the extended grapheme clusters of <paramref name="text"/> in order.</summary>
    public static IEnumerable<(int StartScalar, int StartUtf16, string Text)> Enumerate(string text)
    {
        if (text.Length == 0) yield break;

        // Base-code-unit indices of each cluster start, per UAX #29 extended grapheme clusters.
        int[] starts = StringInfo.ParseCombiningCharacters(text);
        for (int k = 0; k < starts.Length; k++)
        {
            int startUtf16 = starts[k];
            int endUtf16 = k + 1 < starts.Length ? starts[k + 1] : text.Length;
            yield return (ScalarText.Utf16IndexToScalar(text, startUtf16), startUtf16, text[startUtf16..endUtf16]);
        }
    }

    public static int Count(string text) => Enumerate(text).Count();

    /// <summary>Scalar offsets at which each grapheme cluster starts; final entry = scalar length (cluster ends).</summary>
    public static int[] ClusterStartScalars(string text)
    {
        var starts = new List<int> { 0 };
        int scalar = 0;
        foreach (var (_, _, cluster) in Enumerate(text))
        {
            scalar += ScalarText.ScalarLength(cluster);
            starts.Add(scalar);
        }
        return [.. starts];
    }
}
