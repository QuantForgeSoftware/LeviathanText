# Textured synthetic contrast holdout

Both stretch variants recovered two additional generated words for Mistral
relative to fading. Both lost two exact words for Qwen3. The pair-shared
stretch also produced two truncated, nonempty Qwen3 responses on grain
blanks. Independent per-image stretch preserved all 12 blank passes in
both models.

| Model | Grayscale variant | Strict completed / 24 | Empty blanks / 12 | Exact source words / 12 | Word scalar edits | Joint pairs / 12 |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Qwen3-VL 8B | Source | 24 | 12 | 6 | 6 | 6 |
| Qwen3-VL 8B | Faded | 24 | 12 | 8 | 4 | 8 |
| Qwen3-VL 8B | Pair-shared stretch | 22 | 10 | 6 | 6 | 5 |
| Qwen3-VL 8B | Independent image stretch | 24 | 12 | 6 | 6 | 6 |
| Mistral Small 3.1 24B | Source | 19 | 10 | 7 | 10 | 6 |
| Mistral Small 3.1 24B | Faded | 24 | 12 | 4 | 13 | 4 |
| Mistral Small 3.1 24B | Pair-shared stretch | 24 | 12 | 6 | 9 | 6 |
| Mistral Small 3.1 24B | Independent image stretch | 24 | 12 | 6 | 9 | 6 |

Joint passes require an empty completed blank and an exact completed word
on the same background. Word matches use NFC and the strict derived JSON
view. The source words are generator labels, with no human readability
judgment on the attenuated raster.

Both stretches rescued Mistral's grain-1 and edge-2 words. Qwen3 lost
tone-2 and gradient-1 in both. The two Qwen3 pair-shared grain blanks
contained long unsupported Greek prose in unfinished JSON and hit the
128-token cap. They fail blank scoring through truncation and captured
nonempty output. They do not qualify as schema-valid nonempty
transcriptions. Pair-shared and independent stretch have different
blank behavior on this set even where word counts coincide.

All 192 slots have raw result rows. Mistral's grayscale-source condition
retains two InvalidOutput rows and three HTTP errors. One InvalidOutput
row held JSON text with an incomplete protocol response. The other
reported an unloaded model. The HTTP errors were one 400 reporting no
loaded model and two 500 responses. These transport failures prevent a
clean source-to-faded accuracy comparison for Mistral. All three Mistral
attenuated variants completed. The runner separately exited with status
1 after Mistral's named model was unavailable to unload and the idle
server assertion failed. The evidence does not establish a cause or
ownership of the server activity.

## Fixtures

The 12 original synthetic pairs provide six background families.
Grayscale conversion uses rounded integer luma. Fading keeps 8% of the
signed foreground-minus-background pixel difference. Pair-shared
stretch maps one range across both faded members. Independent stretch
chooses each image's own range and preserves uniform images. The former
uses both members and therefore serves as a control. The latter can
amplify background texture and change the two members differently.

The independent audit checked all 4,423,680 derived pixel values and
recorded 60 unique PNG hashes across 96 image slots. Source and faded
blank images duplicate each other by design. The repeated backgrounds,
glyph labels and transformations constrain the study to this synthetic
cohort.

[Registered design](../../.artifacts/vlm-benchmark/textured-contrast-v1/design.md) ·
[Pixel and duplicate audit](../../.artifacts/vlm-benchmark/textured-contrast-v1/verification.json) ·
[Raw and score receipts](../../.artifacts/vlm-benchmark/textured-contrast-v1/live.sQg9Mm) ·
[Matched result summary](../../.artifacts/vlm-benchmark/textured-contrast-v1/report.json).
