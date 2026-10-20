using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Bulk annotation import from the CSV produced by AnnotationCsv (or an external sheet).
/// Rows go through <see cref="TranscriptionService.AddAnnotation"/>, so range checks, layer
/// anchoring, gap-extent and alternative-reading rules still apply. Invalid rows are reported
/// and skipped; the annotation ledger remains append-only.
/// </summary>
public static class AnnotationCsvImport
{
    public sealed record RowResult(string LineId, string Kind, bool Applied, string? Reason);

    public sealed record Result(int Applied, int Skipped, IReadOnlyList<RowResult> Rows, NativeProject Project);

    public static Result Import(
        NativeProject project, string csv, TranscriptionService transcription, IProjectStore store)
    {
        var rows = LineTextImport.ParseCsv(csv);
        if (rows.Count == 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "csv",
                Message = "CSV is empty.",
            });

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Column(string name) => header.IndexOf(name);
        int lineColumn = Column("line_id"), kindColumn = Column("kind");
        int startColumn = Column("start"), endColumn = Column("end");
        if (lineColumn < 0 || kindColumn < 0 || startColumn < 0 || endColumn < 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "csv",
                Message = "CSV header must contain line_id, kind, start and end.",
            });

        string? Cell(List<string> row, int column) =>
            column >= 0 && column < row.Count ? row[column] : null;

        int applied = 0, skipped = 0;
        var results = new List<RowResult>();
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            var lineText = Cell(row, lineColumn)?.Trim() ?? "";
            var kindText = Cell(row, kindColumn)?.Trim() ?? "";
            try
            {
                if (!EntityId.TryParse(lineText, EntityKind.Line, out var lineId))
                    throw new FormatException("malformed line id");
                if (!Enum.TryParse<AnnotationKind>(kindText, ignoreCase: true, out var kind))
                    throw new FormatException("unknown annotation kind");
                if (!int.TryParse(Cell(row, startColumn), out var start)
                    || !int.TryParse(Cell(row, endColumn), out var end))
                    throw new FormatException("start/end must be integers");

                GapExtent? gap = null;
                if (Enum.TryParse<GapExtent>(Cell(row, Column("gap_extent")), ignoreCase: true, out var parsedGap))
                    gap = parsedGap;

                var annotation = new Annotation
                {
                    Id = new EntityId(EntityKind.Annotation, Guid.NewGuid().ToString("N")[..12]),
                    Kind = kind,
                    AnchoredRevisionId = default, // bind to current L1 server-side
                    Range = new ScalarRange(start, end),
                    GapExtent = gap,
                    AlternativeText = NullIfEmpty(Cell(row, Column("alternative_text"))),
                    HandLabel = NullIfEmpty(Cell(row, Column("hand_label"))),
                    Description = NullIfEmpty(Cell(row, Column("description"))),
                };

                var (updated, _, _) = transcription.AddAnnotation(project, lineId, annotation);
                project = updated;
                applied++;
                results.Add(new RowResult(lineText, kindText, true, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                skipped++;
                results.Add(new RowResult(lineText, kindText, false, ex.Message));
            }
        }

        return new Result(applied, skipped, results, store.LoadProject(project.Id) ?? project);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
