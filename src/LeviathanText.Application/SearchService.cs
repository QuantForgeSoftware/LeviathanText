using System.Globalization;
using System.Text;
using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;
using LeviathanText.Formats;
using ScalarText = LeviathanText.Core.Unicode.ScalarText;

namespace LeviathanText.Application;

/// <summary>
/// Text search across a project's revisions (collection workflow). Two modes:
/// <list type="bullet">
/// <item><c>literal</c> — ordinal substring on the stored text (marks are significant);</item>
/// <item><c>base</c> — comparison on NFD-decomposed text with combining marks and case
/// folded away (search convenience only; nothing is stored or exported folded).</item>
/// </list>
/// Hits report the revision/layer and a scalar range in the <b>stored</b> text.
/// </summary>
public static class SearchService
{
    public const int MaxHits = 200;

    public sealed record Hit(
        EntityId SurfaceId,
        EntityId LineId,
        EntityId RevisionId,
        TextLayer Layer,
        string Author,
        string Snippet,
        ScalarRange Match,
        int ScalarLength);

    public sealed record Result(string Query, string Mode, int TotalHits, bool Truncated, IReadOnlyList<Hit> Hits);

    public static Result Search(NativeProject project, string query, string mode = "literal")
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(query))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = "search",
                Message = "Search query is empty.",
            });

        bool baseMode = mode.Equals("base", StringComparison.OrdinalIgnoreCase);
        if (!baseMode && !mode.Equals("literal", StringComparison.OrdinalIgnoreCase))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = mode,
                Message = "Search mode must be 'literal' or 'base'.",
            });

        var hits = new List<Hit>();
        bool truncated = false;
        int total = 0;

        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
        {
            foreach (var revision in surface.Revisions.OrderBy(r => r.CreatedAt))
            {
                var match = baseMode
                    ? FindBaseMatch(revision.Text, query)
                    : FindLiteralMatch(revision.Text, query);
                if (match is null) continue;

                total++;
                if (hits.Count >= MaxHits) { truncated = true; continue; }

                hits.Add(new Hit(
                    surface.Id,
                    revision.ScopeId,
                    revision.Id,
                    revision.Layer,
                    revision.Author.Id,
                    Snippet(revision.Text, match.Value.Start),
                    match.Value,
                    ScalarText.ScalarLength(revision.Text)));
            }
        }

        return new Result(query, baseMode ? "base" : "literal", total, truncated, hits);
    }

    private static ScalarRange? FindLiteralMatch(string text, string query)
    {
        int index = text.IndexOf(query, StringComparison.Ordinal);
        if (index < 0) return null;
        int start = ScalarText.ScalarLength(text.AsSpan(0, index));
        return new ScalarRange(start, start + ScalarText.ScalarLength(query));
    }

    /// <summary>Mark/case-folded search; maps the hit back to scalar offsets in the stored text.</summary>
    private static ScalarRange? FindBaseMatch(string text, string query)
    {
        var transformed = new StringBuilder(text.Length);
        var scalarToTransformed = new List<int>(text.Length); // transformed index -> stored scalar offset
        int scalar = 0;

        foreach (var rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            bool isMark = category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
            if (!isMark)
            {
                foreach (var c in rune.ToString().Normalize(NormalizationForm.FormD))
                {
                    var folded = char.ToLowerInvariant(c);
                    if (CharUnicodeInfo.GetUnicodeCategory(folded) is UnicodeCategory.NonSpacingMark
                        or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
                        continue;
                    transformed.Append(folded);
                    scalarToTransformed.Add(scalar);
                }
            }
            scalar++;
        }

        var normalizedText = transformed.ToString();
        var normalizedQuery = Fold(query);
        int found = normalizedText.IndexOf(normalizedQuery, StringComparison.Ordinal);
        if (found < 0) return null;

        int startScalar = scalarToTransformed[found];
        int lastTransformed = found + normalizedQuery.Length - 1;
        int endScalar = lastTransformed < scalarToTransformed.Count
            ? scalarToTransformed[lastTransformed] + 1
            : ScalarText.ScalarLength(text);
        return new ScalarRange(startScalar, endScalar);
    }

    private static string Fold(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
                continue;
            foreach (var c in rune.ToString().Normalize(NormalizationForm.FormD))
            {
                var folded = char.ToLowerInvariant(c);
                if (CharUnicodeInfo.GetUnicodeCategory(folded) is UnicodeCategory.NonSpacingMark
                    or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
                    continue;
                builder.Append(folded);
            }
        }
        return builder.ToString();
    }

    private static string Snippet(string text, int scalarStart, int radius = 32)
    {
        var scalars = new List<string>();
        foreach (var rune in text.EnumerateRunes()) scalars.Add(rune.ToString());
        int from = Math.Max(0, scalarStart - radius);
        int to = Math.Min(scalars.Count, scalarStart + radius);
        var body = string.Concat(scalars.Skip(from).Take(to - from));
        return (from > 0 ? "…" : "") + body + (to < scalars.Count ? "…" : "");
    }
}
