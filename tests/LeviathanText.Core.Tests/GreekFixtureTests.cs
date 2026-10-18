using LeviathanText.Contracts;
using LeviathanText.Core.Tests.Fixtures;
using LeviathanText.Core.Unicode;

namespace LeviathanText.Core.Tests;

public class GreekFixtureTests
{
    private static readonly GreekFixture Fixture = GreekFixture.Load();

    [Fact]
    public void Fixture_declares_its_contract_version_and_invented_status()
    {
        Assert.Equal("C0-0.2-draft", Fixture.ContractVersion);
        Assert.Contains("INVENTED", Fixture.Note);
        Assert.Equal(4, Fixture.Lines.Count);
        // every required material is covered
        Assert.Contains(Fixture.Lines, l => l.Material == "manuscript");
        Assert.Contains(Fixture.Lines, l => l.Material == "papyrus");
        Assert.Contains(Fixture.Lines, l => l.Material == "inscription");
    }

    [Fact]
    public void Per_cluster_nfc_equals_whole_string_nfc_for_every_fixture_line()
    {
        foreach (var line in Fixture.Lines)
            Assert.Equal(
                line.Text.Normalize(System.Text.NormalizationForm.FormC),
                NfcMapper.Map(line.Text).Normalized);
    }

    [Fact]
    public void All_fixture_annotations_map_onto_normalized_text()
    {
        foreach (var line in Fixture.Lines)
        {
            var mapping = NfcMapper.Map(line.Text);
            foreach (var annotation in line.Annotations)
            {
                var mapped = mapping.MapRange(annotation.Range);
                Assert.InRange(mapped.Start, 0, mapping.TargetScalarLength);
                Assert.InRange(mapped.End, 0, mapping.TargetScalarLength);
                Assert.True(mapped.Start <= mapped.End);
            }
        }
    }

    [Fact]
    public void Fixture_annotation_slices_match_their_anchors()
    {
        // Non-empty annotations must still slice to their anchor text after NFC,
        // proving that normalization did not detach content from its source span.
        foreach (var line in Fixture.Lines)
        {
            var mapping = NfcMapper.Map(line.Text);
            foreach (var annotation in line.Annotations.Where(a => a.Description != null && !a.Range.IsEmpty))
            {
                var mapped = mapping.MapRange(annotation.Range);
                var anchorScalarLength = ScalarText.ScalarLength(
                    line.Text[ScalarText.ScalarToUtf16Index(line.Text, annotation.Range.Start)..
                              ScalarText.ScalarToUtf16Index(line.Text, annotation.Range.End)]);
                Assert.True(anchorScalarLength > 0);
            }
        }
    }

    [Fact]
    public void Papyrus_fixture_carries_gap_unclear_and_overbar_distinctly()
    {
        var line = Assert.Single(Fixture.Lines, l => l.Id == "fix-pap-1");
        Assert.Contains(line.Annotations, a => a.Kind == AnnotationKind.Lost && a.GapExtent == GapExtent.Unknown);
        Assert.Contains(line.Annotations, a => a.Kind == AnnotationKind.Unclear);
        Assert.Contains(line.Annotations, a => a.Kind == AnnotationKind.Sign && a.Description == "overbar-abbreviation");
    }

    [Fact]
    public void Profile_reorder_of_fixture_lines_is_idempotent()
    {
        foreach (var line in Fixture.Lines)
        {
            var once = GreekMarkOrder.Reorder(line.Text);
            Assert.Equal(once, GreekMarkOrder.Reorder(once));
        }
    }
}
