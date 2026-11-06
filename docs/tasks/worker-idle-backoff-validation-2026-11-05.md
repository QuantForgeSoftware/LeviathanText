# PERF-01 worker idle-backoff validation — parallel task briefs

Status: planning only; no implementation, test, inference, profiling or other workload
is authorized or executed by this brief. Later execution requires root authorization,
a frozen registration and fresh owned evidence paths. Root owns integration,
engineering-gate selection and the final fresh package. Production polling remains
unchanged until that review; the isolated no-op candidate is not adopted today.

## Evidence and hypothesis

Read the [no-op investigation](../evaluation/worker-idle-backoff-investigation-2026-11-05.md),
[generated worker study](../evaluation/nt20-worker-budget-2026-11-05.md) and
[retained-page study](../evaluation/nt20-p46-page-resource-2026-11-05.md).

- The isolated no-op ABBA comparison processed 100 jobs per process: baseline 99.488 /
  99.514 seconds, candidate 0.328 / 0.328 seconds. All 400 outputs matched, with one
  pending sentinel per route and clean measured exits. The candidate had only three
  RSS samples per process. This proves removal of 99 mandated waits, not faster OCR
  or lower native memory.
- The earlier 8,000 generated inference jobs and 120 retained-page visits/3,240 L0
  revisions had exact output parity under process-budget comparisons. Neither study
  supported a consistent sampled peak-RSS reduction. Both retained the one-second
  delay. Those snapshots also predate later ownership/SQLite/API corrections.

The hypothesis is narrowly that waiting after completed work throttles a queued
worker unnecessarily. A non-null RunNext result would permit the next poll
immediately; a null result retains the existing one-second backoff. Null can mean
idle, failed claim **or lost finalization**, so this is null-result backoff, not an
assumption that every null means no completed attempt. Native retention is not
repaired by faster dispatch and may accumulate faster per wall second.

Historical negative resource results and the best observed no-op timings are
context only. Do not choose a future threshold from those measured values, select
the best route afterward, discard negative runs or relabel an exploratory metric as
preregistered. Correctness criteria, workload, operational caps and analysis must
be frozen before execution; any release SLO must come independently from root.

## Instance A — isolated polling candidate and semantic controls

Own a fresh isolated source/package tree under a versioned PERF-01 evidence root,
its copied Worker Program polling statement, build receipt and unchanged semantic
controls. Do not edit main `src/LeviathanText.Worker/Program.cs`, a frozen package,
model/checkpoint, dependency or B's evaluation/registration code.

1. Start from the exact current root-approved release baseline. Preserve the entire
   worker dependency/runtime/native closure. The sole candidate behavior delta is
   whether the existing one-second delay executes after a non-null result; keep
   cancellation token, loop condition, counter, log and retirement ordering unchanged.
2. Build against the same dependency assemblies and runtime versions. Candidate
   Worker DLL/PDB can differ; every other package file must be byte-identical. Pin
   compiler/runtime/RID/build options and the one-line source diff. Do not use an old
   pre-correction worker as the current baseline merely because it has measurements.
3. Keep the existing WorkerBudgetTests and WorkerAttemptBudgetTests unchanged and
   explicitly selected in an isolated test host. Preserve `--once`, invalid arguments
   before filesystem writes, idle enqueue/polling, successful/rejected/unsupported/
   retried attempts, lost finalization and completed-attempt budget accounting.
4. Preserve SIGTERM/SIGINT behavior: finish the active handler, finalize once, release
   its lease and leave the next job pending. Use generated FIFO/barrier fixtures;
   never interrupt a real model/corpus job merely to test cleanup.
5. Separate the existing counter contract from returned records. Idle/failed claims
   consume no attempts; a completed dispatch whose finalization loses its claim
   still consumes one, even though its null return must retain the backoff.

