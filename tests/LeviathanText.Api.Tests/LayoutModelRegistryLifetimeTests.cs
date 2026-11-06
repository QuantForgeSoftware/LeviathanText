using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using LeviathanText.Layout;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TorchSharp;
using static TorchSharp.torch;

namespace LeviathanText.Api.Tests;

/// <summary>Generated random CNN parameters; no optimizer or fit.</summary>
public sealed class LayoutModelRegistryLifetimeTests
{
    [Fact]
    public void Container_shutdown_disposes_the_registered_singleton_models()
    {
        var directory = Directory.CreateTempSubdirectory("lt-api-layout-owner-").FullName;
        try
        {
            Package(directory);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["LeviathanText:Layout:Models:*"] = directory }).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config); services.AddLogging();
            services.AddSingleton<LayoutModelRegistry>();
            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<LayoutModelRegistry>();
            var borrowed = registry.Resolve("manuscript-page"); Assert.NotNull(borrowed);
            Assert.Equal(.5f, borrowed.Threshold);
            provider.Dispose(); provider.Dispose();
            registry.Dispose();
            Assert.Throws<ObjectDisposedException>(() => borrowed.Threshold);
            Assert.Throws<ObjectDisposedException>(() => registry.Resolve("*"));
            Assert.Throws<ObjectDisposedException>(() => registry.IsEmpty);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Duplicate_keys_and_failed_registry_construction_release_every_owned_model(bool fail)
    {
        var directory = Directory.CreateTempSubdirectory("lt-api-layout-owner-").FullName;
        try
        {
            Package(directory);
            using var scope = NewDisposeScope();
            long baseline = DisposeScopeManager.Statistics.ThreadTotalLiveCount;
            var children = new[] { new Section("page", directory), new Section("PAGE", directory) };
            var config = new Section("Models", null, children);
            if (fail)
                Assert.Throws<InvalidOperationException>(() => new LayoutModelRegistry(config, new FailingLogger()));
            else
            {
                using var registry = new LayoutModelRegistry(config,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<LayoutModelRegistry>.Instance);
                Assert.NotNull(registry.Resolve("page"));
            }
            Assert.Equal(baseline, DisposeScopeManager.Statistics.ThreadTotalLiveCount);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class Section(string key, string? value, IConfigurationSection[]? children = null) : IConfigurationSection
    {
        public string Key => key;
        public string Path => key;
        public string? Value { get; set; } = value;
        public string? this[string name] { get => null; set => throw new NotSupportedException(); }
        public IEnumerable<IConfigurationSection> GetChildren() => children ?? [];
        public IConfigurationSection GetSection(string name) => this;
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() =>
            new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None);
    }

    private sealed class FailingLogger : ILogger<LayoutModelRegistry>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error,
            Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("Generated startup failure");
    }

    private static void Package(string directory)
    {
                using (var scope = NewDisposeScope())
                using (var network = (nn.Module<Tensor, Tensor>)Activator.CreateInstance(
                           typeof(PatchLayoutModel).GetNestedType("PatchCnn", BindingFlags.NonPublic)!, nonPublic: true)!)
                {
                    var weights = Path.Combine(directory, "model.pt"); network.save(weights);
                    File.WriteAllText(Path.Combine(directory, "layout-metadata.json"), JsonSerializer.Serialize(new
                    {
                        Architecture = PatchLayoutModel.Architecture, InputHeight = 64, MinInputWidth = 64,
                        MaxInputWidth = 64, Threshold = .5, MinBandBins = 2, CoreFraction = .5, ExpansionFactor = 1.6,
                        WeightsDigest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(weights)))
                    }));
                }
    }

}
