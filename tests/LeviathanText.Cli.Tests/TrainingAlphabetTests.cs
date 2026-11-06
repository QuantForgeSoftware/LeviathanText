using LeviathanText.TestSupport;
using LeviathanText.Training;
using SkiaSharp;

namespace LeviathanText.Cli.Tests;

/// <summary>Synthetic PAGE fixtures exercise split leakage; they are not Greek ground truth.</summary>
public sealed class TrainingAlphabetTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-train-alphabet", Guid.NewGuid().ToString("N"));

    private string Data(bool unknownDev)
    {
        string directory = Path.Combine(_root, "data");
        Directory.CreateDirectory(directory);
        for (int i = 0; i < 3; i++)
        {
            using var bitmap = new SKBitmap(96, 64);
            bitmap.Erase(SKColors.White);
            using (var canvas = new SKCanvas(bitmap))
            using (var paint = new SKPaint { Color = SKColors.Black })
                canvas.DrawRect(new SKRect(12, 10, 80, 52), paint);
            using var encoded = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100);
            using (var stream = File.Create(Path.Combine(directory, $"{i}.jpg"))) encoded.SaveTo(stream);
            string first = i == 0 ? "ζ" : i == 1 ? (unknownDev ? "αχχ" : "α") : "βα";
            string second = i == 1 && unknownDev ? """
                <TextLine id="d2"><Coords points="8,34 86,34 86,56 8,56"/>
                  <TextEquiv><Unicode>άβ</Unicode></TextEquiv>
                </TextLine>
                """ : "";
            File.WriteAllText(Path.Combine(directory, $"{i}.xml"), $"""
                <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
                  <Page imageFilename="{i}.jpg" imageWidth="96" imageHeight="64">
                    <TextRegion id="r"><Coords points="0,0 95,0 95,63 0,63"/>
                      <TextLine id="d1"><Coords points="8,8 86,8 86,30 8,30"/>
                        <TextEquiv><Unicode>{first}</Unicode></TextEquiv>
                      </TextLine>
                      {second}
                    </TextRegion>
                  </Page>
                </PcGts>
                """);
        }
        return directory;
    }

    private int Run(string data, string package) => LeviathanText.Cli.Program.Main(
        ["--store", Path.Combine(_root, "store.db"), "train", "lines", data, "--out", package, "--epochs", "1"]);

    [FittingFact]
    public void Development_only_graphemes_fail_before_training_without_expanding_or_dropping_labels()
    {
        TorchSharp.torch.set_num_threads(1);
        string data = Data(unknownDev: true), package = Path.Combine(_root, "rejected-package");
        var originalError = Console.Error;
        using var error = new StringWriter();
        try
        {
            Console.SetError(error);
            Assert.Equal(1, Run(data, package));
        }
        finally { Console.SetError(originalError); }
        Assert.False(Directory.Exists(package));
        string message = error.ToString();
        Assert.Contains("development OOV: 2 line(s), 3 occurrence(s), 2 distinct grapheme(s)", message);
        Assert.Contains("1.xml#d1", message);
        Assert.Contains("1.xml#d2", message);
        Assert.Contains("U+03C7", message);
        Assert.Contains("U+03B1 U+0301", message);
        Assert.DoesNotContain("0.xml", message); // Locked labels do not define the alphabet.
    }

    [FittingFact]
    public void Compatible_development_keeps_training_order_and_excludes_locked_only_labels()
    {
        TorchSharp.torch.set_num_threads(1);
        string data = Data(unknownDev: false), package = Path.Combine(_root, "package");
        Assert.Equal(0, Run(data, package));
        var manifest = TrainingRunManifestWriter.Read(Path.Combine(package, "run-manifest.json"));
        var metadata = CheckpointStore.ReadMetadata(package, manifest.ModelName, ".best");
        Assert.Equal(new[] { "β", "α" }, metadata.Labels);
        Assert.Equal(3, metadata.Labels.Count + 1);
        Assert.Equal(1, manifest.TrainLines);
        Assert.Equal(1, manifest.DevLines);
        Assert.Null(manifest.TestLockedLines);
        Assert.Equal(DatasetPartitionAccess.TrainDev, manifest.LoadedPartitions);
    }

    [FittingFact]
    public void PAGE_sources_keep_distinct_document_and_line_ids_without_changing_old_line_JSON()
    {
        string data = Data(unknownDev: true);
        var snapshot = P01LineDataset.Load(data);
        Assert.Equal("2.xml#d1", Assert.Single(snapshot.Train).SourceId);
        Assert.Equal(new[] { "1.xml#d1", "1.xml#d2" }, snapshot.Dev.Select(line => line.SourceId));
        Assert.Equal("0.xml#d1", Assert.Single(snapshot.TestLocked).SourceId);
        var original = snapshot.Dev[0];
        Assert.Equal(original.SourceId, (original with { Text = "β" }).SourceId);
        string json = System.Text.Json.JsonSerializer.Serialize(original);
        Assert.Equal(original.SourceId, System.Text.Json.JsonSerializer.Deserialize<LabeledLine>(json)!.SourceId);
        var oldLine = new LabeledLine(original.Text, original.Image);
        string legacyJson = System.Text.Json.JsonSerializer.Serialize(oldLine);
        Assert.DoesNotContain("SourceId", legacyJson);
        Assert.Null(System.Text.Json.JsonSerializer.Deserialize<LabeledLine>(legacyJson)!.SourceId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
