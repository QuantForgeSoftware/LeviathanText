# Fixed-geometry synthetic contrast recovery

Global contrast stretching recovered exact readings on faint flat-background
fixtures for both tested packages. Its effect on total edits differed by
package. The experiment holds glyph geometry fixed and measures recovery of
the originally drawn text.

The cohort uses 24 compact glyph images and one flat blank. Each image has
five intensity variants. An independent decoder checked all 125 PNG hashes,
5,760,000 pixels, dimensions and source targets. Grayscale source, 12% ink
contrast and 4% ink contrast retain the same geometry. The two stretched
conditions map each attenuated image's observed intensity range to 0–255.
A uniform blank stays flat. Its five slots repeat identical bytes.

Both models used the shipped transcription-JSON profile. Each completed all
125 requests with valid objects. The strict decoded score preserves case,
sigma, marks and spaces. Text CER uses 168 source scalars per model and
variant. Exact pairs require both members of a glyph contrast to be exact.

| Model | Variant | Exact text / 24 | Both-exact pairs / 12 | NFC edits / 168 | Blank pass / 1 |
| --- | --- | ---: | ---: | ---: | ---: |
| Qwen3-VL 8B | Grayscale source | 12 | 3 | 19 | 1 |
| Qwen3-VL 8B | 12% contrast | 11 | 3 | 13 | 1 |
| Qwen3-VL 8B | 12%, stretched | 12 | 3 | 20 | 1 |
| Qwen3-VL 8B | 4% contrast | 10 | 3 | 15 | 1 |
| Qwen3-VL 8B | 4%, stretched | 12 | 3 | 19 | 1 |
| Mistral Small 3.1 24B | Grayscale source | 4 | 1 | 34 | 1 |
| Mistral Small 3.1 24B | 12% contrast | 0 | 0 | 40 | 1 |
| Mistral Small 3.1 24B | 12%, stretched | 4 | 1 | 36 | 1 |
| Mistral Small 3.1 24B | 4% contrast | 0 | 0 | 88 | 1 |
| Mistral Small 3.1 24B | 4%, stretched | 4 | 1 | 35 | 1 |

Mistral regained four exact lines at each contrast level. Qwen3 regained one
at 12% and two at 4%. The matched transition counts show no loss of an exact
line in these attenuated-to-stretched comparisons. Qwen3 accumulated more
edits despite these recovered exact lines. The report preserves both metrics.
All five blank slots per model passed on a single underlying flat raster.

The run took 329 seconds including worker setup and unload. Its 273 process
samples recorded a maximum summed RSS of about 16.2 GiB for selected app,
backend and runner processes. Shared pages can be counted more than once,
and the measure is separate from GPU allocation. Both workers were unloaded.

This is one generated contrast family. The reference is the source string,
including marks whose visibility may diminish under attenuation. The test
supplies no human readability finding. Global contrast stretching also acts
on background variation. The paired textured study measures that case
separately.
[Registration](../../.artifacts/vlm-benchmark/contrast-recovery-v1/design.md) ·
[Strict results](../../.artifacts/vlm-benchmark/contrast-recovery-v1/report.json) ·
[Independent fixture check](../../.artifacts/vlm-benchmark/contrast-recovery-v1/independent-fixture-audit.json) ·
[Resource receipt](../../.artifacts/vlm-benchmark/contrast-recovery-v1/resource-samples/supervisor-summary.json).
