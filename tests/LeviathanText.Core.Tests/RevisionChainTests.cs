using LeviathanText.Contracts;
using LeviathanText.Core.Revisions;

namespace LeviathanText.Core.Tests;

public class RevisionChainTests
{
    private static readonly EntityId Scope = new(EntityKind.Line, "testline");
    private static readonly AgentRef Agent = new() { Id = "test-agent", IsModel = false };

    private static Annotation AnnotationOn(TextRevision revision, ScalarRange range, AnnotationKind kind = AnnotationKind.Unclear) =>
        new()
        {
            Id = new EntityId(EntityKind.Annotation, "a1"),
            Kind = kind,
            AnchoredRevisionId = revision.Id,
            Range = range,
        };

    [Fact]
    public void First_append_has_no_parent()
    {
        var chain = new RevisionChain();
        var rev = chain.Append(Scope, TextLayer.L1Diplomatic, "αβ", Agent);
        Assert.Null(rev.ParentRevisionId);
        Assert.Equal(rev.Id, chain.Current(Scope, TextLayer.L1Diplomatic)!.Id);
    }

    [Fact]
    public void Append_on_current_parent_extends_the_chain()
    {
        var chain = new RevisionChain();
        var r1 = chain.Append(Scope, TextLayer.L1Diplomatic, "αβ", Agent);
        var r2 = chain.Append(Scope, TextLayer.L1Diplomatic, "αβγ", Agent, parentRevisionId: r1.Id);
        Assert.Equal(r1.Id, r2.ParentRevisionId);
        Assert.Equal(r2.Id, chain.Current(Scope, TextLayer.L1Diplomatic)!.Id);
        Assert.Equal(2, chain.History(Scope, TextLayer.L1Diplomatic).Count);
    }

    [Fact]
    public void Stale_parent_fails_with_revision_conflict()
    {
        var chain = new RevisionChain();
        var r1 = chain.Append(Scope, TextLayer.L1Diplomatic, "αβ", Agent);
        chain.Append(Scope, TextLayer.L1Diplomatic, "αβγ", Agent, parentRevisionId: r1.Id);

        var ex = Assert.Throws<LeviathanException>(() =>
            chain.Append(Scope, TextLayer.L1Diplomatic, "δ", Agent, parentRevisionId: r1.Id));

        Assert.Equal(ErrorCode.RevisionConflict, ex.Error.Code);
        Assert.Equal(RemedyHint.RebaseRevision, ex.Error.RemedyHint);
        Assert.True(ex.Error.Details!.ContainsKey("currentRevision"));
    }

    [Fact]
    public void Superseded_revisions_remain_retrievable()
    {
        var chain = new RevisionChain();
        var r1 = chain.Append(Scope, TextLayer.L1Diplomatic, "αβ", Agent);
        chain.Append(Scope, TextLayer.L1Diplomatic, "αβγ", Agent, parentRevisionId: r1.Id);

        Assert.Equal("αβ", chain.Get(r1.Id)!.Text); // acceptance invariant: history retrievable
        Assert.Equal(2, chain.History(Scope, TextLayer.L1Diplomatic).Count);
    }

    [Fact]
    public void Layers_are_independent_chains()
    {
        var chain = new RevisionChain();
        chain.Append(Scope, TextLayer.L1Diplomatic, "Κϲ", Agent);
        chain.Append(Scope, TextLayer.L2Editorial, "Κυρίου", Agent);
        Assert.Single(chain.History(Scope, TextLayer.L1Diplomatic));
        Assert.Single(chain.History(Scope, TextLayer.L2Editorial));
    }

    [Fact]
    public void NormalizeToNfc_is_noop_for_already_normalized_text()
    {
        var chain = new RevisionChain();
        var rev = chain.Append(Scope, TextLayer.L1Diplomatic, "ἄνθρωπος", Agent); // precomposed
        var (result, _) = chain.NormalizeToNfc(rev, Agent, []);
        Assert.Same(rev, result);
        Assert.Equal(rev.Id, chain.Current(Scope, TextLayer.L1Diplomatic)!.Id);
    }

    [Fact]
    public void NormalizeToNfc_creates_new_revision_and_remaps_annotations()
    {
        var chain = new RevisionChain();
        // Decomposed "ἄνθρωπ" (α + psili + acute + ν θ ρ ω π): annotate νθρωπ at
        // source scalars [3,8) — the psili+acute merge into ἄ under NFC.
        var rev = chain.Append(Scope, TextLayer.L1Diplomatic, "\u03B1\u0313\u0301\u03BD\u03B8\u03C1\u03C9\u03C0", Agent);
        var annotation = AnnotationOn(rev, new ScalarRange(3, 8));

        var (normalized, remapped) = chain.NormalizeToNfc(rev, Agent, [annotation]);

        Assert.NotEqual(rev.Id, normalized.Id);
        Assert.Equal(rev.Id, normalized.ParentRevisionId);
        Assert.Equal("ἄνθρωπ", normalized.Text);
        Assert.Equal(rev.Id, chain.Get(rev.Id)!.Id); // original retained
        var newRange = Assert.Single(remapped).Range;
        Assert.Equal(new ScalarRange(1, 6), newRange);
        Assert.Equal(normalized.Id, Assert.Single(remapped).AnchoredRevisionId);
    }

    [Fact]
    public void NormalizeToNfc_leaves_unmappable_annotation_on_original_revision()
    {
        var chain = new RevisionChain();
        var rev = chain.Append(Scope, TextLayer.L1Diplomatic, "\u03B1\u0313\u0301", Agent); // α + psili + acute → merges to ἄ
        var annotation = AnnotationOn(rev, new ScalarRange(2, 3)); // only the combining acute

        Assert.Throws<LeviathanException>(() => chain.NormalizeToNfc(rev, Agent, [annotation]));

        // Original revision and its annotation are untouched; nothing detached silently.
        Assert.Equal(rev.Id, chain.Current(Scope, TextLayer.L1Diplomatic)!.Id);
        Assert.Equal("\u03B1\u0313\u0301", chain.Get(rev.Id)!.Text);
    }

    [Fact]
    public void RequireCurrentAnchor_rejects_superseded_anchor()
    {
        var chain = new RevisionChain();
        var r1 = chain.Append(Scope, TextLayer.L1Diplomatic, "αβ", Agent);
        chain.Append(Scope, TextLayer.L1Diplomatic, "αβγ", Agent, parentRevisionId: r1.Id);

        var ex = Assert.Throws<LeviathanException>(() => chain.RequireCurrentAnchor(AnnotationOn(r1, new ScalarRange(0, 1))));
        Assert.Equal(ErrorCode.StaleAnnotationAnchor, ex.Error.Code);
    }

    [Fact]
    public void RequireCurrentAnchor_accepts_current_anchor()
    {
        var chain = new RevisionChain();
        var r1 = chain.Append(Scope, TextLayer.L1Diplomatic, "αβ", Agent);
        chain.RequireCurrentAnchor(AnnotationOn(r1, new ScalarRange(0, 1))); // no throw
    }
}
