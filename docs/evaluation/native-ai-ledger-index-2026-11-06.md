# Native AI request-local ledger index: incomplete resource campaign

The isolated index candidate is **not adopted**. The registered 24-visit comparison stopped on its first resource failure: visit 6, index/F1000, exceeded the unchanged 1 GiB sampled-RSS abort after 25 successful warmup requests. Five visits completed; no growing-history visit or second/third replicate ran. This is a resource regression at the registered cap, not a completed performance improvement or evidence of a native memory leak.

## Registration and preservation

All evidence below is under `.artifacts/experiments/perf02-ledger-index-v1/`. The main `NativeAiProposalService.cs` remains unchanged, SHA256 `c58da0748fe022dcca19ef00bc1b0db73ff06b0b0f5b84f13c7cc975002b0e54`. Candidate v2 SHA256 is `61daaa7792219543e794a39ea5f37b4a87feff5ced59f9ec4793a98436a35084`.

`full24-execution-registration-v2.json` and `full24-root-binding-v2.json` bind explicit root approval to the reviewed design, 491 hashed inputs and the actual execution. An inherited scope string says “PROPOSAL ONLY”; the separate approval binding and `executionAuthorized: true` authorize this run. The sealed file was not rewritten to conceal that wording.

Actual start was November 6, 02:36:37 UTC. The hard campaign deadline was 10:00 UTC, with a 1,800-second API visit cap, 1 GiB sampled-RSS abort and five-second TERM grace. Each visit scheduled 1,000 operations at one-second start cadence. Fifty warmup operations remained inside state, operation, provider and resource budgets; only latency distributions excluded them. Fixed arms replay an already accepted proposal without native writes or provider calls. The planned growing arm would issue and accept 1,000 new proposals. It never ran.

The frozen API pair contains 112 files per variant, differing only in Application DLL/PDB. Its dependency closure predates the separately corrected main Imaging fallback guard; no images are used here. It is an internal generated-text experiment, not a current release package or security clearance. All 491 registered inputs were unchanged at final closure.

## First failure and cleanup

At **04:00:33.626074 UTC**, the visit-6 RSS sample was **1,066,272 KiB (1,041.28125 MiB)**. `06-index-F1000/watchdog.json` records `reason: rss_cap`, immediate TERM to owned API PID 91696, and exit 0. The next HTTP attempt received connection refused because the watchdog had already stopped that API. Connection refusal is therefore the downstream error, not the first cause. The wall/deadline caps did not fire.

Visit 6 completed 25 requests, all warmup. It did not reach its first scheduled original-verifier checkpoint at operation 50. A separately labelled, post-failure read-only SQLite comparison found its native JSON and row version **exactly equal** to the registered F1000 prefix: 4,180,973 UTF-8 bytes, row version 2, raw SHA256 `587b7043e776a9e16bb3ac784b12753f71c5acaf2a6a4bdcd994516840438b22`. This byte comparison does not invent a missing scheduled checkpoint.

Final `results.json` records every owned API/verifier exited with code 0, every API sampler exited, all watchdogs joined and `providerThreadAlive: false`. The driver exited 1 to report failure. All 5,025 completed requests were fixed-history replays; there were zero dummy-provider requests. `owned.json` is an earlier snapshot; final closure metadata is in `results.json`. No visit was retried, discarded or substituted, and no cap/order/deadline changed.

## Partial observations

Each completed visit passed all six original-service integrity checkpoints and retained exact native JSON/row version. Complete F1 and F250 pairs have identical full comparison hashes within each arm. These are one pair each, not the planned three replicates.

| Visit | Variant / retained acceptances | Completed requests | Timed median, ms | Sampled peak RSS, MiB |
| --- | --- | ---: | ---: | ---: |
| 1 | Baseline / 1 | 1,000 | 4.231 | 235.719 |
| 2 | Index / 1 | 1,000 | 5.339 | 300.234 |
| 3 | Baseline / 250 | 1,000 | 18.752 | 445.984 |
| 4 | Index / 250 | 1,000 | 17.225 | 460.359 |
| 5 | Baseline / 1,000 | 1,000 | 58.192 | 913.594 |
| 6 | Index / 1,000 | 25 warmup only | Not a timed visit | 1,041.281, abort |

