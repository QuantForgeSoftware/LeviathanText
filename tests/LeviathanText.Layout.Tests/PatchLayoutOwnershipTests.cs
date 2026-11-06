using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TorchSharp;
using static TorchSharp.torch;

namespace LeviathanText.Layout.Tests;

/// <summary>Random initial parameters only: no optimizer, fitting, or source corpus.</summary>
public sealed class PatchLayoutOwnershipTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lt-layout-owner-").FullName;

    [Fact]
    public void Loaded_model_survives_the_callers_dispose_scope()
    {
        set_num_threads(1);
        var package = Package();
        PatchLayoutModel model;
        using (var caller = NewDisposeScope()) model = PatchLayoutModel.Load(package);
        try
        {
            using var page = Page();
            Assert.NotEmpty(model.PredictRowProbabilities(page));
        }
        finally
        {
            model.Dispose();
        }
    }

    [Fact]
    public void Public_model_exposes_deterministic_disposal()
    {
        var model = PatchLayoutModel.Load(Package());
        try { Assert.IsAssignableFrom<IDisposable>(model).Dispose(); }
        finally { model.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Aliases_release_native_parameters_only_after_the_last_wrapper(bool reverse)
    {
        set_num_threads(1);
        string package = Package();
        using var scope = NewDisposeScope();
        long baseline = DisposeScopeManager.Statistics.ThreadTotalLiveCount;
        var original = PatchLayoutModel.Load(package);
        var alias = original.WithThreshold(.9f);
        using var page = Page();
        using (var inference = NewDisposeScope())
        {
            var expected = original.PredictRowProbabilities(page);
            Assert.Equal(expected, alias.PredictRowProbabilities(page));
            Assert.Equal(original.Detect(page).ParametersHash, alias.Detect(page).ParametersHash);
        }
        Assert.Equal(.5f, original.Threshold); Assert.Equal(.9f, alias.Threshold);
        long retained = DisposeScopeManager.Statistics.ThreadTotalLiveCount;
        Assert.True(retained > baseline);
        var first = reverse ? alias : original; var last = reverse ? original : alias;
        first.Dispose(); first.Dispose();
        Assert.Equal(retained, DisposeScopeManager.Statistics.ThreadTotalLiveCount);
        Assert.Throws<ObjectDisposedException>(() => first.PredictRowProbabilities(page));
        Assert.Throws<ObjectDisposedException>(() => first.WithThreshold(.1f));
        Assert.Throws<ObjectDisposedException>(() => first.Options);
        using (var inference = NewDisposeScope()) Assert.NotEmpty(last.PredictRowProbabilities(page));
        var saved = Path.Combine(_directory, "saved-" + reverse);
        last.Save(saved);
        Assert.Equal(File.ReadAllBytes(Path.Combine(package, "model.pt")), File.ReadAllBytes(Path.Combine(saved, "model.pt")));
        last.Dispose(); last.Dispose();
        Assert.Equal(baseline, DisposeScopeManager.Statistics.ThreadTotalLiveCount);
        Assert.Throws<ObjectDisposedException>(() => last.Save(Path.Combine(_directory, "disposed")));
        Assert.False(Directory.Exists(Path.Combine(_directory, "disposed")));
        Assert.Throws<ObjectDisposedException>(() => last.Detect(page));
        Assert.Throws<ObjectDisposedException>(() => last.Detect("missing.png"));
        Assert.Throws<ObjectDisposedException>(() => last.DetectFromProbabilities(page, [], .5f));
    }

    [Fact]
    public void Repeated_owned_loads_release_all_construction_resources_and_preserve_caller_tensor()
    {
        string package = Package();
        using var scope = NewDisposeScope();
        using var caller = ones(2);
        long baseline = DisposeScopeManager.Statistics.ThreadTotalLiveCount;
        for (int i = 0; i < 8; i++)
        {
            using (var model = PatchLayoutModel.Load(package)) Assert.Equal(.5f, model.Threshold);
            Assert.Equal(baseline, DisposeScopeManager.Statistics.ThreadTotalLiveCount);
            Assert.True(scope.Contains(caller));
        }
        using var sum = caller.sum(); Assert.Equal(2, sum.item<float>());
    }

    [Fact]
    public void Owned_untrained_constructor_survives_its_private_scope_without_retained_temporaries()
    {
        using var scope = NewDisposeScope();
        long baseline = DisposeScopeManager.Statistics.ThreadTotalLiveCount;
        var factory = typeof(PatchLayoutModel).GetMethod("CreateOwnedNetwork", BindingFlags.Static | BindingFlags.NonPublic)!;
        using (var module = (nn.Module<Tensor, Tensor>)factory.Invoke(null, null)!)
        {
            using var inference = NewDisposeScope();
            using var noGrad = no_grad();
            using var input = zeros(1, 1, 64, 64);
            using var output = module.forward(input);
            Assert.NotEmpty(output.shape);
            module.save(Path.Combine(_directory, "untrained.pt"));
        }
        Assert.Equal(baseline, DisposeScopeManager.Statistics.ThreadTotalLiveCount);
    }

    [Fact]
    public async Task Final_disposal_waits_for_the_shared_operation_gate()
    {
        using var model = PatchLayoutModel.Load(Package());
        var owner = typeof(PatchLayoutModel).GetField("_owner", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(model)!;
        var gate = owner.GetType().GetField("Gate")!.GetValue(owner)!;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var operation = Task.Run(() =>
        {
            lock (gate)
            {
                entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
                using var page = Page();
                Assert.NotEmpty(model.PredictRowProbabilities(page));
                model.Save(Path.Combine(_directory, "admitted-save"));
            }
        });
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        using var disposeEntered = new ManualResetEventSlim();
        var dispose = Task.Run(() => { disposeEntered.Set(); model.Dispose(); });
        try
        {
            Assert.True(disposeEntered.Wait(TimeSpan.FromSeconds(10)));
            await Task.Delay(100);
            Assert.False(dispose.IsCompleted);
        }
        finally { release.Set(); }
        await Task.WhenAll(operation, dispose).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Throws<ObjectDisposedException>(() => model.Threshold);
    }

    [Fact]
    public async Task Concurrent_public_operations_and_final_disposal_complete_or_report_disposed()
    {
        set_num_threads(1);
        string package = Package();
        for (int trial = 0; trial < 8; trial++)
        {
            using var model = PatchLayoutModel.Load(package);
            using var barrier = new Barrier(3);
            string save = Path.Combine(_directory, "concurrent-" + trial);
            var predict = Task.Run(() =>
            {
                using var page = Page(); barrier.SignalAndWait(TimeSpan.FromSeconds(10));
                try { Assert.NotEmpty(model.PredictRowProbabilities(page)); }
                catch (ObjectDisposedException) { }
            });
            var saving = Task.Run(() =>
            {
                barrier.SignalAndWait(TimeSpan.FromSeconds(10));
                try { model.Save(save); Assert.True(File.Exists(Path.Combine(save, "layout-metadata.json"))); }
                catch (ObjectDisposedException) { Assert.False(Directory.Exists(save)); }
            });
            barrier.SignalAndWait(TimeSpan.FromSeconds(10)); model.Dispose();
            await Task.WhenAll(predict, saving).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private string Package()
    {
        var package = Path.Combine(_directory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(package);
        var cnn = typeof(PatchLayoutModel).GetNestedType("PatchCnn", BindingFlags.NonPublic)!;
        using var scope = NewDisposeScope();
        using var module = (nn.Module<Tensor, Tensor>)Activator.CreateInstance(cnn, nonPublic: true)!;
        string weights = Path.Combine(package, "model.pt"); module.save(weights);
        File.WriteAllText(Path.Combine(package, "layout-metadata.json"), JsonSerializer.Serialize(new
        {
            Architecture = PatchLayoutModel.Architecture, InputHeight = 64, MinInputWidth = 64,
            MaxInputWidth = 64, Threshold = .5, MinBandBins = 2, CoreFraction = .5,
            ExpansionFactor = 1.6, WeightsDigest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(weights)))
        }));
        return package;
    }

    private static SKBitmap Page() { var page = new SKBitmap(64, 64); page.Erase(SKColors.White); return page; }
    private static nn.Module<Tensor, Tensor> Network(PatchLayoutModel model) =>
        (nn.Module<Tensor, Tensor>)typeof(PatchLayoutModel).GetField("_model", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(model)!;
    public void Dispose() => Directory.Delete(_directory, true);
}
