using LeviathanText.Contracts;
using LeviathanText.Core.Unicode;

namespace LeviathanText.Core.Tests;

public class NfcMapperTests
{
    private const string DecomposedAnthropos = "\u03B1\u0313\u0301\u03BD\u03B8\u03C1\u03C9\u03C0\u03BF\u03C2"; // α+psili+acute ν θ ρ ω π ο ς
    private const string PrecomposedAnthropos = "\u1F04\u03BD\u03B8\u03C1\u03C9\u03C0\u03BF\u03C2"; // ἄνθρωπος
    private const string BreathingPlusAcute = "\u03B1\u0313\u0301"; // α + psili + acute
    private const string AcutePlusBreathing = "\u03B1\u0301\u0313"; // wrong-order input

    [Fact]
    public void Decomposed_input_normalizes_to_precomposed()
    {
        var mapping = NfcMapper.Map(DecomposedAnthropos);
        Assert.Equal(PrecomposedAnthropos, mapping.Normalized);
        Assert.Equal(10, mapping.SourceScalarLength);  // α + psili + acute + 7 letters
        Assert.Equal(8, mapping.TargetScalarLength);   // ἄ + 7 letters
    }

    [Fact]
    public void Per_cluster_normalization_equals_whole_string_nfc_for_greek_sample()
    {
        string[] samples =
        [
            DecomposedAnthropos,
            BreathingPlusAcute,
            AcutePlusBreathing,
            "\u03C4\u03B7\u0342\u03C2 \u03C7\u03B1\u0301\u03C1\u03B9\u03C4\u03BF\u03C2",
            "\u039A\u0305\u03F2\u0305",
            "ΕΑΝΤΙϹΑΛΛΟΓΡΑΦΗ",
            "\u03B1\u0305\u03BB\u0305\u03B1\u0305\u03BD\u03B8\u03C1\u03C9\u03C0",
            "plain ascii",
            "",
        ];
        foreach (var sample in samples)
            Assert.Equal(sample.Normalize(System.Text.NormalizationForm.FormC), NfcMapper.Map(sample).Normalized);
    }

    [Fact]
    public void Already_normalized_text_maps_identity()
    {
        var mapping = NfcMapper.Map(PrecomposedAnthropos);
        Assert.Equal(PrecomposedAnthropos, mapping.Normalized);
        Assert.Equal(Enumerable.Range(0, 9), mapping.SourceToTarget); // 8 scalars + end boundary
    }

    [Fact]
    public void Annotation_after_composition_region_maps_shifted()
    {
        // "άνθρωπ": annotate νθρωπ (source scalars 2..7) — after NFC the alpha
        // composition shifts everything left by one scalar.
        var text = "\u03B1\u0301\u03BD\u03B8\u03C1\u03C9\u03C0"; // α+́ ν θ ρ ω π
        var mapping = NfcMapper.Map(text);
        var mapped = mapping.MapRange(new ScalarRange(2, 7));
        Assert.Equal(new ScalarRange(1, 6), mapped);
        // the mapped range slices the same visible text
        Assert.Equal("νθρωπ", ScalarText.Slice(mapping.Normalized, mapped.Start, mapped.End));
    }

    [Fact]
    public void Range_splitting_a_composed_cluster_fails_loudly()
    {
        // Annotate only the combining acute (scalar 1) of α+́ — under NFC it is absorbed
        // into ά, so the annotation cannot be represented without detaching.
        var mapping = NfcMapper.Map("\u03B1\u0301");
        var ex = Assert.Throws<LeviathanException>(() => mapping.MapRange(new ScalarRange(1, 2)));
        Assert.Equal(ErrorCode.StaleAnnotationAnchor, ex.Error.Code);
        Assert.Equal(RemedyHint.RebaseRevision, ex.Error.RemedyHint);
    }

    [Fact]
    public void Reordered_marks_compose_the_same_but_shift_counts()
    {
        // NFC(α + acute + psili): the acute composes to ά; psili has no composition
        // pair with ά, so the target has 2 scalars — fewer than the 3 source scalars.
        var mapping = NfcMapper.Map(AcutePlusBreathing);
        Assert.Equal(2, mapping.TargetScalarLength);

        // The standalone psili (source scalar 2) maps to target scalar 1.
        Assert.Equal(new ScalarRange(1, 2), mapping.MapRange(new ScalarRange(2, 3)));

        // A range covering the composed alpha+acute cannot survive normalization
        // (its contents merged) — it must fail loudly, not detach silently.
        Assert.Throws<LeviathanException>(() => mapping.MapRange(new ScalarRange(1, 3)));
    }
}
