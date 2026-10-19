using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class TranscriptionServiceTests
{
    private static readonly AgentRef Editor = new() { Id = "editor", IsModel = false };

    private static (EntityId LineId, TranscriptionService Service, InMemoryStore Store) Setup()
    {
        var store = new InMemoryStore();
        store.SaveProject(SampleProjectBuilder.Build());
        return (SampleProjectBuilder.Build().Objects[0].Surfaces[0].Lines[0].Id, new TranscriptionService(store), store);
    }

    private NativeProject LoadCurrent(InMemoryStore store, EntityId projectId) =>
        store.LoadProject(projectId) ?? throw new InvalidOperationException("project missing in test store");

    [Fact]
    public void AppendLineRevision_extends_L1_and_moves_the_line_pointer()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);

        var (updated, surface, revision) = service.AppendLineRevision(project, lineId, TextLayer.L1Diplomatic, "ἄνθρωπος", Editor);

        Assert.Equal(TextLayer.L1Diplomatic, revision.Layer);
        Assert.Contains(surface.Lines, l => l.Id == lineId && l.DiplomaticRevisionId == revision.Id);

        // The pointer persists.
        var reloaded = LoadCurrent(store, updated.Id);
        var line = reloaded.Objects[0].Surfaces[0].Lines.Single(l => l.Id == lineId);
        Assert.Equal(revision.Id, line.DiplomaticRevisionId);
    }

    [Fact]
    public void Stale_parent_fails_with_revision_conflict()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);

        var (afterFirst, _, r1) = service.AppendLineRevision(project, lineId, TextLayer.L1Diplomatic, "α", Editor);
        var (afterSecond, _, _) = service.AppendLineRevision(
            LoadCurrent(store, afterFirst.Id), lineId, TextLayer.L1Diplomatic, "αβ", Editor, parentRevisionId: r1.Id);

        // r1 is now superseded: another write naming it as parent must fail.
        var ex = Assert.Throws<LeviathanException>(() => service.AppendLineRevision(
            LoadCurrent(store, afterSecond.Id), lineId, TextLayer.L1Diplomatic, "γ", Editor, parentRevisionId: r1.Id));
        Assert.Equal(ErrorCode.RevisionConflict, ex.Error.Code);
    }

    [Fact]
    public void L2_edit_does_not_move_the_L1_pointer()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);
        var l1Pointer = project.Objects[0].Surfaces[0].Lines.Single(l => l.Id == lineId).DiplomaticRevisionId;

        var (updated, surface, editorial) = service.AppendLineRevision(project, lineId, TextLayer.L2Editorial, "κύριε", Editor);

        Assert.Equal(TextLayer.L2Editorial, editorial.Layer);
        var reloadedLine = LoadCurrent(store, updated.Id).Objects[0].Surfaces[0].Lines.Single(l => l.Id == lineId);
        Assert.Equal(l1Pointer, reloadedLine.DiplomaticRevisionId); // L1 untouched
    }

    [Fact]
    public void Annotation_with_empty_anchor_binds_to_current_revision()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);

        var (_, _, annotation) = service.AddAnnotation(project, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "a1"),
            Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = default,
            Range = new ScalarRange(0, 2),
        });

        Assert.NotEqual(default, annotation.AnchoredRevisionId);
    }

    [Fact]
    public void Annotation_anchored_to_superseded_revision_is_rejected()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);

        var (afterFirst, _, r1) = service.AppendLineRevision(project, lineId, TextLayer.L1Diplomatic, "α", Editor);
        var (afterSecond, _, _) = service.AppendLineRevision(
            LoadCurrent(store, afterFirst.Id), lineId, TextLayer.L1Diplomatic, "αβ", Editor, parentRevisionId: r1.Id);

        var ex = Assert.Throws<LeviathanException>(() => service.AddAnnotation(
            LoadCurrent(store, afterSecond.Id), lineId, new Annotation
            {
                Id = new EntityId(EntityKind.Annotation, "a2"),
                Kind = AnnotationKind.Unclear,
                AnchoredRevisionId = r1.Id, // superseded by the second edit
                Range = new ScalarRange(0, 1),
            }));
        Assert.Equal(ErrorCode.StaleAnnotationAnchor, ex.Error.Code);
    }

    [Fact]
    public void History_returns_revisions_newest_last()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);
        var importedCount = service.RevisionHistory(project, lineId, TextLayer.L1Diplomatic).Count;

        var (afterFirst, _, r1) = service.AppendLineRevision(project, lineId, TextLayer.L1Diplomatic, "α", Editor);
        var (afterSecond, _, _) = service.AppendLineRevision(
            LoadCurrent(store, afterFirst.Id), lineId, TextLayer.L1Diplomatic, "αβ", Editor, parentRevisionId: r1.Id);

        var history = service.RevisionHistory(LoadCurrent(store, afterSecond.Id), lineId, TextLayer.L1Diplomatic);
        Assert.Equal(importedCount + 2, history.Count);
        Assert.Equal("αβ", history[^1].Text);
    }
}
