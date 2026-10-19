using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Imaging;

/// <summary>
/// Line extraction from a page bitmap (todo Phase 1: extraction, with bounded resource
/// use). The C0 rule "line crops must retain diacritics, overbars and interlinear
/// additions" is implemented as mandatory vertical padding: the crop extends above and
/// below the line's text band by a configurable factor of the band height, and crop
/// bounds + information loss are recorded (todo §4).
/// </summary>
public static class LineCropper
{
    public sealed record CropParameters
    {
        /// <summary>Vertical padding above/below the line band, as a fraction of band height (default 0.5).</summary>
        public double VerticalPaddingFraction { get; init; } = 0.5;

        /// <summary>Horizontal margin in pixels (default 8).</summary>
        public int HorizontalMarginPx { get; init; } = 8;

        public int TargetHeightPx { get; init; } = 64;
    }

    public sealed record ExtractedLine(
        SKBitmap Bitmap,
        ImageTransform.Crop AppliedCrop,
        Checksum DerivativeChecksum,
        string Losses);

    /// <summary>Extract a line given its polygon/baseline points in the page frame.</summary>
    public static ExtractedLine Extract(SKBitmap page, IReadOnlyList<SKPoint> linePoints, CropParameters? parameters = null)
    {
        parameters ??= new CropParameters();
        if (linePoints.Count == 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "extraction",
                Message = "Cannot extract a line with no geometry: represent absent geometry explicitly instead.",
            });

        float minX = linePoints.Min(p => p.X), maxX = linePoints.Max(p => p.X);
        float minY = linePoints.Min(p => p.Y), maxY = linePoints.Max(p => p.Y);
        float bandHeight = Math.Max(maxY - minY, 1);

        // Mandatory padding: marks and interlinear additions above/below the text band.
        float pad = (float)(bandHeight * parameters.VerticalPaddingFraction);
        int left = Math.Max(0, (int)Math.Floor(minX - parameters.HorizontalMarginPx));
        int top = Math.Max(0, (int)Math.Floor(minY - pad));
        int width = Math.Min(page.Width - left, (int)Math.Ceiling(maxX - minX + 2 * parameters.HorizontalMarginPx));
        int height = Math.Min(page.Height - top, (int)Math.Ceiling(bandHeight + 2 * pad));

        var crop = new ImageTransform.Crop(left, top, width, height);
        var derivative = DerivativeFactory.Apply(page, [crop]);
        var losses = width < Math.Ceiling(maxX - minX + 2 * parameters.HorizontalMarginPx) ? "horizontal crop truncated at page edge; edge marks may be lost" : "";

        return new ExtractedLine(derivative.Bitmap, crop, ChecksumOfBitmap(derivative.Bitmap), losses);
    }

    public static Checksum ChecksumOfBitmap(SKBitmap bitmap)
    {
        var bytes = bitmap.Bytes;
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return new Checksum { Algorithm = "sha256", Digest = Convert.ToHexStringLower(hash) };
    }
}
