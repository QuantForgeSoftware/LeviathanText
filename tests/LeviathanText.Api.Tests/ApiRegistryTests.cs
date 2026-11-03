using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LeviathanText.Api.Tests;

/// <summary>Exercise the real registry HTTP transport, including its redirect policy.</summary>
public sealed class ApiRegistryTests
{
    [Fact]
    public async Task Index_redirect_to_unapproved_host_is_rejected_before_request()
    {
        await using var server = new RegistryServer((request, origin) => request == "/index"
            ? Reply.Redirect(new Uri(origin, "/forbidden").ToString().Replace("localhost", "127.0.0.1"))
            : Reply.Json("[]"));
        using var fixture = new ApiFactory();
        using var factory = Configured(fixture, server.ModelRoot);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/models/registry", new { indexUrl = server.Url("/index") });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("stageContractViolation", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("/forbidden", server.Requests);
    }

    [Fact]
    public async Task Package_redirect_to_unapproved_host_is_rejected_before_request()
    {
        var archive = Package();
        await using var server = new RegistryServer((request, origin) => request switch
        {
            "/index" => Reply.Json(Catalog(new Uri(origin, "/package"), archive)),
            "/package" => Reply.Redirect(new Uri(origin, "/forbidden").ToString().Replace("localhost", "127.0.0.1")),
            _ => Reply.Bytes(archive),
        });
        using var fixture = new ApiFactory();
        using var factory = Configured(fixture, server.ModelRoot);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/models/registry/pull", new
        {
            indexUrl = server.Url("/index"), modelId = "synthetic",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.DoesNotContain("/forbidden", server.Requests);
        Assert.False(Directory.Exists(Path.Combine(server.ModelRoot, "synthetic")));
    }

    [Fact]
    public async Task Allowed_relative_index_and_package_redirects_preserve_download()
    {
        var archive = Package();
        await using var server = new RegistryServer((request, origin) => request switch
        {
            "/index" => Reply.Redirect("/catalog"),
            "/catalog" => Reply.Json(Catalog(new Uri(origin, "/package"), archive)),
            "/package" => Reply.Redirect("/archive"),
            _ => Reply.Bytes(archive),
        });
        using var fixture = new ApiFactory();
        using var factory = Configured(fixture, server.ModelRoot);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/models/registry/pull", new
        {
            indexUrl = server.Url("/index"), modelId = "synthetic",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("synthetic registry fixture", File.ReadAllText(Path.Combine(server.ModelRoot, "synthetic", "proof.txt")));
        Assert.Equal(["/index", "/catalog", "/package", "/archive"], server.Requests.ToArray());
    }

    [Fact]
    public async Task Registry_accepts_preprocessing_profile_package_format_two()
    {
        await using var server = new RegistryServer((_, origin) =>
            Reply.Json(Catalog(new Uri(origin, "/package"), Package(), version: 2)));
        using var fixture = new ApiFactory();
        using var factory = Configured(fixture, server.ModelRoot);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/models/registry", new { indexUrl = server.Url("/index") });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"packageFormatVersion\":2", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_registry_identifier_returns_contract_error_without_installing()
    {
        await using var server = new RegistryServer((_, origin) =>
            Reply.Json(Catalog(new Uri(origin, "/package"), Package(), id: "../escaped")));
        using var fixture = new ApiFactory();
        using var factory = Configured(fixture, server.ModelRoot);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/models/registry", new { indexUrl = server.Url("/index") });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("stageContractViolation", await response.Content.ReadAsStringAsync());
        Assert.False(Directory.Exists(server.ModelRoot));
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Configured(ApiFactory factory, string root) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LeviathanText:Models:Directory"] = root,
                ["LeviathanText:Models:RegistryAllowHosts"] = "localhost",
                ["LeviathanText:Models:RegistryRequireHttps"] = "false",
            })));

    private static string Catalog(Uri url, byte[] bytes, int version = 1, string id = "synthetic") =>
        JsonSerializer.Serialize(new[] { new
        {
            id, name = "Synthetic fixture", task = "recognition", downloadUrl = url,
            sha256 = Convert.ToHexString(SHA256.HashData(bytes)), packageFormatVersion = version,
        } });

    private static byte[] Package()
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(zip.CreateEntry("proof.txt").Open()))
            writer.Write("synthetic registry fixture");
        return output.ToArray();
    }

    private sealed record Reply(int Status, byte[] Body, string? Location = null)
    {
        public static Reply Json(string value) => new(200, Encoding.UTF8.GetBytes(value));
        public static Reply Bytes(byte[] value) => new(200, value);
        public static Reply Redirect(string location) => new(302, [], location);
    }

    private sealed class RegistryServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new(TimeSpan.FromSeconds(30));
        private readonly Task _serve;
        private readonly string _scratch = Path.Combine(Path.GetTempPath(), $"leviathan-registry-api-{Guid.NewGuid():N}");
        public ConcurrentQueue<string> Requests { get; } = new();
        public Uri Origin { get; }
        public string ModelRoot => Path.Combine(_scratch, "models");
        public string Url(string path) => new Uri(Origin, path).ToString();

        public RegistryServer(Func<string, Uri, Reply> route)
        {
            _listener.Start();
            Origin = new Uri($"http://localhost:{((IPEndPoint)_listener.LocalEndpoint).Port}");
            _serve = ServeAsync(route);
        }

        private async Task ServeAsync(Func<string, Uri, Reply> route)
        {
            while (!_stop.IsCancellationRequested)
            {
                using var connection = await _listener.AcceptTcpClientAsync(_stop.Token);
                await using var stream = connection.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var request = await reader.ReadLineAsync(_stop.Token);
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(_stop.Token))) { }
                var path = request!.Split(' ')[1];
                Requests.Enqueue(path);
                var response = route(path, Origin);
                var location = response.Location is null ? "" : $"Location: {response.Location}\r\n";
                var headers = $"HTTP/1.1 {response.Status} Fixture\r\n{location}Content-Type: application/json\r\nContent-Length: {response.Body.Length}\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), _stop.Token);
                await stream.WriteAsync(response.Body, _stop.Token);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            try { await _serve; }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (SocketException) when (_stop.IsCancellationRequested) { }
            _stop.Dispose();
            if (Directory.Exists(_scratch)) Directory.Delete(_scratch, recursive: true);
        }
    }
}
