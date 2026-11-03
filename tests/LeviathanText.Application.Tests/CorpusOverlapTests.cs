using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

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

    [Fact]
    public void Repeated_fragment_refs_are_reported()
    {
        var project = SampleProjectBuilder.Build();
        var obj = project.Objects[0];
        var surface = obj.Surfaces[0];
        NativeFragment Fragment(string id) => new()
        {
            Id = new EntityId(EntityKind.Fragment, id),
            ExternalRefs = new Dictionary<string, string> { ["TM"] = "12345" },
            SurfaceIds = [surface.Id],
        };
        var modified = project with
        {
            Objects =
            [
                obj with { Fragments = [Fragment("frag-a"), Fragment("frag-b")] },
            ],
        };

        var overlaps = CorpusOverlap.Audit(modified);

        Assert.Contains(overlaps, overlap => overlap.Kind == "fragment-ref" && overlap.Value == "TM=12345");
    }

    [Fact]
    public void Cross_surface_source_line_ids_are_reported()
    {
        var project = SampleProjectBuilder.Build();
        var obj = project.Objects[0];
        var surface = obj.Surfaces[0];
        var line = surface.Lines[0];
        IReadOnlyDictionary<string, IReadOnlyList<string>> tags =
            new Dictionary<string, IReadOnlyList<string>> { ["source-id"] = ["same-line"] };
        var first = line with { Tags = tags };
        var second = line with { Id = new EntityId(EntityKind.Line, "ln-overlap"), Tags = tags };
        var secondSurface = surface with
        {
            Id = new EntityId(EntityKind.Surface, "surf-overlap"),
            Lines = [second],
        };
        var withTwo = project with
        {
            Objects =
            [
                obj with { Surfaces = [surface with { Lines = [first] }, secondSurface] },
            ],
        };

        var overlaps = CorpusOverlap.Audit(withTwo);

        Assert.Contains(overlaps, overlap => overlap.Kind == "line-source-id" && overlap.Value == "same-line");
    }
}
