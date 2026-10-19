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

    /// <summary>All annotations of a line's revisions (any layer), oldest first.</summary>
    public IReadOnlyList<Annotation> AnnotationsFor(NativeProject project, EntityId lineId)
    {
        var (_, surface, _) = FindLine(project, lineId);
        var revisionIds = surface.Revisions.Where(r => r.ScopeId == lineId).Select(r => r.Id).ToHashSet();
        return [.. surface.Annotations.Where(a => revisionIds.Contains(a.AnchoredRevisionId))];
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
