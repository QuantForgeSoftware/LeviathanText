# Worker idle-backoff investigation — 2026-11-05

This research tests an isolated next-step performance option: skip the worker's
one-second delay after a non-null `RunNext` result, while retaining that delay after
a null result. **The production worker source and frozen release remain unchanged.**
No adoption is proposed from a no-op queue alone; representative model work needs
its own registration and owner review first.

## Registered isolated comparison

The baseline is the frozen published worker under
`.artifacts/releases/midday-final-supplements/worker/`. The candidate copies current
worker source and changes exactly one polling statement:

```csharp
if (finished is null) await Task.Delay(1000, stopping.Token);
```

Only the copied Program changes; no main checkout source, dependency, model profile,
checkpoint, label or package file changes. The candidate assembly is built against
published dependency assemblies. Its isolated package links the frozen package's
other files byte-for-byte; only Worker DLL/PDB differ. Both use the same runtime
configuration, dependency graph and assembly versions. Release build: zero warnings
and errors after correcting an initial research-project self-reference.

Four measured processes run sequentially in ABBA order: baseline, candidate,
candidate, baseline. Each separate generated LocalStore starts with 101 identical
`noop` jobs, inserted using `LocalStore.SaveJob` with fixed identities/creation times.
Each process receives `--max-jobs 100`; the final pending sentinel proves budget
retirement while more work is queued. Layout-model directories are explicitly empty.
The no-op handler only publishes its existing `noop:completed` artifact. No image,
Greek text, model, tensor, training, OCR/VLM/provider inference or corpus asset is used.

Registration precedes measurement and pins source, candidate diff, baseline/candidate
binaries, generated stores and harness identity. The process limit is 150 seconds;
OS RSS is sampled using `ps` restricted to each newly owned PID at approximately
100-ms intervals. All owned processes are awaited; a finally block drains/stops only
those process handles on failure. No unrelated worker or API process is touched.

Acceptance requires exactly 400 successful jobs, four untouched pending sentinels,
no running/failed/cancelled jobs, no retained leases/retries/failures, exactly 100
success log lines per worker, exact normalized job/output digests and all four
measured workers exiting zero. Timestamps and lease claim IDs are excluded from the
normalized equality digest; persisted IDs, statuses, outputs and retry/failure state
are included. Elapsed time and RSS are observations on a shared development host,
not release thresholds or memory-limit claims.

## Results

All 400 generated dispatches succeeded, with exactly one pending sentinel per route,
no leases/retries/failures, and one exact normalized job/output SHA-256 across all
four routes: `fbcfaa2364b6381b576a9ae6456892b0630b22a2660728b92f2d2c024029933b`.
All four measured workers exited zero and were awaited; the driver also exited zero.

| Route | Successful / pending sentinel | Seconds | Largest sampled RSS, MiB | RSS samples | Exit |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1-baseline | 100 / 1 | 99.488 | 76.89 | 899 | 0 |
| 1-candidate | 100 / 1 | 0.328 | 71.41 | 3 | 0 |
| 2-candidate | 100 / 1 | 0.328 | 71.94 | 3 | 0 |
| 2-baseline | 100 / 1 | 99.514 | 79.48 | 904 | 0 |

The no-op queue demonstrates a substantial bounded scheduler-policy benefit, matching
the removal of 99 mandatory waits. Candidate elapsed observations include cold startup,
SQLite work and the monitoring loop. The candidate has only three RSS samples per
process; its sampled peak can miss transient allocations. Neither lower sampled RSS
nor long-running memory improvement is established. The shared host also ran other
engineering activity, so no universal throughput multiplier is claimed.

## Candidate semantics and adoption gaps

The change removes 99 mandatory one-second waits from each full 100-job process.
This is a scheduler-policy result, not faster OCR/model execution. Earlier resource
protocols deliberately retained the production one-second wait. Their wall time
cannot be compared with this no-op probe as model throughput or native-retention
improvement, and their historical frozen binaries/results remain unchanged.

A null `RunNext` result is not exclusively an empty queue: a failed claim or lost
finalization may also return null. Those paths still back off. Successful,
unsupported, unauthorized and failed/requeued dispatches with returned records
would continue immediately, so retry/fairness policy requires explicit review.

Required followup before adoption:

- Register matched representative frozen model/page work, exact output preservation,
  completed-attempt counts, actual resource/allocator measurements and cleanup.
- Measure CPU/SQLite contention, competing-worker fairness, rapid retry behavior,
  API/event/heartbeat visibility and cancellation under sustained queued work.
- Recheck process-budget sizing at the new work rate. Existing native retention is
  not repaired; removing pauses can accelerate retained allocations and peak demand.
- Distinguish inter-job queue backoff from per-job retry delay and supervisor restart
  policy. One budgeted job can still contain many surfaces/lines and has no byte or
  wall-clock ceiling.

The existing idle delay, `--once` behavior, option validation and completed-attempt
budget must remain explicit contracts. A successful no-op burst alone does not
establish fairness, sustained production throughput, model resource bounds or
cross-platform safety.

## Focused regression and evidence

An isolated test project links the unchanged `WorkerBudgetTests` and
`WorkerAttemptBudgetTests` against the copied candidate; shared pure host fixtures
are compiled but their additional test class is not selected. **21/21 selected tests
pass**, zero skipped/failures/build warnings/errors. They cover exact budget and
pending work, idle enqueue/polling, failed/requeued attempts, lost finalization,
invalid/no-state-write options, `--once`, and deterministic POSIX FIFO signal drain.
SIGTERM and SIGINT both leave the blocked handler alive until input arrives, then
finalize it and retain the next pending job. No model-fitting test is invoked.

Evidence root: `.artifacts/experiments/worker-idle-backoff-v1/`, containing copied
candidate source, one-line patch, registration/hashes, isolated test project and
TRX, per-route input stores/snapshots/logs/RSS, summary and cleanup receipts. The
published baseline is never overwritten. All main worker-source hashes are checked
against their before snapshot after the experiment.

The first RSS attempt was denied by the sandbox's `ps` restriction. Its finally
block stopped and awaited its newly owned startup process (exit -15); source/store/
registration/cleanup evidence is retained under `initial-sandbox-failure/`. The
registered final run uses process-read permission only for its own PIDs. Expected
invalid-option test subprocess exits are 2; these are distinct from the four measured
workers' required zero exits. No failed attempt is removed from the evidence.
