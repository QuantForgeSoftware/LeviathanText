# VLM benchmark session, 2026-11-04

The session began at approximately 13:13 CST. This handoff records generated-image
experiments and mechanical review preparation. The original evidence, new registrations,
and retained model outputs are under `.artifacts/vlm-benchmark/`.

| Work | Verified result |
| --- | --- |
| Debug trace audit | Nine image identities joined to grid and batch logs by model ID and request time. One malformed non-grid record retained. [Trace audit](../evaluation/vlm-09-debug-trace-2026-11-04.md) |
| Geometry interpretation | The earlier 90-row study and the nine matching Debug responses were independently joined. Padding cost eight to nine times as many image tokens on the traced line, with mixed effects on edit distance. [Paired audit](../evaluation/vlm-geometry-paired-offline-2026-11-04.md) |
| Crossed OCR hints | 81 completed requests and 54 available paired comparisons. Every assisted blank returned text. [Crossed report](../evaluation/vlm-crossed-grounding-2026-11-04.md) |
| Matched glyph controls | 300 original and 500 compact planned rows retained. The first layout had artificial visible gaps inconsistent with its continuous-text targets. The compact follow-up improved strict accuracy on all three matched packages. [Glyph report](../evaluation/vlm-glyph-contrast-2026-11-04.md) |
| Occluded focal glyph | All 108 requests completed. Neither hidden alternative appeared between exact anchors after whitespace removal. The diagnostic and literal outcomes remain separate. [Masked report](../evaluation/vlm-masked-focal-2026-11-04.md) |
| Native reasoning | 12 matched setting probes and 200 compact requests. Reasoning off recovered final-content availability for Gemma under the fixed budget. [Native report](../evaluation/vlm-native-reasoning-2026-11-04.md) |
| Scholarly review intake | 36 simulated editor assignments and 18 worksheet-checker cases. All 51 natural crop rows remain provisional. [Preflight and evidence](../evaluation/vlm-reviewed-run-preflight.md) |

The glyph report uses the CLI scorer's NFC scalar metric. Eight independent
replays agreed with the diagnostic's full denominators and scalar edit counts.
The generator verified pair-local pixel differences and an independent decoder
rechecked them. The first layout is preserved with its known spacing flaw.
One build could not retrieve NuGet vulnerability data. Fixture generation,
subsequent offline builds and runtime verification completed.

The current packages and configurations all remain exploratory. The P46
release requires actual specialist decisions, adjudicated crop geometry and
a named evaluation custodian before VLM-06. A Classical Greek claim requires
a separately reviewed corpus and an appropriate comparator. No fitting was
performed during this two-hour session.

All four fresh-worker visits completed. Each of the two packages retained exact output
across visits and immediate repeat batches on the five fixtures. Load time and request
time are reported separately. [Repeatability evidence](../evaluation/vlm-reload-stability-2026-11-04.md).

The shipped CLI response-format comparison completed all 200 requests. Both
JSON profiles produced valid objects on every request and empty decoded text
on both presentations of the blank. Exact recognition of both pair members
was unchanged for each model. [CLI profile results](../evaluation/vlm-cli-format-compact-2026-11-04.md).

Across the new experiments, the session retained 1,481 planned synthetic
inference slots. The figure counts repeated requests and includes truncated
and final-message-unavailable responses. No natural corpus rows were sent
in this session.

The final artifact audit matched every CLI result to its planned sample,
repetition and image hash, checked native slot counts, and verified local
report links. The five installed model files and their five projectors
matched the hashes already pinned for their respective runs.
[Session audit](../../.artifacts/vlm-benchmark/two-hour-session-audit/receipt.json) ·
[Model hash verification](../../.artifacts/vlm-benchmark/reload-stability-v1/final-model-hash-check.txt).
