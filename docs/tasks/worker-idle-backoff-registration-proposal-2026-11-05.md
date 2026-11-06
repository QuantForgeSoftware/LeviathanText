# PERF-01 concrete preparation proposal

Prepared November 5, 2026, during the decoder/archive quiet window. **No build, test, worker dispatch, inference or measurement was executed for this proposal.** Root must approve the concrete final registration, release baseline, resource budgets and quiet-window release before execution. This supplements, rather than replaces, the [two-instance brief](worker-idle-backoff-validation-2026-11-05.md).

## Inspected source and permitted inputs

`.artifacts/experiments/perf01-preparation-v1/source-inventory.json` pins the current worker, attempt/semantic tests, authorization/store sources and historical harness/registration metadata. This is a dated preparation snapshot, not a final release closure. Root/R/B are still changing other application dependencies; no old published worker is a valid substitute for a new root-approved baseline.

Current Worker Program calls `RunNext`, logs a non-null result, checks `--once`/`CompletedAttempts`, then unconditionally delays one second. The proposed **isolated-only** delta is exactly:

```diff
-                    await Task.Delay(1000, stopping.Token);
+                    if (finished is null) await Task.Delay(1000, stopping.Token);
```

The textual `candidate.patch` is retained beside the inventory. Main production polling remains unchanged. A lost finalization returns null even after a completed attempt: it must keep the delay, while the unchanged completed-attempt counter still enforces retirement. The break remains before the conditional delay, and cancellation/drain ordering is unchanged.

The existing `WorkerBudgetTests` has 17 generated cases; `WorkerAttemptBudgetTests` has four. The latter explicitly covers lost finalization, failed claim, authorization rejection, unsupported type and retry/success accounting. The two POSIX shutdown cases in the former return early on Windows, so an all-green Windows count would not prove signal behavior. The target here is the existing osx-arm64 host. Add the explicitly selected existing `WorkerHostTests` heartbeat, lease-holder, cancellation/replacement and ordering cases; do not widen to ML/Pilot or enable any capability flags.

Historical authorized input anchors, read from the existing registration/hash metadata rather than model weights or target labels:

| Input | Frozen identity / bounded use |
| --- | --- |
| Generated inputs | Existing harness source generates three `240×96` white/bar/diagonal PNGs with one fixed `(8,16)–(232,80)` polygon and no target strings. Fresh PNGs must be hashed before inference; compare their hashes with preserved historical generated images if available. |
| P46 geometry | `nt20-p46-page-resource-v1/inputs/geometry-only.json`, SHA-256 `2a5c5cf82028f4ae769ee5d25ef65a926fd998e0b6369fd3f400cea12d345829`; 81 rows, counts 27/26/28. Its only row keys are `decision`, `geometry_status`, `line_id`, `page_id`, `proposed_row_quad_review_px`, `review_frame`. No source-reading fields. |
| P46 photographs | Existing retained `inputs/pages/{1440,1450,1460}.jpg`; registered hashes in the parent brief. Only those images and copied Michigan rights notices; no new acquisition or image conversion. |
| Checkpoint | Existing `nt20-p46-page-resource-v1/package/h16-aug.best.model`; registered weight hash `03952a43d94bf9e53d8f5de886d3470057a2db55222c01d14172111a47b06fda`. Weights were **not read** during this preparation. Later approved inference needs the complete registered package closure; optimizer state must never be loaded. |
| Legacy preprocessing | Existing h16 profile absent; preserve the production legacy ink-positive grayscale/standard padded crop route. No diagnostic geometry substitution or profile inference. |

The photographs remain provisional review inputs. The geometry/81 decisions are unadjudicated; this campaign would write L0 only, never score accuracy or create/accept L1/L2. Do not open original review packets, raw crop maps, PAGE/TEI source readings, dataset loaders, accepted labels or locked partitions. Historical attribution includes transcript notices, but no transcription asset is an input to this campaign.

## Frozen build and independent roles

A prepares a fresh source snapshot and worker package from the **next exact root-approved current release**, not the old no-op package. Preserve its complete dependency/native/runtime closure. Compile only the candidate worker sources against that same dependency closure, same target framework/RID/SDK/build options/assembly identity. Copy the baseline package, replacing only Worker DLL/PDB. Fail the preflight if any other file differs. Do not copy the candidate's generated deps/runtime files over the baseline. The one changed source statement must be the only source delta.

A runs the unchanged 21 budget cases plus explicitly named managed lease/order/heartbeat controls against isolated outputs, preserving their test-discovery counts and subprocess shutdown receipts. The subprocess fixture must resolve the intended baseline/candidate Worker assembly, not an unrelated normal build. Generated failure, idle and FIFO signal controls are outside the timed dense-queue matrices.

B independently verifies the sole diff, full file closure, native RID, controls and normalization code. Before the first timed process B seals one fresh registration and retains all failures. Hash full copied source, runner/normalizer/seed sources, exact command lines/environment, baseline/candidate worker closure, inference package, generated images, reduced geometry, retained photos and rights notices, initial DB snapshots and root's current package manifest. Record runtime/OS/hardware/thread/GC settings without tuning from partial results. All output roots must be new and owned. No executable artifact from this preparation is authorized.

## Proposed 90-minute campaign

ABBA means baseline, candidate, candidate, baseline, sequential fresh processes/stores, with **identical** process budget for both variants. No simultaneous performance builds, other registered resource runs or adaptive reruns.

