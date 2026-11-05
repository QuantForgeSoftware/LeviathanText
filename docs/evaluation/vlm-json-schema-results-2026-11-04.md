# Paired JSON-schema experiment — 2026-11-04

An explicit schema request improved Mistral's formatting compliance on these generated
fixtures. It did **not** correct the Greek characters. Qwen3 and Qwen2.5 produced identical
matrix text with and without the schema request. The evidence supports a **CLI opt-in for
the fixed transcription schema**, while preserving the existing default and keeping every
result advisory. It does not support making schema output the default, replacing the
recognizer, accepting generated training labels or claiming manuscript accuracy.

All three runs are closed: **276 planned slots, 276 transport completions, no missing,
failed or unattempted slots**. Of these, 270 belong to the paired transcription matrix and
six are separate format-stress diagnostics. Transport completion is assessed independently
of JSON validity, exact characters and blank-region behavior.

## Matched design and evidence

Each model used the same five setup images and ten **raw** geometry images: 13 text fixtures
and two blanks, repeated three times in each route. These are generated typography, not
additional independent manuscript samples. No natural labels, training, new specialist
decisions or acceptance material were used.

Both routes used the frozen `visible-json-v1` system/user prompts, temperature 0, top-p 1,
seed 42, 512 output tokens, 30-second request deadlines and one attempt at a time. Adjacent
route pairs used identical PNG bytes; deterministic shuffled order approximately balanced
which route ran first. `prompt-only-json` omitted `response_format`; `schema-requested`
requested the fixed `transcription_v1` object with one required string property,
`transcription`, and no additional properties. The original generated references were
never sent to a model.

The registered common-window deadline was 12:01:55 CST, within the 60-minute limit
and before the 12:30 hard stop. Actual inference finished at 11:18:39 CST; the empty
loaded-model list was restored at 11:19:11 CST. Model loading/setup consumed part of
the registered window. Root performed all live calls. Exact outgoing
request bodies and full transport results are retained. A prepared request artifact is not
proof of server receipt: cancellation can occur after audit and before transmission. The
observed completed responses supply separate evidence here.

The immutable [final report](../../.artifacts/vlm-benchmark/vlm-bench-2/b/report-r2/report.json),
[closure](../../.artifacts/vlm-benchmark/vlm-bench-2/b/closure-r2.json) and
[receipt](../../.artifacts/vlm-benchmark/vlm-bench-2/b/report-r2/receipt.json) bind the inputs,
producer, common window and all planned rows. The partial `report-r1` remains unchanged.
The [runbook](../../.artifacts/vlm-benchmark/vlm-bench-2/b/RUNBOOK.md) and
[design](../../.artifacts/vlm-benchmark/vlm-bench-2/b/design.md) describe reproduction and limits.

## Format, exact text and blanks

Strict validity requires one complete object with exactly one string property named
`transcription`. Fences, duplicate/extra keys, surrounding prose, invalid syntax and invalid
Unicode fail without repair. Exact-text counts below require both valid JSON and completed
transport. Each route has 45 matrix requests: 39 text requests and six blank requests.

| Model | Route | Transport completed | Strict JSON valid | Completed literal text exact | Blank success |
| --- | --- | ---: | ---: | ---: | ---: |
| Qwen3-VL 8B | Prompt only | 45/45 | 45/45 | 3/39 | 6/6 |
| Qwen3-VL 8B | Schema requested | 45/45 | 45/45 | 3/39 | 6/6 |
| Qwen2.5-VL 7B | Prompt only | 45/45 | 45/45 | 3/39 | 3/6 |
| Qwen2.5-VL 7B | Schema requested | 45/45 | 45/45 | 3/39 | 3/6 |
| Mistral Small 3.1 24B | Prompt only | 45/45 | 3/45 | 0/39 | 3/6 |
| Mistral Small 3.1 24B | Schema requested | 45/45 | 45/45 | 6/39 | 6/6 |

The [complete-string wrapper audit](../../.artifacts/vlm-benchmark/vlm-bench-2/b/paired-wrapper-audit.json)
explains Mistral's apparent exact-match improvement: three paired strings were identical,
and the other 42 differed **only by an exact code-fence wrapper**. This was a comparison
of whole original strings; no failed response was repaired or rescored as valid. Mistral's
Greek content was unchanged by the schema route. Both Qwen models had identical raw
model-text strings in all 45 paired matrix requests. Here, "raw model text" means the
unaltered completion content returned by the API, not an independently observed decoder
stream. The separate [runtime audit](../../.artifacts/vlm-benchmark/vlm-bench-2/c/runtime-report-r3/report.json)
retains 44 diagnostic-output versus API-content differences, all on the schema route
(one Qwen3 challenge and 43 Mistral requests). The pinned parser source permits fenced
or bare JSON while tagging only its JSON subtree as content; this is consistent with
the observed wrapper differences. It does not establish effective sampler enforcement.

Qwen2.5 returned `λογισμός` for the setup blank on all three repetitions in both routes,
while correctly leaving the geometry blank empty. Valid JSON did not prevent invented
text. Qwen3 continued to return Latin `AB` in `ABΓΔ` and Latin letters in `CΩMA` for fixtures
whose generated targets use Greek letters. Mistral returned only `Ω` for the `ϹΩΜΑ`
fixture in both routes. These are exact encoded-character discrepancies in generated
fixtures; visually similar Greek/Latin glyphs and these few fonts do not establish broad
recognition performance.

