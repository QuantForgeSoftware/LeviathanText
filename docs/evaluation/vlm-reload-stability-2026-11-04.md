# LM Studio fresh-worker repeatability

Both models returned byte-identical text on matched fixtures across four
fresh worker visits and their immediate repeat batches. All 80 planned
requests completed, and all eight owned workers were unloaded.

The visits started at 13:40, 14:05, 14:30 and 14:55 CST. They alternated model
load order and used the five frozen generated setup PNGs. Each worker ran
two consecutive identical batches of five images. The saved LM Studio
verbosity was Debug at every visit.

| Model | Exact first-to-repeat text pairs | Exact text slots / 32 | Blank passes / 8 | Median first-batch request | Median repeat-batch request | Median paired change | Load range |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Qwen3-VL 8B | 20/20 | 16 | 0 | 461 ms | 158 ms | −260 ms | 1.26–4.85 s |
| Mistral Small 3.1 24B | 20/20 | 16 | 8 | 593 ms | 197 ms | −321 ms | 4.58–14.04 s |

Request time is the provider's elapsed time. The paired change takes the
median of repeat minus first for each matched fixture and visit. Load times
are separate elapsed CLI load durations. Every fixture also had a single
unique final string across visits within each batch position. Stable text
includes stable recognition errors. Host activity and cache history were
uncontrolled, and reloading a worker does not empty the OS page cache.
The timing comparison is descriptive.

The offline evaluator retains every planned visit and slot, verifies each
image and manifest join, and reports the completed pairs separately. An
empty-run control preserves all 80 slots as missing.
[Registered design](../../.artifacts/vlm-benchmark/reload-stability-v1/design.md) ·
[Full results and pinned source receipts](../../.artifacts/vlm-benchmark/reload-stability-v1/report.json).
