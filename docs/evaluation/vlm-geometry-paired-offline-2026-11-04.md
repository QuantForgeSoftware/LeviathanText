# Paired synthetic geometry analysis

The Debug trace reproduced the exact captured transcription in all nine joins to the earlier geometry study. Each join matched the model family, representation, image hash, and prompt hash. The trace used one fixture, a 128-token output cap, and a later CLI build. The earlier study used a 512-token cap. The repeated outputs support linking the measured token grids to this fixture's observed transcription behavior. A single repeat per condition gives no estimate of variance.

## Edits by fixture

The table gives NFC scalar edit counts in raw / padded / 2× order. Eight uppercase text fixtures have 50 reference scalars each. The polytonic fixture has 30, giving 430 text scalars per condition. The blank is scored separately. The 90 historical requests contain 81 text observations and nine blank observations on ten shared fixtures.

| Fixture | Reference scalars | Qwen3-VL 8B | Qwen2.5-VL 7B | Mistral Small 3.1 24B |
| --- | ---: | ---: | ---: | ---: |
| 01 | 50 | 5 / 4 / 6 | 15 / 11 / 15 | 17 / 20 / 44 |
| 02 | 50 | 3 / 2 / 1 | 3 / 6 / 8 | 15 / 19 / 41 |
| 03 | 50 | 5 / 3 / 4 | 15 / 10 / 9 | 17 / 16 / 40 |
| 04 | 50 | 3 / 2 / 2 | 8 / 8 / 10 | 17 / 13 / 41 |
| 05 | 50 | 5 / 1 / 5 | 8 / 50 / 50 | 14 / 14 / 47 |
| 06 | 50 | 4 / 3 / 1 | 7 / 5 / 7 | 10 / 6 / 37 |
| 07 | 50 | 21 / 3 / 5 | 11 / 18 / 17 | 16 / 27 / 36 |
| 08 | 50 | 15 / 16 / 6 | 19 / 20 / 17 | 15 / 17 / 41 |
| 09, polytonic | 30 | 6 / 6 / 6 | 5 / 3 / 8 | 11 / 11 / 29 |
| Total text edits | 430 | 67 / 40 / 36 | 91 / 131 / 141 | 132 / 143 / 356 |
| 10, blank output length | 0 | 1 / 1 / 1 | 2 / 2 / 2 | 0 / 0 / 49 |

Qwen3 saved 27 edits with padding, 18 of them on fixture 07. Both transformations still saved edits when any one text fixture was removed. Qwen2.5 added 40 edits with padding. Fixture 05 alone added 42, with replacement characters and a switch to lowercase in the captured output. Removing that fixture changes the padded comparison to two edits saved across the remaining eight. Its 2× comparison remains worse even after the same omission. Mistral's 2× hypotheses ranged from 1 to 22 scalars on 30- to 50-scalar text fixtures, and every text fixture worsened. These omission checks describe sensitivity within the observed set.

## Token cost on fixture 01

Each entry gives observed image tokens and edit count. The token count comes from one backend image batch. Mistral reports a one-dimensional grid. The three token costs per model belong to fixture 01 alone.

| Model | Raw tokens / edits | Padded tokens / edits | 2× tokens / edits |
| --- | ---: | ---: | ---: |
| Qwen3-VL 8B | 106 / 5 | 848 / 4 | 424 / 6 |
| Qwen2.5-VL 7B | 122 / 15 | 1098 / 11 | 605 / 15 |
| Mistral Small 3.1 24B | 123 / 17 | 1002 / 20 | 609 / 44 |

Padding cost eight to nine times the raw image tokens and saved one edit for Qwen3 and four for Qwen2.5 on this line. Mistral added three edits. The earlier latency runs had host and cache confounds, so this table uses measured token counts as its resource indicator. The experiment covers generated typography. Manuscript preprocessing choices require reviewed natural crops, including blank and damaged controls, and token measurements across those same crops.

The read-only audit recomputed all 90 historical distances, verified the nine image/prompt joins and exact text matches, checked each logged grid against the batch total, and verified 17 input hashes before and after analysis. [Audit and source](../../.artifacts/vlm-benchmark/geometry-paired-offline/audit.json) · [Reproduction script](../../.artifacts/vlm-benchmark/geometry-paired-offline/audit.py) · [Historical study](vlm-geometry-results-2026-11-04.md) · [Debug trace](vlm-09-debug-trace-2026-11-04.md).
