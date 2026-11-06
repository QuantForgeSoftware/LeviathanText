using LeviathanText.TestSupport;
using System.Text.Json;
using LeviathanText.Training;
using LeviathanText.Recognition;
using SkiaSharp;

namespace LeviathanText.Cli.Tests;

public sealed class TrainingDiagnosticsCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-cli-telemetry", Guid.NewGuid().ToString("N"));

    public TrainingDiagnosticsCommandTests() => Directory.CreateDirectory(_root);

    [FittingTheory]
    [InlineData("--diagnostics-every", "-1")]
    [InlineData("--diagnostics-every", "oops")]
    [InlineData("--diagnostics-samples", "101")]
    public void Invalid_diagnostic_options_fail_before_loading_data(string name, string value)
    {
        Assert.Equal(1, LeviathanText.Cli.Program.Main(["--store", Path.Combine(_root, "store.db"),
            "train", "lines", Path.Combine(_root, "does-not-exist"), name, value]));
    }

    [FittingFact]
    public void Train_command_packages_requested_diagnostics()
    {
        TorchSharp.torch.set_num_threads(1);
        var dataDirectory = Path.Combine(_root, "data");
        var package = Path.Combine(_root, "package");
        Directory.CreateDirectory(dataDirectory);
        for (int i = 0; i < 3; i++)
        {
            using var bitmap = new SKBitmap(80, 32);
            bitmap.Erase(SKColors.White);
            using (var canvas = new SKCanvas(bitmap))
            using (var paint = new SKPaint { Color = SKColors.Black })
                canvas.DrawRect(new SKRect(15, 8, 60, 24), paint);
            using var encoded = bitmap.Encode(SKEncodedImageFormat.Jpeg, 100);
            using (var stream = File.Create(Path.Combine(dataDirectory, $"{i}.jpg"))) encoded.SaveTo(stream);
            File.WriteAllText(Path.Combine(dataDirectory, $"{i}.xml"), $"""
                <PcGts xmlns="http://schema.primaresearch.org/PAGE/gts/pagecontent/2019-07-15">
                  <Page imageFilename="{i}.jpg" imageWidth="80" imageHeight="32">
                    <TextRegion id="r"><Coords points="0,0 79,0 79,31 0,31"/>
                      <TextLine id="l"><Coords points="8,6 68,6 68,26 8,26"/>
                        <TextEquiv><Unicode>α</Unicode></TextEquiv>
                      </TextLine>
                    </TextRegion>
                  </Page>
                </PcGts>
                """);
        }
        int exitCode = LeviathanText.Cli.Program.Main(["--store", Path.Combine(_root, "store.db"), "train", "lines",
            dataDirectory, "--out", package, "--epochs", "1", "--diagnostics-every", "5", "--diagnostics-samples", "1"]);
        Assert.Equal(0, exitCode);
        var manifest = TrainingRunManifestWriter.Read(Path.Combine(package, "run-manifest.json"));
        Assert.Equal(LinePreprocessingProfile.P01Otsu(), manifest.PreprocessingProfile);
        var loaded = RecognitionModelLoader.Load(package);
        using var model = loaded.Model;
        Assert.Equal(manifest.PreprocessingProfile, loaded.DatasetPreprocessingProfile);
        Assert.Equal(5, manifest.DiagnosticsEveryNEpochs);
        Assert.Equal(1, manifest.DiagnosticsSampleCount);
        var epoch = Assert.Single(manifest.History);
        Assert.Equal(1, epoch.GlobalStep);
        Assert.Single(epoch.TrainingEvaluation!.Diagnostics.Samples);
        Assert.Single(epoch.ValidationEvaluation!.Diagnostics.Samples);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(package, "run-manifest.json")));
        Assert.True(json.RootElement.GetProperty("History")[0].TryGetProperty("ValidationEvaluation", out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
