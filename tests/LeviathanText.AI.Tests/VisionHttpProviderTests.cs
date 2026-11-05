using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeviathanText.AI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace LeviathanText.AI.Tests;

public sealed class VisionHttpProviderTests
{
    private static AiProviderSettings Settings() => new()
    {
        ProviderId = "lm-studio-test", ModelId = "synthetic-vision", Host = AiProviderHost.Local,
        Protocol = AiWireProtocol.OpenAiChatCompletions, Endpoint = new Uri("http://127.0.0.1:1234/v1"),
        MaxAttempts = 1, Timeout = TimeSpan.FromSeconds(5), MaxOutputTokens = 1024,
    };

    private static byte[] Png(byte value = 255, int width = 16, int height = 8)
    {
        using var image = new Image<L8>(width, height, new L8(value));
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static VisionInferenceRequest Request(byte value = 255)
    {
        var bytes = Png(value);
        return new()
        {
            SampleId = "opaque-01", Image = VisionImage.FromPng(bytes, Hash(bytes)),
            SystemPrompt = "Return only visible characters; empty for a blank image.",
            UserPrompt = "Read α\u0313\u0301𐅀 without normalization.", PromptVersion = "synthetic-v1",
        };
    }
    private static HttpResponseMessage Json(string text, string reason = "stop") => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = "synthetic-vision", choices = new[] { new { index = 0, message = new { content = text }, finish_reason = reason } },
            usage = new { prompt_tokens = 31, completion_tokens = 4 },
        }), Encoding.UTF8, "application/json"),
    };
    private static string Event(string text, string? finish = null) => "data: " + JsonSerializer.Serialize(new
    {
        choices = new[] { new { index = 0, delta = new { content = text }, finish_reason = finish } },
    }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n\n";
    private static HttpResponseMessage Sse(Stream stream) => new(HttpStatusCode.OK)
    {
        Content = new StreamContent(stream) { Headers = { ContentType = new MediaTypeHeaderValue("text/event-stream") } },
    };

    [Fact]
    public async Task Standalone_request_sends_exact_immutable_image_and_unicode_without_revision_or_sample_metadata()
    {
        var bytes = Png(0);
        var expected = bytes.ToArray();
        var image = VisionImage.FromPng(bytes, Hash(bytes).ToUpperInvariant());
        Array.Fill(bytes, (byte)9);
        var handler = new Handler((_, _) => Task.FromResult(Json("α\u0313\u0301𐅀")));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var request = Request() with { Image = image };
        var result = await provider.InferAsync(request);

        Assert.Equal(VisionInferenceStatus.Completed, result.Status);
        Assert.Equal("α\u0313\u0301𐅀", result.Text);
        Assert.Equal(Hash(expected), result.ImageSha256);
        Assert.Equal(16, image.Width);
        Assert.Equal(8, image.Height);
        Assert.Equal(expected.Length, image.EncodedBytes);
        Assert.Equal("http://127.0.0.1:1234/v1/chat/completions", Assert.Single(handler.Uris).AbsoluteUri);
        var body = Assert.Single(handler.Bodies);
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.True(root.GetProperty("stream").GetBoolean());
        Assert.Equal(0, root.GetProperty("temperature").GetInt32());
        Assert.Equal(1, root.GetProperty("top_p").GetInt32());
        Assert.Equal(42, root.GetProperty("seed").GetInt32());
        Assert.Equal(1024, root.GetProperty("max_tokens").GetInt32());
        var content = root.GetProperty("messages")[1].GetProperty("content");
        Assert.Equal(request.UserPrompt, content[0].GetProperty("text").GetString());
        Assert.Equal("data:image/png;base64," + Convert.ToBase64String(expected), content[1].GetProperty("image_url").GetProperty("url").GetString());
        Assert.DoesNotContain(request.SampleId, body);
        Assert.DoesNotContain("revision", body);
        Assert.Equal(Hash(Encoding.UTF8.GetBytes(body)), result.RequestSha256);
        Assert.Equal(Encoding.UTF8.GetByteCount(body), result.RequestBytes);
        Assert.Equal(31, result.InputTokens);
        Assert.Equal(4, result.OutputTokens);
        Assert.True(result.RawResponseComplete);
        Assert.NotNull(result.FirstByteMilliseconds);
    }

    [Fact]
    public async Task Contrasting_and_blank_fixtures_reach_transport_as_distinct_pinned_pixels()
    {
        var handler = new Handler((_, _) => Task.FromResult(Json("")));
        using var provider = new VisionHttpProvider(Settings(), handler);
        foreach (byte tone in new byte[] { 0, 127, 255 }) await provider.InferAsync(Request(tone));
        Assert.Equal(3, handler.Bodies.Distinct().Count());
        var observed = new List<byte>();
        foreach (var body in handler.Bodies)
        {
            using var json = JsonDocument.Parse(body);
            var uri = json.RootElement.GetProperty("messages")[1].GetProperty("content")[1].GetProperty("image_url").GetProperty("url").GetString()!;
            using var image = Image.Load<L8>(Convert.FromBase64String(uri["data:image/png;base64,".Length..]));
            observed.Add(image[0, 0].PackedValue);
        }
        Assert.Equal(new byte[] { 0, 127, 255 }, observed);
    }

    [Fact]
    public void Stale_corrupt_oversized_and_non_png_inputs_are_rejected_before_any_request()
    {
        var bytes = Png();
        Assert.Throws<InvalidDataException>(() => VisionImage.FromPng(bytes, new string('0', 64)));
        Assert.Throws<InvalidDataException>(() => VisionImage.FromPng(bytes, "not-a-hash"));
        Assert.Throws<InvalidDataException>(() => VisionImage.FromPng([], Hash([])));
        var oversized = new byte[VisionImage.MaxEncodedBytes + 1];
        Assert.Throws<InvalidDataException>(() => VisionImage.FromPng(oversized, Hash(oversized)));
        var tooManyPixels = Png(width: 2001, height: 2000);
        Assert.Throws<InvalidDataException>(() => VisionImage.FromPng(tooManyPixels, Hash(tooManyPixels)));
        var broken = bytes[..35];
        Assert.Throws<InvalidDataException>(() => VisionImage.FromPng(broken, Hash(broken)));
        using var image = new Image<L8>(4, 4);
        using var jpeg = new MemoryStream();
        image.SaveAsJpeg(jpeg);
        Assert.Throws<InvalidDataException>(() => VisionImage.FromPng(jpeg.ToArray(), Hash(jpeg.ToArray())));
    }

    [Fact]
    public async Task Successful_blank_completion_is_distinct_from_an_empty_failed_attempt()
    {
        var handler = new Handler((_, _) => Task.FromResult(Json("")));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.Completed, result.Status);
        Assert.Equal("", result.Text);
        Assert.Equal("stop", result.FinishReason);
    }

    [Fact]
    public async Task LmStudio_mistral_empty_delta_stop_and_done_is_a_completed_blank()
    {
        // Exact retained LM Studio Mistral setup-05 wire response, 2026-11-04.
        // The blank emits no content field and no role; both are optional in this SSE chunk.
        const string raw = "data: {\"id\":\"chatcmpl-cgda8ebb14liwg7fwkzar9\",\"object\":\"chat.completion.chunk\",\"created\":1793802452,\"model\":\"lt-vlm-mistral-24b\",\"system_fingerprint\":\"lt-vlm-mistral-24b\",\"choices\":[{\"index\":0,\"delta\":{},\"logprobs\":null,\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
        var handler = new Handler((_, _) => Task.FromResult(Sse(new MemoryStream(Encoding.UTF8.GetBytes(raw)))));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.Completed, result.Status);
        Assert.Equal("", result.Text);
        Assert.Equal(raw, result.RawResponse);
        Assert.Equal("stop", result.FinishReason);
        Assert.True(result.RawResponseComplete);
    }

    [Theory]
    [InlineData("data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n", true)]
    [InlineData("data: {\"choices\":[{\"delta\":{},\"finish_reason\":null}]}\n\ndata: [DONE]\n\n", true)]
    [InlineData("data: {\"choices\":[{\"delta\":{\"tool_calls\":[]},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n", true)]
    [InlineData("data: malformed\n\ndata: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n", true)]
    [InlineData("{\"choices\":[{\"message\":{},\"finish_reason\":\"stop\"}]}", false)]
    public async Task Empty_output_still_requires_valid_transport_and_completion_markers(string raw, bool sse)
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(raw, Encoding.UTF8, sse ? "text/event-stream" : "application/json") }));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.InvalidOutput, result.Status);
        Assert.Equal("", result.Text);
    }

    [Fact]
    public async Task Sse_preserves_unicode_across_one_byte_network_chunks_and_records_token_timing()
    {
        var raw = Event("α\u0313\u0301") + Event("𐅀", "stop") + "data: [DONE]\n\n";
        var handler = new Handler((_, _) => Task.FromResult(Sse(new ChunkStream(Encoding.UTF8.GetBytes(raw), 1))));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.Completed, result.Status);
        Assert.Equal("α\u0313\u0301𐅀", result.Text);
        Assert.Equal(raw, result.RawResponse);
        Assert.NotNull(result.FirstTokenMilliseconds);
        Assert.True(result.RawResponseComplete);
    }

    [Theory]
    [InlineData("")]
    [InlineData("This is prose: κς")]
    public async Task Token_limit_retains_all_emitted_text(string text)
    {
        var handler = new Handler((_, _) => Task.FromResult(Json(text, "length")));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.Truncated, result.Status);
        Assert.Equal(text, result.Text);
        Assert.Single(handler.Bodies);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Timeout_or_cancellation_after_headers_retains_streamed_model_text(bool cancel)
    {
        var partial = Event("ἀβ");
        var handler = new Handler((_, _) => Task.FromResult(Sse(new ChunkStream(Encoding.UTF8.GetBytes(partial), 8192, stall: true))));
        using var provider = new VisionHttpProvider(Settings() with { Timeout = TimeSpan.FromMilliseconds(cancel ? 3000 : 100) }, handler);
        using var cts = new CancellationTokenSource();
        if (cancel) cts.CancelAfter(100);
        var result = await provider.InferAsync(Request(), cts.Token);
        Assert.Equal(cancel ? VisionInferenceStatus.Cancelled : VisionInferenceStatus.Timeout, result.Status);
        Assert.Equal("ἀβ", result.Text);
        Assert.Equal(partial, result.RawResponse);
        Assert.False(result.RawResponseComplete);
        Assert.Single(handler.Bodies);
    }

    [Fact]
    public async Task Connection_failure_after_content_retains_partial_result()
    {
        var handler = new Handler((_, _) => Task.FromResult(Sse(new ChunkStream(Encoding.UTF8.GetBytes(Event("λόγ")), 8192, fail: true))));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.TransportError, result.Status);
        Assert.Equal("λόγ", result.Text);
        Assert.False(result.RawResponseComplete);
    }

    [Fact]
    public async Task Torn_json_keeps_complete_model_content_tokens_and_raw_suffix()
    {
        const string raw = "{\"choices\":[{\"message\":{\"content\":\"λογος\"},";
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(raw, Encoding.UTF8, "application/json") }));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.InvalidOutput, result.Status);
        Assert.Equal("λογος", result.Text);
        Assert.Equal(raw, result.RawResponse);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Torn_string_retains_decodable_model_prefix_without_completing_an_unfinished_escape(bool sse)
    {
        string raw = (sse ? "data: " : "") + "{\"choices\":[{\"" + (sse ? "delta" : "message") + "\":{\"content\":\"Prose α\\u03b2\\uD800";
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(raw, Encoding.UTF8, sse ? "text/event-stream" : "application/json") }));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.InvalidOutput, result.Status);
        Assert.Equal("Prose αβ", result.Text);
        Assert.Equal(raw, result.RawResponse);
    }

    [Fact]
    public async Task Invalid_utf8_keeps_prior_model_events_from_the_same_network_chunk()
    {
        var bytes = Encoding.UTF8.GetBytes(Event("αβ")).Concat(new byte[] { 0xff }).ToArray();
        var handler = new Handler((_, _) => Task.FromResult(Sse(new ChunkStream(bytes, 8192))));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.InvalidOutput, result.Status);
        Assert.Equal("αβ", result.Text);
        Assert.Equal(bytes.Length, result.CapturedResponseBytes);
    }

    [Theory]
    [InlineData("\\uD800")]
    [InlineData("\\uDC00")]
    public async Task Invalid_escaped_unicode_is_a_recorded_failure_with_its_valid_prefix(string invalidEscape)
    {
        var raw = "{\"choices\":[{\"message\":{\"content\":\"α" + invalidEscape + "\"},\"finish_reason\":\"stop\"}]}";
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(raw, Encoding.UTF8, "application/json") }));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.InvalidOutput, result.Status);
        Assert.Equal("α", result.Text);
        Assert.Equal(raw, result.RawResponse);
    }

    [Fact]
    public async Task Invalid_utf8_inside_json_is_a_recorded_failure_with_the_valid_content_prefix()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"choices\":[{\"message\":{\"content\":\"α").Concat(new byte[] { 0xff })
            .Concat(Encoding.UTF8.GetBytes("\"},\"finish_reason\":\"stop\"}]}")).ToArray();
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new ByteArrayContent(bytes) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } } }));
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.InvalidOutput, result.Status);
        Assert.Equal("α", result.Text);
        Assert.Equal(Hash(bytes), result.RawResponseSha256);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Json_escaped_credentials_are_redacted_in_complete_and_torn_wire_bodies(bool torn)
    {
        const string secret = "token";
        var raw = "{\"choices\":[{\"message\":{\"content\":\"\\u0074oken" + (torn ? "" : "\"},\"finish_reason\":\"stop\"}]}");
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(raw, Encoding.UTF8, "application/json") }));
        using var provider = new VisionHttpProvider(Settings() with { CredentialEnvironmentVariable = "TEST_KEY" }, handler,
            new StaticCredentialSource(new Dictionary<string, string> { ["TEST_KEY"] = secret }));
        var result = await provider.InferAsync(Request());
        Assert.True(result.CredentialsRedacted);
        Assert.Equal("[redacted]", result.Text);
        Assert.DoesNotContain("\\u0074oken", result.RawResponse);
        Assert.DoesNotContain(secret, result.RawResponse);
    }

    [Fact]
    public async Task Sse_missing_done_or_with_malformed_event_is_not_successful()
    {
        foreach (var raw in new[] { Event("α", "stop"), Event("α") + "data: broken\n\n" + Event("β", "stop") + "data: [DONE]\n\n" })
        {
            var handler = new Handler((_, _) => Task.FromResult(Sse(new MemoryStream(Encoding.UTF8.GetBytes(raw)))));
            using var provider = new VisionHttpProvider(Settings(), handler);
            var result = await provider.InferAsync(Request());
            Assert.Equal(VisionInferenceStatus.InvalidOutput, result.Status);
            Assert.StartsWith("α", result.Text);
        }
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(307)]
    public async Task Http_failures_are_one_attempt_and_error_bodies_never_become_model_text(int status)
    {
        var handler = new Handler((_, _) =>
        {
            var response = Json("diagnostic-not-model-output");
            response.StatusCode = (HttpStatusCode)status;
            response.Headers.Location = new Uri("https://example.invalid/fallback");
            return Task.FromResult(response);
        });
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.HttpError, result.Status);
        Assert.Equal(status, result.HttpStatusCode);
        Assert.Equal("", result.Text);
        Assert.Contains("diagnostic-not-model-output", result.RawResponse);
        Assert.Single(handler.Bodies);
    }

    [Fact]
    public async Task Oversized_unknown_length_response_keeps_bounded_prefix_and_text()
    {
        var raw = Event("α") + ": " + new string('x', 2000);
        var handler = new Handler((_, _) => Task.FromResult(Sse(new ChunkStream(Encoding.UTF8.GetBytes(raw), 17))));
        using var provider = new VisionHttpProvider(Settings() with { MaxResponseBytes = 1024 }, handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.ResponseTooLarge, result.Status);
        Assert.Equal(1024, result.CapturedResponseBytes);
        Assert.Equal("α", result.Text);
        Assert.False(result.RawResponseComplete);
    }

    [Fact]
    public async Task Credentials_are_only_in_header_and_redacted_if_server_echoes_them()
    {
        const string secret = "private-test-secret";
        var handler = new Handler((_, _) => Task.FromResult(Json(secret)));
        using var provider = new VisionHttpProvider(Settings() with { CredentialEnvironmentVariable = "TEST_KEY" }, handler,
            new StaticCredentialSource(new Dictionary<string, string> { ["TEST_KEY"] = secret }));
        var result = await provider.InferAsync(Request());
        Assert.Equal("Bearer " + secret, Assert.Single(handler.Authorization));
        Assert.DoesNotContain(secret, Assert.Single(handler.Bodies));
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(result));
        Assert.Equal("[redacted]", result.Text);
        Assert.True(result.CredentialsRedacted);
    }

    [Fact]
    public async Task Default_transport_does_not_follow_a_real_loopback_redirect()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/v1");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        int requests = 0;
        var server = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    using var connection = await listener.AcceptTcpClientAsync(stop.Token);
                    using var stream = connection.GetStream();
                    var header = new List<byte>();
                    var one = new byte[1];
                    while (header.Count < 65536)
                    {
                        await stream.ReadExactlyAsync(one, stop.Token);
                        header.Add(one[0]);
                        if (header.Count >= 4 && header.TakeLast(4).SequenceEqual("\r\n\r\n"u8.ToArray())) break;
                    }
                    var contentLength = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n")
                        .Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                    await stream.ReadExactlyAsync(new byte[int.Parse(contentLength.Split(':')[1])], stop.Token);
                    int count = Interlocked.Increment(ref requests);
                    string response = count == 1
                        ? $"HTTP/1.1 307 Temporary Redirect\r\nLocation: {endpoint}/trap\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                        : "HTTP/1.1 500 Unexpected Followup\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(response), stop.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) when (stop.IsCancellationRequested) { }
        });
        try
        {
            using var provider = new VisionHttpProvider(Settings() with { Endpoint = endpoint });
            var result = await provider.InferAsync(Request(), stop.Token);
            Assert.Equal(VisionInferenceStatus.HttpError, result.Status);
            Assert.Equal(307, result.HttpStatusCode);
            Assert.Equal(1, requests);
        }
        finally
        {
            await stop.CancelAsync();
            listener.Stop();
            await server;
        }
    }

    [Fact]
    public async Task Unsupported_content_type_is_retained_but_never_treated_as_a_completion()
    {
        var handler = new Handler((_, _) =>
        {
            var response = Json("α");
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return Task.FromResult(response);
        });
        using var provider = new VisionHttpProvider(Settings(), handler);
        var result = await provider.InferAsync(Request());
        Assert.Equal(VisionInferenceStatus.InvalidOutput, result.Status);
        Assert.Equal("", result.Text);
        Assert.Contains("choices", result.RawResponse);
    }

    [Fact]
    public void Animation_is_rejected_instead_of_silently_selecting_a_frame()
    {
        var png = Png();
        byte[] animationHeader = [0, 0, 0, 8, (byte)'a', (byte)'c', (byte)'T', (byte)'L', 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0];
        // Reject by the declared animation before any image frame can be selected.
        var animated = png[..33].Concat(animationHeader).Concat(png[33..]).ToArray();
        Assert.Contains("Animated", Assert.Throws<InvalidDataException>(() => VisionImage.FromPng(animated, Hash(animated))).Message);
    }

    [Fact]
    public async Task Missing_key_invalid_input_and_precancellation_do_not_send_requests()
    {
        var handler = new Handler((_, _) => Task.FromResult(Json("")));
        using var provider = new VisionHttpProvider(Settings() with { CredentialEnvironmentVariable = "UNSET_TEST_KEY" }, handler,
            new StaticCredentialSource(new Dictionary<string, string>()));
        Assert.Equal(VisionInferenceStatus.ConfigurationError, (await provider.InferAsync(Request())).Status);
        Assert.Equal(VisionInferenceStatus.InputRejected, (await provider.InferAsync(Request() with { SystemPrompt = new string('x', 20001) })).Status);
        Assert.Equal(VisionInferenceStatus.Cancelled, (await provider.InferAsync(Request(), new CancellationToken(true))).Status);
        Assert.Empty(handler.Bodies);
    }

    [Fact]
    public async Task A_second_in_flight_request_is_recorded_as_busy_without_another_send()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler(async (_, ct) => { entered.SetResult(); await release.Task.WaitAsync(ct); return Json("α"); });
        using var provider = new VisionHttpProvider(Settings(), handler);
        var first = provider.InferAsync(Request());
        await entered.Task;
        Assert.Equal(VisionInferenceStatus.Busy, (await provider.InferAsync(Request())).Status);
        release.SetResult();
        Assert.Equal(VisionInferenceStatus.Completed, (await first).Status);
        Assert.Single(handler.Bodies);
    }

    [Fact]
    public async Task Request_budget_stops_before_network_and_seed_can_be_omitted()
    {
        var handler = new Handler((_, _) => Task.FromResult(Json("")));
        using var provider = new VisionHttpProvider(Settings() with { MaxRequests = 1, UseMaxCompletionTokens = true }, handler);
        Assert.Equal(VisionInferenceStatus.Completed, (await provider.InferAsync(Request() with { Seed = null })).Status);
        Assert.Equal(VisionInferenceStatus.InputRejected, (await provider.InferAsync(Request())).Status);
        var body = Assert.Single(handler.Bodies);
        Assert.DoesNotContain("\"seed\"", body);
        Assert.Contains("\"max_completion_tokens\":1024", body);
    }

    [Fact]
    public void Nonlocal_retrying_or_unbounded_configurations_are_refused()
    {
        var invalid = new[]
        {
            Settings() with { Host = AiProviderHost.Hosted }, Settings() with { Endpoint = new Uri("https://remote.invalid/v1") },
            Settings() with { Endpoint = new Uri("http://user:secret@127.0.0.1/v1") }, Settings() with { MaxAttempts = 2 },
            Settings() with { Timeout = TimeSpan.FromSeconds(121) }, Settings() with { MaxResponseBytes = 1024 * 1024 + 1 },
            Settings() with { Protocol = AiWireProtocol.OllamaChat },
        };
        foreach (var settings in invalid) Assert.Throws<AiProviderException>(() => new VisionHttpProvider(settings));
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        public List<Uri> Uris { get; } = [];
        public List<string?> Authorization { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uris.Add(request.RequestUri!);
            Authorization.Add(request.Headers.Authorization?.ToString());
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return await respond(request, cancellationToken);
        }
    }

    private sealed class ChunkStream(byte[] bytes, int chunkSize, bool stall = false, bool fail = false) : Stream
    {
        private int _offset;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _offset; set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_offset == bytes.Length)
            {
                if (stall) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                if (fail) throw new IOException("Synthetic broken connection.");
                return 0;
            }
            int count = Math.Min(Math.Min(buffer.Length, chunkSize), bytes.Length - _offset);
            bytes.AsMemory(_offset, count).CopyTo(buffer);
            _offset += count;
            return count;
        }
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
