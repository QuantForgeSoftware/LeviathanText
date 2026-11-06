# Retry-safe worker polling follow-up

The first null-only polling candidate failed the fixed delayed-package recovery control: the original worker retried twice and succeeded after publication, while the candidate exhausted four attempts before publication. Those primary throughput and negative recovery receipts remain immutable. This new experiment evaluates a distinct candidate that preserves the one-second pause for idle polls **and Pending retries**, removing it only after terminal attempts.

The independently sealed registration is `.artifacts/experiments/perf01-retry-safe-independent-v2/registration.json` (SHA-256 `cdfd94732da6b139a71d5edfa232192ac90aeff1956cf323febd0a136c9b0a64`). The root owns the frozen worker source and packages under `.artifacts/experiments/perf01-retry-safe-v2`. The baseline is the exact original109-file package; the candidate differs only in Worker DLL/PDB and was compiled against its exact28 dependencies. The original baseline35 semantic cases and the new candidate35 cases pass.

Before dispatch, root approval must bind this exact registration. First, two fresh owned default-loop workers run the fixed generated one-line delayed-package control. The trusted package symlink is published1.25 seconds after observed polling readiness, complete state is inspected at2.75 seconds, and the worker drains. Both must report Pending,Pending,Succeeded, retain two retries, and issue exactly one L0 revision. Full SQLite/native/job/confidence state must match across variants except the single enumerated L0 creation timestamp; observation and post-drain states must also match.

Only after that passes,12 primary processes run the same predeclared ABBA order for100 noops,200 generated one-line surface visits, and30 provisional P46 surface visits. The original12 operational-control processes are not repeated. Complete state parity, sentinels, source/model/runtime hashes, exact attempt IDs and ordering remain mandatory. The17 pure evaluator controls pass; no worker dispatch has occurred at registration time.

The90-minute maximum includes nine minutes reserved for cleanup. Observed process/aggregate RSS abort limits remain2/4GiB with250ms target sampling, retained actual sample times, bounded logs, owned-process-only cleanup, and no adaptive reruns. Fast-process sampling can miss peaks. This is inference resource evaluation only: no fitting, optimizer restore, Greek readings, corpus labels, CER, VLM, LM Studio calls, or new accuracy search. All81 geometry rows remain pending specialist review. ImageSharp advisory and native-memory attribution limits of the historical baseline remain explicit.

## Registered recovery outcome: strict gate failed before primary dispatch

The two-process recovery pair completed, but its additional requirement that the fixed2.75-second snapshot equal the post-drain snapshot failed. At2.75 seconds **both** workers were Running on their third attempt, with no new L0 and an active lease. Both then drained successfully under SIGTERM, reporting Pending,Pending,Succeeded, two retries, and exactly one L0. Their complete final SQLite/native/job/confidence states match after normalizing only `rev-l0-retry-recovery-retry-line.createdAt`.

The observation-equality expectation was too strong for this observed inference completion time. It has not been relaxed inside the sealed experiment: the registration remains failed, its failure is retained, and none of its12 primary workers were dispatched. Both owned workers exited0, the owned publication symlink was removed, and all registered source/runtime/input files remain unchanged. No adaptive timing adjustment or rerun occurred. Details are in `campaign/independent-failure-analysis.json` and both complete observation/drain snapshots.

## Separate primary-only follow-up

After reviewing that retained failure, root approved a separate12-process primary-only study. Its registration is `.artifacts/experiments/perf01-retry-safe-primary-v3/registration.json`, SHA-256 `716a230cd61da30f319793637fc386cce6919cadfc2d4dcc5b8c3779e7e1177b`. The failed observation-equality experiment and complete raw evidence are frozen external inputs to this new registration. It does not repeat recovery, move either timing, or relabel the original gate as passed. The observed Pending intervals were1.005 seconds for baseline and1.007 seconds for the retry-safe candidate, followed by successful drain; this supports preserved retry behavior without a2.75-second latency claim.

