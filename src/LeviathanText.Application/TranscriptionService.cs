using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application;

/// <summary>
/// Transcription write use cases (todo Phase 2: users transcribe/review with layers,
/// revision-based undo and source-linked annotations). All writes go through the
/// revision semantics: stale parents fail with <see cref="ErrorCode.RevisionConflict"/>,
/// annotations anchored to superseded revisions fail with
/// <see cref="ErrorCode.StaleAnnotationAnchor"/>; history is append-only and retrievable.
/// </summary>
public sealed class TranscriptionService(IProjectStore store)
{
    public (NativeProject Project, NativeSurface Surface, TextRevision Revision) AppendLineRevision(
        NativeProject project, EntityId lineId, TextLayer layer, string text, AgentRef author, EntityId? parentRevisionId = null)
    {
        var (foundProject, surface, line) = FindLine(project, lineId);

        var current = CurrentRevision(surface, lineId, layer, line);
        if (parentRevisionId is { } requested && (current is null || requested != current.Id))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.RevisionConflict,
                Scope = lineId.ToString(),
                Details = new Dictionary<string, string>
                {
                    ["requestedParent"] = requested.ToString(),
                    ["currentRevision"] = current?.Id.ToString() ?? "none",
                },
                RemedyHint = RemedyHint.RebaseRevision,
                Message = "Write targeted a superseded revision; rebase on the current revision.",
            });

        var revision = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, Guid.NewGuid().ToString("N")[..12]),
            ScopeId = lineId,
            Layer = layer,
            ParentRevisionId = current?.Id,
            Author = author,
            CreatedAt = DateTimeOffset.UtcNow,
            Text = text,
        };

        surface = surface with
        {
            Revisions = [.. surface.Revisions.Append(revision)],
            Lines = [.. surface.Lines.Select(l => l.Id == lineId && layer == TextLayer.L1Diplomatic
                ? l with { DiplomaticRevisionId = revision.Id }
                : l)],
        };
        foundProject = WithSurface(foundProject, surface);
        store.SaveProject(foundProject);
        return (foundProject, surface, revision);
    }

    public IReadOnlyList<TextRevision> RevisionHistory(NativeProject project, EntityId lineId, TextLayer layer) =>
        project.Objects.SelectMany(o => o.Surfaces)
            .SelectMany(s => s.Revisions)
            .Where(r => r.ScopeId == lineId && r.Layer == layer)
            .OrderBy(r => r.CreatedAt)
            .ToList();

    /// <summary>Every revision of a line across layers, oldest first (audit/display).</summary>
    public IReadOnlyList<TextRevision> AllRevisions(NativeProject project, EntityId lineId)
    {
        var (_, surface, _) = FindLine(project, lineId);
        return [.. surface.Revisions.Where(r => r.ScopeId == lineId).OrderBy(r => r.CreatedAt)];
    }

    /// <summary>Active (non-retracted) annotations of a line's revisions, oldest first.</summary>
    public IReadOnlyList<Annotation> AnnotationsFor(NativeProject project, EntityId lineId)
    {
        var (_, surface, _) = FindLine(project, lineId);
        var revisionIds = surface.Revisions.Where(r => r.ScopeId == lineId).Select(r => r.Id).ToHashSet();
        var retracted = surface.AnnotationRetractions.Select(r => r.AnnotationId).ToHashSet();
        return [.. surface.Annotations.Where(a => revisionIds.Contains(a.AnchoredRevisionId) && !retracted.Contains(a.Id))];
    }

    /// <summary>Retract an annotation (append-only; the annotation record itself is preserved).</summary>
    public (NativeProject Project, NativeSurface Surface, AnnotationRetraction Retraction) RetractAnnotation(
        NativeProject project, EntityId annotationId, string reviewer, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(reviewer))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = annotationId.ToString(),
                Message = "A reviewer identity is required.",
            });

        foreach (var obj in project.Objects)
            foreach (var surface in obj.Surfaces)
            {
                var annotation = surface.Annotations.FirstOrDefault(a => a.Id == annotationId);
                if (annotation is null) continue;

                if (surface.AnnotationRetractions.Any(r => r.AnnotationId == annotationId))
                    throw new LeviathanException(new LeviathanError
                    {
                        Code = ErrorCode.StageContractViolation,
                        Scope = annotationId.ToString(),
                        Message = "Annotation is already retracted.",
                    });

                var retraction = new AnnotationRetraction
                {
                    Id = $"ar-{Guid.NewGuid().ToString("N")[..12]}",
                    AnnotationId = annotationId,
                    Reviewer = reviewer,
                    At = DateTimeOffset.UtcNow,
                    Reason = string.IsNullOrWhiteSpace(reason) ? null : reason,
                };
                var updated = surface with { AnnotationRetractions = [.. surface.AnnotationRetractions, retraction] };
                var updatedProject = WithSurface(project, updated);
                store.SaveProject(updatedProject);
                return (updatedProject, updated, retraction);
            }

        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.InputMissing,
            Scope = annotationId.ToString(),
            Message = "No such annotation in the project.",
        });
    }

    public (NativeProject Project, NativeSurface Surface, Annotation Annotation) AddAnnotation(
        NativeProject project, EntityId lineId, Annotation annotation)
    {
        var (foundProject, surface, line) = FindLine(project, lineId);

        // Bind empty anchors to the current revision; reject stale anchors loudly.
        var anchor = annotation.AnchoredRevisionId;
        if (anchor == default)
        {
            var current = CurrentRevision(surface, lineId, TextLayer.L1Diplomatic, line)
                ?? throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StaleAnnotationAnchor,
                    Scope = lineId.ToString(),
                    Message = "Line has no diplomatic revision to anchor to.",
                });
            annotation = annotation with { AnchoredRevisionId = current.Id };
        }
        else
        {
            var current = CurrentRevision(surface, lineId, TextLayer.L1Diplomatic, line);
            if (current is null || anchor != current.Id)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StaleAnnotationAnchor,
                    Scope = annotation.Id.ToString(),
                    Details = new Dictionary<string, string> { ["currentRevision"] = current?.Id.ToString() ?? "none" },
                    RemedyHint = RemedyHint.RebaseRevision,
                    Message = "Annotation is anchored to a superseded revision.",
                });
        }

        // Range and payload validation against the anchored revision (scalar offsets).
        var revision = surface.Revisions.Single(r => r.Id == annotation.AnchoredRevisionId);
        int length = LeviathanText.Core.Unicode.ScalarText.ScalarLength(revision.Text);
        if (annotation.Range.Start < 0 || annotation.Range.End < annotation.Range.Start || annotation.Range.End > length)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = annotation.Id.ToString(),
                Details = new Dictionary<string, string>
                {
                    ["range"] = $"{annotation.Range.Start}..{annotation.Range.End}",
                    ["length"] = length.ToString(),
                },
                Message = "Annotation range falls outside the anchored revision.",
            });

        if (annotation.Kind is not (AnnotationKind.Lost or AnnotationKind.WordBoundary)
            && annotation.Range.IsEmpty)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = annotation.Id.ToString(),
                Message = "Only a lost-text marker or word boundary may have an empty range (a position); other annotations need a span.",
            });

        if (annotation.Kind == AnnotationKind.Lost)
        {
            annotation = annotation.GapExtent is null
                ? annotation with { GapExtent = GapExtent.Unknown }
                : annotation;
            if (annotation.GapExtent == GapExtent.Estimated && annotation.EstimatedScalarCount is null)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StageContractViolation,
                    Scope = annotation.Id.ToString(),
                    Message = "An estimated gap must carry EstimatedScalarCount.",
                });
        }

        if (annotation.Kind == AnnotationKind.AlternativeReading && string.IsNullOrWhiteSpace(annotation.AlternativeText))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = annotation.Id.ToString(),
                Message = "An alternative reading must carry AlternativeText.",
            });

        if (annotation.Kind == AnnotationKind.Abbreviation)
        {
            if (string.IsNullOrWhiteSpace(annotation.ExpansionText))
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StageContractViolation,
                    Scope = annotation.Id.ToString(),
                    Message = "An abbreviation must carry ExpansionText.",
                });
            if (annotation.ExpansionRevisionId is not { } expansionRevisionId || annotation.ExpansionRange is not { } expansionRange)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StageContractViolation,
                    Scope = annotation.Id.ToString(),
                    Message = "An abbreviation must link both ExpansionRevisionId and ExpansionRange.",
                });
            var expansionRevision = surface.Revisions.FirstOrDefault(r =>
                r.Id == expansionRevisionId && r.ScopeId == lineId && r.Layer == TextLayer.L2Editorial);
            if (expansionRevision is null)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.InputMissing,
                    Scope = annotation.Id.ToString(),
                    Message = "The linked expansion revision is not an L2 revision of this line.",
                });
            int expansionLength = LeviathanText.Core.Unicode.ScalarText.ScalarLength(expansionRevision.Text);
            if (expansionRange.Start < 0 || expansionRange.End <= expansionRange.Start || expansionRange.End > expansionLength)
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StageContractViolation,
                    Scope = annotation.Id.ToString(),
                    Message = "The expansion range falls outside the linked L2 revision.",
                });
            var linkedText = LeviathanText.Core.Unicode.ScalarText.Slice(
                expansionRevision.Text, expansionRange.Start, expansionRange.End);
            if (!string.Equals(linkedText, annotation.ExpansionText, StringComparison.Ordinal))
                throw new LeviathanException(new LeviathanError
                {
                    Code = ErrorCode.StageContractViolation,
                    Scope = annotation.Id.ToString(),
                    Message = "ExpansionText does not equal the text at ExpansionRange in the linked L2 revision.",
                });
        }

        if (annotation.SequenceIndex is < 0)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = annotation.Id.ToString(),
                Message = "SequenceIndex must be non-negative.",
            });
        if (annotation.SequenceIndex is not null && string.IsNullOrWhiteSpace(annotation.SequenceId))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = annotation.Id.ToString(),
                Message = "SequenceIndex requires a SequenceId.",
            });
        if (annotation.SequenceIndex is { } sequenceIndex
            && !string.IsNullOrWhiteSpace(annotation.SequenceId)
            && surface.Annotations.Any(existing =>
                existing.Kind == AnnotationKind.CorrectionSequence
                && existing.SequenceId == annotation.SequenceId
                && existing.SequenceIndex == sequenceIndex
                && existing.OrderBasis is not AnnotationOrderBasis.Inferred))
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation,
                Scope = annotation.Id.ToString(),
                Message = "An explicit correction-sequence index is already used by another annotation.",
            });
        if (annotation.SequenceIndex is not null && annotation.OrderBasis is null)
            annotation = annotation with { OrderBasis = AnnotationOrderBasis.Explicit };

        surface = surface with { Annotations = [.. surface.Annotations.Append(annotation)] };
        foundProject = WithSurface(foundProject, surface);
        store.SaveProject(foundProject);
        return (foundProject, surface, annotation);
    }

    private static TextRevision? CurrentRevision(NativeSurface surface, EntityId lineId, TextLayer layer, NativeLine line) =>
        layer switch
        {
            TextLayer.L1Diplomatic => line.DiplomaticRevisionId is { } id
                ? surface.Revisions.FirstOrDefault(r => r.Id == id)
                : null,
            _ => surface.Revisions
                .Where(r => r.ScopeId == lineId && r.Layer == layer)
                .OrderBy(r => r.CreatedAt)
                .LastOrDefault(),
        };

    private static (NativeProject Project, NativeSurface Surface, NativeLine Line) FindLine(NativeProject project, EntityId lineId)
    {
        foreach (var obj in project.Objects)
            foreach (var surface in obj.Surfaces)
                foreach (var line in surface.Lines)
                    if (line.Id == lineId)
                        return (project, surface, line);

        throw new LeviathanException(new LeviathanError
        {
            Code = ErrorCode.InputMissing,
            Scope = lineId.ToString(),
            Message = "No such line in the project.",
        });
    }

    private static NativeProject WithSurface(NativeProject project, NativeSurface surface) =>
        project with
        {
            Objects = [.. project.Objects.Select(o => o.Surfaces.Any(s => s.Id == surface.Id)
                ? o with { Surfaces = [.. o.Surfaces.Select(s => s.Id == surface.Id ? surface : s)] }
                : o)],
        };
}
