using LeviathanText.Contracts;
using LeviathanText.Formats;
using Xunit;
namespace LeviathanText.Formats.Tests;
public class TeiUntranscribedLossTests
{
    private static readonly EntityId Line = new(EntityKind.Line,"untranscribed");
    private static TeiXml.TeiExportResult Export(TextRevision? l1) => TeiXml.ExportDocument(
        [new TeiXml.TeiExportLine(Line,l1,null,[],null)],"Generated empty-state fixture",
        new TeiXml.TeiExportContext { Graphics=[new TeiXml.TeiGraphic("generated-image.png")] });
    [Fact]
    public void Unknown_native_reading_cannot_claim_lossless_roundtrip_into_explicit_empty_l1()
    {
        var exported=Export(null);
        var imported=TeiXml.Import(exported.Xml);
        Assert.Equal("",Assert.Single(imported.Revisions,r=>r.Layer==TextLayer.L1Diplomatic).Text);
        Assert.Contains(exported.Report.Entries,e=>e.Category=="diplomatic-layer");
        Assert.False(exported.Report.IsLossless);
    }
    [Fact]
    public void Explicit_empty_witness_reading_stays_explicit_empty_without_false_loss()
    {
        var l1=new TextRevision { Id=new(EntityKind.TranscriptionRevision,"empty"),ScopeId=Line,
            Layer=TextLayer.L1Diplomatic,Text="",Author=new AgentRef{Id="generated",IsModel=false},CreatedAt=DateTimeOffset.UnixEpoch };
        var exported=Export(l1);
        var imported=TeiXml.Import(exported.Xml);
        Assert.Equal("",Assert.Single(imported.Revisions,r=>r.Layer==TextLayer.L1Diplomatic).Text);
        Assert.DoesNotContain(exported.Report.Entries,e=>e.Category=="diplomatic-layer");
        Assert.True(exported.Report.IsLossless);
    }
}
