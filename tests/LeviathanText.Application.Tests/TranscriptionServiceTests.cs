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
    public void Automatic_merge_combines_non_overlapping_edits_on_a_stale_parent()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);
        var (_, _, baseRevision) = service.AppendLineRevision(
            project, lineId, TextLayer.L1Diplomatic, "αβγ", Editor);

        var beforeRemote = LoadCurrent(store, project.Id);
        var (_, _, remoteRevision) = service.AppendLineRevision(
            beforeRemote, lineId, TextLayer.L1Diplomatic, "αβΓ", Editor, parentRevisionId: baseRevision.Id);

        var (mergedProject, _, mergedRevision, merged) = service.AppendLineRevisionMerged(
            beforeRemote, lineId, TextLayer.L1Diplomatic, "Αβγ", Editor, parentRevisionId: baseRevision.Id);

        Assert.True(merged);
        Assert.Equal("ΑβΓ", mergedRevision.Text);
        Assert.Equal(remoteRevision.Id, mergedRevision.ParentRevisionId);
        var reloaded = LoadCurrent(store, mergedProject.Id);
        Assert.Equal(mergedRevision.Id,
            reloaded.Objects[0].Surfaces[0].Lines.Single(l => l.Id == lineId).DiplomaticRevisionId);
    }

    [Fact]
    public void Automatic_merge_refuses_overlapping_edits_and_names_both_sides()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);
        var (_, _, baseRevision) = service.AppendLineRevision(
            project, lineId, TextLayer.L1Diplomatic, "αβγ", Editor);
        var beforeRemote = LoadCurrent(store, project.Id);
        service.AppendLineRevision(beforeRemote, lineId, TextLayer.L1Diplomatic, "αΒγ", Editor,
            parentRevisionId: baseRevision.Id);

        var ex = Assert.Throws<LeviathanException>(() => service.AppendLineRevisionMerged(
            beforeRemote, lineId, TextLayer.L1Diplomatic, "αbγ", Editor, parentRevisionId: baseRevision.Id));

        Assert.Equal(ErrorCode.RevisionConflict, ex.Error.Code);
        Assert.Equal("Β", ex.Error.Details!["remoteText"]);
        Assert.Equal("b", ex.Error.Details!["localText"]);
        Assert.Equal(RemedyHint.RebaseRevision, ex.Error.RemedyHint);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Automatic_merge_refuses_a_concurrent_first_revision(bool raceDuringSave)
    {
        var empty = SampleProjectBuilder.Build();
        var originalSurface = empty.Objects[0].Surfaces[0];
        var line = originalSurface.Lines[0] with { DiplomaticRevisionId = null };
        empty = empty with
        {
            Objects = [empty.Objects[0] with
            {
                Surfaces = [originalSurface with { Lines = [line], Revisions = [] }],
            }],
        };
        var store = new InMemoryStore();
        store.SaveProject(empty);
        var initialSnapshot = store.LoadProject(empty.Id)!;
        var firstService = new TranscriptionService(store);
        TextRevision? firstRevision = null;
        void FirstWriter() => firstRevision = firstService.AppendLineRevisionMerged(
            initialSnapshot, line.Id, TextLayer.L1Diplomatic, "α", Editor, null).Revision;
        IProjectStore secondStore = store;
        if (raceDuringSave)
            secondStore = new InterleavingProjectStore(store, FirstWriter);
        else
            FirstWriter();

        var exception = Assert.Throws<LeviathanException>(() =>
            new TranscriptionService(secondStore).AppendLineRevisionMerged(
                initialSnapshot, line.Id, TextLayer.L1Diplomatic, "β", Editor, null));

        Assert.Equal(ErrorCode.RevisionConflict, exception.Error.Code);
        Assert.Equal("none", exception.Error.Details!["requestedParent"]);
        var persisted = store.LoadProject(empty.Id)!.Objects[0].Surfaces[0];
        Assert.Equal("α", Assert.Single(persisted.Revisions).Text);
        Assert.Equal(firstRevision!.Id, persisted.Lines[0].DiplomaticRevisionId);
    }

    [Theory]
    [InlineData("foreign-line")]
    [InlineData("foreign-layer")]
    [InlineData("disconnected")]
    [InlineData("cycle")]
    public void Automatic_merge_requires_a_base_in_the_target_revision_chain(string scenario)
    {
        var (lineId, service, store) = Setup();
        var project = store.LoadProject(SampleProjectBuilder.Build().Id)!;
        var surface = project.Objects[0].Surfaces[0];
        var current = surface.Revisions[0] with { Text = "αβγ" };
        var other = current with
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "other-base"),
            ScopeId = scenario == "foreign-line" ? new EntityId(EntityKind.Line, "other-line") : lineId,
            Layer = scenario == "foreign-layer" ? TextLayer.L2Editorial : TextLayer.L1Diplomatic,
        };
        current = current with
        {
            ParentRevisionId = scenario switch
            {
                "foreign-line" or "foreign-layer" => other.Id,
                "cycle" => current.Id,
                _ => null,
            },
        };
        store.SaveProject(project with
        {
            Objects = [project.Objects[0] with
            {
                Surfaces = [surface with { Revisions = [current, other] }],
            }],
        });
        var before = store.LoadProject(project.Id)!;

        var exception = Assert.Throws<LeviathanException>(() => service.AppendLineRevisionMerged(
            before, lineId, TextLayer.L1Diplomatic, "Αβγ", Editor, other.Id));

        Assert.Equal(ErrorCode.RevisionConflict, exception.Error.Code);
        Assert.Equal(before.Version, store.LoadProject(project.Id)!.Version);
        Assert.Equal(2, store.LoadProject(project.Id)!.Objects[0].Surfaces[0].Revisions.Count);
    }

    [Fact]
    public void Automatic_merge_accepts_current_and_multi_generation_ancestor_bases()
    {
        var (lineId, service, store) = Setup();
        var project = store.LoadProject(SampleProjectBuilder.Build().Id)!;
        var original = project.Objects[0].Surfaces[0].Revisions[0];
        var currentEdit = service.AppendLineRevisionMerged(project, lineId, TextLayer.L1Diplomatic,
            "αβγδε", Editor, original.Id);
        Assert.False(currentEdit.Merged);
        var second = service.AppendLineRevisionMerged(currentEdit.Project, lineId, TextLayer.L1Diplomatic,
            "αβγδΕ", Editor, currentEdit.Revision.Id);
        var third = service.AppendLineRevisionMerged(second.Project, lineId, TextLayer.L1Diplomatic,
            "αβγΔΕ", Editor, second.Revision.Id);

        var merged = service.AppendLineRevisionMerged(currentEdit.Project, lineId, TextLayer.L1Diplomatic,
            "Αβγδε", Editor, currentEdit.Revision.Id);

        Assert.Equal("ΑβγΔΕ", merged.Revision.Text);
        Assert.True(merged.Merged);
        Assert.Equal(third.Revision.Id, merged.Revision.ParentRevisionId);
    }

    private sealed class InterleavingProjectStore(IProjectStore inner, Action firstWrite) : IProjectStore
    {
        private Action? _beforeSave = firstWrite;

        public long SaveProject(NativeProject project)
        {
            var callback = _beforeSave;
            _beforeSave = null;
            callback?.Invoke();
            return inner.SaveProject(project);
        }

        public NativeProject? LoadProject(EntityId id) => inner.LoadProject(id);
        public IReadOnlyList<(EntityId Id, string Name)> ListProjects() => inner.ListProjects();
    }

    [Fact]
    public void Automatic_merge_retries_an_initial_editorial_revision_after_an_unrelated_project_save()
    {
        var (lineId, _, store) = Setup();
        var project = store.LoadProject(SampleProjectBuilder.Build().Id)!;
        var l1RevisionId = project.Objects[0].Surfaces[0].Lines[0].DiplomaticRevisionId;
        var racingStore = new InterleavingProjectStore(store, () =>
            store.SaveProject(store.LoadProject(project.Id)! with { Name = "updated metadata" }));

        var saved = new TranscriptionService(racingStore).AppendLineRevisionMerged(
            project, lineId, TextLayer.L2Editorial, "ἄλφα", Editor, null);

        Assert.False(saved.Merged);
        Assert.Null(saved.Revision.ParentRevisionId);
        Assert.Equal("updated metadata", saved.Project.Name);
        Assert.Equal(project.Version + 2, saved.Project.Version);
        Assert.Equal(l1RevisionId, saved.Surface.Lines[0].DiplomaticRevisionId);
        var persisted = store.LoadProject(project.Id)!;
        Assert.Equal("ἄλφα", Assert.Single(persisted.Objects[0].Surfaces[0].Revisions,
            r => r.Layer == TextLayer.L2Editorial).Text);
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
    public void Abbreviation_requires_a_matching_l2_expansion_link()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);
        var (withL2, _, l2) = service.AppendLineRevision(project, lineId, TextLayer.L2Editorial, "κυρίου", Editor);

        var (updated, _, abbreviation) = service.AddAnnotation(withL2, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "abbr-1"),
            Kind = AnnotationKind.Abbreviation,
            AnchoredRevisionId = default,
            Range = new ScalarRange(0, 2),
            ExpansionText = "κυρίου",
            ExpansionRevisionId = l2.Id,
            ExpansionRange = new ScalarRange(0, 6),
        });
        Assert.Equal(AnnotationKind.Abbreviation, abbreviation.Kind);
        Assert.Equal(l2.Id, abbreviation.ExpansionRevisionId);
        Assert.Contains(updated.Objects[0].Surfaces[0].Annotations, a => a.Id == abbreviation.Id);

        var mismatch = Assert.Throws<LeviathanException>(() => service.AddAnnotation(updated, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "abbr-2"),
            Kind = AnnotationKind.Abbreviation,
            AnchoredRevisionId = default,
            Range = new ScalarRange(0, 2),
            ExpansionText = "wrong",
            ExpansionRevisionId = l2.Id,
            ExpansionRange = new ScalarRange(0, 6),
        }));
        Assert.Contains("does not equal", mismatch.Error.Message);
    }

    [Fact]
    public void Correction_sequence_requires_group_and_rejects_duplicate_explicit_index()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);
        var (withFirst, _, _) = service.AddAnnotation(project, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "seq-1"),
            Kind = AnnotationKind.CorrectionSequence,
            AnchoredRevisionId = default,
            Range = new ScalarRange(0, 1),
            SequenceId = "main",
            SequenceIndex = 0,
            OrderBasis = AnnotationOrderBasis.Explicit,
        });

        var duplicate = Assert.Throws<LeviathanException>(() => service.AddAnnotation(withFirst, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "seq-duplicate"),
            Kind = AnnotationKind.CorrectionSequence,
            AnchoredRevisionId = default,
            Range = new ScalarRange(1, 2),
            SequenceId = "main",
            SequenceIndex = 0,
            OrderBasis = AnnotationOrderBasis.Explicit,
        }));
        Assert.Contains("already used", duplicate.Error.Message);

        var missingGroup = Assert.Throws<LeviathanException>(() => service.AddAnnotation(withFirst, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "seq-no-group"),
            Kind = AnnotationKind.CorrectionSequence,
            AnchoredRevisionId = default,
            Range = new ScalarRange(1, 2),
            SequenceIndex = 1,
        }));
        Assert.Contains("requires a SequenceId", missingGroup.Error.Message);
    }

    [Fact]
    public void Word_boundary_is_allowed_as_a_zero_width_annotation()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);

        var (_, _, boundary) = service.AddAnnotation(project, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "wb-1"),
            Kind = AnnotationKind.WordBoundary,
            AnchoredRevisionId = default,
            Range = new ScalarRange(2, 2),
            Description = "word division proposal",
        });

        Assert.Equal(AnnotationKind.WordBoundary, boundary.Kind);
        Assert.True(boundary.Range.IsEmpty);
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

    [Fact]
    public void Concurrent_retractions_do_not_silently_overwrite_the_project()
    {
        var (lineId, service, store) = Setup();
        var project = LoadCurrent(store, SampleProjectBuilder.Build().Id);
        var (withAnnotation, _, annotation) = service.AddAnnotation(project, lineId, new Annotation
        {
            Id = new EntityId(EntityKind.Annotation, "ann-race"),
            Kind = AnnotationKind.Unclear,
            AnchoredRevisionId = default,
            Range = new ScalarRange(1, 2),
        });
        var first = LoadCurrent(store, withAnnotation.Id);
        var second = LoadCurrent(store, withAnnotation.Id);

        service.RetractAnnotation(first, annotation.Id, "reviewer-a");
        var stale = Assert.Throws<LeviathanException>(() =>
            service.RetractAnnotation(second, annotation.Id, "reviewer-b"));

        Assert.Equal(ErrorCode.RevisionConflict, stale.Error.Code);
        Assert.Single(LoadCurrent(store, withAnnotation.Id).Objects[0].Surfaces[0].AnnotationRetractions);
    }
}
