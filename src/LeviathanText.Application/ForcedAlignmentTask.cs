using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Recognition;

namespace LeviathanText.Application;

/// <summary>A forced alignment tied to the source line and region, with the geometry
/// needed to render cuts. Time steps are model steps, not source pixels.</summary>
public sealed record ForcedAlignmentRecord(
    EntityId LineId,
    EntityId RegionId,
    string Text,
    IReadOnlyList<PointD> Box,
    IReadOnlyList<AlignedGrapheme> Graphemes);

/// <summary>
/// Task-level wrapper over <see cref="CtcForcedAligner"/>. It does not choose a model or
/// crop the page; callers supply the already-extracted line image and its source geometry.
/// </summary>
public static class ForcedAlignmentTask
{
    public static ForcedAlignmentRecord Align(
        CrnnModel model,
        LabelSet labels,
        NativeLine line,
        LineImage image,
        string text)
    {
        ArgumentNullException.ThrowIfNull(line);
        var geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
        if (geometry.Count < 2)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = line.Id.ToString(),
                Message = "Forced alignment requires line geometry.",
            });

        var result = CtcForcedAligner.Align(model, labels, image, text);
        double left = geometry.Min(p => p.X), right = geometry.Max(p => p.X);
        double top = geometry.Min(p => p.Y), bottom = Math.Max(geometry.Max(p => p.Y), top + 1);
        var box = new[]
        {
            new PointD(left, top), new PointD(right, top),
            new PointD(right, bottom), new PointD(left, bottom),
        };
        return new ForcedAlignmentRecord(line.Id, line.RegionId, result.Text, box, result.Graphemes);
    }

    public static IReadOnlyList<ForcedAlignmentRecord> Align(
        CrnnModel model,
        LabelSet labels,
        IEnumerable<(NativeLine Line, LineImage Image, string Text)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return [.. lines.Select(entry => Align(model, labels, entry.Line, entry.Image, entry.Text))];
    }
}
