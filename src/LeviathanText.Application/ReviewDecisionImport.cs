using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>Applies reviewer-packet decisions through the existing review/correction ledger.</summary>
public static class ReviewDecisionImport
{
    public sealed record RowResult(string LineId, string Decision, bool Applied, string? Reason);
    public sealed record Result(int Applied, int Skipped, IReadOnlyList<RowResult> Rows, NativeProject Project);

    public static Result Apply(NativeProject project, string csv, string reviewer,
        ReviewService reviews, IProjectStore store)
    {
        var rows = LineTextImport.ParseCsv(csv);
        if (rows.Count == 0)
            throw new LeviathanException(new LeviathanError { Code = ErrorCode.StageContractViolation, Scope = "review", Message = "Decisions CSV is empty." });

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int lineColumn = header.IndexOf("line_id");
        int decisionColumn = header.IndexOf("decision");
        int textColumn = header.IndexOf("corrected_text");
        int noteColumn = header.IndexOf("notes");
        if (lineColumn < 0 || decisionColumn < 0)
            throw new LeviathanException(new LeviathanError { Code = ErrorCode.StageContractViolation, Scope = "review", Message = "Decisions CSV must contain line_id and decision." });

        string? Cell(List<string> row, int column) =>
            column >= 0 && column < row.Count ? row[column] : null;

        int applied = 0, skipped = 0;
        var results = new List<RowResult>();
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            var lineText = Cell(row, lineColumn)?.Trim() ?? "";
            var decision = Cell(row, decisionColumn)?.Trim().ToLowerInvariant() ?? "";
            try
            {
                if (!EntityId.TryParse(lineText, EntityKind.Line, out var lineId))
                    throw new FormatException("malformed line id");
                var (outcome, text, note) = decision switch
                {
                    "agree" => (ReviewOutcome.Accept, (string?)null, Cell(row, noteColumn)),
                    "correct" => (ReviewOutcome.Correct, Cell(row, textColumn), Cell(row, noteColumn)),
                    "unclear" => (ReviewOutcome.Dispute, null, "unclear: " + (Cell(row, noteColumn) ?? "")),
                    "not-visible" or "not visible" => (ReviewOutcome.Dispute, null, "not visible: " + (Cell(row, noteColumn) ?? "")),
                    _ => throw new FormatException("unknown decision"),
                };
                if (outcome == ReviewOutcome.Correct && string.IsNullOrWhiteSpace(text))
                    throw new FormatException("correct decision requires corrected_text");

                var (updated, _, _, _) = reviews.Record(project, lineId, reviewer, outcome, note, text);
                project = store.LoadProject(updated.Id) ?? updated;
                applied++;
                results.Add(new RowResult(lineText, decision, true, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                skipped++;
                results.Add(new RowResult(lineText, decision, false, ex.Message));
            }
        }
        return new Result(applied, skipped, results, project);
    }
}
