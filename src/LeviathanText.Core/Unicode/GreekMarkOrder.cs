namespace LeviathanText.Core.Unicode;

/// <summary>
/// Versioned combining-mark ordering for the Greek diplomatic profile
/// (greek-transcription.md §4: breathing → diaeresis → accent → vowel-length/other).
///
/// This is a *profile* order, not Unicode canonical equivalence: marks at the same
/// combining class are canonically unordered, so this reordering is a deliberate,
/// versioned editorial decision applied at import/storage time as a new revision —
/// it is never applied invisibly. Reordering is stable within equal ranks and
/// idempotent. Scoring normalizes through NFC (NfcMapper), so canonical-equivalent
/// strings still compare equal regardless of stored mark order.
/// </summary>
public static class GreekMarkOrder
{
    public const string Version = "greek-diplomatic-1.0-draft";

    // Rank per combining mark (combining class values are all 230 for these, so
    // canonical ordering alone does not order them; the profile does).
    private static readonly IReadOnlyDictionary<int, int> Ranks = new Dictionary<int, int>
    {
        [0x0313] = 0, // combining Greek psili (smooth breathing)
        [0x0314] = 0, // combining Greek dasia (rough breathing)
        [0x0308] = 1, // combining diaeresis
        [0x0300] = 2, // combining grave
        [0x0301] = 2, // combining acute
        [0x0342] = 2, // combining Greek perispomeni (circumflex)
        [0x0311] = 2, // combining inverted breve (variant circumflex)
        [0x0345] = 3, // combining Greek ypogegrammeni (iota subscript)
    };

    private const int FallbackRank = 4;

    /// <summary>
    /// Apply the profile's mark order to each extended grapheme cluster. Marks not in
    /// the rank table keep their relative order after all ranked marks. Base characters
    /// and non-combining content are untouched.
    /// </summary>
    public static string Reorder(string text) =>
        string.Concat(Graphemes.Enumerate(text).Select(c => ReorderCluster(c.Text)));

    private static string ReorderCluster(string cluster)
    {
        int firstMark = IndexOfFirstMark(cluster);
        if (firstMark == cluster.Length) return cluster; // no combining marks

        var prefix = cluster[..firstMark];

        var ranked = new List<(int Rank, int OriginalIndex, char Mark)>();
        var unranked = new List<char>();
        for (int i = firstMark; i < cluster.Length; i++)
        {
            char ch = cluster[i];
            if (char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark && Ranks.TryGetValue(ch, out var rank))
                ranked.Add((rank, i, ch));
            else
                unranked.Add(ch); // unranked combining marks and any other tail content keep original order
        }

        var sb = new System.Text.StringBuilder(prefix);
        foreach (var (_, _, mark) in ranked.OrderBy(m => m.Rank).ThenBy(m => m.OriginalIndex))
            sb.Append(mark);
        foreach (var mark in unranked)
            sb.Append(mark);
        return sb.ToString();
    }

    private static int IndexOfFirstMark(string cluster)
    {
        for (int i = 0; i < cluster.Length; i++)
            if (char.GetUnicodeCategory(cluster[i]) == System.Globalization.UnicodeCategory.NonSpacingMark)
                return i;
        return cluster.Length;
    }
}
