using System.Net;
using System.Security.Cryptography;
using LeviathanText.Application;
using LeviathanText.Contracts;
using LeviathanText.Formats;

namespace LeviathanText.Application.Tests;

public sealed class ImageResolverTests : IDisposable
{
    private readonly string _cache = Path.Combine(Path.GetTempPath(), $"lt-images-{Guid.NewGuid():N}");

    private static (NativeProject Project, EntityId SurfaceId, EntityId CaptureId) ProjectWithCapture(string location)
    {
        var captureId = new EntityId(EntityKind.Capture, "cap1");
        var surfaceId = new EntityId(EntityKind.Surface, "surf1");
        var project = new NativeProject
        {
            Id = new EntityId(EntityKind.Collection, "col1"),
            Name = "resolver",
            Objects =
            [
                new NativeObject
                {
                    Id = new EntityId(EntityKind.PhysicalObject, "obj1"),
                    Surfaces =
                    [
                        new NativeSurface
                        {
                            Id = surfaceId,
                            Kind = "inscription-face",
                            Captures = [new NativeCapture
                            {
                                Id = captureId,
                                Frame = new CoordinateFrame
                                {
                                    ImageRef = captureId,
                                    ImageChecksum = new Checksum { Algorithm = "unavailable", Digest = "pending" },
                                },
                                ImageLocation = location,
                            }],
                            Regions = [],
                            Lines = [],
                        },
                    ],
                },
            ],
        };
        return (project, surfaceId, captureId);
    }

    [Fact]
    public void Local_file_passes_through_unchanged()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Fixtures", "isic001151.manifest.json"); // any existing file
        var (project, surfaceId, _) = ProjectWithCapture(file);
        var store = new InMemoryStore();
        store.SaveProject(project);

        var result = new ImageResolver(store, _cache).EnsureLocal(project, surfaceId);
        Assert.Equal(file, result.Capture.ImageLocation);
        Assert.Equal("unavailable", result.Capture.Frame.ImageChecksum.Algorithm); // untouched
    }

    [Fact]
    public void Remote_image_is_cached_with_a_recorded_checksum()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4 };
        var handler = new StubHandler(bytes, "image/png");
        var client = new HttpClient(handler);
        var (project, surfaceId, captureId) = ProjectWithCapture("https://example.test/img.png");
        var store = new InMemoryStore();
        store.SaveProject(project);

        var result = new ImageResolver(store, _cache, client).EnsureLocal(project, surfaceId);

        var expectedDigest = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Assert.Contains(expectedDigest, result.Capture.ImageLocation);
        Assert.True(File.Exists(result.Capture.ImageLocation));
        Assert.Equal(expectedDigest, result.Capture.Frame.ImageChecksum.Digest);
        Assert.Equal("sha256", result.Capture.Frame.ImageChecksum.Algorithm);
        Assert.Equal(captureId, result.Capture.Id);

        // Persisted, and a second call is a local passthrough (no second fetch).
        var reloaded = store.LoadProject(project.Id)!;
        Assert.True(File.Exists(reloaded.Objects[0].Surfaces[0].Captures[0].ImageLocation));
        Assert.Equal(1, handler.Calls);

        new ImageResolver(store, _cache, client).EnsureLocal(reloaded, surfaceId);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void Non_http_scheme_is_refused()
    {
        var (project, surfaceId, _) = ProjectWithCapture("ftp://example.test/img.png");
        var store = new InMemoryStore();
        store.SaveProject(project);

        var ex = Assert.Throws<LeviathanException>(() => new ImageResolver(store, _cache).EnsureLocal(project, surfaceId));
        Assert.Equal(ErrorCode.InputMissing, ex.Error.Code);
        Assert.Contains("http(s)", ex.Error.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cache)) Directory.Delete(_cache, recursive: true);
    }

    private sealed class StubHandler(byte[] payload, string contentType) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            return Task.FromResult(response);
        }
    }
}