| Phase | Per route | Exact expected state | Hard per-process cap |
| --- | --- | --- | --- |
| N00 | 101 pending no-ops, `--max-jobs 100` | 100 succeeded, sentinel pending, no failed/running/retry/lease | 150 s |
| G00 | 200 fixed generated one-line recognition jobs + sentinel, budget 200 | 200 exact L0 records, sentinel pending, no other layer writes | 360 s |
| P00 | Ten fixed cycles 1440/1450/1460 + sentinel, budget 30 | 30 successful surface jobs, 810 exact L0 records, sentinel pending | 180 s |

The four routes per matrix yield 400 no-op attempts, 800 generated inference attempts and 120 retained-page visits/3,240 L0 records. Repeated visits are engineering repetitions, not independent natural pages. Per-process caps total at most 46 minutes. Proposed remaining allocations: setup/hash/control freeze 10 minutes; semantic/visibility controls 10; fixed fairness/retry/optional identical profiling 15; final comparisons/cleanup reserve at least nine. Overall 90 minutes from campaign start; no automatic extension. Stop launching a phase when its cap plus cleanup reserve no longer fits.

Proposed operational abort budgets are 2 GiB sampled RSS per owned worker / 4 GiB aggregate, pending root approval; they are aborts, not performance acceptance thresholds. Sample OS RSS/CPU at a registered 250 ms interval and retain raw samples plus startup/first/last-attempt timestamps and complete stdout/stderr. Do not infer cold OS cache from fresh processes. Identify loaded native libraries where available; missing native-allocation attribution keeps the resource gate open. Managed heap/live/native telemetry requires a separately registered identical mechanism; absent tools must be disclosed, not replaced by tensor-wrapper or RSS claims.

## Correctness and control details

The normalizer must retain complete job and native-project records, not repeat the historical harness's narrow selected-field inspector. Fix seed IDs, timestamps, common copied input/model paths and order before execution. Allow only explicitly enumerated revision creation clocks and lease process/claim/timing identities to vary; retain their raw records separately. Keep exact IDs, scope/layer/model author/provenance, parents, text, confidence scales/values, scalar/grapheme spans, outputs, retries, failures, progress, geometry and unrelated fields. Do not round confidences or remove a field after a mismatch. Stage-run random IDs not persisted in these job/native outputs are not a reason to drop other identities. All four complete normalized outputs must match, with no missing/duplicate/non-L0 revisions.

Additional generated controls must be fixed before the registration is sealed:

1. **Idle schedule:** launch empty store, observe polling line, wait 1.25 seconds, assert still alive and zero completed attempts, admit one no-op and require exact success/budget exit. Keep null-result backoff. Existing failed-claim/lost-finalization tests are required; do not interpret null as necessarily idle.
2. **Priority/ownership:** fixed priorities/creation times/IDs and the documented descending-priority, ascending-time, ordinal-ID order. Run two separately owned workers on 60 generated no-ops in a separate control store; record every process's completed job IDs/stdout and authoritative final state. Report distribution without a post-hoc fairness threshold. Finalization stdout does not by itself capture every claim: if exact claim ownership/transition evidence is needed, preregister a generated-store-only SQLite transition audit before controls, or disclose that limitation. No instrumentation in primary matrices.
3. **Retries:** a fixed missing-input job and unsupported-type job, bounded attempt budget, existing retry cap and terminal behavior. Preserve authorization and attempt accounting. Do not create a real failing corpus/model job.
4. **Shutdown:** both existing FIFO/blocking generated import controls for SIGTERM/SIGINT, plus idle shutdown and budget exit. Signal only recorded owned PIDs, verify active handler drains/finalizes once and next sentinel remains pending.
5. **Lease/heartbeat:** `WorkerHost.Heartbeat` exists, but **production Worker has no caller**. Verify explicit lease-holder renewal with the existing generated semantic test and record actual persisted lease/progress observation. Do not claim automatic background heartbeat renewal or add it in this performance patch.
6. **Quota/auth/visibility:** real generated admission quota and project binding with counts held stable during rejection, then finalize/admit; disabled/member-revoked jobs must not dispatch. Use a separate provider-free API/worker control store. Actual bounded job SSE/replay must preserve terminal/cursor/authorization semantics with persisted snapshot rechecks. Reuse current generated HTTP controls where appropriate; mere passing managed bus tests cannot be reported as browser SSE evidence.

On drift, parity failure, unauthorized/unknown access, malformed outputs, unknown child PID, resource/time cap or failed drain, stop. Signal only recorded owned groups, allow at most 60 seconds for a bounded active attempt, then terminate only that owned tree if required and record the failed/partial state. Preserve leases and all receipts; do not erase failures or automatically requeue partial writes. Recheck all registered inputs after execution. Native retention is not repaired by removing delays and may grow faster per wall second.

## Decisions still required from root

- Exact current release/source/package hash after the concurrent SEC/JOB/GEO work is reviewed; dependency-advisory status must be stated honestly.
- Approval of the sole isolated candidate, 90-minute start/deadline, quiet-window release, RSS abort budgets and exact remaining generated controls.
- Who independently seals/runs B's evaluation; A must not self-certify both its polling delta and the comparative results.
- Whether available native/managed profiling is sufficient for any resource adoption claim. Otherwise throughput may be evaluated while native-resource acceptance remains open.

No production adoption, training, scientific accuracy, model promotion or host-wide setting change follows from this proposal. Source-only preparation can proceed during the current quiet window; actual execution cannot.
