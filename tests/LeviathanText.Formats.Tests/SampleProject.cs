using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Formats.Tests;

/// <summary>Shared builders for native-project test data (invented fixtures only).</summary>
public static class SampleProject
{
    public static readonly Checksum Sum = new() { Algorithm = "sha256", Digest = new string('a', 64) };
    public static readonly AgentRef Agent = new() { Id = "annotator", IsModel = false };

    public static NativeProject Build()
    {
        var surfaceId = new EntityId(EntityKind.Surface, "s1");
        var regionId = new EntityId(EntityKind.Region, "reg1");
        var lineId = new EntityId(EntityKind.Line, "ln1");
        var captureId = new EntityId(EntityKind.Capture, "cap1");

        var revision = new TextRevision
        {
            Id = new EntityId(EntityKind.TranscriptionRevision, "r1"),
            ScopeId = lineId,
            Layer = TextLayer.L1Diplomatic,
            Author = Agent,
            CreatedAt = new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero),
            Text = "α\u0305λ\u0305α\u0305νθρωπ", // overbarred abbreviation, no word division
        };

        var annotations = new List<Annotation>
        {
            new()
            {
                Id = new EntityId(EntityKind.Annotation, "overbar"),
                Kind = AnnotationKind.Sign,
                AnchoredRevisionId = revision.Id,
                Range = new ScalarRange(0, 3),
                Description = "overbar-abbreviation",
            },
            new()
            {
                Id = new EntityId(EntityKind.Annotation, "gap1"),
                Kind = AnnotationKind.Lost,
                AnchoredRevisionId = revision.Id,
                Range = new ScalarRange(0, 0),
                GapExtent = GapExtent.Unknown,
            },
            new()
            {
                Id = new EntityId(EntityKind.Annotation, "alt1"),
                Kind = AnnotationKind.AlternativeReading,
                AnchoredRevisionId = revision.Id,
                Range = new ScalarRange(3, 4),
                AlternativeText = "µ",
            },
        };

        return new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "c1"),
            Name = "fixture-project",
            Objects =
            [
                new NativeObject
                {
                    Id = new EntityId(EntityKind.PhysicalObject, "o1"),
                    ExternalRefs = new Dictionary<string, string> { ["TM"] = "12345" },
                    Track = MaterialTrack.Papyrus,
                    Metadata = new Dictionary<string, string> { ["language"] = "Greek, Koine", ["material"] = "papyrus" },
                    Surfaces =
                    [
                        new NativeSurface
                        {
                            Id = surfaceId,
                            Kind = "papyrus-side",
                            Captures =
                            [
                                new NativeCapture
                                {
                                    Id = captureId,
                                    Frame = new CoordinateFrame { ImageRef = captureId, ImageChecksum = Sum },
                                    ImageLocation = "fixture-papyrus-01.jpg",
                                    WidthPx = 4000,
                                    HeightPx = 3000,
                                },
                            ],
                            Regions =
                            [
                                new NativeRegion
                                {
                                    Id = regionId,
                                    FrameRef = captureId,
                                    Polygon = [new PointD(10, 10), new PointD(200, 10), new PointD(200, 60), new PointD(10, 60)],
                                    LayoutSource = LayoutSource.Manual,
                                    ReadingOrderIndex = 0,
                                },
                            ],
                            Lines =
                            [
                                new NativeLine
                                {
                                    Id = lineId,
                                    RegionId = regionId,
                                    FrameRef = captureId,
                                    Baseline = [new PointD(10, 38), new PointD(200, 36)],
                                    DiplomaticRevisionId = revision.Id,
                                },
                            ],
                            Revisions = [revision],
                            Annotations = annotations,
                        },
                    ],
                },
            ],
        };
    }
}
