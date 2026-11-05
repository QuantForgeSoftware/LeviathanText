# Observed LM Studio runtime behavior — 2026-11-04

The closed synthetic runs now have backend-reported prompt strings and runtime logs. They
**do not expose the effective image dimensions, patch grid or separate vision-token count**.
VLM-09 therefore remains partial. No preprocessing default changed, and these observations
do not establish handwritten Greek accuracy, model adoption or a speed improvement.

The root runner captured both log subscriptions around each registered paired run, using
LM Studio 0.4.26 and runtime 2.65.1. All three runs completed their 90 matrix requests plus
2 separate format-stress requests. This audit made no model calls and performed no training.
The synthetic fixtures are repeated observations of 15 generated images, not 90 independent
texts per model. See the [benchmark specification](vlm-lmstudio-benchmark.md) and
[embedded model metadata](vlm-model-input-metadata-2026-11-04.md) for the preceding scope.

## Captured evidence and association

| Model | Input / output events | Runtime events | Native worker PID | Prompt evaluations of only one token |
| --- | ---: | ---: | ---: | ---: |
| Qwen3-VL 8B | 92 / 92 | 269 | 42587 | 47 / 92 |
| Qwen2.5-VL 7B | 92 / 92 | 273 | 47701 | 47 / 92 |
| Mistral Small 3.1 24B | 92 / 92 | 250 | 48804 | 47 / 92 |

Every captured event carries the expected model identifier; runtime events also carry the
listed PID and engine identity. All six stdout streams parsed without malformed records
or byte-cap truncation. The owned subscriber processes exited 137 because the collector
stopped them after closure; that is not a model inference failure.

The audit pins 318 input files, including registration/results, exact outbound requests,
logger startup/closure receipts and stdout/stderr. It verifies these hashes before and
after analysis. The root's broader closure additionally pins resource observations. Each
model input/output event falls inside exactly one recorded request interval. This is a
**time-compatible association**, supported by model identity, serialized execution and
prompt or output comparisons; it is not a shared unique request ID. Backend task numbers
are not treated as client request IDs. The audit supports an explicit 5 ms fallback, but
none of the final model-event time associations needed it.

Artifacts:

- [Pinned closure](../../.artifacts/vlm-benchmark/vlm-bench-2/c/runtime-closure-r1.json),
  [final runtime audit](../../.artifacts/vlm-benchmark/vlm-bench-2/c/runtime-report-r3/report.json)
  and [hash receipt](../../.artifacts/vlm-benchmark/vlm-bench-2/c/runtime-report-r3/receipt.json).
- [Bounded offline parser](../../.artifacts/vlm-benchmark/vlm-bench-2/c/audit_runtime.py) and
  [32 self-checks](../../.artifacts/vlm-benchmark/vlm-bench-2/c/runtime-selftests-r3.json).
  An initial audit could associate a mismatching output with a neighboring request inside
  its fallback window. A failing regression now requires time candidates to be selected
  independently of content. The earlier audit is preserved. A second failing fixture showed that a duplicate
  registration ordinal could be collapsed before validation. The final validator requires
  exactly 92 integer ordinals covering 1–92; r3 is authoritative. The frozen observations
  are unchanged from r2.
- [Root closure](../../.artifacts/vlm-benchmark/vlm-bench-2/root/closed-live-receipt.json)
  and [post-run model verification](../../.artifacts/vlm-benchmark/vlm-bench-2/root/post-run-model-hashes.json).

## Rendered input observed

For both Qwen models, the diagnostic input used this structure. Placeholders below replace
only the exact registered prompt strings; the surrounding characters are observed:

```text
<|im_start|>system
{REGISTERED_SYSTEM_PROMPT}<|im_end|>
<|im_start|>user
{REGISTERED_USER_PROMPT}<|vision_start|><|vision_end|><|im_end|>
<|im_start|>assistant
```

Mistral used:

```text
<s>[SYSTEM_PROMPT]{REGISTERED_SYSTEM_PROMPT}[/SYSTEM_PROMPT][INST]{REGISTERED_USER_PROMPT}[IMG_END][/INST]
```

Each model produced two distinct input strings: one shared by its 90 matrix calls and one
shared by the two format-stress calls. The Qwen matrix strings have the same SHA-256,
`667c058b75be0719d26f14b1e0035f294e2c6bc0022efff7534f1e060d0cfe0d`;
Mistral's is `5b7b1e7e26670d3c1d6115424339a1687cee4de1c572cc4058e78274a42f9d1b`.
The complete hashes, marker counts, original log offsets and stress-prompt hashes are in
the audit. Matching rendered strings do not establish identical embedded template files.

