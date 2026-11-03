using LeviathanText.Application;
using LeviathanText.Contracts;

namespace LeviathanText.Application.Tests;

public sealed class CorpusOverlapTests
{
    [Fact]
    public void Repeated_external_refs_and_image_bytes_are_reported()
    {
        var project = SampleProjectBuilder.Build();
        var first = project.Objects[0] with
        {
            ExternalRefs = new Dictionary<string, string> { ["TM"] = "12345" },
        };
        var capture = first.Surfaces[0].Captures[0];
        var second = first with
        {
            Id = new EntityId(EntityKind.PhysicalObject, "obj-overlap"),
            Surfaces =
            [
                first.Surfaces[0] with
                {
                    Id = new EntityId(EntityKind.Surface, "surf-overlap"),
                    Captures = [capture with { Id = new EntityId(EntityKind.Capture, "cap-overlap") }],
                },
            ],
        };
        var withTwo = project with { Objects = [first, second] };

        var overlaps = CorpusOverlap.Audit(withTwo);

        Assert.Contains(overlaps, overlap => overlap.Kind == "external-ref");
        Assert.Contains(overlaps, overlap => overlap.Kind == "image-checksum");
        Assert.Contains("image-checksum", CorpusOverlap.ToJson(overlaps));
        Assert.Contains("kind,value,entities,note", CorpusOverlap.ToCsv(overlaps));
        Assert.Equal(2, withTwo.Objects.Count); // read-only
    }
}
