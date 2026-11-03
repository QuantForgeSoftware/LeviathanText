namespace LeviathanText.Core.Text;

/// <summary>
/// Bounded three-way text merge for concurrent line revisions.
///
/// The merge is deliberately conservative: it combines non-overlapping edits and identical
/// edits, and refuses every other overlap instead of choosing a winner. A refusal carries the
/// base range and both candidate replacements so the caller can surface a real conflict.
/// Offsets are Unicode scalars (the same currency as annotations).
/// </summary>
public static class ThreeWayMerge
{
    /// <summary>Largest base×variant scalar product for which an exact LCS diff is attempted.</summary>
    public const long MaxExactDiffCells = 4_000_000;

    public sealed record Hunk(int Start, int End, string Replacement);

    public sealed record MergeConflict(int Start, int End, string Local, string Remote);

    public sealed record MergeResult(
        bool Success,
        string Text,
        IReadOnlyList<MergeConflict> Conflicts)
    {
        public static MergeResult Clean(string text) => new(true, text, []);
        public static MergeResult Conflicted(string baseText, IReadOnlyList<MergeConflict> conflicts) =>
            new(false, baseText, conflicts);
    }

    public static MergeResult Merge(string baseText, string localText, string remoteText)
    {
        if (localText == remoteText) return MergeResult.Clean(localText);
        if (localText == baseText) return MergeResult.Clean(remoteText);
        if (remoteText == baseText) return MergeResult.Clean(localText);

        var baseScalars = Scalars(baseText);
        var localHunks = Diff(baseScalars, Scalars(localText));
        var remoteHunks = Diff(baseScalars, Scalars(remoteText));

        var accepted = new List<SourcedHunk>();
        var conflicts = new List<MergeConflict>();
        foreach (var candidate in localHunks.Select(h => new SourcedHunk(h, true))
                     .Concat(remoteHunks.Select(h => new SourcedHunk(h, false)))
                     .OrderBy(h => h.Hunk.Start)
                     .ThenBy(h => h.Hunk.End)
                     .ThenBy(h => h.IsLocal ? 0 : 1))
        {
            var clash = accepted.FirstOrDefault(existing => Overlaps(existing.Hunk, candidate.Hunk));
            if (clash is null)
            {
                accepted.Add(candidate);
                continue;
            }

            if (clash.Hunk.Start == candidate.Hunk.Start && clash.Hunk.End == candidate.Hunk.End
                && clash.Hunk.Replacement == candidate.Hunk.Replacement)
                continue; // both writers made the same edit

            var localHunk = clash.IsLocal ? clash.Hunk : candidate.Hunk;
            var remoteHunk = clash.IsLocal ? candidate.Hunk : clash.Hunk;
            conflicts.Add(new MergeConflict(
                Math.Min(clash.Hunk.Start, candidate.Hunk.Start),
                Math.Max(clash.Hunk.End, candidate.Hunk.End),
                localHunk.Replacement,
                remoteHunk.Replacement));
        }

        if (conflicts.Count > 0) return MergeResult.Conflicted(baseText, conflicts);

        var builder = new System.Text.StringBuilder();
        int cursor = 0;
        foreach (var hunk in accepted.Select(h => h.Hunk).OrderBy(h => h.Start).ThenBy(h => h.End))
        {
            if (hunk.Start > cursor) AppendScalars(builder, baseScalars, cursor, hunk.Start);
            builder.Append(hunk.Replacement);
            cursor = hunk.End;
        }
        if (cursor < baseScalars.Length) AppendScalars(builder, baseScalars, cursor, baseScalars.Length);
        return MergeResult.Clean(builder.ToString());
    }

    private sealed record SourcedHunk(Hunk Hunk, bool IsLocal);

    private static bool Overlaps(Hunk a, Hunk b)
    {
        if (a.Start == a.End && b.Start == b.End) return a.Start == b.Start;
        if (a.Start == a.End) return b.Start < a.Start && a.Start < b.End;
        if (b.Start == b.End) return a.Start < b.Start && b.Start < a.End;
        return a.Start < b.End && b.Start < a.End;
    }

    private static List<Hunk> Diff(int[] baseScalars, int[] variant)
    {
        long cells = (long)(baseScalars.Length + 1) * (variant.Length + 1);
        return cells <= MaxExactDiffCells ? LcsHunks(baseScalars, variant) : SingleHunk(baseScalars, variant);
    }

    private static List<Hunk> LcsHunks(int[] a, int[] b)
    {
        int n = a.Length, m = b.Length;
        var table = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                table[i, j] = a[i] == b[j]
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);

        var hunks = new List<Hunk>();
        int x = 0, y = 0;
        while (x < n || y < m)
        {
            if (x < n && y < m && a[x] == b[y])
            {
                x++;
                y++;
                continue;
            }

            int start = x;
            var replacement = new List<int>();
            while (x < n || y < m)
            {
                if (x < n && y < m && a[x] == b[y]) break;
                if (x < n && (y >= m || table[x + 1, y] >= table[x, y + 1]))
                    x++;
                else
                {
                    replacement.Add(b[y]);
                    y++;
                }
            }
            hunks.Add(new Hunk(start, x, FromScalars(replacement)));
        }
        return hunks;
    }

    private static List<Hunk> SingleHunk(int[] a, int[] b)
    {
        int prefix = 0;
        while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix]) prefix++;
        int suffix = 0;
        while (suffix < a.Length - prefix && suffix < b.Length - prefix
               && a[a.Length - 1 - suffix] == b[b.Length - 1 - suffix]) suffix++;
        var replacement = new int[b.Length - prefix - suffix];
        Array.Copy(b, prefix, replacement, 0, replacement.Length);
        return [new Hunk(prefix, a.Length - suffix, FromScalars(replacement))];
    }

    private static int[] Scalars(string text) =>
        text.EnumerateRunes().Select(rune => rune.Value).ToArray();

    private static string FromScalars(IReadOnlyList<int> scalars)
    {
        var builder = new System.Text.StringBuilder(scalars.Count);
        foreach (var scalar in scalars) builder.Append(char.ConvertFromUtf32(scalar));
        return builder.ToString();
    }

    private static void AppendScalars(System.Text.StringBuilder builder, int[] scalars, int start, int end)
    {
        for (int i = start; i < end; i++) builder.Append(char.ConvertFromUtf32(scalars[i]));
    }
}
