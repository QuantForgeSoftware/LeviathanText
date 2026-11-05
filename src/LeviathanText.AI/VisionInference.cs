namespace LeviathanText.AI;

/// <summary>Optional wire constraint; requesting a format does not establish response validity.</summary>
public enum VisionResponseFormat
{
    PlainText = 0,
    TranscriptionJsonV1 = 1,
}

/// <summary>A standalone image request. It neither names nor changes a transcription revision.</summary>
public sealed record VisionInferenceRequest
{
    public required string SampleId { get; init; }
    public required VisionImage Image { get; init; }
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public required string PromptVersion { get; init; }
    public int? Seed { get; init; } = 42;
    public VisionResponseFormat ResponseFormat { get; init; } = VisionResponseFormat.PlainText;
}

/// <summary>Transport completion, not scholarly validity or image-grounding assurance.</summary>
public enum VisionInferenceStatus
{
    Completed,
    InvalidOutput,
    Truncated,
    Timeout,
    TransportError,
    Cancelled,
    InputRejected,
    HttpError,
    ResponseTooLarge,
    Busy,
    ConfigurationError,
}

/// <summary>
/// One attempted request, including failures. Text is all captured model content, unnormalized;
/// RawResponse is the bounded wire body. Neither is an accepted transcription or proposal.
/// Credentials are redacted from both if a server echoes them.
/// </summary>
public sealed record VisionInferenceResult
{
    public required string SampleId { get; init; }
    public required string ImageSha256 { get; init; }
    public required string PromptSha256 { get; init; }
    public required string PromptVersion { get; init; }
    public required string ProviderId { get; init; }
    public required string ModelId { get; init; }
    public required VisionInferenceStatus Status { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public string? RequestSha256 { get; init; }
    public int RequestBytes { get; init; }
    public string Text { get; init; } = string.Empty;
    public string RawResponse { get; init; } = string.Empty;
    /// <summary>Digest of the exact captured bytes before UTF-8 rendering or credential redaction.</summary>
    public string? RawResponseSha256 { get; init; }
    public int CapturedResponseBytes { get; init; }
    public bool RawResponseComplete { get; init; }
    public bool CredentialsRedacted { get; init; }
    public string? FinishReason { get; init; }
    public string? ResponseModelId { get; init; }
    public int? HttpStatusCode { get; init; }
    public string? Detail { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public double EncodingMilliseconds { get; init; }
    public double? FirstByteMilliseconds { get; init; }
    public double? FirstTokenMilliseconds { get; init; }
    public double ElapsedMilliseconds { get; init; }
}
