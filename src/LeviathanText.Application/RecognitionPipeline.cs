using LeviathanText.Contracts;
using LeviathanText.Formats;
using LeviathanText.Imaging;
using LeviathanText.Recognition;
using SkiaSharp;

namespace LeviathanText.Application;

/// <summary>
/// Recognition pipeline use case (todo Phase 1: preparation, extraction, recognition
/// and alignment with bounded resource use). Takes a surface with captured/imported
/// line geometry and a trained model package, crops lines from the original capture,
/// recognizes L0 text with per-grapheme confidence, and records stage provenance.
/// L0 results are stored as raw-recognition revisions — never merged into L1
/// (greek-transcription.md §1).
/// </summary>
public sealed class RecognitionPipeline
{
    public sealed record Options
    {
        public string? ModelDirectory { get; init; }
        public string ModelName { get; init; } = "p01-pilot-crnn";
        public string ModelVersion { get; init; } = "pilot";
        public required Func<CrnnModel> ModelFactory { get; init; }
        public required Func<LabelSet> LabelSetFactory { get; init; }

        /// <summary>The caller owns the returned model and uses it sequentially across surfaces.</summary>
        public bool LeaveModelOpen { get; init; }

        /// <summary>Package-declared preparation; null retains legacy ink-positive grayscale.</summary>
        public LinePreprocessingProfile? PreprocessingProfile { get; init; }

        /// <summary>
        /// Stable id for this recognition run. Re-running with the same id does not append a
        /// second copy of the same L0 revisions (job retries stay idempotent); null generates
        /// a fresh id per run.
        /// </summary>
        public string? RunId { get; init; }
    }

    public sealed record SurfaceOutcome(
        IReadOnlyList<LineRecognition> Lines,
        IReadOnlyList<StageRun> Stages,
        IReadOnlyList<EntityId> SkippedLines);

    public sealed record LineRecognition(
        EntityId LineId,
        string L0Text,
        IReadOnlyList<GraphemeConfidence> Confidence,
        Checksum LineCropChecksum,
        string TransformChain);

    private readonly IProjectStore _store;
    private readonly ImageResolver? _imageResolver;

    public RecognitionPipeline(IProjectStore store, ImageResolver? imageResolver = null)
    {
        _store = store;
        _imageResolver = imageResolver;
    }

