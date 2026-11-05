# VLM resource observations — 2026-11-04

**Frozen run-r4: 12 closed natural P46 conditions; 0 repeatability conditions remain unscored.** This appendix reads existing supervisor traces and retained request receipts. It makes no live system probes, model calls or fitting operations. Synthetic controls have different workloads and are kept in their separate reports.

These measurements describe a shared, changing host. They establish no code speedup, isolated model-memory requirement or clean comparison between models. Model reload, cache history, downloads/acquisition, other applications and concurrent builds differ across runs. A solution verification build at approximately 09:35–09:36 CST lasted 4.26 seconds and is another possible confound during late Qwen2.5 V1 work.

## Request latency and elapsed time

All finite recorded request durations—including failures and truncations—are retained. Missing/unattempted durations are unavailable, never zero. Quantiles use nearest rank ceil(p×n), with no interpolation. For 51 requests, p95 selects rank49 and excludes the largest two durations; maximum and total elapsed therefore matter. Request-duration sums exclude time outside measured requests. Supervisor wall time also includes child startup, validation and shutdown, and does not necessarily include model loading.

| Condition | Timed / planned | p50 ms | p95 ms | Maximum ms | Request sum s | Supervisor wall s | Sampled summed RSS MiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| First: Qwen3-VL 8B V1 | 51/51 | 5,722.71 | 7,555.07 | 8,287.73 | 283.61 | 284.85 | 5,918.42 |
| First: Qwen3-VL 8B V2 | 51/51 | 4,961.13 | 6,661.69 | 18,044.87 | 276.19 | 276.97 | 6,628.62 |
| First: Qwen2.5-VL 7B V1 | 51/51 | 676.67 | 4,396.86 | 20,211.43 | 107.72 | 109.05 | 5,890.58 |
| First: Qwen2.5-VL 7B V2 | 51/51 | 645.73 | 1,195.73 | 7,356.90 | 45.60 | 46.35 | 6,611.77 |
| First: Mistral Small 3.1 24B V1 | 51/51 | 1,005.12 | 2,348.36 | 3,624.25 | 59.28 | 60.03 | 15,998.48 |
| First: Mistral Small 3.1 24B V2 | 51/51 | 1,276.57 | 1,831.26 | 15,156.25 | 80.54 | 81.32 | 15,782.30 |
| Repeat: Mistral Small 3.1 24B V1 | 153/153 | 836.09 | 2,268.38 | 14,630.67 | 162.44 | 163.84 | 16,057.11 |
| Repeat: Mistral Small 3.1 24B V2 | 153/153 | 1,143.38 | 1,666.68 | 16,519.23 | 215.96 | 217.27 | 17,038.39 |
| Repeat: Qwen3-VL 8B V1 | 153/153 | 477.31 | 606.07 | 766.79 | 73.84 | 75.03 | 9,890.42 |
| Repeat: Qwen3-VL 8B V2 | 153/153 | 590.03 | 731.00 | 1,957.86 | 93.96 | 95.13 | 12,508.52 |
| Repeat: Qwen2.5-VL 7B V1 | 153/153 | 499.65 | 3,121.64 | 37,567.63 | 187.91 | 189.13 | 7,860.48 |
| Repeat: Qwen2.5-VL 7B V2 | 147/153 | 2,359.97 | 6,997.17 | 42,205.52 | 478.64 | 480.72 | 6,618.52 |

For example, first-sweep Qwen2.5 V1 has p95 4,396.86 ms but maximum 20,211.43 ms; Mistral V2 has p95 1,831.26 ms but maximum 15,156.25 ms. Reporting only p50/p95 would obscure those tails. The first timed request, mean, CLI elapsed time and every source-window timestamp remain in the artifact summary.

The Qwen2.5 V2 repeatability condition reached its eight-minute allocation: 147 attempted requests include 140 Completed, six Truncated and one Cancelled; six remaining slots are explicit NotAttempted. All 153 planned slots remain in the outcome report. The four earlier repeatability conditions had 20-minute caps, Qwen2.5 V1 had 12 minutes and V2 had eight; unequal caps and the global stop preclude a clean full-condition rate/quality comparison. Unattempted slots have no invented latency or model-error attribution.

Qwen3 V1 p50 changed from 5,722.71 ms in the first sweep to 477.31 ms after reload and acquisition completed. This is a host/cache/acquisition confound, not a code speedup. The model/request settings alone do not make these execution environments equivalent.

RSS is an approximately one-second sum of LM Studio and related processes plus the runner. It can double-count shared pages, miss shorter peaks and include related download/helper activity. It is neither GPU allocation, exclusive model memory nor an exact peak. The artifact also records the maximum across before/during/after snapshots separately from the supervisor loop peak.

## Global VM and swap observations

The following deltas subtract the first resource snapshot from the last. `Pageins`, `Swapins` and `Swapouts` are global host counters, not per-process or per-model attribution. Every trace reports a 16,384-byte VM page size; page counts are retained without pretending they equal physical disk bytes. Swap-used values retain macOS's displayed unit label `M` rather than imposing an undocumented conversion. A decrease in swap used does not establish absence of memory pressure.

