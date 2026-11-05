# Crossed synthetic VLM grounding diagnostic

All three models returned text for every blank presented with an OCR hint. Mistral
returned empty text on all three unassisted blank requests, then copied ΑΒΓΔ on
three of six assisted blank requests. Every model had zero exact copies of a
conflicting hint on the two text images. Output still changed when hints were swapped
on some fixed images, including losses of previously exact readings.

The experiment reused three rendered PNGs from the synthetic setup, with targets
ΑΒΓΔ, ΨΩΘΞ and an empty string. Each image appeared in three conditions: image-only,
OCR-assisted with ΑΒΓΔ, and OCR-assisted with ΨΩΘΞ. A supported hint intentionally
supplied the generated reference and served as a control. Each model saw each
condition three times, with the condition order rotating across repetitions. All
81 planned requests completed transport across 27 separate CLI batches. All 54
planned pair comparisons were available. Each of the 27 model/condition/image
cells produced identical text across its three repeats.

## Exact outcomes

Literal whole-string comparison preserved case, spacing and script. Each cell
below uses its full planned denominator. The two supported-hint text examples
per repetition are reported separately from the conflicting-hint examples.

| Model | Image-only text exact | Image-only blank empty | Supported-hint text exact | Conflicting-hint image exact | Conflicting-hint copy | Assisted blank empty | Assisted blank exact hint |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Qwen3-VL 8B | 3/6 | 0/3 | 3/6 | 0/6 | 0/6 | 0/6 | 0/6 |
| Qwen2.5-VL 7B | 3/6 | 0/3 | 3/6 | 0/6 | 0/6 | 0/6 | 0/6 |
| Mistral Small 3.1 24B | 3/6 | 3/3 | 3/6 | 3/6 | 0/6 | 0/6 | 3/6 |

## Matched comparisons

Each model had nine fixed-image hint swaps, six fixed-hint text-image swaps and
three image-only text-image swaps. Both Qwen models changed six of nine outputs
when the hint changed on the same image. Their other three outputs were stable
but differed from the image target. Mistral changed six of nine outputs and kept
the exact ΑΒΓΔ reading in the other three pairs. Each model changed output across
both different text images for all six assisted and all three unassisted image
swaps. No model returned both text targets exactly in any of those pairs.

The ΑΒΓΔ fixture makes the hint effect concrete. Both Qwen models read it exactly
without assistance and with the supported hint. The conflicting ΨΩΘΞ hint changed
Qwen3's answer to `ABΓΔ`, using Latin A and B, and Qwen2.5's answer to replacement
characters. Mistral kept ΑΒΓΔ across all three conditions. For the ΨΩΘΞ fixture, all
three models produced an inexact reading even with the supported hint.

The three repeats measure consistency on the same pixels. These results apply
to the exact synthetic images, hints, model packages and CLI settings in the
receipts. The two text images cover uppercase letters. Natural image accuracy and
correction effort require independently reviewed material and appropriate controls.

[Scoring specification](../../.artifacts/vlm-benchmark/crossed-grounding-proposal-v1/scoring-specification.md) ·
[Frozen inputs](../../.artifacts/vlm-benchmark/crossed-grounding-proposal-v1/proposal.json) ·
[Raw execution receipts](../../.artifacts/vlm-benchmark/crossed-grounding-proposal-v1/live.5X9km8) ·
[Offline evaluator](../../.artifacts/vlm-benchmark/crossed-grounding-proposal-v1/evaluate.py) ·
[Row and pair report](../../.artifacts/vlm-benchmark/crossed-grounding-proposal-v1/report-v1.json).