    public SurfaceOutcome Run(NativeProject project, EntityId surfaceId, Options options, CancellationToken cancellation = default,
        bool persistL0 = true)
    {
        var matchingSurfaces = project.Objects.SelectMany(o => o.Surfaces).Where(s => s.Id == surfaceId).Take(2).ToArray();
        if (matchingSurfaces.Length > 1)
            throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.StageContractViolation, Scope = surfaceId.ToString(),
                Details = new Dictionary<string, string> { ["reason"] = "ambiguous-recognition-surface" },
                Message = "The surface identity is ambiguous; repair its registration before recognition. No capture or model was opened.",
            });
        var surface = matchingSurfaces.FirstOrDefault()
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = surfaceId.ToString(),
                Message = "No such surface in the project.",
            });

        var lineIds = new HashSet<EntityId>();
        foreach (var line in surface.Lines)
            if (line.Id.Kind != EntityKind.Line || !lineIds.Add(line.Id)) throw IdentityConflict(line.Id);
        var selectedIdentities = new HashSet<EntityId>();
        foreach (var candidate in project.Objects.SelectMany(obj => obj.Surfaces).SelectMany(page => page.Lines))
            if (lineIds.Contains(candidate.Id) && !selectedIdentities.Add(candidate.Id)) throw IdentityConflict(candidate.Id);

        var capture = surface.Captures.FirstOrDefault()
            ?? throw new LeviathanException(new LeviathanError
            {
                Code = ErrorCode.InputMissing,
                Scope = surfaceId.ToString(),
                Message = "Surface has no capture to recognize against.",
            });

        // Remote captures (IIIF URLs) are fetched once into the checksummed cache.
        if (!File.Exists(capture.ImageLocation) && _imageResolver is not null)
        {
            var resolved = _imageResolver.EnsureLocal(project, surfaceId);
            project = resolved.Project;
            surface = resolved.Surface;
            capture = resolved.Capture;
        }

        var labels = options.LabelSetFactory();
        var model = options.ModelFactory();
        using var ownedModel = options.LeaveModelOpen ? null : model;
        var preprocessing = options.PreprocessingProfile ?? LinePreprocessingProfile.LegacyInkPositive(model.InputHeightPixels);
        preprocessing.Validate(model.InputHeightPixels);
        preprocessing.RequireStandardGeometry();
        var recognizer = new TorchLineRecognizer(model, labels);

        // Large JPEG pages use bounded horizontal band decoding per line; PNG/TIFF
        // keep the full-frame path until a portable tiled decoder exists.
        var extension = Path.GetExtension(capture.ImageLocation).ToLowerInvariant();
        bool useBanded = extension is ".jpg" or ".jpeg" or ".png";
        if (useBanded)
        {
            var (width, height) = ImageDecoder.Dimensions(capture.ImageLocation);
            useBanded = (long)width * height > 25_000_000;
        }

        ImageDecoder.DecodedImage? decoded = useBanded ? null : ImageDecoder.Decode(capture.ImageLocation);
        Checksum sourceChecksum;
        if (decoded is not null)
        {
            sourceChecksum = decoded.SourceChecksum;
        }
        else
        {
            using var stream = File.OpenRead(capture.ImageLocation);
            sourceChecksum = ImageDecoder.ChecksumOf(stream);
        }
        var stages = new List<StageRun>();
        var modelRef = new ModelRef
        {
            PackageId = options.ModelName,
            Version = options.ModelVersion,
            IoContractHash = LabelSetHash(labels),
        };

        var results = new List<LineRecognition>();
        var lineImages = new List<(EntityId Id, LineImage Image, Checksum CropChecksum)>();
        var skippedLines = new List<EntityId>();

        try
        {
            foreach (var line in surface.Lines)
            {
                cancellation.ThrowIfCancellationRequested();
                var geometry = line.Polygon.Count >= 3 ? line.Polygon : line.Baseline;
                if (geometry.Count < 2)
                {
                    skippedLines.Add(line.Id); // absent geometry: explicit skip, never silent
                    continue;
                }

                var points = geometry.Select(p => new SKPoint((float)p.X, (float)p.Y)).ToList();
                var extracted = useBanded
                    ? LineCropper.ExtractBanded(capture.ImageLocation, points)
                    : LineCropper.Extract(decoded!.Bitmap, points);
                try
                {
                    lineImages.Add((line.Id, ToLineImage(extracted.Bitmap, preprocessing), extracted.DerivativeChecksum));
                }
                finally
                {
                    extracted.Bitmap.Dispose();
                }
            }
        }
        finally
        {
            decoded?.Dispose();
        }

        var recognition = recognizer.Recognize([.. lineImages.Select(li => li.Image)]);
        for (int i = 0; i < lineImages.Count; i++)
        {
            var (lineId, _, cropChecksum) = lineImages[i];
            results.Add(new LineRecognition(lineId, recognition[i].Text, recognition[i].Graphemes, cropChecksum, $"crop>{preprocessing.Describe()}"));
        }

        if (persistL0)
        {
            // Raw recognition is kept as L0 revisions with model provenance — never merged
            // into L1 (greek-transcription.md §1; project-plan §2 revision separation).
            var modelAuthor = new AgentRef
            {
                Id = $"model:{options.ModelName}",
                IsModel = true,
                ModelIdentity = $"{options.ModelName}@{options.ModelVersion}",
            };
            var runId = options.RunId ?? Guid.NewGuid().ToString("N")[..10];
            var existing = surface.Revisions.Select(r => r.Id).ToHashSet();
            var revisionIds = AllocateRawRevisionIds(project, surface, results, runId);
            var l0Revisions = results.Select(line =>
            {
                var values = line.Confidence
                    .Where(c => !c.Confidence.IsNone)
                    .Select(c => c.Confidence.Value)
                    .ToList();
                var confidence = values.Count == 0
                    ? Confidence.None
                    : Confidence.Of(values.Average(), ConfidenceScale.RawModelProbability);

                return new TextRevision
                {
                    Id = revisionIds[line.LineId],
                    ScopeId = line.LineId,
                    Layer = TextLayer.L0RawRecognition,
                    Author = modelAuthor,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Text = line.L0Text,
                    Confidence = confidence,
                    GraphemeConfidences =
                    [
                        .. line.Confidence.Select(g => new GraphemeConfidenceSpan
                        {
                            Range = new ScalarRange(g.StartScalar, g.EndScalar),
                            Confidence = g.Confidence,
                        }),
                    ],
                };
            }).Where(r => !existing.Contains(r.Id)).ToList();

            if (l0Revisions.Count > 0)
            {
                var updatedSurface = surface with { Revisions = [.. surface.Revisions, .. l0Revisions] };
                var updatedProject = project with
                {
                    Objects = [.. project.Objects.Select(o => o.Surfaces.Any(s => s.Id == surfaceId)
                        ? o with { Surfaces = [.. o.Surfaces.Select(s => s.Id == surfaceId ? updatedSurface : s)] }
                        : o)],
                };
                updatedProject = updatedProject with { Version = _store.SaveProject(updatedProject) };
            }
        }

        stages.Add(new StageRun
        {
            Id = new EntityId(EntityKind.Job, Guid.NewGuid().ToString("N")[..12]),
            Stage = StageName.Recognition,
            ContractVersion = ContractVersion.Current,
            Inputs = [new StageArtifact(capture.Id, sourceChecksum)],
            Outputs = [.. lineImages.Select(li => new StageArtifact(li.Id, li.CropChecksum))],
            Model = modelRef,
            ConfigHash = options.ModelName,
            Status = StageStatus.Succeeded,
            LayoutSourceUsed = LayoutSource.Imported,
        });

        return new SurfaceOutcome(results, stages, skippedLines);
    }

    private static IReadOnlyDictionary<EntityId, EntityId> AllocateRawRevisionIds(
        NativeProject project, NativeSurface surface, IReadOnlyList<LineRecognition> lines, string runId)
    {
        var global = project.Objects.SelectMany(obj => obj.Surfaces).SelectMany(page => page.Revisions).ToLookup(revision => revision.Id);
        var local = surface.Revisions.ToLookup(revision => revision.Id);
        var assigned = new Dictionary<EntityId, EntityId>();
        var batch = new HashSet<EntityId>();
        foreach (var line in lines)
        {
            var suffix = line.LineId.Suffix;
            var legacySuffix = suffix.Length > 24 ? suffix[..24] : suffix;
            bool validLegacy = EntityId.TryParse($"rev-l0-{runId}-{legacySuffix}", EntityKind.TranscriptionRevision, out var legacy);
            var historical = validLegacy ? global[legacy].ToArray() : [];
            bool sameSource(TextRevision revision) => revision.ScopeId == line.LineId && revision.Layer == TextLayer.L0RawRecognition;
            // Preserve already persisted, unambiguous legacy long-line identities on retry.
            // Never silently reinterpret an old duplicate ID or another layer as this output.
            if (historical.Length > 1 || historical.Any(revision => revision.ScopeId == line.LineId && !sameSource(revision)))
                throw IdentityConflict(validLegacy ? legacy : line.LineId);
            bool reuseLegacy = historical.Length == 1 && sameSource(historical[0]) && local[legacy].Count() == 1;
            EntityId id;
            if (validLegacy && (suffix.Length <= 24 || reuseLegacy)) id = legacy;
            else
            {
                // Hash the complete pair, with unambiguous JSON boundaries. Base64url
                // retains all 256 bits while fitting the native 63-character suffix limit.
                var pair = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new[] { runId, line.LineId.ToString() });
                var digest = System.Security.Cryptography.SHA256.HashData(pair);
                var token = Convert.ToBase64String(digest).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                id = new(EntityKind.TranscriptionRevision, "l0h-" + token);
            }
            var occupied = global[id].ToArray();
            if (line.LineId.Kind != EntityKind.Line || occupied.Length > 1
                || occupied.Length == 1 && (!sameSource(occupied[0]) || local[id].Count() != 1)
                || !batch.Add(id) || !assigned.TryAdd(line.LineId, id))
                throw IdentityConflict(id);
        }
        return assigned;
    }

    private static LeviathanException IdentityConflict(EntityId id) => new(new LeviathanError
    {
        Code = ErrorCode.StageContractViolation, Scope = id.ToString(),
        Details = new Dictionary<string, string> { ["reason"] = "ambiguous-recognition-revision" },
        Message = "Raw recognition revision identity conflicts with native history; repair the registration before retrying.",
    });

    /// <summary>Stable label-set identity for model provenance (same convention as CheckpointStore).</summary>
    private static string LabelSetHash(LabelSet labels)
    {
        var joined = string.Join('\u001f', labels.Labels);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(joined))).ToLowerInvariant();
    }

    /// <summary>Resize/pad a page or line crop to the recognizer's fixed-height input.</summary>
    public static LineImage ToLineImage(SKBitmap crop)
        => ToLineImage(crop, LinePreprocessingProfile.LegacyInkPositive(CrnnModel.InputHeight));

    /// <summary>Apply the shared versioned line transform selected by a model package.</summary>
    public static LineImage ToLineImage(SKBitmap crop, LinePreprocessingProfile preprocessing)
        => LinePreprocessor.Apply(crop, preprocessing);
}
