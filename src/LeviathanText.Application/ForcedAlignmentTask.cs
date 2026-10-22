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
    IReadOnlyList<PointD> Baseline,
    IReadOnlyList<AlignedGrapheme> Graphemes)
{
    public BaselineOcrRecord ToBaselineOcrRecord()
    {
        var left = Box.Count >= 4 ? Box[0].X : 0;
        var right = Box.Count >= 4 ? Box[2].X : 0;
        var top = Box.Count >= 4 ? Box[0].Y : 0;
        var bottom = Box.Count >= 4 ? Box[2].Y : 0;
        var cuts = new List<BaselineCut>(Graphemes.Count);
        for (int i = 0; i < Graphemes.Count; i++)
        {
            double x0 = left + (right - left) * i / Graphemes.Count;
            double x1 = left + (right - left) * (i + 1) / Graphemes.Count;
            cuts.Add(new BaselineCut(Graphemes[i].Text,
                [new PointD(x0, top), new PointD(x1, top), new PointD(x1, bottom), new PointD(x0, bottom)],
                Graphemes[i].Confidence));
        }
        return new BaselineOcrRecord(LineId, RegionId, Text, Baseline, Box, cuts);
    }
}

/// <summary>One aligned grapheme cut; boxes are proportional approximations within the line box.</summary>
public sealed record BaselineCut(string Text, IReadOnlyList<PointD> Box, Confidence Confidence);

/// <summary>Baseline-OCR-shaped serialization of an aligned line.</summary>
public sealed record BaselineOcrRecord(
    EntityId LineId,
    EntityId RegionId,
    string Prediction,
    IReadOnlyList<PointD> Baseline,
    IReadOnlyList<PointD> Boundary,
    IReadOnlyList<BaselineCut> Cuts);

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
        var baseline = line.Baseline.Count >= 2 ? line.Baseline : [new PointD(left, bottom), new PointD(right, bottom)];
        return new ForcedAlignmentRecord(line.Id, line.RegionId, result.Text, box, baseline, result.Graphemes);
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