A acceptance: source diff and binary closure contain no unrelated changes; focused
controls pass with actual execution counts and expected skips, not an empty green
selection; all owned test workers are awaited. No fitting, model promotion, real
label read or natural accuracy evaluation is needed. Hand B exact source/binary
hashes, commands, semantic receipts and known platform-specific skips. Freeze the
candidate before B starts timed work; no adaptive fix/rebuild inside a measured set.

## Instance B — independent registered evaluation

Own the registration, generated/authorized input manifest, seed stores, independent
runner, comparisons, native/resource measurements, cleanup and report. Do not edit
A's polling source or the baseline. B can prepare source-only expectations and
harnesses while A works; timed runs wait for A's frozen candidate and root approval.

Use only existing authorized generated inputs and retained provisional inference
assets. Do not load ground-truth/accepted training labels, PAGE/TEI transcriptions,
source reading strings, review answers or locked evaluation partitions. The frozen
model vocabulary/metadata required for inference is permitted; it is not permission
to open corpus target labels. Do not call P01LineDataset.Load to obtain these inputs.
Use the existing reduced geometry-only native fixture, not a raw crop map that also
contains source readings.

### Preregistration and exact identity

Create a fresh run root; refuse existing output directories and retain all failures.
Before any timed process, record full SHA-256 for:

- all copied source/project files, original/candidate polling diff, semantic test
  sources, runner/normalizer/seed source and build settings;
- baseline and candidate Worker assemblies, every dependency DLL, runtime/deps files,
  actual loaded native TorchSharp/libtorch/Skia/SQLite libraries and RID assets;
- complete checkpoint package: weights, manifests, architecture, vocabulary,
  preprocessing metadata and notices; all generated PNGs, reduced geometry,
  retained images, rights notices and initialized database inputs;
- root's release/package manifest and the final registration itself. Pin process
  environment, thread/OpenMP settings, GC options, hardware/OS, cache directories and
  empty optional layout-model directory identically; do not tune them from partial
  results. Recheck hashes after execution. Copying/hashing another RID asset does not validate that RID.

Do not invent new future baseline hashes in this planning document. The exact future
identity must come from the package root approves immediately before that run.
Historical anchors below identify permitted material and old receipts only:

| Historical material | SHA-256 / location |
| --- | --- |
| P01 h16 augmented checkpoint weights | `03952a43d94bf9e53d8f5de886d3470057a2db55222c01d14172111a47b06fda` |
| Reduced, label-free geometry input | `2a5c5cf82028f4ae769ee5d25ef65a926fd998e0b6369fd3f400cea12d345829` (`nt20-p46-page-resource-v1/inputs/geometry-only.json`) |
| Page 1440 retained image | `ed2b5873fcf76c410ed94fb0bc99d56ecd4c0af4b61ec23054ebcef1d338ca5d` |
| Page 1450 retained image | `a13a5e4bfbdc3ee8299d47590efd84bd4479d41b7221e7c3367f9434df139608` |
| Page 1460 retained image | `4079842d996f67a137c61b700b8e694f5c135ee2aecefdbe19e19e0a15a7d4a4` |
| Historical osx-arm64 libtorch | `619206f7f2917cef9869bc74c7179ebd2183c17fb33a0d51e8b0a501bcc87c89` |
| Historical osx-arm64 libLibTorchSharp | `3f643eebb2c41450493213ae504e89eb1ca2b51603f8208c82c6f104f2f75d9c` |

Historical source/checkpoint/binary manifests are retained in
`.artifacts/experiments/nt20-worker-budget-v2/{source,binary}-sha256.json` and
`.artifacts/experiments/nt20-p46-page-resource-v1/{source-input,binary}-sha256.json`.
The original no-op registration is under `worker-idle-backoff-v1/`. If a future
asset differs, stop and register the deliberate difference before inference; do
not silently substitute another model, geometry profile or source photograph.

### Fixed initial ABBA matrix — for later approval

Keep the process budget identical between baseline and candidate so polling is the
only compared change. Do not combine a new recycling policy with the delay change.
Run each matrix sequentially ABBA, with fresh stores and fresh processes:

