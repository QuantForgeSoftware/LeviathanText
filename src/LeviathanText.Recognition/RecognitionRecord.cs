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
