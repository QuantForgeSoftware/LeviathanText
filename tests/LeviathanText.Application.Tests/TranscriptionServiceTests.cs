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

    [Fact]
    public void AnnotationsFor_returns_only_the_lines_revisions()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);

        var (one, _, _) = service.AddAnnotation(project, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "a1"),
            Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = default,
            Range = new ScalarRange(0, 2),
        });
        var (two, _, _) = service.AddAnnotation(one, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "a2"),
            Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = default,
            Range = new ScalarRange(3, 4),
        });

        // A foreign annotation anchored to an unrelated revision must not appear.
        var surface = two.Objects[0].Surfaces[0];
        var withForeign = two with
        {
            Objects = [two.Objects[0] with
            {
                Surfaces = [surface with
                {
                    Annotations =
                    [
                        .. surface.Annotations,
                        new Annotation
                        {
                            Id = new EntityId(EntityKind.Annotation, "foreign"),
                            Kind = AnnotationKind.Unclear,
                            AnchoredRevisionId = new EntityId(EntityKind.TranscriptionRevision, "rev-other"),
                            Range = new ScalarRange(0, 1),
                        },
                    ],
                }],
            }],
        };

        var annotations = service.AnnotationsFor(withForeign, lineId);
        Assert.Equal(2, annotations.Count);
        Assert.DoesNotContain(annotations, a => a.Id.Suffix == "foreign");
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

    [Fact]
    public void Annotation_ranges_are_validated_against_the_anchored_revision()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);

        var beyond = Assert.Throws<LeviathanException>(() => service.AddAnnotation(project, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "a-beyond"),
            Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = default,
            Range = new ScalarRange(0, 999),
        }));
        Assert.Equal(ErrorCode.StageContractViolation, beyond.Error.Code);

        var emptyUnclear = Assert.Throws<LeviathanException>(() => service.AddAnnotation(project, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "a-empty"),
            Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = default,
            Range = new ScalarRange(2, 2),
        }));
        Assert.Contains("empty range", emptyUnclear.Error.Message);

        // A lost marker may be a position; its extent defaults to Unknown.
        var (withLost, _, lost) = service.AddAnnotation(project, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "a-lost"),
            Kind = AnnotationKind.Lost,
            AnchoredRevisionId = default,
            Range = new ScalarRange(3, 3),
        });
        Assert.Equal(GapExtent.Unknown, lost.GapExtent);

        // Estimated gaps must carry a count; alternatives must carry text.
        Assert.Throws<LeviathanException>(() => service.AddAnnotation(withLost, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "a-est"),
            Kind = AnnotationKind.Lost,
            AnchoredRevisionId = default,
            Range = new ScalarRange(4, 5),
            GapExtent = GapExtent.Estimated,
        }));
        Assert.Throws<LeviathanException>(() => service.AddAnnotation(withLost, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "a-alt"),
            Kind = AnnotationKind.AlternativeReading,
            AnchoredRevisionId = default,
            Range = new ScalarRange(0, 2),
        }));
    }

    [Fact]
    public void Retraction_hides_an_annotation_without_deleting_it()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);
        var (withAnnotation, _, annotation) = service.AddAnnotation(project, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "ann-retract"),
            Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = default,
            Range = new ScalarRange(1, 3),
        });
        Assert.Single(service.AnnotationsFor(withAnnotation, lineId));

        var (retracted, surface, retraction) = service.RetractAnnotation(withAnnotation, annotation.Id, "reviewer-a", "misread");
        Assert.Equal(annotation.Id, retraction.AnnotationId);
        Assert.Contains(surface.Annotations, a => a.Id == annotation.Id); // evidence preserved
        Assert.Empty(service.AnnotationsFor(retracted, lineId));
        Assert.Single(surface.AnnotationRetractions);

        Assert.Throws<LeviathanException>(() =>
            service.RetractAnnotation(retracted, annotation.Id, "reviewer-b"));
        Assert.Throws<LeviathanException>(() =>
            service.RetractAnnotation(retracted, new EntityId(EntityKind.Annotation, "missing"), "reviewer-a"));
    }
}