| Matrix | Each route | Exact expected result | Per-process limit |
| --- | --- | --- | --- |
| N00 no-op | 101 queued; `--max-jobs 100` | 100 succeeded, one pending sentinel; no retries/leases/failures | 150 seconds |
| G00 generated inference | 200 fixed generated one-line jobs plus pending no-op sentinel; budget 200 | 200 L0 outputs; exact frozen text/confidence/span equality; no non-L0 writes | 360 seconds |
| P00 provisional retained pages | Ten cycles 1440/1450/1460 plus pending no-op sentinel; budget 30 | 30 surface jobs; 27/26/28 rows each cycle; 810 L0 per route, no non-L0 writes | 180 seconds |

This gives 400 no-op dispatches, 800 generated inference jobs and 120 surface visits /
3,240 natural-page L0 records across the three four-route matrices. They are repeated
engineering visits, not additional independent pages or accuracy evidence. Do not
expand scale, choose a winning checkpoint or change normalization from partial
results. A sustained extension requires its own preregistration/authorization.

For G00 keep the three previously authorized 240×96 generated patterns and fixed
geometry/order, with no target strings. For P00 keep the 2200-pixel retained Michigan
review photographs and all 81 unchanged proposed quadrilaterals. Preserve Michigan
Public Domain Mark 1.0 notices/attribution and item/image hashes; no CSNTM photographs.
All geometry and specialist questions remain pending, including the 1440 row 10
verse association. No metadata, rights, reviewer or model gate is bypassed to make
this benchmark run.

The legacy h16 checkpoint has no typed preprocessing profile: production uses its
existing ink-positive grayscale/standard crop fallback. Register that exact route;
runtime compatibility does not establish historical training-preprocessing parity.
Do not substitute externally rectified P46 diagnostic geometry. This matrix does
not cover original-resolution or greater-than 25-MP tiled/banded decoding.

### Exact preservation and mandatory timing

Compare stable job IDs, statuses, attempt counts, retries/leases/failures, output
scope/identity, L0 text, mean confidence, scalar/grapheme confidence arrays and
layer/provenance metadata. Keep a fixed explicit normalization allowlist for clocks,
process IDs and lease claim IDs only. Never drop a field after it differs. No missing,
duplicate or non-L0 revisions; every pending sentinel remains pending. Candidate and
both baseline replicates must agree on complete normalized output, not just counts.
Historical digests are references, not a substitute for new source-matched parity.

Record wall/startup/first-job/last-job times and per-attempt completion log gaps.
Show idle time separately from completed-work time and process-restart overhead.
Register a generated idle-enqueue schedule before running it: empty queue, confirmed
polling, delayed enqueue, then exact completion. It must retain null-result backoff
and consume zero idle attempts. Preserve failed-claim and lost-finalization controls.
Do not claim that the dense queue's fast timings demonstrate idle behavior.

### Quotas, priorities, multiple workers and visibility

Use a separate generated control store/server, never the user's API or worker.
Keep owner/account/project-binding, disabled-user and membership gates unchanged.
Quota controls go through the real admission/enqueuer policy, not SaveJob bypass:
freeze active counts while checking rejection, then finalize one job and check the
next admission. Faster draining can change observed active counts, not the quota rule.

Register fixed priorities, creation times and tie-break IDs. Single-worker claim
order must preserve the documented priority/time/ID rule. With two separately owned
workers, record claim/finalization ownership, per-owner progress, lock contention,
duplicates, retries and completion distribution. Require no lost/double-finalized
jobs or unauthorized dispatch. Do not promise equal sharing or add a balancing
threshold after observing results; starvation/monopolization is a reported adoption
risk requiring root's policy decision, not something to hide behind aggregate speed.

Observe actual job replay/SSE and persisted progress/lease/heartbeat behavior under
queued work, including external-worker state observation, authorization revocation,
terminal status, cursor ordering and cancellation. A faster producer must not lose
advisory events, starve persisted-state rechecks or bypass bounded live batches.
Use generated jobs/provider-free API fixtures; no proposal/VLM calls. Test retry
bursts separately with bounded generated failures: record attempts and timing,
verify existing retry caps/terminal behavior and preserve budget accounting.

