# Synthetic Greek glyph contrast

The compact layout improved strict transcription for all three models that ran
both layouts. Qwen3-VL 8B recovered both members in 12 of 48 repeated pair trials,
and none of the four models that produced final text on the compact text images
passed its blank controls. Gemma returned empty final text on the blank slots and
reached the output cap on every text slot.

## Scope

Each layout has 48 text images and two copies of one byte-identical blank.
There are six contrasts in two fonts and two carrier strings, for 24 pairs
representing 12 focal raster patterns. Each pair differs inside one glyph
cell. The full references are identical by sample ID between layouts. The
first layout introduced 16–72 px ink gaps around the focal glyph. The references
specified continuous text, making strict scores for that layout sensitive to a
fixture spacing mismatch.

The compact follow-up reduced the ink gaps to 2–13 px. All 48 focal and 48
suffix raster crops matched after translation, along with 46 of 48 prefix
crops. The two prefix raster differences limit attribution to spacing alone.
The follow-up was chosen after inspecting the first layout and remains
exploratory. Every model/condition ran two repetitions. The observations
are repeated generated images and use six contrast types.
[Original fixture audit](../../.artifacts/vlm-benchmark/glyph-contrast-v1/independent-fixture-review/audit.json) ·
[Compact fixture audit](../../.artifacts/vlm-benchmark/glyph-contrast-v2/independent-fixture-review/audit.json).

## Strict results

The original three models completed all 300 initial requests. Their strict
exact-line and both-exact-pair counts were zero. The compact study retained
all 500 planned rows. Counts below include both repetitions. CER uses 672
reference scalars per model and retains captured text on failed rows.
Gemma's 96 truncated text slots had empty final content.

| Model package | Original CER | Compact CER | Compact exact lines / 96 | Compact both-exact pairs / 48 | Compact collapsed pairs / 48 | Compact blank passes / 4 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Qwen3-VL 8B | 61.90% | 16.37% | 46 | 12 | 18 | 0 |
| Qwen2.5-VL 7B | 46.13% | 16.37% | 36 | 6 | 20 | 0 |
| Mistral Small 3.1 24B | 43.45% | 26.49% | 20 | 4 | 14 | 0 |
| Qwen3.8 27B Q4_0 | — | 18.45% | 28 | 4 | 12 | 0 |
| Gemma 4 12B IT IQ4_NL | — | 100.00% | 0 | 0 | 0 | 4 |

Collapse means both transports completed with identical NFC output despite
different input glyphs. For Gemma all 48 text pairs were unavailable because
all 96 text requests were truncated under the 128-token cap. Its numerical
CER describes the observed output budget failure. The two added packages are
the installed Unsloth GGUFs with their matching projectors. The publishers
declare Apache 2.0, and the exact local file hashes are retained. Their
compact outputs have no corresponding first-layout run.
[Gemma upstream](https://huggingface.co/google/gemma-4-12B-it) ·
[Gemma package](https://huggingface.co/unsloth/gemma-4-12b-it-GGUF) ·
[Qwen upstream](https://huggingface.co/Qwen/Qwen3.8-27B) ·
[Qwen package](https://huggingface.co/unsloth/Qwen3.8-27B-GGUF) ·
[Installed-file hashes](../../.artifacts/vlm-benchmark/glyph-contrast-v2/added-models.sha256).

## Focal diagnostic

An exploratory view deletes Unicode whitespace and accepts the focal glyph
only if the exact prefix and suffix anchor a single scalar. All 96 text slots
remain in the denominator. Qwen3 recovered 24 focal glyphs on the first layout
and 58 on the compact layout. Qwen2.5 recovered 30 and 46, and Mistral
recovered 26 and 24. Among slots whose carriers anchored in both layouts,
Qwen3 improved from 24 to 44 correct of 62, and Qwen2.5 from 26 to 46 of 80.
Mistral had 20 of 26 correct in each. The Qwen gains therefore extend beyond
strict spacing penalties within this diagnostic. It does not change official
transcription scores or prove that a specific mark will survive manuscript imaging.
[Anchored diagnostic method](../../.artifacts/vlm-benchmark/glyph-focal-offline/design.md) ·
[Matched transitions](../../.artifacts/vlm-benchmark/glyph-focal-offline/comparison.json).

The runners unloaded each owned worker. The initial 20-minute plan relied on
three per-model six-minute limits plus setup and cleanup, without a common
clock guard. The initial study finished within that plan. The compact study
used five per-model limits. This study establishes fixture and runtime
behavior on generated examples. Reviewed natural images and references are
still needed to evaluate a manuscript workflow.
[Original report and receipts](../../.artifacts/vlm-benchmark/glyph-contrast-v1/report.json) ·
[Compact report and receipts](../../.artifacts/vlm-benchmark/glyph-contrast-v2/report.json).
