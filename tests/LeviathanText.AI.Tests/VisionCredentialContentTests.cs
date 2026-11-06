using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeviathanText.AI;
using Xunit;

namespace LeviathanText.AI.Tests;

public sealed class VisionCredentialContentTests
{
    [Theory]
    [InlineData("literal")]
    [InlineData("wire-escape")]
    [InlineData("nested")]
    [InlineData("nested-sse")]
    [InlineData("nested-truncated")]
    [InlineData("nested-key")]
    [InlineData("greek-control")]
    [InlineData("escaped-greek-control")]
    [InlineData("mixed-literal-escaped")]
    public async Task Credential_echoes_in_one_structured_content_layer_cannot_be_recovered_offline(string kind)
    {
        const string secret = "token";
        string text = kind switch
        {
            "literal" or "wire-escape" => secret,
            "nested-truncated" => "{\"transcription\":\"\\u0074oken",
            "nested-key" => "{\"\\u0074oken\":\"α\"}",
            "greek-control" => "{\"transcription\":\"ἄ𐅀\"}",
            "escaped-greek-control" => "{\"transcription\":\"\\u03B1\\u0313\\u0301𐅀\"}",
            "mixed-literal-escaped" => "{\"transcription\":\"token \\u0074oken\"}",
            _ => "{\"transcription\":\"\\u0074oken\"}",
        };
        var finish = kind == "nested-truncated" ? "length" : "stop";
        string wire = JsonSerializer.Serialize(new { model = "mock", choices = new[] { new { message = new { content = text }, finish_reason = finish } } });
        if (kind == "wire-escape") wire = wire.Replace(secret, "\\u0074oken", StringComparison.Ordinal);
        if (kind == "nested-sse")
        {
            static string Delta(string content, string? reason) => "data: " + JsonSerializer.Serialize(new { choices = new[] { new { index = 0, delta = new { content }, finish_reason = reason } } }) + "\n\n";
            wire = Delta(text[..20], null) + Delta(text[20..], "stop") + "data: [DONE]\n\n";
        }
        using var image = new SkiaFixture(2, 1, (byte)255); using var png = new MemoryStream(); image.SaveAsPng(png); var bytes = png.ToArray();
        using var handler = new Handler(wire, kind == "nested-sse" ? "text/event-stream" : "application/json");
        using var provider = new VisionHttpProvider(new AiProviderSettings
        {
            ProviderId = "mock", ModelId = "mock", Host = AiProviderHost.Local, Protocol = AiWireProtocol.OpenAiChatCompletions,
            Endpoint = new Uri("http://127.0.0.1:1234/v1"), MaxAttempts = 1, CredentialEnvironmentVariable = "MOCK_TOKEN",
        }, handler, new StaticCredentialSource(new Dictionary<string, string> { ["MOCK_TOKEN"] = secret }));
        var result = await provider.InferAsync(new VisionInferenceRequest
        {
            SampleId = "mock", Image = VisionImage.FromPng(bytes, Hash(bytes)), SystemPrompt = "Only Greek.", UserPrompt = "Image only.",
            PromptVersion = "mock", ResponseFormat = VisionResponseFormat.TranscriptionJsonV1,
        });
        Assert.Equal(kind == "nested-truncated" ? VisionInferenceStatus.Truncated : VisionInferenceStatus.Completed, result.Status);
        Assert.Equal(Hash(Encoding.UTF8.GetBytes(wire)), result.RawResponseSha256);
        if (kind.EndsWith("-control", StringComparison.Ordinal))
        {
            Assert.False(result.CredentialsRedacted); Assert.Equal(text, result.Text); Assert.Equal(wire, result.RawResponse);
        }
        else
        {
            Assert.True(result.CredentialsRedacted);
            Assert.DoesNotContain(secret, result.Text); Assert.DoesNotContain("\\u0074oken", result.Text);
            Assert.DoesNotContain(secret, result.RawResponse); Assert.DoesNotContain("\\u0074oken", result.RawResponse);
        }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private sealed class Handler(string wire, string mediaType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(wire, Encoding.UTF8, mediaType) });
    }
}
