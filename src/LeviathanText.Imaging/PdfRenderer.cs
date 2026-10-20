using LeviathanText.Contracts;
using PDFtoImage;
using SkiaSharp;

namespace LeviathanText.Imaging;

/// <summary>
/// Bounded PDF page rasterization for ingestion (Phase 4). PDFtoImage/PDFium is used only
/// to render pages to PNG; no PDF text extraction or layout interpretation is claimed.
/// Pages are rendered sequentially because PDFium is not thread-safe. The original PDF is
/// not copied into the project store; rendered derivatives are immutable captures.
/// </summary>
public static class PdfRenderer
{
    public const long MaxEncodedBytes = 200L * 1024 * 1024;
    public const int MaxPages = 500;
    public const int DefaultDpi = 200;
    public const int MaxDimension = 12_000;
    public const long MaxPixels = 50_000_000;

    public sealed record RenderedPage(int PageNumber, string ImagePath, int WidthPx, int HeightPx, Checksum Checksum);

    // PDFtoImage declares platform support but its analyzer only recognizes a platform
    // attribute; the runtime guard below is the effective check for this application.
#pragma warning disable CA1416
    public static IReadOnlyList<RenderedPage> Render(
        string pdfPath, string outputDirectory, int dpi = DefaultDpi, int maxPages = MaxPages)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = pdfPath,
                Message = "PDF file is not readable on this host.",
            });
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("An output directory is required.", nameof(outputDirectory));

        var file = new FileInfo(pdfPath);
        if (file.Length > MaxEncodedBytes)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = pdfPath,
                Details = new Dictionary<string, string>
                {
                    ["bytes"] = file.Length.ToString(),
                    ["max"] = MaxEncodedBytes.ToString(),
                },
                Message = "PDF exceeds the configured size limit.",
            });

        dpi = Math.Clamp(dpi, 72, 600);
        maxPages = Math.Clamp(maxPages, 1, MaxPages);
        var bytes = File.ReadAllBytes(pdfPath);

        try
        {
            var sizes = Conversion.GetPageSizes(bytes, password: null);
            if (sizes.Count == 0)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.RejectedArchive,
                    Scope = pdfPath,
                    Message = "PDF contains no pages.",
                });

            int count = Math.Min(sizes.Count, maxPages);
            for (int i = 0; i < count; i++)
            {
                var width = (long)Math.Ceiling(sizes[i].Width * dpi / 72.0);
                var height = (long)Math.Ceiling(sizes[i].Height * dpi / 72.0);
                if (width > MaxDimension || height > MaxDimension || width * height > MaxPixels)
                    throw new LeviathanException(new LeviathanError
                    {
                        Code = ErrorCode.RejectedArchive,
                        Scope = $"{pdfPath}#page-{i + 1}",
                        Details = new Dictionary<string, string>
                        {
                            ["width"] = width.ToString(),
                            ["height"] = height.ToString(),
                            ["maxDimension"] = MaxDimension.ToString(),
                            ["maxPixels"] = MaxPixels.ToString(),
                        },
                        Message = "Rendered PDF page would exceed the configured pixel/dimension limit.",
                    });
            }

            Directory.CreateDirectory(outputDirectory);
            var stem = SafeStem(Path.GetFileNameWithoutExtension(pdfPath));
            var pages = new List<RenderedPage>(count);

            for (int page = 0; page < count; page++)
            {
                using var bitmap = Conversion.ToImage(bytes, page, password: null, new RenderOptions(Dpi: dpi));
                using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 90)
                    ?? throw new LeviathanException(new LeviathanError
                    {
                        Code = ErrorCode.RejectedArchive,
                        Scope = $"{pdfPath}#page-{page + 1}",
                        Message = "Rendered PDF page could not be encoded as PNG.",
                    });

                var imagePath = Path.Combine(outputDirectory, $"{stem}-page-{page + 1:0000}.png");
                var imageBytes = encoded.ToArray();
                File.WriteAllBytes(imagePath, imageBytes);
                pages.Add(new RenderedPage(page + 1, imagePath, bitmap.Width, bitmap.Height,
                    ImageDecoder.ChecksumOf(imageBytes)));
            }

            return pages;
        }
        catch (LeviathanException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RejectedArchive,
                Scope = pdfPath,
                Details = new Dictionary<string, string> { ["reason"] = ex.GetType().Name },
                Message = "PDF could not be rendered; it may be encrypted, malformed or unsupported.",
            });
        }
    }
#pragma warning restore CA1416

    private static string SafeStem(string stem)
    {
        var chars = stem.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        var safe = new string(chars).Trim('-');
        return string.IsNullOrEmpty(safe) ? "pdf" : safe[..Math.Min(48, safe.Length)];
    }
}
