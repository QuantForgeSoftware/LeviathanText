using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

/// <summary>Minimal invented native-project fixture for application-layer tests.</summary>
public static class SampleProjectBuilder
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
            CreatedAt = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero),
            Text = "α\u0305λ\u0305α\u0305νθρωπ",
        };

        return new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "c1"),
            Name = "application-fixture",
            Objects =
            [
                new NativeObject
                {
                    Id = new EntityId(EntityKind.PhysicalObject, "o1"),
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
                        },
                    ],
                },
            ],
        };
    }
}