The new study retains exactly the12 primary ABBA visits, full graph/confidence/SQL parity, original work counts and package/input hashes, and bounded owned-process cleanup. Root bound the new registration at02:18:41UTC before launch. All 12 visits passed; details follow.


## Primary-only results and independent recommendation

The separately approved primary-only campaign passed all 12 visits in 673.321 seconds. Every owned worker exited zero; no observed cap or log failure occurred. All registered source, runtime, evaluator, model, input, and external evidence hashes remained unchanged. The complete project/job SQL columns and parsed native/job JSON—including exact predictions, confidences, IDs, unknown fields and array order—match across all four visits for each route, with only the registered new L0 creation clocks normalized. Each route also matches the original fixed normalized digest. All sentinels remain pending.

The primary campaign performed 400 noops, 800 generated one-line recognition visits, and 120 provisional P46 surface visits emitting 3,240 L0 lines. Geometry remains unapproved; this is not an accuracy result.

| Route | Variant | All visit wall seconds | Median seconds | Observed peak RSS MiB, both visits | Last sampled CPU seconds, both visits |
|---|---|---:|---:|---|---:|
| N00 | baseline | 99.666150 / 99.667780 | 99.666965 | 79.19 / 79.88 | 0.78 / 0.80 |
| N00 | candidate | 0.276842 / 0.276673 | 0.276758 | not representative; startup sample only | 0.00 / 0.00 |
| G00 | baseline | 201.490281 / 201.363605 | 201.426943 | 194.00 / 194.30 | 3.16 / 3.18 |
| G00 | candidate | 1.120845 / 1.108155 | 1.114500 | 180.42 / 179.36 | 0.88 / 0.85 |
| P00 | baseline | 31.115703 / 31.342305 | 31.229004 | 346.62 / 345.91 | 3.11 / 3.21 |
| P00 | candidate | 1.675347 / 1.687246 | 1.681297 | 337.14 / 336.64 | 1.71 / 1.72 |

CPU counters are the last sampled cumulative values, not final process CPU. The subsecond noop candidates completed between samples: their raw zero CPU and roughly 3 MiB startup RSS are not representative execution measurements. RSS includes managed, native, runtime and shared pages, and short visits have fewer samples. These observations establish neither lower native allocation nor a leak correction, and must not be generalized to every model or platform. All raw visits and sample timestamps are retained in `analysis/full-analysis.json` and worker receipts.

I recommend adopting the **revised idle-or-Pending delay only**, subject to root's fresh production build and required safe regression gate. The one-line change removes the measured fixed pause after terminal work while retaining the demonstrated one-second retry cadence. The original null-only candidate remains rejected. The failed fixed-observation gate remains failed; successful final drain and exact final-state parity support retry recovery without a 2.75-second completion claim.

Operational limits remain: the new primary study does not repeat historical multiworker, authorization, quota, SSE or heartbeat controls. The earlier finite two-worker test showed 60/0 distribution for the fast candidate, so equal sharing and starvation freedom are not established. Automatic production heartbeat has no caller. Faster dispatch increases allocation and event rates; optional process job budgets, existing permissions/model/reviewer gates, and explicit resource limits remain necessary. No universal throughput, fairness, native-memory, all-platform, or scholarly acceptance is claimed.

Receipts: `.artifacts/experiments/perf01-retry-safe-primary-v3/campaign/verification.json`, `analysis/full-analysis.json`, all 12 complete normalized states and preserved raw states, plus the separate failed recovery evidence under `perf01-retry-safe-independent-v2`.

## Main adoption and safe engineering gate

Root subsequently applied the revised `finished is null || finished.Status == JobStatus.Pending` polling condition to main. The existing `engineering-v4` receipts report 1,938 passed checks across 15 selected suites/codecs, zero failed or skipped checks, and a zero-warning/error build. This reviewer inspected those receipts and current polling source, without rerunning tests during the next long API quiet window. The gate is a later main integration check; the independent 109-file timing packages remain their historical frozen versions and must not be relabeled as the full current main package. The known model/platform/resource and scholarly limits above remain unchanged.
