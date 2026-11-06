# Native AI fixed-history GC observation, Phase A

**The index remains unadopted.** Both baseline visits completed 100 requests. The first index visit completed 100; the second exceeded the unchanged 1 GiB sampled-RSS cap after 45 warmup requests. Exact native data and original-service integrity passed before/after every visit, including the capped visit. No forced GC or GC-setting change occurred. This separate diagnostic does not erase the [failed 24-visit campaign](native-ai-ledger-index-2026-11-06.md).

## Registered execution and closure

Evidence is under `.artifacts/experiments/perf02-ledger-index-v1/`. `gc-phaseA-execution-v1.json` SHA256 `b5a3c68668cf2ffc6106d22e6305a69004681f2fe1c3be74a4f0a423f2f783dd` binds root approval, 505 unchanged inputs and the preserved baseline/index runtime pair. Its separate root binding SHA256 is `b918b98801a1265a3cbb0f6d15c98a64dcec2905a70cc9a37fcc5c7fc11d8243`. The original execution-disabled template remains unchanged.

Actual start was 04:31:53 UTC, with a sealed 04:46:53.676587 UTC deadline. Fixed order was baseline/index/index/baseline, 100 requests at one-second cadence, 1 GiB sampled-RSS cap, 180-second API wall cap, independent 60-second verifier caps and five-second TERM grace. The 330-second next-visit reserve includes both verifiers and cleanup. Pre-verification finished before API launch; post-verification followed complete API/sampler/watchdog closure. No intermediate verifier interrupted the request sequence.

At 04:36:14.890274 UTC, visit 3 recorded **1,062,400 KiB = 1,037.5 MiB** RSS. The watchdog recorded `rss_cap`, sent TERM to owned API PID 9918 and observed exit 0. Its subsequent HTTP failure is downstream of that cap. The prespecified continuation policy permitted visit 4 only after cleanup, original-service validation and exact native-byte/version equality. There was no replacement or retry.

All four visits were attempted; three passed and one failed. All 12 API/verifier records exited 0, all watchdogs joined, all samplers exited, and the dummy-provider thread closed with zero requests. The driver exited 1 as required. All native snapshots stayed at row version 2 and raw SHA256 `587b7043e776a9e16bb3ac784b12753f71c5acaf2a6a4bdcd994516840438b22` (4,180,973 UTF-8 bytes). Inputs remained unchanged. The data and closures are in `gc-phaseA-v1/results.json`; post-run extraction is `gc-phaseA-analysis-v1.json`.

## Matched Phase-A observations

| Visit | Variant | Completed | First 45 request median, ms | Sampled peak RSS, MiB | Cumulative allocation delta, bytes | Final collection counts, gen 0/1/2 |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| 1 | Baseline | 100 | 59.519 | 807.578 | 8,036,120,000 | 37 / 22 / 9 |
| 2 | Index | 100 | 44.360 | 971.313 | 4,761,709,024 | 17 / 11 / 8 |
| 3 | Index | 45, cap failure | 44.884 | 1,037.500 | 2,178,436,616 | 5 / 3 / 2 |
| 4 | Baseline | 100 | 59.233 | 817.344 | 8,019,381,776 | 40 / 24 / 11 |

The first 45 requests are matched by recorded operation number and **all are warmup**. Their diagnostic timings are not a release benchmark. The failed index visit has no post-warmup latency population; do not pool its missing operations with surviving runs. Allocation deltas above cover each visit's actual sampled interval, so the 45-request total cannot be directly compared with 100-request totals as a normalized rate. Full per-operation rows preserve nulls for its unperformed operations.

The driver recorded request operation number/duration/lateness but not absolute request UTC/start epoch. Hook samples can therefore align by elapsed seconds from their first sample, **not an exact request boundary**. The analysis preserves a separate elapsed-time counter table and does not invent per-request GC attribution. This is a telemetry limitation to correct before a later intervention.

All samples report server GC, 18 logical processors, interactive latency, .NET 10.0.9/Arm64. The exact numeric/boolean runtime GC allowlist is retained in every raw sample and analysis. Its values are identical across visits: concurrent GC true, retain-VM false, high-memory percentage 90, hard-limit values 0 and dynamic-adaptation mode 1. Last-GC available memory was 68,719,476,736 bytes. These are observations, not a proposal to change GC settings or proof that a particular setting caused failure.

The passing index visit's current-heap estimate reached 949,936,808 bytes and later ended at 265,444,392. The failed index visit's estimate reached 947,456,544 and ended at 697,514,688; its final reported last-GC heap was 346,725,968 bytes at GC index 5. In its last five samples, allocated bytes and the current estimate grew while that last-GC heap/index stayed fixed. This demonstrates why last-GC metadata cannot substitute for current estimates. A fall in an estimate is compatible with collection, but is not a managed-root census or proof that no references survive requests.

## Interpretation and remaining gate

Within this diagnostic, the index reduced observed work/allocation in the passing visit and had higher sampled RSS peaks in both visits, including another cap failure. Fewer collections and different accumulation patterns are visible; their cause and retained object roots remain unproved. There is no basis to call the change safe at the registered cap or infer native allocations by subtracting managed estimates from RSS.

Comparisons with the original campaign are not controlled causal tests: Phase A used a new hook with `GetTotalMemory(false)`, a 100-request budget and pre/post-only verifier scheduling, plus fresh processes/order. Its first index pass cannot be attributed to the new counter read or any GC effect without isolation, and cannot override either capped index failure. Counter samples are non-atomic; last-GC fields, current heap estimates, cumulative allocation and OS RSS answer different questions. The new observer has a harmless managed functional smoke, not a fresh performance-overhead acceptance claim.

The next possible step is a separately reviewed sacrificial collection/root diagnostic at a fixed no-request-in-flight boundary, with exact request lifecycle observation and explicit intervention metadata. Preparation is authorized; execution is not. No production GC behavior, heap limit, model, provider, corpus, training or main-service change follows from this report.