## Strata and character-distance interpretation

The scorer retains NFC scalar substitutions, deletions and insertions without case/sigma,
space or mark folding. The *strict-derived* view uses decoded transcription only for a
valid object; malformed output remains the complete raw string with invalid-format status.
Consequently, its distance combines formatting and transcription failure. A lower value
caused by removing code fences is not evidence of corrected Greek characters. Raw JSON
payload distance, which also counts JSON syntax, remains a separately labeled diagnostic.

All three repetitions produced the same values below. The counts are repeated views of
four setup text fixtures and nine geometry text fixtures, not 39 independent examples.

| Model / route | Setup literal exact | Geometry literal exact | Setup strict-derived distance | Geometry strict-derived distance |
| --- | ---: | ---: | ---: | ---: |
| Qwen3 / prompt only | 3/12 | 0/27 | 23.08% | 13.72% |
| Qwen3 / schema requested | 3/12 | 0/27 | 23.08% | 13.72% |
| Qwen2.5 / prompt only | 3/12 | 0/27 | 19.23% | 25.35% |
| Qwen2.5 / schema requested | 3/12 | 0/27 | 19.23% | 25.35% |
| Mistral / prompt only | 0/12 | 0/27 | 530.77% | 102.79% |
| Mistral / schema requested | 6/12 | 0/27 | 11.54% | 34.42% |

All 90 model/route/fixture triplets retained identical raw model-text strings across their
three repetitions. Stable output includes stable mistakes and format failures; it does
not establish correct transcription or generalizable determinism.

## Separate format-stress diagnostics

Each model received one additional matched pair on the existing blank setup image with
an instruction to return the plain text `NOT_JSON`. These six requests have no recognition
score and are excluded from the transcription matrix above.

| Model | Prompt-only response | Schema-requested response | Strict validity |
| --- | --- | --- | --- |
| Qwen3 | `NOT_JSON` | Object containing transcription `NOT_JSON` | 0/1 → 1/1 |
| Qwen2.5 | `NOT_JSON` | Object whose transcription echoes the diagnostic system instruction | 0/1 → 1/1 |
| Mistral | `NOT_JSON` | Object containing transcription `NOT_JSON` | 0/1 → 1/1 |

This shows observed format compliance under conflicting prompting. It does not establish
that the text describes the image or prove the backend's internal grammar mechanism.
The route remains named **schema-requested**. Effective-runtime claims require the separate
pinned runtime investigation; request acceptance and valid output alone are insufficient.

## Timing and verification

The report preserves first/p50/p95/maximum latency per route, repetition and stratum.
Quantiles use nearest rank `ceil(p×n)` without interpolation. Missing durations are never
zero, and failed durations would remain in the full planned report. The following compact
summary pools the 45 timed matrix requests per route only for descriptive latency;
it excludes the two challenge calls and is not an independent-sample or speed-gain claim.

| Model / route | p50 ms | p95 ms | Maximum ms | Sum of measured request time s |
| --- | ---: | ---: | ---: | ---: |
| Qwen3 / prompt only | 570.70 | 756.57 | 786.68 | 20.93 |
| Qwen3 / schema requested | 530.61 | 765.00 | 812.22 | 21.34 |
| Qwen2.5 / prompt only | 484.61 | 641.93 | 676.03 | 18.38 |
| Qwen2.5 / schema requested | 490.91 | 677.36 | 704.71 | 18.81 |
| Mistral / prompt only | 1,467.77 | 1,978.08 | 2,165.01 | 55.80 |
| Mistral / schema requested | 1,525.25 | 1,967.94 | 2,109.78 | 57.22 |

Host/cache/model-load conditions and sequential pairs remain confounds. These 30-second,
interleaved requests cannot be compared directly with bench1's 120-second, separately
ordered conditions. Request-time sums exclude model loading and other work outside the
provider's timer. No performance advantage is established.

The frozen producer passed **280 mocked offline checks** with zero compiler warnings/errors.
Fail-before/candidate evidence covers interrupted receipts, nested request-field changes,
absolute-deadline handling and unavailable capture. Independent verification of the final
live report passed **6,165 checks** covering raw strings, strict parsing, separately computed
NFC distances, full denominators, pairs, stability and timing. See the
[producer receipt](../../.artifacts/vlm-benchmark/vlm-bench-2/b/producer-v1-receipt.json),
[independent audit](../../.artifacts/vlm-benchmark/vlm-bench-2/b/report-r2-independent-audit.json)
and [B handoff](../../.artifacts/vlm-benchmark/vlm-bench-2/b/HANDOFF.md).
Final report SHA-256: `79f5f8fdd8ff706b2473b930f38c413a2e274f4ac65ff2db08d0cd3b4b21d5ff`.

## CLI recommendation

Offer the fixed transcription schema as an **explicit opt-in**. Preserve the current
default request bytes, complete raw model content, transport status, format metadata/hash
and request provenance. Offline processing must validate the declared schema independently,
retain malformed/failed content and all denominators, and distinguish format success from
exact text and blank-region behavior. Legacy runs without the new option must keep their
existing scoring semantics.

The demonstrated benefit is usable structured output for a model that otherwise wraps
its answers in fences. This experiment supplies no evidence that the option improves
visible Greek characters or prevents invented transcription. Natural Greek manuscripts,
papyri and inscriptions still require reviewed references and the existing evaluation gates.
