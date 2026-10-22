using LeviathanText.Contracts;
using LeviathanText.Imaging;
using SkiaSharp;

namespace LeviathanText.Layout;

/// <summary>A page-segmentation result in the image's source coordinate frame.</summary>
public sealed record PageSegmentationResult(
    string ImageName,
    string Type,
    IReadOnlyList<DetectedLine> Lines,
    string Method,
    string ParametersHash);

/// <summary>
/// Page-segmentation contract for bi-level page images. Color/grayscale input is rejected
/// explicitly (callers can binarize first); this avoids silently applying a threshold that
/// changes the evidence. The detector itself is the bounded projection baseline, so the
/// returned boxes are line bands, not word boxes or scholarly region claims.
/// </summary>
public static class PageSegmentation
{
    public static PageSegmentationResult Segment(SKBitmap page, string imageName,
        ProjectionProfileLayout.Options? options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageName);
        if (!Binarization.IsBlackWhite(page))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = "page-segmentation",
                Message = "Page segmentation requires a black/white image; binarize the page first.",
            });

        var result = ProjectionProfileLayout.Detect(page, options);
        return new PageSegmentationResult(imageName, "bbox", result.Lines, result.Method, result.ParametersHash);
    }
}
