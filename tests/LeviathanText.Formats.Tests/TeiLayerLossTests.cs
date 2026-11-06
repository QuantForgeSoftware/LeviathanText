using LeviathanText.Contracts;

namespace LeviathanText.Formats.Tests;

public sealed class TeiLayerLossTests
{
    private static readonly EntityId Line = new(EntityKind.Line, "layer-check");
    private static TextRevision Revision(TextLayer layer, string text) => new()
    {
        Id = new(EntityKind.TranscriptionRevision, layer.ToString()), ScopeId = Line, Layer = layer,
        Text = text, Author = new AgentRef { Id = "fixture", IsModel = false }, CreatedAt = DateTimeOffset.UnixEpoch,
    };
    private static TeiXml.TeiExportResult Export(TextRevision? l1, TextRevision l2, params Annotation[] annotations) =>
        TeiXml.ExportDocument([new(Line, l1, l2, annotations)], "layer-proof",
            new TeiXml.TeiExportContext(Graphics: [new("generated.png")]));

    [Theory]
    [InlineData("αβ", "αγ")]
    [InlineData("αβ", "αΩβ")]
    [InlineData("αβγ", "αγ")]
    public void Unlinked_editorial_changes_cannot_be_reported_as_lossless(string visible, string editorial)
    {
        var result = Export(Revision(TextLayer.L1Diplomatic, visible), Revision(TextLayer.L2Editorial, editorial));
        Assert.Contains(result.Report.Entries, e => e.Category == "diplomatic-layer" && e.Count == 1);
        Assert.Contains(result.Report.Entries, e => e.Category == "editorial-layer" && e.Count == 1);
    }

    [Fact]
    public void A_partial_link_does_not_hide_an_unmapped_change_elsewhere_on_the_line()
    {
        var l1 = Revision(TextLayer.L1Diplomatic, "αβγδε");
        var l2 = Revision(TextLayer.L2Editorial, "αΩγδζ");
        var result = Export(l1, l2, new Annotation
        {
            Id = new(EntityKind.Annotation, "partial"), Kind = AnnotationKind.AlternativeReading,
            AnchoredRevisionId = l1.Id, Range = new(1, 2), AlternativeText = "Ω", Description = "choice/orig-reg",
        });
        Assert.Contains(result.Report.Entries, e => e.Category == "diplomatic-layer");
        Assert.DoesNotContain(result.Report.Entries, e => e.Category == "editorial-layer");
    }

    [Fact]
    public void Exact_source_link_preserves_both_layers_including_combining_scalars()
    {
        var l1 = Revision(TextLayer.L1Diplomatic, "α\u0313β");
        var l2 = Revision(TextLayer.L2Editorial, "α\u0313γ");
        var result = Export(l1, l2, new Annotation
        {
            Id = new(EntityKind.Annotation, "exact"), Kind = AnnotationKind.AlternativeReading,
            AnchoredRevisionId = l1.Id, Range = new(2, 3), AlternativeText = "γ", Description = "choice/orig-reg",
        });
        Assert.DoesNotContain(result.Report.Entries, e => e.Category is "diplomatic-layer" or "editorial-layer");
        var parsed = TeiXml.Import(result.Xml);
        Assert.Equal(l1.Text, Assert.Single(parsed.Revisions, r => r.Layer == TextLayer.L1Diplomatic).Text);
        Assert.Equal(l2.Text, Assert.Single(parsed.Revisions, r => r.Layer == TextLayer.L2Editorial).Text);
    }

    [Fact]
    public void Exact_supplied_insertion_preserves_visible_text_without_fabricated_reading()
    {
        var l1 = Revision(TextLayer.L1Diplomatic, "αβ");
        var l2 = Revision(TextLayer.L2Editorial, "αΩβ");
        var result = Export(l1, l2, new Annotation
        {
            Id = new(EntityKind.Annotation, "supplied"), Kind = AnnotationKind.SuppliedEditorial,
            AnchoredRevisionId = l2.Id, Range = new(1, 2), Description = "editorial restoration",
        });
        Assert.DoesNotContain(result.Report.Entries, e => e.Category is "diplomatic-layer" or "editorial-layer");
        var parsed = TeiXml.Import(result.Xml);
        Assert.Equal(l1.Text, Assert.Single(parsed.Revisions, r => r.Layer == TextLayer.L1Diplomatic).Text);
        Assert.Equal(l2.Text, Assert.Single(parsed.Revisions, r => r.Layer == TextLayer.L2Editorial).Text);
    }

    [Fact]
    public void Editorial_only_plain_text_cannot_silently_become_visible_evidence()
    {
        var result = Export(null, Revision(TextLayer.L2Editorial, "αβ"));
        Assert.Contains(result.Report.Entries, e => e.Category == "diplomatic-layer");
        Assert.Contains(result.Report.Entries, e => e.Category == "editorial-layer");
    }

    [Fact]
    public void Equal_text_does_not_preserve_the_existence_of_an_unmarked_editorial_layer()
    {
        var result = Export(Revision(TextLayer.L1Diplomatic, "αβ"), Revision(TextLayer.L2Editorial, "αβ"));
        Assert.DoesNotContain(result.Report.Entries, e => e.Category == "diplomatic-layer");
        Assert.Contains(result.Report.Entries, e => e.Category == "editorial-layer");
    }
    [Fact]
    public void Absent_visible_layer_is_not_equivalent_to_a_reimported_empty_revision()
    {
        var l2 = Revision(TextLayer.L2Editorial, "αβ");
        var result = Export(null, l2, new Annotation
        {
            Id = new(EntityKind.Annotation, "all-supplied"), Kind = AnnotationKind.SuppliedEditorial,
            AnchoredRevisionId = l2.Id, Range = new(0, 2), Description = "editorial",
        });
        Assert.Contains(result.Report.Entries, e => e.Category == "diplomatic-layer");
        Assert.DoesNotContain(result.Report.Entries, e => e.Category == "editorial-layer");
        var parsed = TeiXml.Import(result.Xml);
        Assert.Equal("", Assert.Single(parsed.Revisions, r => r.Layer == TextLayer.L1Diplomatic).Text);
    }

}
