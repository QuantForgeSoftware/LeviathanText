using System.Text;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Bulk text import from a CSV produced outside the workspace (review spreadsheet, adjudication
/// sheet, aligned edition). Every row becomes a normal revision through the transcription
/// service, so conflicts, provenance and layer separation still apply. Unknown line ids or
/// empty text are reported and skipped; nothing is silently dropped.
/// </summary>
public static class LineTextImport
{
    public sealed record RowResult(string LineId, bool Applied, string? Reason);

    public sealed record Result(
        int Applied,
        int Skipped,
        IReadOnlyList<RowResult> Rows,
        NativeProject Project);

    /// <summary>CSV must have a header with at least line_id and text (order free).</summary>
    public static Result Import(
        NativeProject project,
        string csv,
        TextLayer layer,
        string author,
        TranscriptionService transcription,
        IProjectStore store)
    {
        var rows = ParseCsv(csv);
        if (rows.Count == 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "csv",
                Message = "CSV is empty.",
            });

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int idColumn = header.IndexOf("line_id");
        int textColumn = header.IndexOf("text");
        if (idColumn < 0 || textColumn < 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "csv",
                Message = "CSV header must contain line_id and text.",
            });

        int applied = 0, skipped = 0;
        var results = new List<RowResult>();
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            if (row.Count <= Math.Max(idColumn, textColumn))
            {
                results.Add(new RowResult("", false, "row has too few columns"));
                skipped++;
                continue;
            }

            var lineText = row[idColumn].Trim();
            var text = row[textColumn];
            if (string.IsNullOrWhiteSpace(lineText) || string.IsNullOrWhiteSpace(text))
            {
                results.Add(new RowResult(lineText, false, "line_id or text is empty"));
                skipped++;
                continue;
            }

            EntityId lineId;
            try { lineId = EntityId.Parse(lineText, EntityKind.Line); }
            catch (Exception)
            {
                results.Add(new RowResult(lineText, false, "malformed line id"));
                skipped++;
                continue;
            }

            try
            {
                var line = project.Objects.SelectMany(o => o.Surfaces).SelectMany(s => s.Lines)
                    .FirstOrDefault(l => l.Id == lineId);
                if (line is null)
                {
                    results.Add(new RowResult(lineText, false, "line not found"));
                    skipped++;
                    continue;
                }

                var parent = line.DiplomaticRevisionId;
                var (updated, _, revision) = transcription.AppendLineRevision(project, lineId, layer, text,
                    new AgentRef { Id = author, IsModel = false }, parent);
                project = updated;
                applied++;
                results.Add(new RowResult(lineText, true, revision.Id.ToString()));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new RowResult(lineText, false, ex.Message));
                skipped++;
            }
        }

        return new Result(applied, skipped, results, store.LoadProject(project.Id) ?? project);
    }

    /// <summary>Minimal RFC-4180 CSV parser (quoted fields may contain commas/quotes/newlines).</summary>
    public static List<List<string>> ParseCsv(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = [];
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
