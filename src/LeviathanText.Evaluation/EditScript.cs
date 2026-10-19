namespace LeviathanText.Evaluation;

public enum EditKind
{
    Match,
    Substitute,
    Insert,
    Delete,
}

/// <summary>
/// One alignment operation. <see cref="ReferenceIndex"/> is -1 for an insertion;
/// <see cref="HypothesisIndex"/> is -1 for a deletion.
/// </summary>
public readonly record struct EditOp(EditKind Kind, int ReferenceIndex, int HypothesisIndex);

/// <summary>A Levenshtein alignment between a reference and a hypothesis.</summary>
public sealed class EditScript
{
    private EditScript(IReadOnlyList<EditOp> ops, int matches, int substitutions, int insertions, int deletions)
    {
        Ops = ops;
        Matches = matches;
        Substitutions = substitutions;
        Insertions = insertions;
        Deletions = deletions;
    }

    public IReadOnlyList<EditOp> Ops { get; }
    public int Matches { get; }
    public int Substitutions { get; }
    public int Insertions { get; }
    public int Deletions { get; }

    /// <summary>Total edit distance (substitutions + insertions + deletions).</summary>
    public int Distance => Substitutions + Insertions + Deletions;

    /// <summary>
    /// Standard dynamic-programming Levenshtein alignment with traceback.
    /// Tie-breaking prefers match, then substitute, then delete, then insert,
    /// which is deterministic and stable across runs.
    /// </summary>
    public static EditScript Compute<T>(IReadOnlyList<T> reference, IReadOnlyList<T> hypothesis, IEqualityComparer<T>? comparer = null)
    {
        comparer ??= EqualityComparer<T>.Default;
        int n = reference.Count;
        int m = hypothesis.Count;
        var dp = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) dp[i, 0] = i;
        for (int j = 0; j <= m; j++) dp[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                bool equal = comparer.Equals(reference[i - 1], hypothesis[j - 1]);
                int diag = dp[i - 1, j - 1] + (equal ? 0 : 1);
                int del = dp[i - 1, j] + 1;
                int ins = dp[i, j - 1] + 1;
                dp[i, j] = Math.Min(diag, Math.Min(del, ins));
            }
        }

        var ops = new List<EditOp>(Math.Max(n, m));
        int matches = 0, substitutions = 0, insertions = 0, deletions = 0;
        int x = n, y = m;
        while (x > 0 || y > 0)
        {
            if (x > 0 && y > 0 && comparer.Equals(reference[x - 1], hypothesis[y - 1]) && dp[x, y] == dp[x - 1, y - 1])
            {
                ops.Add(new EditOp(EditKind.Match, x - 1, y - 1));
                matches++;
                x--; y--;
            }
            else if (x > 0 && y > 0 && dp[x, y] == dp[x - 1, y - 1] + 1)
            {
                ops.Add(new EditOp(EditKind.Substitute, x - 1, y - 1));
                substitutions++;
                x--; y--;
            }
            else if (x > 0 && dp[x, y] == dp[x - 1, y] + 1)
            {
                ops.Add(new EditOp(EditKind.Delete, x - 1, -1));
                deletions++;
                x--;
            }
            else
            {
                ops.Add(new EditOp(EditKind.Insert, -1, y - 1));
                insertions++;
                y--;
            }
        }

        ops.Reverse();
        return new EditScript(ops, matches, substitutions, insertions, deletions);
    }
}
