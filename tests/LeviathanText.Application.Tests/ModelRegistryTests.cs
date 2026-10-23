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

    [Theory]
    [InlineData("""[{"id":"x","name":"X","task":"wrong","downloadUrl":"https://example.test/x.zip"}]""")]
    [InlineData("""[{"id":"x","name":"X","task":"recognition","downloadUrl":"file:///tmp/x.zip"}]""")]
    [InlineData("""[{"id":"x","name":"X","task":"recognition","downloadUrl":"https://example.test/x.zip","sha256":"bad"}]""")]
    [InlineData("""[{"id":"x","name":"X","task":"recognition","downloadUrl":"https://example.test/x.zip","packageFormatVersion":99}]""")]
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
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                ModelRegistryDownload.DownloadAsync(client, traversal, root));
            Assert.False(File.Exists(Path.Combine(root, "escape.txt")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
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
