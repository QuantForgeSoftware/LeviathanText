# VLM-09 debug trace

All nine requests completed. The trace used one generated line in three forms and one request per form and model. The runtime reported one image entry and one encoding batch per request. Request order was scale2, pad, raw in each run. The table pairs that order with the recorded grid and independently logged batch token count.

| Model | Raw grid / tokens | Padded grid / tokens | 2× grid / tokens |
| --- | ---: | ---: | ---: |
| Qwen3-VL 8B | 53 × 2 / 106 | 53 × 16 / 848 | 106 × 4 / 424 |
| Qwen2.5-VL 7B | 61 × 2 / 122 | 61 × 18 / 1098 | 121 × 5 / 605 |
| Mistral Small 3.1 24B | 123 × 1 / 123 | 1002 × 1 / 1002 | 609 × 1 / 609 |

These are backend token grids. Mistral reports a one-dimensional grid. Padding increased the image token count about eight to nine times for this fixture. Recognition quality and natural-image geometry remain separate evaluation questions.

The runner unloaded each trace model and checked that no trace model remained loaded. The user operated the logging selector. [Run receipts](../../.artifacts/vlm-benchmark/vlm-09-debug-retry/live.K1qczI).

The offline audit matches each grid and batch event to its model identifier and
the corresponding request time interval. It verifies all nine image hashes and
checks that each grid product equals the separately logged batch total. One
Mistral log record is malformed. It contains neither grid nor batch markers, and
all three of Mistral's expected grid and batch pairs have valid records. The
operator chose to retain Debug after the run.
[Audit and source hashes](../../.artifacts/vlm-benchmark/vlm-09-debug-retry/audit.json).
