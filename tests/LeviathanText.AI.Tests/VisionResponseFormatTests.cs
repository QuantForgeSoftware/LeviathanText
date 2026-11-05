using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeviathanText.AI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace LeviathanText.AI.Tests;

public sealed class VisionResponseFormatTests
{
    private const string Schema = """{"type":"json_schema","json_schema":{"name":"transcription_v1","strict":true,"schema":{"type":"object","properties":{"transcription":{"type":"string"}},"required":["transcription"],"additionalProperties":false}}}""";
    private static AiProviderSettings Settings() => new()
    {
        ProviderId = "schema-test", ModelId = "synthetic-vision", Host = AiProviderHost.Local,
        Protocol = AiWireProtocol.OpenAiChatCompletions, Endpoint = new Uri("http://127.0.0.1:1234/v1"),
        MaxAttempts = 1, Timeout = TimeSpan.FromSeconds(5), MaxOutputTokens = 256,
    };
    private static byte[] Png()
    {
        using var image = new Image<L8>(2, 1, new L8(0));
        using var buffer = new MemoryStream(); image.SaveAsPng(buffer); return buffer.ToArray();
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static VisionInferenceRequest Request(byte[] png) => new()
    {
        SampleId = "schema-01", Image = VisionImage.FromPng(png, Hash(png)),
        SystemPrompt = "Transcribe Greek.", UserPrompt = "Only the image.", PromptVersion = "schema-mock-v1",
    };
    private static string LegacyBody(byte[] png) => "{\"model\":\"synthetic-vision\",\"messages\":[{\"role\":\"system\",\"content\":\"Transcribe Greek.\"},{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":\"Only the image.\"},{\"type\":\"image_url\",\"image_url\":{\"url\":\"data:image/png;base64," +
        Convert.ToBase64String(png).Replace("+", "\\u002B", StringComparison.Ordinal) +
        "\"}}]}],\"temperature\":0,\"top_p\":1,\"stream\":true,\"max_tokens\":256,\"seed\":42}";
    private static string Wire(string text, string reason = "stop") => JsonSerializer.Serialize(new
    {
        model = "synthetic-vision", choices = new[] { new { index = 0, message = new { content = text }, finish_reason = reason } },
    });
    private static HttpResponseMessage Response(string wire, HttpStatusCode status = HttpStatusCode.OK, string mediaType = "application/json") => new(status)
    {
        Content = new StringContent(wire, Encoding.UTF8, mediaType),
    };

    [Fact]
    public async Task Default_and_explicit_plain_text_keep_the_exact_legacy_payload_bytes()
    {
        byte[] png = Png(); var request = Request(png);
        var handler = new CaptureHandler(() => Response(Wire("α")));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var implicitDefault = await provider.InferAsync(request);
        var explicitDefault = await provider.InferAsync(request with { ResponseFormat = VisionResponseFormat.PlainText });
        byte[] expected = Encoding.UTF8.GetBytes(LegacyBody(png));
        Assert.Equal(2, handler.Bodies.Count);
        Assert.All(handler.Bodies, body => Assert.Equal(expected, body));
        Assert.Equal(Hash(expected), implicitDefault.RequestSha256);
        Assert.Equal(implicitDefault.RequestSha256, explicitDefault.RequestSha256);
        Assert.Equal(expected.Length, implicitDefault.RequestBytes);
        Assert.DoesNotContain("response_format", Encoding.UTF8.GetString(expected));
    }

    [Fact]
    public async Task Opt_in_appends_the_fixed_strict_schema_and_hashes_the_exact_payload()
    {
        byte[] png = Png(); var request = Request(png);
        var handler = new CaptureHandler(() => Response(Wire("{\"transcription\":\"α\"}")));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var plain = await provider.InferAsync(request);
        var structured = await provider.InferAsync(request with { ResponseFormat = VisionResponseFormat.TranscriptionJsonV1 });
        string expected = LegacyBody(png)[..^1] + ",\"response_format\":" + Schema + "}";
        byte[] body = handler.Bodies[1];
        Assert.Equal(Encoding.UTF8.GetBytes(expected), body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(Schema, json.RootElement.GetProperty("response_format").GetRawText());
        Assert.Equal(Hash(body), structured.RequestSha256);
        Assert.Equal(body.Length, structured.RequestBytes);
        Assert.NotEqual(plain.RequestSha256, structured.RequestSha256);
        Assert.Equal(plain.PromptSha256, structured.PromptSha256);
        Assert.Equal(plain.ImageSha256, structured.ImageSha256);
    }

    [Fact]
    public async Task Unknown_format_is_rejected_before_encoding_or_http()
    {
        var handler = new CaptureHandler(() => Response(Wire("unexpected")));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request(Png()) with { ResponseFormat = (VisionResponseFormat)999 });
        Assert.Equal(VisionInferenceStatus.InputRejected, result.Status);
        Assert.Empty(handler.Bodies);
        Assert.Null(result.RequestSha256);
        Assert.Equal(0, result.RequestBytes);
    }

    [Theory]
    [InlineData("{\"transcription\":\"ἄ𐅀\"}")]
    [InlineData("{\"transcription\":\"\"}")]
    [InlineData("{\"text\":\"wrong key\"}")]
    [InlineData("{\"transcription\":17}")]
    [InlineData("{\"transcription\":\"x\",\"extra\":true}")]
    [InlineData("not JSON")]
    public async Task Transport_completion_preserves_model_content_without_schema_validation_or_extraction(string modelText)
    {
        string wire = Wire(modelText);
        var handler = new CaptureHandler(() => Response(wire));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request(Png()) with { ResponseFormat = VisionResponseFormat.TranscriptionJsonV1 });
        Assert.Equal(VisionInferenceStatus.Completed, result.Status);
        Assert.Equal(modelText, result.Text);
        Assert.Equal(wire, result.RawResponse);
        Assert.Equal(Hash(Encoding.UTF8.GetBytes(wire)), result.RawResponseSha256);
        Assert.True(result.RawResponseComplete);
        Assert.Single(handler.Bodies);
    }

    [Fact]
    public async Task Truncated_structured_content_retains_the_exact_partial_string()
    {
        const string partial = "{\"transcription\":\"ἀ";
        string wire = "data: " + JsonSerializer.Serialize(new { choices = new[] { new { index = 0, delta = new { content = partial }, finish_reason = "length" } } }) + "\n\ndata: [DONE]\n\n";
        var handler = new CaptureHandler(() => Response(wire, mediaType: "text/event-stream"));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request(Png()) with { ResponseFormat = VisionResponseFormat.TranscriptionJsonV1 });
        Assert.Equal(VisionInferenceStatus.Truncated, result.Status);
        Assert.Equal(partial, result.Text);
        Assert.Equal(wire, result.RawResponse);
        Assert.Equal("length", result.FinishReason);
    }

    [Fact]
    public async Task Server_rejection_is_retained_without_plain_text_fallback_or_retry()
    {
        const string wire = "{\"error\":\"unsupported response_format\"}";
        var handler = new CaptureHandler(() => Response(wire, HttpStatusCode.BadRequest));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request(Png()) with { ResponseFormat = VisionResponseFormat.TranscriptionJsonV1 });
        Assert.Equal(VisionInferenceStatus.HttpError, result.Status);
        Assert.Equal(400, result.HttpStatusCode);
        Assert.Equal(wire, result.RawResponse);
        Assert.Empty(result.Text);
        Assert.Single(handler.Bodies);
    }

    private sealed class CaptureHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<byte[]> Bodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
            return response();
        }
    }
}
