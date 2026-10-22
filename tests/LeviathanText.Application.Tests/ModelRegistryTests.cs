using System.Net;
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

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