The log contains image boundary markers but no expanded image embeddings. In particular,
absence of `<|image_pad|>` or `[IMG]` from these strings does not mean the image was ignored
or had zero vision tokens. The installed engine path forwards the backend's
`__lmstudio.model_input` diagnostic extension; it is a textual representation of input,
not an internal image tensor receipt. The
[static source trace](../../.artifacts/vlm-benchmark/vlm-bench-2/c/runtime-observability-addendum.md)
records the installed bundle hash and exact byte offsets.

## Diagnostic output differs from HTTP content

| Model | Diagnostic output identical to HTTP text | Diagnostic output has an additional exact JSON fence wrapper | Other differences |
| --- | ---: | ---: | ---: |
| Qwen3-VL 8B | 91 | 1 | 0 |
| Qwen2.5-VL 7B | 92 | 0 | 0 |
| Mistral Small 3.1 24B | 49 | 43 | 0 |

All 44 differences occur in schema-requested calls. They comprise Qwen3's schema stress
call, Mistral's schema stress call and 42 Mistral matrix calls. In each case the diagnostic
string equals the HTTP text surrounded by an opening JSON code fence and a closing fence,
with one newline on each side. The
[representation audit](../../.artifacts/vlm-benchmark/vlm-bench-2/c/output-representation-audit.json)
retains both text hashes, source log locations, request ordinals and the exact comparison.
This comparison classifies a difference; it does not strip fences for scoring or replace
any frozen HTTP output.

A concrete backend path explains why these representations can differ. The runtime's
reported upstream revision is `a4699400e`. Its
[auto-parser generator](https://github.com/ggml-org/llama.cpp/blob/a4699400e/common/chat-auto-parser-generator.cpp#L119)
permits either fenced or bare schema-conforming JSON and tags only the JSON subtree as
content. The [PEG mapper](https://github.com/ggml-org/llama.cpp/blob/a4699400e/common/chat-peg-parser.cpp#L316)
puts content-tagged nodes into returned message content. The installed common library
contains the corresponding parser symbols and literals; its full hash and symbol addresses
are pinned in the representation audit. This is a source-level explanation consistent with
the observed difference. We did not instrument the sampler or prove which generated grammar
was active on each call.

Consequently, valid JSON in the HTTP response is an observed property of the served output.
It must not be promoted to a claim that every underlying generated token was bare JSON,
or that a particular sampler constraint was proven effective. Conversely, diagnostic fences
alone do not prove that a schema grammar was ignored: the pinned parser explicitly allows
them around the content subtree.

## Preprocessing and timing limits

No recognized per-image resize, tiling grid, image-token dimension or encoded-image-token
message appears in these captures. Load-level image-size, patch-size and min/max-pixel
parameter messages were also unavailable. Detailed fields exist at debug level in the
pinned implementation, but this session retained the existing verbosity. Their absence is
an observability gap, not evidence that the backend performed no resize, patching or other
transform.

The saved application preference is a 2048-pixel image cap. Static inspection shows that
this installed OpenAI inline-image route preserves the PNG payload through the application's
engine media resolver; the saved preference does not establish a 2048-pixel API resize.
Backend preprocessing remains separate. The
[read-only investigation](../../.artifacts/vlm-benchmark/vlm-bench-2/c/runtime-observability.md)
distinguishes saved settings, installed code capability and runtime evidence. Both Qwen
load logs also recommend a minimum image-token budget for grounding tasks; this advisory
message does not report the budget actually used, and no setting was changed from it.

Output diagnostics expose aggregate prompt counts, generated counts and timing. They do
not expose a separate image-token count. The 47 one-token prompt evaluations per model,
with matching cache-reuse messages, demonstrate materially different cache work across
requests. Model loading, warmup and cache state are not balanced experimental controls;
alternating the paired request order does not remove those effects. These timings cannot
establish a clean speed gain from requesting a schema, nor be compared directly with the
previous longer-timeout benchmark as a software performance improvement.

All 276 outbound requests contain temperature 0, top-p 1 and seed 42. The captured runtime
messages and model stats provide no corresponding effective sampling-setting receipt.
Requested values and repeatability are not proof that the backend honored a seed.
The remaining work is an explicitly registered way to observe effective preprocessing and
sampling configuration without confusing source metadata, rendered markers, aggregate
prompt counts or served-output formatting with internal execution evidence.
