# Anchored focal diagnostic for the glyph-contrast studies

The strict whole-line scores remain the primary benchmark results. This secondary
analysis separates recoverable whitespace differences from focal glyph substitutions.
It accepts only completed outputs with both exact three-scalar carriers and exactly
one scalar in the focal position. It reports changed carriers, missing or overlong
focal spans, and failed transport as separate unavailable cases. The whitespace
diagnostic deletes only the listed Unicode White_Space scalars from the already
NFC output. It preserves case and marks and performs no additional normalization.

Each row has 96 planned text slots and 48 planned pair slots. Counts include both
repetitions of the same fixtures. Both correct means that both anchored members
returned their expected focal scalar. The strict correct counts equal whole-line
exactness for this anchored seven-scalar design.

| Study and model | Strict correct | Strict both correct | Correct after whitespace removal | Wrong after whitespace removal | Unavailable after whitespace removal | Both correct after whitespace removal |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Wide, Qwen3-VL 8B | 0 | 0 | 24 | 38 | 34 | 4 |
| Compact, Qwen3-VL 8B | 46 | 12 | 58 | 26 | 12 | 22 |
| Wide, Qwen2.5-VL 7B | 0 | 0 | 30 | 56 | 10 | 0 |
| Compact, Qwen2.5-VL 7B | 36 | 6 | 46 | 36 | 14 | 14 |
| Wide, Mistral Small 3.1 24B | 0 | 0 | 26 | 20 | 50 | 0 |
| Compact, Mistral Small 3.1 24B | 20 | 4 | 24 | 14 | 58 | 4 |
| Compact only, Qwen3.8 27B | 28 | 4 | 34 | 16 | 46 | 6 |
| Compact only, Gemma 4 12B | 0 | 0 | 0 | 0 | 96 | 0 |

The two additional models have no wide comparison. All 96 Gemma text slots were
unavailable at the transport boundary. Qwen3.8 returned a recoverable focal scalar
on 50/96 compact text slots after whitespace removal. The blank slots stay in
the primary results as a separate repeated control.

The matched comparison also counts slots anchored in both versions after whitespace
removal. This subset has 62 slots for Qwen3, 80 for Qwen2.5 and 26 for Mistral. Correct
counts changed from 24 to 44 on Qwen3, 26 to 46 on Qwen2.5 and 20 to 20 on Mistral.
The two Qwen increases therefore include changes in focal correctness even after
accounting for whitespace. The full 96-slot transition matrices remain the basis
for checking unavailable or newly anchored cases. This conditional subset describes
the observed responses and should not substitute for the planned denominator.

Tighter gaps were selected after inspecting the wide runs. The fixture review found
two prefix raster changes in the Times capital-lunate pair alongside the intended
placement changes. The result describes these controlled generated images and the
observed model runs. [Method](../../.artifacts/vlm-benchmark/glyph-focal-offline/design.md) ·
[Wide diagnostic](../../.artifacts/vlm-benchmark/glyph-focal-offline/v1.json) ·
[Compact diagnostic](../../.artifacts/vlm-benchmark/glyph-focal-offline/v2.json) ·
[Matched transitions](../../.artifacts/vlm-benchmark/glyph-focal-offline/comparison.json).
