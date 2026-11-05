# Compact Greek CLI response formats

Both JSON profiles returned valid structured responses on all 50 requests.
Each decoded an empty transcription on both blank slots. The plain-text
profiles returned text on both blank slots for each model. The two slots
repeat one blank raster.

The four batches used identical compact glyph PNGs, one repetition and 128
output tokens per request. Qwen3 ran plain text first, while Mistral ran JSON
first. The CLI selects a different prompt version for each profile. These
results compare complete prompt-and-format profiles. All 200 requests
completed transport and each model was unloaded after its batches.

| Model | CLI profile | Exact text / 48 | Both-exact pairs / 24 | NFC edits / 336 | Blank passes / 2 | Completed schema-valid / 50 |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Qwen3-VL 8B | Plain text | 23 | 6 | 55 | 0 | — |
| Qwen3-VL 8B | Transcription JSON | 24 | 6 | 54 | 2 | 50 |
| Mistral Small 3.1 24B | Plain text | 10 | 2 | 89 | 0 | — |
| Mistral Small 3.1 24B | Transcription JSON | 9 | 2 | 75 | 2 | 50 |

JSON transcription scores use the CLI's strict derived view. The raw
representation views retained 1,062 and 1,022 edits for Qwen3 and Mistral,
respectively, including the JSON syntax. Schema validity establishes a
format property. Both-exact glyph pairs and natural-image grounding are
separate measurements. The compact study includes one repeated blank and
generated glyphs. The observed blank behavior needs broader validation
before use on reviewed collections.
[Registered design](../../.artifacts/vlm-benchmark/cli-format-compact-v1/design.md) ·
[Score receipts](../../.artifacts/vlm-benchmark/cli-format-compact-v1/live.mH3gv5) ·
[Joined summary](../../.artifacts/vlm-benchmark/cli-format-compact-v1/report.json).
