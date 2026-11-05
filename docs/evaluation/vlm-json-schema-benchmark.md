# Paired JSON-schema diagnostic — VLM-09-SCHEMA-PAIR-1

Registered during the owner's extension to 13:00 CST on 2026-11-04. This follows the
closed [first VLM phase](../handoffs/vlm-session-2026-11-04.md); it does not revise those
results. No training, natural-label adjudication, model selection or release is involved.

## Question and controlled comparison

Does a server-requested fixed JSON schema improve usable output format, and what happens
to the returned Greek and blank-image behavior? Compare `prompt-only-json` with
`schema-requested`, keeping the exact image, model, prompt and sampling settings the same.
The sole request difference is `response_format`. LM Studio
[documents JSON-schema requests and GGUF grammar sampling](https://lmstudio.ai/docs/developer/openai-compat/structured-output);
the experiment records observed behavior rather than assuming enforcement from that claim.

The fixed schema requires one object with the sole required string property `transcription`,
uses name `transcription_v1`, boolean `strict:true` and `additionalProperties:false`.
The optional C# `VisionResponseFormat.TranscriptionJsonV1` adds this request. Default
`PlainText` preserves the previous request bytes. Returned `Text`, raw wire and transport
statuses remain unchanged; schema validation and decoded text belong to the offline report.
Unsupported responses never trigger a retry, repair or text-only fallback.

## Frozen inputs, ordering and budget

Use the existing five setup images and ten **raw** geometry images: 13 generated text
fixtures and two blank canvases. These are reused printed controls, not handwritten Greek
or new independent test material. Setup and geometry strata remain separate.

Three pinned local packages are used: Qwen3-VL 8B, Qwen2.5-VL 7B and Mistral Small 3.1 24B,
each Q4_K_M with its existing matching F16 projector. The two routes run adjacent matched
pairs, with deterministic alternating order. Three repetitions use shuffled orders with
seeds 42/1729/2026; sampling seed remains 42, temperature zero and top-p one. One model and
one request run at a time, with context 8192, output limit 512 and a 30-second request cap.

The matrix is 3 models × 2 routes × 15 images × 3 repetitions = **270 planned calls**.
Six separately registered format-stress calls (one route pair per model) precede its matrix:
the same blank image and an instruction demanding literal non-JSON output. Those calls
have no Greek recognition score. The total cap is **276**; no extra warmups or retries are
silently added to the client request plan. Internal model-load behavior is separate.

Root freezes one common execution window immediately before setup: at most 60 minutes,
ending no later than 12:30 CST. Loading, subscriptions and request execution consume that
window. Every run registers 92 slots and retains cancelled/unattempted tails. A per-model
claim file prevents accidental reruns under the same window. Missing files/rows remain
explicit; malformed torn JSONL is an unresolved receipt failure, not repaired evidence.

The immutable fixture release and producer live under
`.artifacts/vlm-benchmark/vlm-bench-2/b/`. Input manifest SHA-256:
`c8bfcffed8e33a02a1c2f85f0802509e84e8e18fce8246876182ede99cf2828d`.
The [design](../../.artifacts/vlm-benchmark/vlm-bench-2/b/design.md) and
[operator runbook](../../.artifacts/vlm-benchmark/vlm-bench-2/b/RUNBOOK.md) specify exact
prompts, reference separation, source/producer hashes, commands and closure rules.

## Scoring and interpretation

Keep transport completion, strict schema validity, literal/NFC exact text, blank outcomes
and latency separate. Decode only a complete object with the sole valid-Unicode string;
never strip fences, extract substrings, fix escapes or drop failures. The strict-derived
scoring view uses that decoded string when valid, otherwise the full captured raw output.
Raw JSON distance is a representation diagnostic. Both views retain full denominators and
NFC scalar I/D/S without case, sigma, space or mark folding. Unsent/uncaptured failures are
not observed empty answers. Valid nonempty output on a blank remains a failure.

Report each repetition, stratum, route and paired sample. Preserve all exact outbound
request bodies and bind them to receipts; reject extra/duplicate nested fields that could
change image handling or prompting. Prepared request artifacts do not prove server receipt.
No comparison with the earlier 120-second-timeout runs establishes an old/new speed or
quality improvement. No result establishes manuscript accuracy, correction-time savings,
fresh-material support or an approved preprocessing/model default.

## Runtime observations

Root captures bounded model/runtime subscriptions under existing settings, using explicit
`--port 1234` to avoid automatic service startup. The CLI startup message is not a
subscription acknowledgment; actual matching events establish delivery. Preserve missing,
truncated and disconnected log coverage. Model input may be backend-reported rendered text;
aggregate prompt-token counts are not dedicated vision-token counts.

The [installed-path investigation](../../.artifacts/vlm-benchmark/vlm-bench-2/c/runtime-observability.md)
distinguishes inline API images from the application's saved chat resize preferences.
Backend transforms remain separate. Record effective grids/dimensions/tokens only if emitted;
do not infer them from [GGUF metadata](vlm-model-input-metadata-2026-11-04.md), a saved 2048-pixel
preference or absent debug lines. The collection helper stops only its owned logging process,
has fixed time and byte bounds, and never changes the service or model configuration.