| Condition | Pageins Δ (pages) | Swapins Δ (pages) | Swapouts Δ (pages) | Swap used first → last (reported M) | Swap-used Δ (reported M) |
| --- | ---: | ---: | ---: | --- | ---: |
| First: Qwen3-VL 8B V1 | 63,687 | 28,182 | 99,984 | 23,555.50 → 24,578.75 | 1,023.25 |
| First: Qwen3-VL 8B V2 | 429,373 | 22,495 | 0 | 25,866.81 → 25,538.56 | -328.25 |
| First: Qwen2.5-VL 7B V1 | 132,335 | 2,507 | 0 | 25,298.56 → 25,234.56 | -64.00 |
| First: Qwen2.5-VL 7B V2 | 11,108 | 1,018 | 0 | 25,226.56 → 25,218.56 | -8.00 |
| First: Mistral Small 3.1 24B V1 | 6,864 | 4,790 | 0 | 24,305.06 → 24,249.06 | -56.00 |
| First: Mistral Small 3.1 24B V2 | 11,748 | 17,256 | 46,996 | 24,193.06 → 24,687.31 | 494.25 |
| Repeat: Mistral Small 3.1 24B V1 | 122,630 | 40,290 | 165,596 | 26,104.19 → 28,083.62 | 1,979.43 |
| Repeat: Mistral Small 3.1 24B V2 | 173,354 | 16,403 | 118,840 | 27,987.62 → 29,604.50 | 1,616.88 |
| Repeat: Qwen3-VL 8B V1 | 7,763 | 5,358 | 0 | 29,508.50 → 29,428.38 | -80.12 |
| Repeat: Qwen3-VL 8B V2 | 7,163 | 2,451 | 0 | 29,236.38 → 29,212.38 | -24.00 |
| Repeat: Qwen2.5-VL 7B V1 | 348,064 | 13,013 | 0 | 27,281.50 → 27,065.50 | -216.00 |
| Repeat: Qwen2.5-VL 7B V2 | 101,976 | 69,245 | 29,241 | 27,033.50 → 25,663.56 | -1,369.94 |

First-sweep Qwen3 V1 coincided with 99,984 global swapout pages and a 1,023.25 reported-M increase in swap used. The two Mistral repeatability conditions coincided with 165,596 and 118,840 global swapout pages, respectively. These observations identify changing host conditions; they do not establish which process caused them or quantify model-specific I/O. Exact first/last cumulative counter values, snapshot counts and warnings are preserved in the summary.

## Separate idle worker footprint snapshots

These are separate OS footprint snapshots after different workloads. Physical footprint and worker-lifetime peak are the tool's `phys_footprint` and `phys_footprint_peak`; the other columns preserve the tool summary's exact `swapped`, `clean` and `wired` counter names. They are not per-condition peaks, application-wide/GPU/combined memory or RSS. Do not add these counters into a total-memory estimate or substitute them into the RSS table.

| Worker | Snapshot start (CST) | PID | Physical footprint bytes | Worker-lifetime peak bytes | Summary swapped bytes | Summary clean bytes | Summary wired bytes |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| qwen3 | 2026-11-04T09:14:58.972-06:00 | 86128 | 7,413,491,432 | 7,555,720,936 | 0 | 5,065,293,824 | 5,028,849,384 |
| qwen25 | 2026-11-04T09:52:44.133-06:00 | 99539 | 5,622,030,544 | 5,761,687,760 | 5,294,669,824 | 4,685,381,632 | 4,683,079,888 |
| mistral | 2026-11-04T10:02:03.021-06:00 | 11973 | 6,099,729,600 | 6,238,256,320 | 6,088,212,480 | 14,369,128,448 | 14,336,818,368 |

- **qwen3**: Idle snapshot after the natural repeatability runs; later synthetic work is outside this snapshot.
- **qwen25**: Idle snapshot after the current worker completed its natural and synthetic studies.
- **mistral**: Idle snapshot of a newly loaded worker used only for the three synthetic geometry conditions; earlier natural/prompt studies used a different worker.

The snapshots establish no clean memory comparison: worker lifetime, prior cache/pressure and completed workloads differ. In particular, the final Mistral worker ran only geometry controls, whereas its earlier natural runs used another worker. The artifact preserves start/end times, full original tool files, target-PID receipts where available and exact reported counters.

## Evidence and interpretation limits

The appendices preserve original hashes and never rewrite request statuses or raw outputs. Missing/reset VM counters produce null deltas rather than fabricated zeroes; swap used may legitimately decrease. Parser checks cover zero/missing counters, reset detection, reported units and nearest-rank quantiles. No source labels are used in resource calculations.

- [Frozen resource summary](../../.artifacts/vlm-benchmark/vlm-bench-1/b/resource-observations/run-r4/summary.json): `9f8112bef273b11af3e97fd056f153a9ed7408dc44d46405f6010744da65b352`.
- [Resource-analysis receipt](../../.artifacts/vlm-benchmark/vlm-bench-1/b/resource-observations/run-r4/receipt.json): `c88b9a3ea4877ca0b888ca1b8579d65bf6f5b28216dee4fccb6f7d87043f264a`.
- [Parser checks](../../.artifacts/vlm-benchmark/vlm-bench-1/b/resource-observations/self-test.log): `581474afe4124a6258d48aff1b8165bc63307265e1e861d74f5b6424d2396cfc`.

See the [first-sweep results](vlm-pilot-results-2026-11-04.md) and [repeatability results](vlm-repeatability-results-2026-11-04.md) for full request denominators and outcome caveats. R0's separate process-only resource replay reported 177.875 MiB and inference-only timings; it is a different measurement scope and is not inserted into this LM Studio summed-RSS comparison. Controlled performance work still requires a separately registered isolated workload, comparable timing boundaries, recorded load/warm-up state and stable host conditions.