The pre-frozen analyzer deliberately wrote `full24-incomplete-analysis-v2.json` and refused full-matrix analysis. `failure-closure-summary-v2.json` contains explicitly post-failure descriptive extraction and exact prefix-byte comparison; it is not a replacement registration. No full-campaign latency ratio, growing-history conclusion, significance claim or release SLO follows.

Earlier isolated functional evidence remains valid within its scope: 96 generated cases across 30 parity/error families, 41 existing native service tests and 30 native HTTP tests passed against v2. Element-visit counters demonstrate linearized history lookup traversal while preserving validation. They do not override the resource failure. Observer controls remain separate: actual-v2 eight-visit ON/OFF controls passed at 100 ms cadence, with median-of-visit-median ON/OFF ratios 1.030 at F1 and 1.013 at F1000. They neither guarantee negligible overhead at one-second cadence nor permit changing the failed campaign's limits.

## Source and telemetry diagnosis, without new execution

The candidate's `RequestHistoryIndex` is allocated inside each `ReadLedger` call. It has no static field, service field, cross-request cache or event subscription. Its revision-location records, first/count wrappers and dictionaries live with that ledger read. The source therefore shows additional request-local allocation and temporary graph references, not an identified permanent retention root. Every attachment parse, checksum, source equality and receipt/revision/annotation digest check still runs. Repeated canonical serialization, UTF-8 digest buffers and full native-project deserialization remain substantial work; indexing does not remove them.

Actual hook metadata is server GC, 18 logical processors, .NET 10.0.9, Arm64. By the failed visit's last one-second sample, cumulative allocated bytes were 1,261,822,528 and collection counts were `[2,1,1]`; the last reported GC index was 1. Baseline F1000 had already reached `[4,3,2]` near its first ten seconds and completed with `[426,257,92]`. This is compatible with different allocation/collection pacing, but does not establish why the cap was crossed. `GCMemoryInfo` heap/committed values describe the last GC; they are not contemporaneous live-heap measurements. The hook misses some GC indices and is not a complete pause or allocation-root trace. OS RSS cannot be split into managed/native bytes by subtraction.

No source-justified fix for the RSS failure is established yet. Eliminating small index wrapper allocations or caching an exact canonical source string might reduce work, but neither is demonstrated to restore the cap and neither is applied. Do not add forced GC, change server/workstation GC, raise the cap or discard warmup to relabel this result successful.

## Proposed next bounded diagnostic, requiring separate approval

Keep main unchanged. Before choosing another implementation, preregister an **unchanged baseline/v2 F1000 ABBA diagnostic**, fresh copied prefix per visit, fixed short request count/cadence and the same 1 GiB abort. Preserve every failure rather than selecting survivors. This is a new diagnostic, not continuation of the failed 24 visits. Capture runtime GC settings/environment, cumulative allocation, collection counts, current managed-allocation estimates separately from last-GC metadata, and an explicit owned-process root/GC diagnostic if approved. Evaluate whether collectible temporary graphs or a retained root dominate; do not infer this from RSS alone. Any instrumentation needs its own bounded overhead qualification.

Only a concrete hypothesis supported by that evidence should lead to another isolated candidate. It must retain the full error-order/parity matrix, source/checksum validation, no persistent cache/schema/history deletion, separate unchanged dependency closure, and root review before any performance run or main adoption. No further API, profiling, model, provider or test process was started for this source-only diagnosis.

Generated ASCII `A`→`B` graphs contain no natural corpus, Greek labels, images, model weights or training. Historical P46/P01 data and specialist gates are unchanged. This work makes no Greek accuracy, memory-safety, native-leak correction, licensing or all-platform readiness claim.
