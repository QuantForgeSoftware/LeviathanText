# Durable worker transition checks — 2026-11-04

A worker launched with redirected standard input now polls until shutdown. `--once` still performs a single pass. Ctrl+C and POSIX SIGTERM stop polling, and a running synchronous handler is allowed to finish so the worker does not requeue a partially applied job during an orderly stop.

A SQLite compare-and-swap update protects job claims, heartbeats, terminal writes and cancellation. It uses the exact persisted prior row, so a losing process rereads current status before applying a transition. Each new lease carries an attempt ID. Finalization and retry writes require the same running attempt, including when the same worker ID has explicitly taken a replacement claim. Older leases without an attempt ID remain readable. Polling only discovers and claims pending jobs. Recovery of an expired running job remains an explicit operation for use after verifying the prior handler stopped.

## Before and after

- The isolated baseline worker soak recorded workers exiting zero with redirected input after its first wave, before later waves could run. Its local baseline receipts are under `.artifacts/worker-soak.mm984Z/baseline/`.
- The unmodified worker binary failed three deterministic transition regressions. A replaced attempt could publish stale success, a stale exception could requeue the replacement, and handler success could resurrect a cancelled record. Each failure is retained in the baseline test log.
- The integrated worker suite passed 26/26. It includes two SQLite connections contending for 24 jobs with one winner per job, cancellation and stale completion/failure checks, a same-worker replacement, and a redirected process that picks up a job enqueued after its idle poll.
- The API job subset passed 6/6. Two independent connections cancelling the same job each read the cancelled state, and only the winning transition publishes the advisory cancellation event.
- The separate recognition lifecycle regression exposed growth from 2 to 18 live wrappers in the first candidate. One verified model per project job reduced this to 2 to 4. The outer worker tensor scope brought the existing lifetime regression into balance. This is distinct from the traced dependency-level native leak. [Native attribution](nt20-native-allocation-attribution-2026-11-04.md).

The API's general singleton SQLite connection remains a separate shared-server concern. The cross-connection test and the compare-and-swap update establish job-row transition behavior under that isolated concurrency model. Project-write races, API enqueue idempotency under contention, crash recovery with fenced side effects, automatic running-job recovery, and unsupported job types need separate handling. [Microsoft.Data.Sqlite concurrency guidance](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors#locking-retries-and-timeouts).

The test logs are under `.artifacts/experiments/nt20-worker-atomic-v1/`. No recognition model fitting was needed for these checks.

## Queue follow-up

Two fresh API processes per variant received 128 same-owner duplicate submissions each, using eight distinct owner/key pairs per process and 16 simultaneous submissions per pair. The per-owner active quota was one. The baseline returned 70 HTTP 200s, 182 HTTP 500s from unique-key insert conflicts, and four HTTP 422 quota errors. The candidate returned HTTP 200 for all 256 requests. Within each owner/key pair, every response exactly matched the single persisted job, including its original creation timestamp. A separate API regression verifies one advisory creation event even when the competing request priorities differ.

The worker's registered type set now determines the outcome of an unknown stored job. The worker atomically claims it, records a terminal `stageContractViolation` with the unsupported type and zero additional retries, then continues polling. In the baseline process the unknown high-priority job triggered an unhandled exception and worker exit, leaving it and the supported job pending. In the candidate the unknown job became failed, the subsequent supported job succeeded, and the worker exited zero on the harness shutdown signal.

The replay artifacts and row reconciliation are under `.artifacts/experiments/nt20-job-queue-baseline-v1/` and `.artifacts/experiments/nt20-job-queue-candidate-v1/`. The affected suites passed API 87, Infrastructure 29 and Worker 26. The API run excluded the existing layout-model fixture that performs fitting. Cross-owner key visibility and quota enforcement for competing distinct keys remain separate.
