using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using LeviathanText.Application;

namespace LeviathanText.Application.Tests;

public sealed class ModelRegistryTests
{
    private const string Valid = """
        [
          {"id":"p01","name":"P01 model","task":"recognition","downloadUrl":"https://example.test/p01.zip","sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","packageFormatVersion":1}
        ]
        """;

    [Fact]
    public void Valid_registry_entry_is_parsed()
    {
        var index = ModelRegistry.Parse(Valid, "fixture");
        var entry = Assert.Single(index.Models);
        Assert.Equal("p01", entry.Id);
        Assert.Equal("fixture", index.Source);
    }

    [Fact]
    public void Registry_accepts_legacy_and_typed_preprocessing_packages_together()
    {
        var json = """
            [
              {"id":"legacy","name":"Legacy","task":"recognition","downloadUrl":"https://example.test/legacy.zip","packageFormatVersion":1},
              {"id":"typed","name":"Typed","task":"recognition","downloadUrl":"https://example.test/typed.zip","packageFormatVersion":2}
            ]
            """;
        Assert.Equal(new[] { 1, 2 }, ModelRegistry.Parse(json).Models.Select(entry => entry.PackageFormatVersion));
    }

    [Theory]
    [InlineData("[null]")]
    [InlineData("""[{"id":"../outside","name":"X","task":"recognition","downloadUrl":"https://example.test/x.zip"}]""")]
    [InlineData("""[{"id":"/outside","name":"X","task":"recognition","downloadUrl":"https://example.test/x.zip"}]""")]
    [InlineData("""[{"id":"C:\\outside","name":"X","task":"recognition","downloadUrl":"https://example.test/x.zip"}]""")]
    [InlineData("""[{"id":"x","name":"X","task":"wrong","downloadUrl":"https://example.test/x.zip"}]""")]
    [InlineData("""[{"id":"x","name":"X","task":"recognition","downloadUrl":"file:///tmp/x.zip"}]""")]
    [InlineData("""[{"id":"x","name":"X","task":"recognition","downloadUrl":"https://example.test/x.zip","sha256":"bad"}]""")]
    [InlineData("""[{"id":"x","name":"X","task":"recognition","downloadUrl":"https://example.test/x.zip","packageFormatVersion":99}]""")]
    [InlineData("""[{"id":"x","name":"X","task":"recognition","downloadUrl":"https://example.test/x.zip","packageFormatVersion":3}]""")]
    [InlineData("""[{"id":"x","name":"X","task":"recognition","downloadUrl":"https://example.test/x.zip"},{"id":"x","name":"Y","task":"recognition","downloadUrl":"https://example.test/y.zip"}]""")]
    public void Malformed_registry_entries_are_rejected(string json)
    {
        Assert.Throws<InvalidDataException>(() => ModelRegistry.Parse(json));
    }

    [Fact]
    public async Task Http_fetch_returns_parsed_index()
    {
        using var client = new HttpClient(new StubHandler(Valid));
        var index = await ModelRegistry.FetchAsync(client, new Uri("https://example.test/models.json"));
        Assert.Equal("p01", Assert.Single(index.Models).Id);
    }

    [Theory]
    [InlineData("/catalog/current.json")]
    [InlineData("https://example.test/catalog/current.json")]
    public async Task Fetch_follows_relative_and_absolute_http_redirects(string location)
    {
        var handler = new RedirectHandler(location, Encoding.UTF8.GetBytes(Valid));
        using var client = new HttpClient(handler);
        var index = await ModelRegistry.FetchAsync(client, new Uri("https://example.test/models.json"));
        Assert.Equal("p01", Assert.Single(index.Models).Id);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(new Uri("https://example.test/catalog/current.json"), handler.Requests[1]);
    }