Shutdown controls use generated FIFO/barrier work: signal only owned PIDs while an
attempt is blocked, observe that it remains alive, release the barrier, require one
finalization and the next job pending. Include idle shutdown and budget retirement.
Never kill real inference mid-write to prove the cancellation path.

### Resource evaluation and operational budgets

Capture managed allocated/live bytes where the approved harness can measure them,
CPU time/utilization, sampled per-process/aggregate RSS, native live allocation
counts/bytes/stacks where supported, and completed attempts. Report native growth
both per attempt **and per wall second**, plus startup/warmup and post-work retained
state. Tensor wrapper counts alone are not native-allocation proof. RSS alone is
not an allocation root or byte ceiling.

Use identical registered sampling/profiler configuration for both variants. Separate
intrusive allocation tracing from primary latency runs; record its overhead and
exact source/binary identity. If native attribution tooling is unavailable, disclose
the missing evidence and leave native/resource adoption open. Do not call stable
RSS or faster process exit a correction of the known native retention.

Draft bounded campaign limits, approved before execution:

- Overall wall cap 90 minutes, including setup/controls/measurement/cleanup. Matrix
  per-process caps above total at most 46 minutes; no automatic extension or rerun.
- Reserve up to 10 minutes for setup/hash verification, 10 for semantic/visibility
  controls, 15 for generated concurrency/retry and optional identical profiling,
  and at least 9 for final validation/cleanup. Stop launching work when that reserve
  cannot accommodate the next registered phase.
- Draft operational abort at 2 GiB RSS per owned worker or 4 GiB across owned workers,
  subject to root's host-capacity approval before registration. These are safety
  stop budgets, not success thresholds derived from earlier negative measurements.
- On stop/error, signal only recorded owned processes, allow up to 60 seconds to
  drain a bounded current attempt, then kill only that owned process tree if needed
  and await exit. Record nonzero/forced exits, partial jobs and leases; do not erase
  evidence, requeue data blindly or count a forced exit as a passing route.

Stop immediately on hash drift, unexpected model/data/provider access, rights or
review-gate mismatch, source/metadata mutation, malformed output, parity failure,
unknown child process, resource/time cap or failure to drain. Investigate a failed
attempt deterministically before any new run; changes require a new registered
source/workload set. Do not erase/reuse the failed registration.

Coordinate a quiet timed window with root. Never stop unrelated APIs/workers, change
LM Studio settings/log level/model state, drop system caches or alter host-wide
profiler settings. If unrelated load overlaps, record it and bound conclusions.
Fresh processes are not proof of cold OS caches. Every owned child/profiler/server
has recorded identity, stdout/stderr, exit and final cleanup verification.

## Parallel dependencies, handoff and adoption

A and B can prepare independent artifacts in parallel. A freezes the one-change
candidate and semantic receipts; B independently checks hashes/diff and freezes the
matrix, expectations, timings, caps and analysis before execution. Neither adjusts
the other's source or results. Resource runs begin only after both freezes and root
authorization; do not run comparative inference while rebuilding the candidate.

B returns every route, negative result, exact parity digest, process lifecycle,
resource limitation and fairness/visibility finding. A consumes concrete failures
in a new isolated source version; B reruns only under a new approved registration.
Root decides whether the bounded performance benefit and remaining risks justify
integration, then applies a reviewed polling diff, reruns the current complete
engineering gate, builds a fresh package and verifies its identity/operations.

No all-platform guarantee, memory-fix claim, accuracy improvement, model promotion,
label acceptance or adoption follows from this brief or the old no-op speed result.
Other RIDs and native builds require separate evidence. Original corpora, checkpoint
choices, rights notices, reviewer statuses, spent/locked evaluations and all frozen
historical receipts remain unchanged. LM Studio remains untouched.
