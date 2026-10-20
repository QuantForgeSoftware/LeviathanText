using LeviathanText.Contracts;
using SkiaSharp;

namespace LeviathanText.Imaging;

/// <summary>
/// Line extraction from a page bitmap (todo Phase 1: extraction, with bounded resource
/// use). The C0 rule "line crops must retain diacritics, overbars and interlinear
/// additions" is implemented as mandatory vertical padding: the crop extends above and
/// below the line's text band by a configurable factor of the band height, and crop
/// bounds + information loss are recorded (project-plan §4).
/// </summary>
public static class LineCropper
{
    public sealed record CropParameters
    {
        /// <summary>
        /// Vertical padding above/below the line band, as a fraction of band height.
        /// Default 0.25 with a hard cap: large padding makes near-square crops that
        /// starve the CTC decoder of frames (measured: docs/evaluation/decoding-comparison.md —
        /// 50/50 P01 dev lines had T &lt; reference scalars under 0.5 padding).
        /// </summary>
        public double VerticalPaddingFraction { get; init; } = 0.25;

        /// <summary>Hard cap on added padding above+below, in source pixels (default 12).</summary>
        public int MaxVerticalPaddingPx { get; init; } = 12;

        /// <summary>Horizontal margin in pixels (default 8).</summary>
        public int HorizontalMarginPx { get; init; } = 8;

        public int TargetHeightPx { get; init; } = 64;
    }

    public sealed record ExtractedLine(
        SKBitmap Bitmap,
        ImageTransform.Crop AppliedCrop,
        Checksum DerivativeChecksum,
        string Losses);

    public sealed record CropPlan(int Left, int Top, int Width, int Height, string Losses);

    /// <summary>Compute the same padded crop rectangle used by full-page extraction.</summary>
    public static CropPlan PlanCrop(int pageWidth, int pageHeight,
        IReadOnlyList<SKPoint> linePoints, CropParameters? parameters = null)
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

        // Mandatory padding: marks and interlinear additions above/below the text band,
        // capped so the crop stays line-shaped (aspect ratio protects the CTC frame budget).
        float pad = Math.Min(
            (float)(bandHeight * parameters.VerticalPaddingFraction),
            parameters.MaxVerticalPaddingPx / 2f);
        int left = Math.Max(0, (int)Math.Floor(minX - parameters.HorizontalMarginPx));
        int top = Math.Max(0, (int)Math.Floor(minY - pad));
        int width = Math.Min(pageWidth - left, (int)Math.Ceiling(maxX - minX + 2 * parameters.HorizontalMarginPx));
        int height = Math.Min(pageHeight - top, (int)Math.Ceiling(bandHeight + 2 * pad));
        var losses = width < Math.Ceiling(maxX - minX + 2 * parameters.HorizontalMarginPx)
            ? "horizontal crop truncated at page edge; edge marks may be lost"
            : "";
        return new CropPlan(left, top, Math.Max(1, width), Math.Max(1, height), losses);
    }

    /// <summary>Extract a line given its polygon/baseline points in the page frame.</summary>
    public static ExtractedLine Extract(SKBitmap page, IReadOnlyList<SKPoint> linePoints, CropParameters? parameters = null)
    {
        var plan = PlanCrop(page.Width, page.Height, linePoints, parameters);
        var crop = new ImageTransform.Crop(plan.Left, plan.Top, plan.Width, plan.Height);
        var derivative = DerivativeFactory.Apply(page, [crop]);
        return new ExtractedLine(derivative.Bitmap, crop, ChecksumOfBitmap(derivative.Bitmap), plan.Losses);
    }

    /// <summary>
    /// Extract a line from a horizontal source band, avoiding a full-page decode. JPEG
    /// scanline decoding is used where available; other formats fall back to a full decode.
    /// The returned crop shares page-frame coordinates in <see cref="ExtractedLine.AppliedCrop"/>.
    /// </summary>
    public static ExtractedLine ExtractBanded(string imagePath, IReadOnlyList<SKPoint> linePoints,
        CropParameters? parameters = null)
    {
        var (width, height) = ImageDecoder.Dimensions(imagePath);
        var plan = PlanCrop(width, height, linePoints, parameters);
        using var band = ImageDecoder.DecodeBand(imagePath, plan.Top, plan.Height);
        var derivative = DerivativeFactory.Apply(band, [new ImageTransform.Crop(plan.Left, 0, plan.Width, plan.Height)]);
        return new ExtractedLine(derivative.Bitmap,
            new ImageTransform.Crop(plan.Left, plan.Top, plan.Width, plan.Height),
            ChecksumOfBitmap(derivative.Bitmap), plan.Losses);
    }

    public static Checksum ChecksumOfBitmap(SKBitmap bitmap)
    {
        var bytes = bitmap.Bytes;
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return new Checksum { Algorithm = "sha256", Digest = Convert.ToHexStringLower(hash) };
    }
}