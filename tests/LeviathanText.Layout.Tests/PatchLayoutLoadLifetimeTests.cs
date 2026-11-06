using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TorchSharp;
using static TorchSharp.torch;

namespace LeviathanText.Layout.Tests;

/// <summary>Random initial weights are an engineering fixture; no optimizer or fitting runs.</summary>
public sealed class PatchLayoutLoadLifetimeTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lt-layout-load-").FullName;

    [Theory]
    [InlineData("missing")]
    [InlineData("digest")]
    public void Rejected_weights_do_not_allocate_model_parameters(string condition)
    {
        var directory = Path.Combine(_directory, condition);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "layout-metadata.json"), Metadata("incorrect"));
        if (condition == "digest") File.WriteAllBytes(Path.Combine(directory, "model.pt"), [1, 2, 3]);
        using var scope = NewDisposeScope();
        long baseline = DisposeScopeManager.Statistics.ThreadTotalLiveCount;

        var error = Assert.Throws<InvalidDataException>(() => PatchLayoutModel.Load(directory));

        Assert.Contains(condition == "missing" ? "not found" : "digest", error.Message);
        Assert.Equal(baseline, DisposeScopeManager.Statistics.ThreadTotalLiveCount);
        Assert.Equal(0, scope.DisposablesCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Digest_valid_unreadable_weights_dispose_failed_model_and_preserve_caller_scope(bool truncated)
    {
        set_num_threads(1);
        string package = Package();
        string weights = Path.Combine(package, "model.pt");
        byte[] valid = File.ReadAllBytes(weights);
        File.WriteAllBytes(weights, truncated ? valid[..(valid.Length / 2)] : []);
        File.WriteAllText(Path.Combine(package, "layout-metadata.json"), Metadata(Digest(weights)));
        using var scope = NewDisposeScope();
        using var caller = ones(2);
        long baseline = DisposeScopeManager.Statistics.ThreadTotalLiveCount;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            Assert.Throws<EndOfStreamException>(() => PatchLayoutModel.Load(package));
            Assert.Equal(baseline, DisposeScopeManager.Statistics.ThreadTotalLiveCount);
            Assert.True(scope.Contains(caller));
        }
        using var sum = caller.sum();
        Assert.Equal(2, sum.item<float>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Successful_load_stays_usable_and_keeps_exact_weights_after_return(bool nested)
    {
        set_num_threads(1);
        string package = Package();
        using var scope = nested ? NewDisposeScope() : null;
        using var loaded = PatchLayoutModel.Load(package);
        var network = Network(loaded);
        Assert.False(network.training);
        using var page = new SKBitmap(64, 64);
        page.Erase(SKColors.White);
        using (var inferenceScope = NewDisposeScope())
        {
            var expected = loaded.PredictRowProbabilities(page);
            Assert.NotEmpty(expected);
            Assert.Equal(expected, loaded.PredictRowProbabilities(page));
        }
        string saved = Path.Combine(_directory, "saved.pt");
        network.save(saved);
        Assert.Equal(File.ReadAllBytes(Path.Combine(package, "model.pt")), File.ReadAllBytes(saved));
    }

    private string Package()
    {
        var directory = Path.Combine(_directory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        // The production private CNN defines the fixture architecture. Reflection
        // avoids exposing an untrained-model factory as a production feature.
        var cnn = typeof(PatchLayoutModel).GetNestedType("PatchCnn", BindingFlags.NonPublic)!;
        using var scope = NewDisposeScope();
        using var network = (nn.Module<Tensor, Tensor>)Activator.CreateInstance(cnn, nonPublic: true)!;
        string weights = Path.Combine(directory, "model.pt");
        network.save(weights);
        File.WriteAllText(Path.Combine(directory, "layout-metadata.json"), Metadata(Digest(weights)));
        return directory;
    }

    private static nn.Module<Tensor, Tensor> Network(PatchLayoutModel model) =>
        (nn.Module<Tensor, Tensor>)typeof(PatchLayoutModel)
            .GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(model)!;

    private static string Digest(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string Metadata(string digest) => JsonSerializer.Serialize(new
    {
        Architecture = PatchLayoutModel.Architecture, InputHeight = 64,
        MinInputWidth = 64, MaxInputWidth = 64, Threshold = .5,
        MinBandBins = 2, CoreFraction = .5, ExpansionFactor = 1.6, WeightsDigest = digest,
    });

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
