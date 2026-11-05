# VLM-A1 transport implementation

Contract: `VLM-BENCH-1`. This is an opt-in image transport, separate from the existing
text-only proposal providers. It produces raw L0 evidence and never creates or modifies
L1, L2 or a proposal. A successful HTTP generation does not establish visual grounding,
Greek correctness, corpus eligibility or model capability.

## Public boundary and ownership

`VisionImage.FromPng(pngBytes, expectedSha256)` copies, hashes, identifies and decodes the
exact input snapshot. The byte buffer stays private. `VisionInferenceRequest` names an
opaque sample, that image, system/user prompts and their version, plus optional seed.
There is no revision requirement and no source-reference field. The opaque sample ID and
prompt version stay in the local receipt; only prompts, model settings and image bytes go
to the model.

`VisionHttpProvider(settings, handler?, credentials?)` owns its HTTP client/handler and is
disposable. `InferAsync(request, cancellationToken)` returns a `VisionInferenceResult`
for each valid request object, including cancellation and recorded transport failures.
Image construction failures throw `InvalidDataException`; the runner must retain those
as input-rejected attempts. Invalid provider configuration throws `AiProviderException`
before any request. Programming misuse such as a null request or reuse after disposal is
not converted into a model attempt.

The default handler disables automatic redirects. An injected handler must also disable
redirects. The initial implementation accepts only loopback OpenAI-compatible endpoints
with `Host=Local` and `MaxAttempts=1`; it makes no native LM Studio or remote-provider
fallback. A received redirect is a failed HTTP attempt. Configuration uses existing
`AiProviderSettings` and `IAiCredentialSource`, without changing their text-only behavior.

Root owns CLI run manifests, model/runtime receipts, live capability checks and execution.
Instance B owns corpus/source joins, independent references and offline scoring. This
transport sees no reference labels and cannot certify a Greek reading.

## Wire and bounds

The provider sends `POST {base}/chat/completions` with a system message and user content
parts: text, then `image_url.url = data:image/png;base64,...`. It uses `stream=true`,
temperature 0, top_p 1, the configured token limit and optional seed (default 42).
The runner records whether the installed model/runtime actually supports those settings.
No images are fetched from URLs and no resizing, normalization or re-encoding occurs.

| Bound | Behavior |
| --- | --- |
| PNG file <=8 MiB | Checked before copying or decoding; reject larger files. |
| Decoded static image <=4,000,000 pixels | Identify before full decode; reject larger dimensions or animated PNG. |
| Request <=12 MiB | Bound the encoded UTF-8 JSON; reject before network transmission. |
| Response <=1 MiB | Configurable downward; read at most one byte beyond the cap and retain only the bounded prefix. |
| Timeout <=120 seconds | One linked deadline covers request encoding, headers and body; caller cancellation is distinct. |
| One attempt; one in-flight request per provider | No automatic retries; simultaneous calls receive `Busy`. The runner must serialize model work across instances. |
| Prompt characters | Existing configured `MaxPromptCharacters`; reject before serializing an oversized prompt. |

ImageSharp 3.1.11 is the same pinned managed decoder already used by the repository.
The new AI project reference does not add a new package version or a native runtime.
Decoding checks pixels but does not rewrite the transmitted PNG: metadata in those pinned
bytes is also transmitted. The crop producer must freeze the intended metadata policy.

Only `text/event-stream` is interpreted as streamed completions. An explicit
`application/json` response is accepted if the server returns the complete JSON form.
Other content types are invalid output. `[DONE]` and a `stop` finish reason are required
for successful SSE completion; a token-limit finish is `Truncated`. HTTP error bodies
remain diagnostics and never become model text. Empty text is a valid blank completion
only when all completion checks pass. A valid SSE stream may emit an empty `delta` with
`stop` and `[DONE]` without ever including a content or role field; this observed LM Studio
Mistral blank-response shape is supported. Complete JSON messages still require their
explicit content string. Missing completion markers, tool calls and malformed events do
not pass as blank output.

