# LM Studio native reasoning and compact glyph screen

Gemma 4 12B IT produced final messages on all 100 compact glyph requests when
reasoning was off. With the OpenAI-compatible CLI's default reasoning behavior
and the same 128-token cap, all 96 text slots ended without final text. A small
paired test on the native API isolated the effect of the reasoning setting.

## Matched reasoning probe

Each package received the same three images under native default reasoning and
native reasoning off. The exact payloads differed only by the reasoning field
within each image and package. Both conditions used 128 output tokens, a local
loopback connection, no integrations, no conversation history and storage
disabled. All 12 requests received HTTP 200.

| Package | Default final messages / 3 | Reasoning-off final messages / 3 | Default reasoning tokens | Reasoning-off reasoning tokens |
| --- | ---: | ---: | --- | --- |
| Gemma 4 12B IT IQ4_NL | 0 | 3 | 125, 125, 99 | 0, 0, 0 |
| Qwen3.8 27B Q4_0 | 3 | 3 | 0, 0, 0 | 0, 0, 0 |

Gemma's two default text requests exhausted the cap. Its default blank response
had reasoning but no final message. With reasoning off it produced one exact
reading in the two text requests and a nonempty response to the blank. Qwen3.8
returned identical final content in both conditions on each image. The result
shows how final-content availability can depend on the package and reasoning
setting within a fixed output budget.
[Native API documentation](https://lmstudio.ai/docs/developer/rest/chat) ·
[Matched requests and results](../../.artifacts/vlm-benchmark/native-reasoning-probe-v1/audit.json).

## Compact native-route comparison

Run the full 50-image compact glyph fixture twice per configuration. Gemma
used reasoning off and Qwen3-VL 8B used the native default. These are distinct
model and settings combinations. All 200 rows returned a single final message
below the cap. NFC distance preserves spaces, letters and marks.

| Configuration | Exact text slots / 96 | Scalar edits / 672 | CER | Both-exact pairs / 48 | Blank passes / 4 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Gemma 4 12B IT, reasoning off | 32 | 162 | 24.11% | 6 | 0 |
| Qwen3-VL 8B, native default | 46 | 110 | 16.37% | 12 | 0 |

The blank slots repeat one raster. Qwen3-VL 8B's 100 final strings were
byte-identical to its corresponding OpenAI-compatible compact run. This
is a matched observation for that package and fixture set. Native HTTP
receipts and OpenAI-compatible streaming statuses retain separate semantics.

Each runner unloaded its owned workers. The native response audit checks
request and response hashes, image bytes, model instance IDs and the absence
of stored conversation IDs. The study uses generated Greek glyphs and does
not establish scholarly accuracy or validate a recognizer change.
[Compact glyph report](vlm-glyph-contrast-2026-11-04.md) ·
[Native configuration report and raw receipt hashes](../../.artifacts/vlm-benchmark/native-compact-v1/report.json).
