using LeviathanText.Contracts;
using LeviathanText.Core.Revisions;
using LeviathanText.Core.Unicode;
using Xunit;

namespace LeviathanText.Core.Tests;

/// <summary>
/// Tests the C0 Core contracts that B's ML/EVAL/AI modules consume
/// (greek-transcription.md §4, domain-and-pipeline.md §1.3, §6).
/// </summary>
public class UnicodeAndRevisionTests
{
    [Fact]
    public void ScalarOffsets_AreCodePoints_NotUtf16Units()
    {
        var text = "α\u0301β"; // alpha + combining acute + beta
        Assert.Equal(3, ScalarText.ScalarLength(text));
        Assert.Equal(2, ScalarText.Utf16IndexToScalar(text, 2));
        Assert.Equal("α\u0301", ScalarText.Slice(text, 0, 2));
        Assert.Equal("β", ScalarText.Slice(text, 2, 3));
    }

    [Fact]
    public void GraphemeClusters_TreatBasePlusMarkAsOneUnit()
    {
        var clusters = Graphemes.Enumerate("α\u0301β").Select(g => g.Text).ToList();
        Assert.Equal(2, clusters.Count);
        Assert.Equal("α\u0301", clusters[0]);
        Assert.Equal("β", clusters[1]);
    }

    [Fact]
    public void NfcMapper_MergesBaseAndMark_AndKeepsEndAnchoredRanges()
    {
        var mapping = NfcMapper.Map("α\u0301");
        Assert.Equal("ά", mapping.Normalized);
        Assert.Equal(1, mapping.TargetScalarLength);

        // Whole-range annotation must map to the full normalized range, not throw.
        var full = mapping.MapRange(new ScalarRange(0, 2));
        Assert.Equal(0, full.Start);
        Assert.Equal(1, full.End);
    }

    [Fact]
    public void GreekMarkOrder_ReordersBreathingsBeforeAccents()
    {
        // Input acute-then-diaeresis; profile order puts diaeresis (rank 1) before acute (rank 2).
        var reordered = GreekMarkOrder.Reorder("ι\u0301\u0308");
        Assert.Equal("ι\u0308\u0301", reordered);
        Assert.Equal(reordered, GreekMarkOrder.Reorder(reordered)); // idempotent
    }

    [Fact]
    public void RevisionChain_RejectsStaleParent_AndKeepsHistory()
    {
        var chain = new RevisionChain();
        var scope = new EntityId(EntityKind.Surface, "s1");
        var human = new AgentRef { Id = "u1", IsModel = false };

        var r1 = chain.Append(scope, TextLayer.L1Diplomatic, "Κϲ", human);
        var r2 = chain.Append(scope, TextLayer.L1Diplomatic, "Κϲ ", human, parentRevisionId: r1.Id);

        var conflict = Assert.Throws<LeviathanException>(() =>
            chain.Append(scope, TextLayer.L1Diplomatic, "other", human, parentRevisionId: r1.Id));
        Assert.Equal(ErrorCode.RevisionConflict, conflict.Error.Code);

        Assert.Equal(r2.Id, chain.Current(scope, TextLayer.L1Diplomatic)!.Id);
        Assert.Equal(2, chain.History(scope, TextLayer.L1Diplomatic).Count); // r1 still retrievable
    }
}