## Receipts and failure scoring

`Text` is the captured model text, without trimming, normalization, prose removal or
editorial repair. Failed and truncated attempts keep their captured text, including the
well-formed scalar/escape prefix of a torn content string. An unfinished escape or scalar
is not guessed. `RawResponse` is separate from that text and retains the bounded wire
representation, subject to credential redaction. Invalid UTF-8 is displayed with replacement
characters only in this diagnostic wire rendering; it is not inserted into model text.
`RawResponseSha256` pins the exact captured byte prefix before rendering/redaction.
A failure may have nonempty text and must not be scored as an empty
hypothesis merely because its status is not `Completed`.

Receipts join the sample ID, image SHA-256, prompt version/hash, provider/model IDs,
request SHA-256 and encoded byte count, timestamps, finish reason, reported model ID,
HTTP status, nullable usage and timing. The prompt hash covers the UTF-8 JSON array
`[systemPrompt,userPrompt]` using the transport's Unicode-preserving serializer. The
request hash covers the exact encoded request body, without authorization headers.
Null timings/usage mean unavailable, not zero. First-token timing is observed receipt
of the first nonempty model-content chunk, not a server-internal generation measurement.

Credentials resolve at send time and are never copied into request receipts. If a server
echoes a configured credential, including JSON-escaped or split SSE forms, the wire field
is replaced with a redaction marker and `CredentialsRedacted=true`; model text and returned
metadata redact the credential too. Its pre-redaction digest remains available. Neither
an HTTP error nor a missing/unsupported model triggers a text-only retry. The raw error
and status let the integrator distinguish the installed model/projector failure without
guessing a diagnostic from a generic HTTP status.

## Verification and remaining gates

The equivalent baseline probe against the pre-change AI DLL demonstrates that
`AiProposalRequest` has no image field, requires a revision/range, and sends string-only
user content. It fails the explicit image-content assertion after a successful build;
this is an unsupported-behavior baseline, not a compilation failure. Evidence is under
`.artifacts/vlm-benchmark/vlm-bench-1/a/baseline/`.

The isolated tests cover exact immutable byte transmission, contrasting synthetic pixels,
Unicode, empty completion, stale digest/corrupt/oversized image rejection, HTTP failures,
no retries, token truncation, cancellation, body timeouts, concurrency, bounded response
retention and credential handling. Existing text-provider, proposal and source-anchor
tests remain unchanged. Final counts and exact hashes belong to the handoff.

No transport test claims that a real model uses the image. Root must retain contrasting
image and blank-control outputs with the exact model/projector/runtime receipt before
collection requests. No natural-text accuracy, speed, correction-effort, R0 performance,
multi-track support or training claim follows from this implementation.

## Candidate receipts delegated to the integrator

| Candidate | Required selected files | Still required in execution receipt |
| --- | --- | --- |
| Qwen3-VL-8B-Instruct | `Qwen3-VL-8B-Instruct-Q4_K_M.gguf`, `mmproj-Qwen3-VL-8B-Instruct-F16.gguf` | Exact repository revision, SHA-256 files/license/tokenizer/template, loaded model ID, capability outputs, runtime and memory. |
| Qwen2.5-VL-7B-Instruct | `Qwen2.5-VL-7B-Instruct-Q4_K_M.gguf`, `mmproj-model-f16.gguf` | Same identities and capability evidence; no substitution from the Qwen3 result. |
| Mistral-Small-3.1-24B-Instruct-2503 | `Mistral-Small-3.1-24B-Instruct-2503-Q4_K_M.gguf`, `mmproj-model-f16.gguf` | Hardware admission plus the same pinned runtime/package evidence. |

The source-card/license links are in the [benchmark specification](vlm-lmstudio-benchmark.md).
The transport agent neither downloaded, loaded nor called these models. One-attempt
timeouts, caps and explicit statuses bound each request; overall wall-clock/request budgets,
server/client memory sampling, cold-load separation and study eligibility remain runner
and evaluator responsibilities.
