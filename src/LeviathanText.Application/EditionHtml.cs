using System.Net;
using System.Text;
using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using SkiaSharp;

namespace LeviathanText.Application;

/// <summary>
/// Bounded presentation export for a single surface: diplomatic text, the linked editorial
/// reading and a line-by-line critical apparatus rendered as HTML. It is a reading/printing
/// aid, not a scholarly interchange format; native/TEI records remain authoritative.
/// </summary>
public static class EditionHtml
{
    // Bounds apply independently to encoded input and embedded PNG bytes. Project exports
    // share both budgets; local previews are limited to four million pixels.
    private const long MaxImageBytes = 25L * 1024 * 1024;
    private const long MaxProjectImageBytes = 250L * 1024 * 1024;
    private const long MaxPreviewPixels = 4_000_000;

    public sealed record Result(string Html, LossReport Report);

    /// <summary>
    /// Local captures become bounded PNG previews; HTTP(S) references are retained without
    /// fetching. A non-null image override replaces that behavior; an empty override omits it.
    /// </summary>
    public static Result Export(NativeProject project, EntityId surfaceId, string title, string? imageSource = null)
        => ExportSurface(project, surfaceId, title, imageSource, new ImageBudget());

    private static Result ExportSurface(NativeProject project, EntityId surfaceId, string title,
        string? imageSource, ImageBudget imageBudget)
    {
        var surface = project.Objects.SelectMany(o => o.Surfaces).FirstOrDefault(s => s.Id == surfaceId)
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = surfaceId.ToString(),
                Message = "Surface not found in the project.",
            });
        var capture = surface.Captures.FirstOrDefault();
        var revisions = surface.Revisions.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First());
        var annotations = surface.Annotations
            .Where(a => !surface.AnnotationRetractions.Any(r => r.AnnotationId == a.Id))
            .ToList();

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"grc\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine($"<title>{Encode(title)}</title>");
        html.AppendLine("<style>");
        html.AppendLine("body{font-family:Georgia,serif;max-width:62rem;margin:2rem auto;line-height:1.55;color:#222}");
        html.AppendLine(".surface{display:grid;grid-template-columns:minmax(0,1fr) minmax(18rem,24rem);gap:2rem;align-items:start}");
        html.AppendLine("figure{margin:0}img{max-width:100%;height:auto;border:1px solid #bbb}");
        html.AppendLine("ol.lines{padding-left:0;list-style:none}.line{margin:0 0 1.2rem 0;padding-bottom:.8rem;border-bottom:1px solid #ddd}");
        html.AppendLine(".ln{color:#667;font-size:.8rem;margin-right:.5rem}.greek{font-size:1.25rem}.editorial{display:block;color:#345;margin-top:.25rem}");
        html.AppendLine(".app{font-size:.82rem;color:#444;margin:.35rem 0 0 1.2rem}.app li{margin:.1rem 0}.kind{font-weight:bold}");
        html.AppendLine("@media print{body{margin:0}.surface{grid-template-columns:1fr}figure{page-break-after:always}}");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine($"<h1>{Encode(title)}</h1>");
        html.AppendLine("<div class=\"surface\"><figure>");
        var image = PresentImage(capture, imageSource, imageBudget);
        if (image.Source is not null)
            html.AppendLine($"<img src=\"{Encode(image.Source)}\" alt=\"source capture\">");
        else
            html.AppendLine("<p class=\"editorial\">No capture attached.</p>");
        html.AppendLine("</figure><main><ol class=\"lines\">");

        int unplaced = 0;
        int apparatusEntries = 0;
        var regionOrder = surface.Regions
            .GroupBy(r => r.Id)
            .ToDictionary(g => g.Key, g => g.First().ReadingOrderIndex ?? int.MaxValue);
        foreach (var line in surface.Lines
                     .OrderBy(l => regionOrder.GetValueOrDefault(l.RegionId, int.MaxValue))
                     .ThenBy(l => l.LineNumber ?? int.MaxValue)
                     .ThenBy(l => l.Id.ToString(), StringComparer.Ordinal))
        {
            var lineRevisions = revisions.Values.Where(r => r.ScopeId == line.Id).ToList();
            var l1 = line.DiplomaticRevisionId is { } pointer && revisions.TryGetValue(pointer, out var pointed)
                ? pointed
                : lineRevisions.Where(r => r.Layer == TextLayer.L1Diplomatic)
                    .OrderByDescending(r => r.CreatedAt).FirstOrDefault();
            var l2 = lineRevisions.Where(r => r.Layer == TextLayer.L2Editorial)
                .OrderByDescending(r => r.CreatedAt).FirstOrDefault();
            if (l1 is null)
            {
                unplaced++;
                continue;
            }
            var lineAnnotations = annotations
                .Where(a => a.AnchoredRevisionId == l1.Id || (l2 is not null && a.AnchoredRevisionId == l2.Id))
                .ToList();
            apparatusEntries += lineAnnotations.Count;
            html.AppendLine("<li class=\"line\">");
            html.AppendLine($"<span class=\"ln\">{(line.LineNumber is { } n ? n.ToString() : "·")}</span>");
            var regionDirection = surface.Regions
                .FirstOrDefault(r => r.Id == line.RegionId)?.Direction;
            var direction = (line.Direction ?? regionDirection) == TextDirection.Rtl
                ? " dir=\"rtl\"" : "";
            html.AppendLine($"<span class=\"greek\"{direction}>{Encode(l1.Text)}</span>");
            if (l2 is not null)
                html.AppendLine($"<span class=\"editorial\"{direction}>{Encode(l2.Text)}</span>");
            if (lineAnnotations.Count > 0)
            {
                html.AppendLine("<ul class=\"app\">");
                foreach (var annotation in lineAnnotations
                             .OrderBy(a => a.Range.Start).ThenBy(a => a.Kind))
                    html.AppendLine($"<li><span class=\"kind\">{Encode(annotation.Kind.ToString())}</span> " +
                                    $"[{annotation.Range.Start}, {annotation.Range.End}) — {Encode(Apparatus(annotation))}</li>");
                html.AppendLine("</ul>");
            }
            html.AppendLine("</li>");
        }
        html.AppendLine("</ol></main></div>");
        html.AppendLine("</body></html>");

        var entries = new List<LossEntry>
        {
            new("presentation-only", 1,
                "HTML edition is a reading/printing aid; use TEI/native JSON for interchange"),
            new("unplaced-lines", unplaced,
                "lines without a diplomatic revision are omitted"),
            new("apparatus-entries", apparatusEntries,
                "apparatus text is escaped plain text; structured links remain in the native/TEI records"),
            new("image-omitted", image.Omission is null ? 0 : 1, image.Omission ?? ""),
            new("image-preview", image.Scaled ? 1 : 0,
                "capture was downsampled to a bounded preview; original image remains authoritative"),
        };
        var report = new LossReport
        {
            Format = "edition-html",
            ContractVersion = ContractVersion.Current,
            Entries = [.. entries.Where(e => e.Count > 0)],
        };
        return new Result(html.ToString(), report);
    }

    /// <summary>Render every surface as one printable edition document.</summary>
    public static Result ExportProject(NativeProject project, string title, Func<NativeSurface, string?>? imageSource = null)
    {
        var sections = new List<Result>();
        var imageBudget = new ImageBudget();
        foreach (var surface in project.Objects.SelectMany(o => o.Surfaces))
            sections.Add(ExportSurface(project, surface.Id, $"{title} — {surface.Id}",
                imageSource?.Invoke(surface), imageBudget));
        if (sections.Count == 0)
        {
            var empty = new LossReport
            {
                Format = "edition-html",
                ContractVersion = ContractVersion.Current,
                Entries = [new LossEntry("presentation-only", 1,
                    "HTML edition is a reading/printing aid; no surfaces were present")],
            };
            return new Result($"<!doctype html><html><head><meta charset=\"utf-8\"><title>{Encode(title)}</title></head>" +
                              $"<body><h1>{Encode(title)}</h1><p>No surfaces in this project.</p></body></html>", empty);
        }
        var prefix = sections[0].Html[..(sections[0].Html.IndexOf("<body>", StringComparison.Ordinal) + 6)];
        var suffix = sections[0].Html[sections[0].Html.LastIndexOf("</body>", StringComparison.Ordinal)..];
        var body = new StringBuilder();
        foreach (var section in sections)
        {
            var start = section.Html.IndexOf("<body>", StringComparison.Ordinal) + 6;
            var end = section.Html.LastIndexOf("</body>", StringComparison.Ordinal);
            body.AppendLine("<section class=\"surface-section\">");
            body.AppendLine(section.Html[start..end]);
            body.AppendLine("</section>");
        }
        var entries = sections.SelectMany(s => s.Report.Entries)
            .GroupBy(e => (e.Category, e.Description))
            .Select(g => new LossEntry(g.Key.Category, g.Sum(e => e.Count), g.Key.Description))
            .ToList();
        return new Result(prefix + body.ToString() + suffix, new LossReport
        {
            Format = "edition-html",
            ContractVersion = ContractVersion.Current,
            Entries = entries,
        });
    }

    private static string Apparatus(Annotation annotation) => annotation.Kind switch
    {
        AnnotationKind.AlternativeReading or AnnotationKind.Translation or AnnotationKind.StructuredExtraction
            or AnnotationKind.LexicalAnnotation or AnnotationKind.MorphologicalAnnotation
            => annotation.AlternativeText ?? annotation.Description ?? "",
        AnnotationKind.Abbreviation => annotation.ExpansionText ?? annotation.Description ?? "",
        AnnotationKind.DifferentHand => annotation.HandLabel ?? annotation.Description ?? "",
        _ => annotation.Description ?? annotation.HandLabel ?? "",
    };

    private sealed class ImageBudget
    {
        public long InputBytes { get; set; }
        public long EmbeddedBytes { get; set; }
    }

    private sealed record ImagePresentation(string? Source, string? Omission = null, bool Scaled = false);

    private static ImagePresentation PresentImage(NativeCapture? capture, string? imageSource, ImageBudget budget)
    {
        if (capture is null) return new(null);
        if (imageSource is not null)
        {
            var source = SafeImageSource(imageSource);
            return new(source, source is null ? "capture omitted by export options or an unsupported image reference" : null);
        }
        if (Uri.TryCreate(capture.ImageLocation, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
            return new(capture.ImageLocation);

        var snapshotPath = Path.Combine(Path.GetTempPath(), $"lt-edition-preview-{Guid.NewGuid():N}");
        try
        {
            // Decode a bounded snapshot, so a changing source cannot bypass the byte limit
            // between a metadata check and the decoder reading the file again.
            using (var input = File.OpenRead(capture.ImageLocation))
            {
                if (input.Length > MaxImageBytes || input.Length > MaxProjectImageBytes - budget.InputBytes)
                    return new(null, "capture omitted by the 25 MiB image or 250 MiB project input limit");
                using var snapshot = new FileStream(snapshotPath, FileMode.CreateNew, FileAccess.Write);
                var buffer = new byte[81920];
                long copied = 0;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    copied += read;
                    budget.InputBytes += read;
                    if (copied > MaxImageBytes || budget.InputBytes > MaxProjectImageBytes)
                        return new(null, "capture omitted by the 25 MiB image or 250 MiB project input limit");
                    snapshot.Write(buffer, 0, read);
                }
            }

            var dimensions = ImageDecoder.Dimensions(snapshotPath);
            if (dimensions.Width > ImageDecoder.MaxDimension || dimensions.Height > ImageDecoder.MaxDimension)
                return new(null, "capture dimensions exceed the supported preview limit");
            using var decoded = ImageDecoder.DecodeBounded(snapshotPath, MaxPreviewPixels);
            // Some codecs round their supported scale up; enforce the presentation bound
            // after decoding as well, while preserving the original aspect ratio.
            var pixels = (long)decoded.Bitmap.Width * decoded.Bitmap.Height;
            var factor = Math.Min(1, Math.Sqrt((double)MaxPreviewPixels / pixels));
            using var resized = pixels > MaxPreviewPixels
                ? decoded.Bitmap.Resize(new SKImageInfo(
                    Math.Max(1, (int)(decoded.Bitmap.Width * factor)),
                    Math.Max(1, (int)(decoded.Bitmap.Height * factor))), new SKSamplingOptions(SKFilterMode.Linear))
                : null;
            if (pixels > MaxPreviewPixels && resized is null)
                return new(null, "capture could not be resized for the preview");
            using var encoded = (resized ?? decoded.Bitmap).Encode(SKEncodedImageFormat.Png, 100);
            if (encoded is null || encoded.Size > MaxImageBytes
                || encoded.Size > MaxProjectImageBytes - budget.EmbeddedBytes)
                return new(null, "capture omitted by the 25 MiB image or 250 MiB project PNG limit");
            budget.EmbeddedBytes += encoded.Size;
            return new("data:image/png;base64," + Convert.ToBase64String(encoded.ToArray()),
                Scaled: decoded.Bitmap.Width != decoded.OriginalWidth
                    || decoded.Bitmap.Height != decoded.OriginalHeight || resized is not null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or LeviathanException)
        {
            return new(null, "capture is unavailable, unreadable, or not a supported image");
        }
        finally
        {
            try { File.Delete(snapshotPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static string? SafeImageSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        var trimmed = source.TrimStart();
        return trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase)
            ? null : source;
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
