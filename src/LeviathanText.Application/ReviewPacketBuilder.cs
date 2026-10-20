using System.Net;
using System.Text;
using System.Text.Json;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using SkiaSharp;

namespace LeviathanText.Application;

/// <summary>
/// Builds a self-contained reviewer packet: one HTML page with a crop and the current L1 text
/// for each line, plus a downloadable decisions CSV. It does not adjudicate anything itself;
/// the reviewer chooses agree/correct/unclear/not-visible and supplies corrected text.
/// </summary>
public static class ReviewPacketBuilder
{
    public sealed record Result(string IndexPath, string DecisionsPath, int LineCount, int Skipped, int SurfaceCount);

    public static Result Build(NativeProject project, string outputDirectory, int limit = 0, EntityId? surfaceId = null)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("An output directory is required.", nameof(outputDirectory));
        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        var cropDirectory = Path.Combine(root, "crops");
        Directory.CreateDirectory(cropDirectory);

        var surfaces = project.Objects.SelectMany(o => o.Surfaces)
            .Where(s => surfaceId is null || s.Id == surfaceId)
            .ToList();
        if (surfaces.Count == 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = surfaceId?.ToString() ?? project.Id.ToString(),
                Message = "No matching surface with a capture was found for the review packet.",
            });

        var rows = new StringBuilder();
        int lineCount = 0, skipped = 0;
        foreach (var surface in surfaces)
        {
            var capture = surface.Captures.FirstOrDefault();
            if (capture is null || !File.Exists(capture.ImageLocation))
            {
                skipped += surface.Lines.Count;
                continue;
            }

            using var page = ImageDecoder.Decode(capture.ImageLocation);
            foreach (var line in surface.Lines)
            {
                if (limit > 0 && lineCount >= limit) break;
                var geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
                if (geometry.Count < 2)
                {
                    skipped++;
                    continue;
                }

                var l1 = surface.Revisions
                    .Where(r => r.ScopeId == line.Id && r.Layer == TextLayer.L1Diplomatic)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();
                var l2 = surface.Revisions
                    .Where(r => r.ScopeId == line.Id && r.Layer == TextLayer.L2Editorial)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();
                var current = l1?.Text ?? "";
                var reference = l2?.Text ?? "";

                var points = geometry.Select(p => new SKPoint((float)p.X, (float)p.Y)).ToList();
                var crop = LineCropper.Extract(page.Bitmap, points);
                var cropName = SafeName(line.Id.ToString()) + ".png";
                using (var data = crop.Bitmap.Encode(SKEncodedImageFormat.Png, 90))
                using (var stream = File.Create(Path.Combine(cropDirectory, cropName)))
                    data.SaveTo(stream);
                crop.Bitmap.Dispose();

                rows.Append("<tr data-line=\"").Append(WebUtility.HtmlEncode(line.Id.ToString()))
                    .Append("\" data-surface=\"").Append(WebUtility.HtmlEncode(surface.Id.ToString())).Append("\">");
                rows.Append("<td><img src=\"crops/").Append(WebUtility.HtmlEncode(cropName))
                    .Append("\" alt=\"line crop\" loading=\"lazy\"></td>");
                rows.Append("<td><code>").Append(WebUtility.HtmlEncode(line.Id.ToString())).Append("</code><br><span class=\"hint\">")
                    .Append(WebUtility.HtmlEncode(surface.Id.ToString())).Append("</span></td>");
                rows.Append("<td class=\"text\">").Append(WebUtility.HtmlEncode(current)).Append("</td>");
                rows.Append("<td class=\"text reference\">").Append(WebUtility.HtmlEncode(reference)).Append("</td>");
                rows.Append("<td><select class=\"decision\"><option value=\"agree\">agree</option><option value=\"correct\">correct</option>")
                    .Append("<option value=\"unclear\">unclear</option><option value=\"not-visible\">not visible</option></select></td>");
                rows.Append("<td><textarea class=\"corrected\" spellcheck=\"false\">")
                    .Append(WebUtility.HtmlEncode(current)).Append("</textarea></td>");
                rows.Append("<td><input class=\"notes\" placeholder=\"notes\"></td></tr>");
                lineCount++;
            }
            if (limit > 0 && lineCount >= limit) break;
        }

        var decisionsPath = Path.Combine(root, "decisions.csv");
        File.WriteAllText(decisionsPath, "line_id,surface_id,decision,corrected_text,notes\n");
        var indexPath = Path.Combine(root, "index.html");
        File.WriteAllText(indexPath, Html(root, project, rows.ToString(), lineCount, skipped));
        return new Result(indexPath, decisionsPath, lineCount, skipped, surfaces.Count);
    }

    private static string Html(string root, NativeProject project, string rows, int lineCount, int skipped) => $$"""
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Review packet — {{WebUtility.HtmlEncode(project.Name)}}</title>
<style>
body{font-family:system-ui,sans-serif;margin:1rem} .hint{color:#667;font-size:.8rem}
table{border-collapse:collapse;width:100%} th,td{border-bottom:1px solid #dde;padding:.35rem;vertical-align:top}
img{max-width:280px;max-height:90px;background:#fff} .text{font-size:1.15rem;max-width:22rem;white-space:pre-wrap}
.reference{color:#556;font-size:1rem} textarea{width:20rem;min-height:3rem;font-size:1rem} input{width:10rem}
button{padding:.45rem .8rem;font-size:1rem}
</style></head><body>
<h1>Review packet — {{WebUtility.HtmlEncode(project.Name)}}</h1>
<p class="hint">{{lineCount}} line(s), {{skipped}} skipped (no geometry/readable capture). Review the crop against the current L1 text; mark agree, correct (edit the text), unclear or not visible. Then download the decisions CSV and send it back. Nothing is changed in the project by this page.</p>
<p><button onclick="downloadCsv()">Download decisions.csv</button></p>
<table><thead><tr><th>crop</th><th>line</th><th>current L1</th><th>L2 reference</th><th>decision</th><th>corrected text</th><th>notes</th></tr></thead>
<tbody>{{rows}}</tbody></table>
<script>
function csv(v){ v = v ?? ''; return /[",\\n\\r]/.test(v) ? '"' + v.replace(/"/g,'""') + '"' : v; }
function downloadCsv(){
  const lines = ['line_id,surface_id,decision,corrected_text,notes'];
  for (const tr of document.querySelectorAll('tr[data-line]')) {
    lines.push([tr.dataset.line,tr.dataset.surface,
      tr.querySelector('.decision').value,
      tr.querySelector('.corrected').value,
      tr.querySelector('.notes').value].map(csv).join(','));
  }
  const blob = new Blob([lines.join('\\n') + '\\n'], {type:'text/csv'});
  const a = document.createElement('a'); a.href = URL.createObjectURL(blob);
  a.download = 'decisions.csv'; a.click(); URL.revokeObjectURL(a.href);
}
</script></body></html>
""";

    private static string SafeName(string value) =>
        new(value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
}
