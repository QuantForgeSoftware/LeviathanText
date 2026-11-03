using LeviathanText.Recognition;
using LeviathanText.Training;
using TorchSharp;
using Xunit;
using static TorchSharp.torch;

namespace LeviathanText.ML.Tests;

public sealed class ModelLoadLifetimeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lt-model-load-lifetime", Guid.NewGuid().ToString("N"));
    private static readonly LabelSet Labels = new(["α", "β"]);
    private static readonly LineImage Image = new(16, 32, new float[512]);

    private string Package(bool? truncate)
    {
        string directory = Path.Combine(_root, truncate?.ToString() ?? "valid");
        Directory.CreateDirectory(directory);
        string path = CheckpointStore.ModelPath(directory, "test", ".best");
        using (var fixture = new CrnnModel(Labels.NumClasses, hiddenSize: 8)) fixture.save(path);
        if (truncate is not null)
        {
            byte[] bytes = File.ReadAllBytes(path);
            File.WriteAllBytes(path, truncate.Value ? bytes[..(bytes.Length / 2)] : []);
        }
        CheckpointStore.WriteMetadata(directory, "test", new CheckpointMetadata
        {
            Epoch = 1, GlobalStep = 1, BestValidationCer = 1, LabelSetHash = CheckpointStore.LabelSetHash(Labels),
            Labels = Labels.Labels, ConfigHash = "synthetic", HiddenSize = 8, InputHeightPixels = 16,
            WidthDownsampleFactor = 4, TorchVersion = __version__, CreatedAt = DateTimeOffset.UnixEpoch,
        }, ".best");
        TrainingRunManifestWriter.Write(directory, new TrainingRunManifest
        {
            ModelName = "test", ConfigHash = "synthetic", NumClasses = Labels.NumClasses, HiddenSize = 8,
            WidthDownsampleFactor = 4, InputHeightPixels = 16, Dropout = 0, AugmentTraining = false,
            WeightDecay = 0, TrainLines = 1, DevLines = 1, TestLockedLines = 0, SplitHash = "synthetic",
            LabelSetHash = CheckpointStore.LabelSetHash(Labels), CheckpointDigest = TrainingRunManifestWriter.FileDigest(path),
            BestValidationCer = 1, EpochsCompleted = 1, CreatedAt = DateTimeOffset.UnixEpoch,
        });
        return directory;
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private static EndOfStreamException DirectLibraryFailure(string path)
    {
        using var scope = NewDisposeScope();
        using var model = new CrnnModel(Labels.NumClasses, hiddenSize: 8);
        return Assert.Throws<EndOfStreamException>(() => model.load(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Digest_valid_unreadable_weights_release_the_owned_model_and_keep_original_failure(bool truncated)
    {
        set_num_threads(1);
        string package = Package(truncated);
        var expectedFailure = DirectLibraryFailure(CheckpointStore.ModelPath(package, "test", ".best"));
        using var outer = NewDisposeScope();
        using var callerModel = new CrnnModel(Labels.NumClasses, hiddenSize: 8);
        using var callerTensor = ones(2);
        var recognizer = new TorchLineRecognizer(callerModel, Labels);
        var expected = recognizer.Recognize([Image])[0];
        Collect();
        long baseline = DisposeScopeManager.Statistics.ThreadTotalLiveCount;
        for (int i = 0; i < 5; i++)
        {
            var error = Assert.Throws<EndOfStreamException>(() => RecognitionModelLoader.Load(package));
            Assert.Equal(expectedFailure.Message, error.Message);
            var actual = recognizer.Recognize([Image])[0];
            Assert.Equal(expected.Text, actual.Text);
            Assert.Equal(expected.Graphemes, actual.Graphemes);
            // TotalCount double-counts moved Parameter handles in this runtime;
            // scope statistics track their replacement and actual disposal.
            Assert.Equal(baseline, DisposeScopeManager.Statistics.ThreadTotalLiveCount);
        }
        using var sum = callerTensor.sum();
        Assert.Equal(2, sum.item<float>());
        Assert.True(callerModel.training);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Successfully_loaded_model_remains_caller_owned_after_loader_scope_returns(bool nested)
    {
        set_num_threads(1);
        string package = Package(truncate: null);
        using var outer = nested ? NewDisposeScope() : null;
        using var callerTensor = ones(2);
        int previousOuterCount = outer?.DisposablesCount ?? 0;
        var loaded = RecognitionModelLoader.Load(package);
        using var model = loaded.Model;
        if (outer is not null)
        {
            Assert.True(outer.DisposablesCount > previousOuterCount);
            Assert.True(outer.Contains(callerTensor));
        }
        Assert.False(model.training);
        var expected = loaded.CreateRecognizer().Recognize([Image])[0];
        var expectedWeights = model.parameters().SelectMany(p => p.data<float>().ToArray()).ToArray();
        Collect();
        for (int i = 0; i < 3; i++)
        {
            var actual = loaded.CreateRecognizer().Recognize([Image])[0];
            Assert.Equal(expected.Text, actual.Text);
            Assert.Equal(expected.Graphemes, actual.Graphemes);
            Assert.Equal(expectedWeights, model.parameters().SelectMany(p => p.data<float>().ToArray()).ToArray());
        }
        string saved = Path.Combine(_root, "returned-model.model");
        model.save(saved);
        Assert.Equal(File.ReadAllBytes(CheckpointStore.ModelPath(package, "test", ".best")), File.ReadAllBytes(saved));
        using var sum = callerTensor.sum();
        Assert.Equal(2, sum.item<float>());
    }

    [Fact]
    public void Lifetime_statistics_balance_parameter_handle_transfer_and_explicit_disposal()
    {
        long baseline = DisposeScopeManager.Statistics.ThreadTotalLiveCount;
        var data = ones(2);
        var parameter = nn.Parameter(data);
        var handle = typeof(Tensor).GetField("handle", System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!;
        Assert.Equal(IntPtr.Zero, handle.GetValue(data)); // Ownership moved, not copied.
        data.Dispose();
        parameter.Dispose();
        Assert.Equal(IntPtr.Zero, handle.GetValue(parameter));
        Assert.Equal(baseline, DisposeScopeManager.Statistics.ThreadTotalLiveCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
