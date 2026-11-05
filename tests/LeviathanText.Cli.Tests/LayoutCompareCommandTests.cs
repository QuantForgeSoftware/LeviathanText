using System.Security.Cryptography;
using System.Text.Json;

namespace LeviathanText.Cli.Tests;

public sealed class LayoutCompareCommandTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("lt-layout-compare-");

    [Theory]
    [InlineData("center-distance", "2", "numPred")]
    [InlineData("vertical-iou", "0.5", "detectedLines")]
    public void Compares_same_frame_with_explicit_metric_and_preserves_store(string metric, string threshold, string predictedCount)
    {
        var truth = Write("truth.xml", "capture.png", Line("truth", 10));
        var prediction = Write("prediction.xml", "capture.png", Line("near", 11) + Line("extra", 40));
        var store = Path.Combine(_root.FullName, "untouched.db");
        using var writer = new StringWriter();
        var original = Console.Out;
        try
        {
            Console.SetOut(writer);
            Assert.Equal(0, Program.Main(["--store", store, "layout", "compare", truth, prediction,
                "--metric", metric, "--threshold", threshold]));
        }
        finally { Console.SetOut(original); }

        Assert.False(File.Exists(store));
        using var report = JsonDocument.Parse(writer.ToString());
        var root = report.RootElement;
        Assert.Equal(metric, root.GetProperty("metric").GetString());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(truth))), root.GetProperty("truthSha256").GetString());
        Assert.Equal("capture.png", root.GetProperty("frame").GetProperty("imageFilename").GetString());
        var result = root.GetProperty("result");
        Assert.Equal(2, result.GetProperty(predictedCount).GetInt32());
        Assert.Equal(0.5, result.GetProperty("precision").GetDouble());
        Assert.Equal(1, result.GetProperty("recall").GetDouble());
    }

    [Fact]
    public void Rejects_mismatched_frames_and_unplaced_lines()
    {
        var truth = Write("truth.xml", "capture.png", Line("truth", 10));
        var mismatch = Write("other.xml", "elsewhere.png", Line("other", 10));
        var unplaced = Write("unplaced.xml", "capture.png", """<TextLine id="unplaced"/>""");

        Assert.Equal(1, Program.Main(["layout", "compare", truth, mismatch, "--metric", "vertical-iou", "--threshold", "0.5"]));
        Assert.Equal(1, Program.Main(["layout", "compare", truth, unplaced, "--metric", "center-distance", "--threshold", "2"]));
    }

    [Fact]
    public void Rejects_unscored_nested_lines_and_discarded_coordinate_tokens()
    {
        var empty = Write("empty.xml", "capture.png", "");
        var nested = Write("nested.xml", "capture.png", $"<TextRegion id=\"nested\">{Line("inside", 10)}</TextRegion>");
        var malformed = Write("malformed.xml", "capture.png", Line("bad", 10).Replace("2,10", "2,10 broken"));

        Assert.Equal(1, Program.Main(["layout", "compare", nested, empty, "--metric", "center-distance", "--threshold", "2"]));
        Assert.Equal(1, Program.Main(["layout", "compare", malformed, empty, "--metric", "vertical-iou", "--threshold", "0.5"]));
    }

    private string Write(string name, string imageName, string lines)
    {
        var path = Path.Combine(_root.FullName, name);
        File.WriteAllText(path, $"""
            <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
              <Page imageFilename="{imageName}" imageWidth="100" imageHeight="100">
                <TextRegion id="region"><Coords points="0,0 99,0 99,99 0,99"/>{lines}</TextRegion>
              </Page>
            </PcGts>
            """);
        return path;
    }

    private static string Line(string id, int top) =>
        $"""<TextLine id="{id}"><Coords points="2,{top} 40,{top} 40,{top + 8} 2,{top + 8}"/></TextLine>""";

    public void Dispose() => _root.Delete(recursive: true);
}