    [Theory]
    [InlineData("https://untrusted.test/index.json")]
    [InlineData("//untrusted.test/index.json")]
    [InlineData("http://example.test/index.json")]
    [InlineData("file:///tmp/index.json")]
    public async Task Fetch_validates_redirect_destination_before_contacting_it(string location)
    {
        var handler = new RedirectHandler(location, Encoding.UTF8.GetBytes(Valid));
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => ModelRegistry.FetchAsync(
            client, new Uri("https://example.test/index.json"),
            trustPolicy: RegistryTrustPolicy.Allow("example.test")));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Fetch_validates_initial_url_before_contacting_it()
    {
        var handler = new RedirectHandler(null, Encoding.UTF8.GetBytes(Valid));
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => ModelRegistry.FetchAsync(
            client, new Uri("https://untrusted.test/index.json"),
            trustPolicy: RegistryTrustPolicy.Allow("example.test")));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Fetch_client_timeout_also_bounds_a_stalled_index_body()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new HttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StalledContent() })))
        {
            Timeout = TimeSpan.FromMilliseconds(100),
        };
        var operation = Record.ExceptionAsync(() => ModelRegistry.FetchAsync(
            client, new Uri("https://example.test/index.json"), cancellation.Token));
        try
        {
            var error = await operation.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsAssignableFrom<OperationCanceledException>(error);
        }
        finally
        {
            cancellation.Cancel();
            await operation;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registry_validates_every_redirect_hop(bool download)
    {
        var root = TemporaryDirectory();
        var bytes = Zip(("run-manifest.json", "{}"));
        var requests = new List<Uri>();
        using var client = new HttpClient(new DelegateHandler((request, _) =>
        {
            requests.Add(request.RequestUri!);
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri(requests.Count == 1 ? "/allowed-hop" : "https://untrusted.test/end", UriKind.RelativeOrAbsolute);
            return Task.FromResult(response);
        }));
        var policy = RegistryTrustPolicy.Allow("example.test");
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                if (download) await ModelRegistryDownload.DownloadAsync(client, Package("p01", bytes), root, policy);
                else await ModelRegistry.FetchAsync(client, new Uri("https://example.test/index.json"), trustPolicy: policy);
            });
            Assert.Equal(2, requests.Count);
            Assert.All(requests, uri => Assert.Equal("example.test", uri.Host));
            Assert.Empty(Directory.Exists(root) ? Directory.EnumerateFileSystemEntries(root) : []);
        }
        finally { DeleteDirectory(root); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registry_stops_after_five_redirects(bool download)
    {
        var root = TemporaryDirectory();
        var requests = 0;
        using var client = new HttpClient(new DelegateHandler((_, _) =>
        {
            requests++;
            var response = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
            response.Headers.Location = new Uri("/loop", UriKind.Relative);
            return Task.FromResult(response);
        }));
        var policy = RegistryTrustPolicy.Allow("example.test");
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                if (download) await ModelRegistryDownload.DownloadAsync(client, Package("p01", []), root, policy);
                else await ModelRegistry.FetchAsync(client, new Uri("https://example.test/index.json"), trustPolicy: policy);
            });
            Assert.Equal(6, requests);
            Assert.Empty(Directory.Exists(root) ? Directory.EnumerateFileSystemEntries(root) : []);
        }
        finally { DeleteDirectory(root); }
    }

    [Fact]
    public void Registry_index_requires_an_explicit_allowed_host_and_https()
    {
        var index = new Uri("https://models.example.test/index.json");
        Assert.Throws<InvalidOperationException>(() => RegistryTrustPolicy.DenyAll.ValidateIndex(index));

        var allow = RegistryTrustPolicy.Allow("example.test");
        Assert.Throws<InvalidDataException>(() => allow.ValidateIndex(index));

        var wildcard = RegistryTrustPolicy.Allow("*.example.test");
        wildcard.ValidateIndex(index);

        Assert.Throws<InvalidDataException>(() =>
            RegistryTrustPolicy.Allow("models.example.test")
                .ValidateIndex(new Uri("http://models.example.test/index.json")));
    }

    [Fact]
    public async Task Download_verifies_checksum_and_extracts_inside_target()
    {
        var bytes = Zip(("run-manifest.json", "{}"));
        var entry = new RemoteModelEntry("p01", "P01", "recognition",
            new Uri("https://example.test/p01.zip"),
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        var root = Path.Combine(Path.GetTempPath(), "lt-registry-tests", Guid.NewGuid().ToString("N"));
        try
        {
            using var client = new HttpClient(new BytesHandler(bytes));
            var result = await ModelRegistryDownload.DownloadAsync(client, entry, root);
            Assert.Equal("p01", result.Id);
            Assert.True(File.Exists(Path.Combine(result.Directory, "run-manifest.json")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("nested/model")]
    [InlineData("nested\\model")]
    [InlineData("C:\\outside")]
    [InlineData("C:/outside")]
    [InlineData("\\\\server\\share")]
    [InlineData("<absolute-unix>")]
    [InlineData("model.")]
    [InlineData("model ")]
    [InlineData("model:stream")]
    [InlineData("model\u0000")]
    [InlineData("CON")]
    [InlineData("nul.zip")]
    public async Task Download_rejects_unsafe_ids_before_filesystem_or_network_access(string id)
    {
        var parent = TemporaryDirectory();
        var root = Path.Combine(parent, "models");
        if (id == "<absolute-unix>") id = Path.Combine(parent, "absolute-outside");
        var bytes = Zip(("run-manifest.json", "{}"));
        var handler = new RedirectHandler(null, bytes);
        try
        {
            using var client = new HttpClient(handler);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                ModelRegistryDownload.DownloadAsync(client, Package(id, bytes), root));
            Assert.Empty(handler.Requests);
            Assert.False(Directory.Exists(root));
            Assert.False(Directory.Exists(Path.Combine(parent, "outside")));
            Assert.False(Directory.Exists(Path.Combine(parent, "absolute-outside")));
        }
        finally { DeleteDirectory(parent); }
    }

    [Theory]
    [InlineData("P46.Michigan-01_v2")]
    [InlineData("Ἑλληνικά-μοντέλο")]
    public async Task Download_preserves_valid_model_ids(string id)
    {
        var root = TemporaryDirectory();
        var bytes = Zip(("run-manifest.json", "{}"));
        try
        {
            using var client = new HttpClient(new BytesHandler(bytes));
            var result = await ModelRegistryDownload.DownloadAsync(client, Package(id, bytes), root);
            Assert.Equal(Path.Combine(root, id), result.Directory);
            Assert.Equal("{}", await File.ReadAllTextAsync(Path.Combine(result.Directory, "run-manifest.json")));
        }
        finally { DeleteDirectory(root); }
    }

    [Theory]
    [InlineData("https://untrusted.test/package.zip")]
    [InlineData("//untrusted.test/package.zip")]
    [InlineData("http://example.test/package.zip")]
    [InlineData("file:///tmp/package.zip")]
    public async Task Download_rejects_untrusted_redirect_before_contacting_target(string location)
    {
        var root = TemporaryDirectory();
        var bytes = Zip(("run-manifest.json", "{}"));
        var handler = new RedirectHandler(location, bytes);
        try
        {
            using var client = new HttpClient(handler);
            await Assert.ThrowsAsync<InvalidDataException>(() => ModelRegistryDownload.DownloadAsync(
                client, Package("p01", bytes), root, RegistryTrustPolicy.Allow("example.test")));
            Assert.Single(handler.Requests);
            Assert.False(Directory.Exists(Path.Combine(root, "p01")));
            Assert.Empty(Directory.Exists(root) ? Directory.EnumerateFileSystemEntries(root) : []);
        }
        finally { DeleteDirectory(root); }
    }

    [Theory]
    [InlineData("/releases/package.zip")]
    [InlineData("https://cdn.example.test/package.zip")]
    public async Task Download_follows_trusted_redirects_and_still_verifies_checksum(string location)
    {
        var root = TemporaryDirectory();
        var bytes = Zip(("run-manifest.json", "{}"));
        var policy = RegistryTrustPolicy.Allow("example.test", "cdn.example.test");
        try
        {
            using (var client = new HttpClient(new RedirectHandler(location, bytes)))
            {
                var result = await ModelRegistryDownload.DownloadAsync(client, Package("good", bytes), root, policy);
                Assert.Equal("{}", await File.ReadAllTextAsync(Path.Combine(result.Directory, "run-manifest.json")));
            }
            using (var client = new HttpClient(new RedirectHandler(location, bytes)))
            {
                var wrongDigest = Package("bad", bytes) with { Sha256 = new string('0', 64) };
                await Assert.ThrowsAsync<InvalidDataException>(() =>
                    ModelRegistryDownload.DownloadAsync(client, wrongDigest, root, policy));
                Assert.False(Directory.Exists(Path.Combine(root, "bad")));
            }
            Assert.Equal(new[] { "good" }, Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName));
        }
        finally { DeleteDirectory(root); }
    }

    [Fact]
    public async Task Concurrent_downloads_publish_only_complete_packages_and_preserve_the_winner()
    {
        var root = TemporaryDirectory();
        var bytes = Zip(("run-manifest.json", "complete"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        using var client = new HttpClient(new DelegateHandler(async (_, cancellation) =>
        {
            if (Interlocked.Increment(ref requestCount) == 1)
            {
                entered.SetResult();
                await release.Task.WaitAsync(cancellation);
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }));
        var entry = Package("p01", bytes);
        Task<Exception?>? first = null;
        Task<Exception?>? second = null;
        try
        {
            first = Record.ExceptionAsync(() => ModelRegistryDownload.DownloadAsync(client, entry, root));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var visibleBeforeCompletion = Directory.Exists(Path.Combine(root, "p01"));
            second = Record.ExceptionAsync(() => ModelRegistryDownload.DownloadAsync(client, entry, root));
            var secondError = await second.WaitAsync(TimeSpan.FromSeconds(10));
            release.TrySetResult();
            var firstError = await first.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.False(visibleBeforeCompletion);
            Assert.Null(secondError);
            Assert.IsType<IOException>(firstError);
            Assert.Equal("complete", await File.ReadAllTextAsync(Path.Combine(root, "p01", "run-manifest.json")));
            Assert.Equal(new[] { "p01" }, Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName));
        }
        finally
        {
            release.TrySetResult();
            if (first is not null) await first;
            if (second is not null) await second;
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Cancelled_download_cleans_staging_without_publishing_a_package()
    {
        var root = TemporaryDirectory();
        var bytes = Zip(("run-manifest.json", "{}"));
        using var cancellation = new CancellationTokenSource();
        using var client = new HttpClient(new DelegateHandler((_, _) =>
        {
            cancellation.Cancel();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }));
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ModelRegistryDownload.DownloadAsync(
                client, Package("p01", bytes), root, cancellation: cancellation.Token));
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally { DeleteDirectory(root); }
    }

    [Fact]
    public async Task Download_rejects_checksum_mismatch_and_archive_traversal()
    {
        var bytes = Zip(("run-manifest.json", "{}"));
        var bad = new RemoteModelEntry("p01", "P01", "recognition",
            new Uri("https://example.test/p01.zip"), new string('0', 64));
        var root = Path.Combine(Path.GetTempPath(), "lt-registry-tests", Guid.NewGuid().ToString("N"));
        try
        {
            using var client = new HttpClient(new BytesHandler(bytes));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                ModelRegistryDownload.DownloadAsync(client, bad, root));

            var traversalBytes = Zip(("../escape.txt", "no"));
            var traversal = bad with
            {
                Sha256 = Convert.ToHexString(SHA256.HashData(traversalBytes)).ToLowerInvariant(),
            };
            using var traversalClient = new HttpClient(new BytesHandler(traversalBytes));
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                ModelRegistryDownload.DownloadAsync(traversalClient, traversal, root));
            Assert.False(File.Exists(Path.Combine(root, "escape.txt")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string TemporaryDirectory() => Path.Combine(Path.GetTempPath(), "lt-registry-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private static RemoteModelEntry Package(string id, byte[] bytes) => new(
        id, "Model", "recognition", new Uri("https://example.test/package.zip"),
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());

    private sealed class RedirectHandler(string? location, byte[] body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var response = new HttpResponseMessage(location is not null && Requests.Count == 1
                ? HttpStatusCode.Found : HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
            if (location is not null && Requests.Count == 1)
                response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return Task.FromResult(response);
        }
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class StalledContent : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new NotSupportedException("The cancellable content path is required.");

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        return memory.ToArray();
    }

    private sealed class BytesHandler(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(body),
            });
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
