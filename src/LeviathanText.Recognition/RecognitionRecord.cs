using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;

namespace LeviathanText.Recognition;

/// <summary>
/// Recognized line text with an explicit logical/display order. The RTL conversion is a
/// bounded grapheme-reversal helper for records; it is not a Unicode BiDi implementation
/// and does not reorder mixed-direction text. Direction is supplied by the caller, never
/// inferred from the language.
/// </summary>
public sealed record RecognitionRecord(
    string Id,
    string Text,
    IReadOnlyList<GraphemeConfidence> Graphemes,
    bool Rtl = false,
    bool IsDisplayOrder = false)
{
    /// <summary>Source region, when the record came from a region-scoped line.</summary>
    public EntityId? RegionId { get; init; }

    /// <summary>Approximate line box; cuts are subdivided across graphemes.</summary>
    public (double Left, double Top, double Right, double Bottom)? Bbox { get; init; }

    /// <summary>Source baseline points, when available.</summary>
    public IReadOnlyList<(double X, double Y)> Baseline { get; init; } = [];

    public RecognitionSlice Slice(int start, int end, int step = 1)
    {
        if (step < 1) throw new ArgumentOutOfRangeException(nameof(step));
        var texts = LeviathanText.Core.Unicode.Graphemes.Enumerate(Text).Select(g => g.Text).ToArray();
        if (start < 0 || end > texts.Length || start >= end)
            throw new ArgumentOutOfRangeException(nameof(start), "Slice range is outside the record.");
        var selected = new List<int>();
        for (int i = start; i < end; i += step) selected.Add(i);
        if (selected.Count == 0) throw new ArgumentException("Slice selects no graphemes.", nameof(start));

        var selectedTexts = selected.Select(i => texts[i]).ToArray();
        var confidences = selected.Where(i => i < Graphemes.Count).Select(i => Graphemes[i].Confidence).ToList();
        var confidence = confidences.Count == 0 || confidences.All(c => c.IsNone)
            ? Confidence.None
            : Confidence.Of(confidences.Where(c => !c.IsNone).Average(c => c.Value), ConfidenceScale.RawModelProbability);
        var bbox = Bbox is { } box
            ? (
                box.Left + (box.Right - box.Left) * start / texts.Length,
                box.Top,
                box.Left + (box.Right - box.Left) * end / texts.Length,
                box.Bottom)
            : ((double Left, double Top, double Right, double Bottom)?)null;
        return new RecognitionSlice(string.Concat(selectedTexts), bbox, confidence);
    }

    public RecognitionRecord LogicalOrder() =>
        !Rtl || !IsDisplayOrder ? this : Reordered(displayOrder: false);

    public RecognitionRecord DisplayOrder() =>
        !Rtl || IsDisplayOrder ? this : Reordered(displayOrder: true);

    private RecognitionRecord Reordered(bool displayOrder)
    {
        var graphemeTexts = LeviathanText.Core.Unicode.Graphemes.Enumerate(Text)
            .Reverse().Select(g => g.Text).ToArray();
        var confidences = Graphemes.Reverse().ToArray();
        if (graphemeTexts.Length != confidences.Length)
            throw new InvalidOperationException("Recognition text and confidence spans do not contain the same grapheme count.");

        int cursor = 0;
        var rebased = new List<GraphemeConfidence>(confidences.Length);
        for (int i = 0; i < graphemeTexts.Length; i++)
        {
            int length = ScalarText.ScalarLength(graphemeTexts[i]);
            rebased.Add(confidences[i] with { StartScalar = cursor, EndScalar = cursor + length });
            cursor += length;
        }
        return this with
        {
            Text = string.Concat(graphemeTexts),
            Graphemes = rebased,
            IsDisplayOrder = displayOrder,
        };
    }
}


/// <summary>A bounded slice of a recognition record, with an approximate box and mean confidence.</summary>
public sealed record RecognitionSlice(
    string Text,
    (double Left, double Top, double Right, double Bottom)? Bbox,
    Confidence Confidence);
