# Masked focal-glyph control

Swapping the supplied focal glyph left the exact response unchanged in all 36
available pairs. All 108 requests completed transport. Every response either
added whitespace between the visible anchors or changed an anchor. After removing
Unicode whitespace in a separately reported diagnostic, neither supplied
alternative appeared between exact anchors in any response.

The 12 stimuli use the first compact carrier from each of six focal contrasts in
two fonts. A C# generator masked the recorded focal cell independently in both
members of each compact pair. Both masks encoded to byte-identical PNGs. The
generator and a separate Pillow decoder verified that the erased cell contained
only the original background and every pixel outside it matched both originals.
The two assisted conditions supplied opposite focal glyphs with the same visible
anchors. The image-only arm supplied no OCR hypothesis. Each model processed
one request per image and condition, with the order of the three condition
blocks rotated across models.

## Literal results

All 108 literal results were classified as **other**. In 77 responses, a single
space separated otherwise exact anchors. The remaining 31 had an anchor
mismatch. Zero responses had exact anchors with an empty residual, one
replacement marker, exact alternative A or exact alternative B. Every condition
had 12 available responses.

## Whitespace diagnostic

The following results remove Unicode whitespace and apply the same ordinal
anchor and residual checks. Case, marks, Greek versus Latin characters, and
sigma forms remain significant.

| Model | Image-only empty residual | Hint A empty residual | Hint B empty residual | Other after whitespace removal, across all conditions |
| --- | ---: | ---: | ---: | ---: |
| Qwen3-VL 8B | 11/12 | 11/12 | 11/12 | 3/36 |
| Qwen2.5-VL 7B | 12/12 | 12/12 | 12/12 | 0/36 |
| Mistral Small 3.1 24B | 2/12 | 3/12 | 3/12 | 28/36 |

Every remaining other outcome had an anchor mismatch. Qwen3 used Latin
lookalikes on one uppercase Arial stimulus. Mistral often changed the final
vowel's diacritics. Mistral's response differed between the unassisted arm and
both assisted arms on three Arial stimuli, while both opposing hints yielded
the same response on each stimulus. That comparison includes a change of
prompt mode as well as the supplied hint.

The focal cell contained background, so exact A and B insertions would both be
unsupported by the visible image. An empty residual describes an output
category. These controls use one rendered carrier per font and contrast, and
cover the registered prompt and settings. The earlier [crossed grounding
diagnostic](vlm-crossed-grounding-2026-11-04.md) exposed copying on a full blank.
The masked test measures behavior with visible anchors and a locally erased
position.

Offline verification replayed the report byte-for-byte and joined all 108 rows
to the frozen inputs. All 36 model/stimulus triples had three distinct prompt
and request digests. The CLI scorer independently validated all nine batches,
including the prompt hashes. The tracked outcome table uses the masked
classifier, with the ordinary CLI output retained as receipt-validation evidence.

[Registered design](../../.artifacts/vlm-benchmark/masked-focal-proposal-v1/design.md) ·
[Fixture receipt](../../.artifacts/vlm-benchmark/masked-focal-proposal-v1/fixtures/fixture-receipt.json) ·
[Offline verification](../../.artifacts/vlm-benchmark/masked-focal-proposal-v1/independent-verification.json) ·
[Live verification](../../.artifacts/vlm-benchmark/masked-focal-proposal-v1/live-verification.json) ·
[Rows and pairs](../../.artifacts/vlm-benchmark/masked-focal-proposal-v1/report-v1.json).
