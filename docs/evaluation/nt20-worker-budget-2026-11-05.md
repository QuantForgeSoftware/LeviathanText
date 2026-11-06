# NT-20 optional worker recycling — 2026-11-05

The worker now accepts an optional completed-attempt budget. This is an operational
mitigation for process-lifetime native retention, not a dependency correction or a
guaranteed memory limit. The default remains a persistent worker. NT-20 stays open for
the native root cause and representative collection/platform resource validation.

## Operator contract

```text
dotnet LeviathanText.Worker.dll --store /path/to/store.db --max-jobs 100
```

An external supervisor must restart workers on successful exit as well as failure if
continuous service is required. The worker exits zero after its hundredth completed
dispatch attempt. It does not start a replacement process itself. The value 100 is an
experimental comparison point, not a recommended universal capacity setting.

- Successful jobs, unsupported job types, rejected authorization, failed/requeued
  attempts and cooperative cancellation consume the budget. A dispatch whose final
  conditional store update loses its lease also consumes the budget. The host's
  `CompletedAttempts` counter advances after execution and before finalization.
- Idle polls and unsuccessful claims consume no budget. An idle budgeted worker waits
  for work; this is not a wall-clock retirement limit. The counter belongs to one
  single-threaded host/process lifetime and resets on restart.
- `--once` retains its existing one-poll behavior, including an empty queue. It cannot
  be combined with `--max-jobs`. Missing, duplicate, conflicting, unknown or invalid
  arguments return exit code 2 before opening a database, creating caches or loading
  optional models. Budgets must be positive decimal integers no greater than
  `Int32.MaxValue`.
- SIGTERM and Ctrl+C still let the current handler finish, then prevent the next
  dispatch. The same rule holds if shutdown arrives before the budget is exhausted.
  A supervisor must allow enough shutdown time for an active work unit to drain.

One `recognize.project`, `layout.project` or directory import may contain many work
units. Input size, model size and time per attempt remain unbounded by this option;
therefore the budget establishes no byte, surface-count, line-count or wall-clock
ceiling. Native allocations are reclaimed when the OS observes actual process exit,
not when a new budget counter is created in the same process.

## Regression evidence

The initial budget subprocess test timed out against the previous worker, while its
two `--once` controls passed. The corrected worker suite passes 57/57 tests with zero
build warnings/errors. Tests include exact two-attempt exit with a third job pending,
idle polling, retry/failure accounting, invalid arguments without filesystem writes,
and compatibility with `--once`.

SIGTERM and SIGINT tests hold an actual import handler on a POSIX FIFO. Each sends
the signal after the job is Running, verifies the worker remains alive while the PAGE
contents are withheld, supplies the document, and requires a successful import,
released lease, no retry and the second queued job still Pending. These tests run on
Unix; their signal/FIFO portion is not exercised on Windows.

Counter tests separately replace a claimed lease during handler execution. Even when
`RunNext` cannot publish a final record and returns null, the completed attempt counts.
Empty queues and deliberately rejected claims do not count. Authorization rejection,
unsupported types, retries and success are covered independently.

Isolated verification: `dotnet test tests/LeviathanText.Worker.Tests/LeviathanText.Worker.Tests.csproj
--no-restore --artifacts-path /private/tmp/lt-midday-resource-build -m:1
-p:UseSharedCompilation=false`. All invoked worker tests were inspected for fitting
entry points; none were invoked. The first restore's NuGet vulnerability lookup could
not reach nuget.org in the restricted environment; the isolated offline build used
`-p:NuGetAudit=false` and changed no dependency versions. TRX receipts are retained
under the experiment's `test-results` directory.

## Registered resource and prediction comparison

The final long comparison completed against a registered, frozen source and binary snapshot.
The baseline runs the default daemon; the candidate recycles after 100 attempts.
Each final v2 route processes the same 2,000 jobs and the same three generated PNG patterns,
cycling a fixed manually bounded line per image, through the previously authorized
`model-p01-h16-aug` checkpoint. These are generated inputs, not a new natural-text
accuracy evaluation. No fitting occurs.

Two replicates use ABBA order. Registration precedes inference and pins source,
checkpoint and executable hashes. Acceptance requires all 8,000 jobs to succeed,
exactly 2,000 persisted L0 revisions per route, exact text/mean-confidence/grapheme-span
confidence equality, and successful observed exit of every owned worker. OS RSS is
sampled every 250 ms alongside completion logs and elapsed time. The one-second
polling delay is preserved in both routes. Other agents' engineering activity on
the same host prevents treating latency differences as a controlled release claim.
At a retirement boundary the worker omits the next polling delay, so elapsed time
also reflects that saved delay as well as the replacement process's startup cost.

The historical v1 capture used 400 jobs per route and counted returned finalizations. Its full evidence is
retained after identifying the lost-claim counter gap. The v2 source instead counts
all completed dispatch attempts before finalization; only v2 measurements describe
the final candidate. Both registrations and all historical/failure receipts remain
under `.artifacts/experiments/nt20-worker-budget-v1/` and
`.artifacts/experiments/nt20-worker-budget-v2/`.

The complete historical v1 comparison succeeded on all 1,600 jobs and all 1,600 L0
revisions, with one exact text/confidence digest across all four routes:
`426e0e0e58a3df6c9d0cdc3a7b10539a8d95ea443fa103baa823b49f1b708d53`.
All ten worker processes exited zero and were awaited.

| Historical v1 route | Jobs | Processes | Most attempts in one process | Route seconds | Largest sampled RSS, MiB |
| --- | ---: | ---: | ---: | ---: | ---: |
| Round 1 default | 400 | 1 | 400 | 404.07 | 188.86 |
| Round 1 budget 100 | 400 | 4 | 100 | 402.22 | 187.67 |
| Round 2 budget 100 | 400 | 4 | 100 | 402.46 | 191.98 |
| Round 2 default | 400 | 1 | 400 | 404.25 | 185.02 |

The short comparison demonstrates process retirement and exact preservation, with
no consistent reduction in sampled peak RSS. Initial runtime warmup and GC dominate
these peaks. In the first default process, exploratory late-window median RSS was
152.92 MiB for jobs 100–199, 153.20 MiB for 200–299 and 154.89 MiB for 300–399.
Those windows do not establish a native allocation slope. Before any final v2
inference, its workload was increased to 2,000 jobs per route to extend observation.

The final v2 comparison completed all 8,000 jobs and 8,000 L0 revisions, with exact
common text/confidence digest
`2aad568c88712225e3dbcf4516203ae04297869adb162b5deef54664a7467735`.
All 42 worker processes exited zero and were awaited.

| Final v2 route | Jobs / L0 | Processes | Maximum attempts per process | Route seconds | Largest sampled RSS, MiB |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1-persistent | 2000 / 2000 | 1 | 2000 | 2037.01 | 182.72 |
| 1-recycled | 2000 / 2000 | 20 | 100 | 2026.95 | 231.44 |
| 2-recycled | 2000 / 2000 | 20 | 100 | 2025.58 | 227.81 |
| 2-persistent | 2000 / 2000 | 1 | 2000 | 2037.54 | 187.48 |

The longer comparison also does **not** support a sampled peak-RSS reduction claim:
the largest recycled-process peaks exceed both persistent peaks. It proves the
100-completed-attempt process bound and exact output preservation on this workload,
not a byte ceiling or correction of the native retention. Startup, persisted-history
work, GC and OS residency must not be conflated with separately traced allocation
roots. Measurements and exploratory RSS windows are retained in `analysis.json`;
no post-hoc slope or latency threshold is used for acceptance.

## Ownership followups and deployment gaps

The frozen worker snapshots' `PatchLayoutModel` has no disposal contract, and `WithThreshold` shares its private
network between wrappers. Adding a naive forwarding `Dispose` would invalidate
sibling wrappers. A safe shared-ownership design and focused regressions were
required before changing that API. The subsequently implemented MID24 design gives each wrapper a lease
on a shared network owner, releases leases idempotently, guards use after disposal
and closes the network only after the last lease is released. Current source also
detaches installed parameters from caller scopes, disposes API/worker/CLI owners,
and drains replaced models and failed startup collections. See the separate
[ownership evidence](patch-layout-ownership-2026-11-05.md); this later source is not
represented by the frozen resource binaries. Callers must dispose every owned alias;
registry-returned wrappers remain borrowed. Recycling alone does not repair ownership.

### Separate failed-layout-loader correction

Inspection found that `Load` constructed a CNN before checking weight existence or
digest, and did not dispose the network if weight loading failed. This narrow path
is now corrected independently of shared-wrapper disposal: validation precedes
allocation, a construction scope isolates new tensor wrappers, and the owned model
is disposed on failure while preserving the original exception. On success all
construction disposables moved to the caller's scope/lifetime at that stage,
preserving the then-existing contract. MID24 subsequently replaces that successful
ownership transfer with selective owned-parameter detachment and public disposal;
construction temporaries are still released rather than detached wholesale.

Five focused tests ran against the previous loader: missing/digest-mismatched and
digest-valid empty/truncated weights each left 21 additional live Torch wrappers
at the assertion, while the successful-load control passed. The corrected six-case
suite passes, with the additional success control checking both ambient and absent
caller scopes. Repeated failures preserve the caller's tensor and live-wrapper
baseline; successful loads retain exact serialized weights and repeatable row
probabilities. Fixture weights come from reflection-constructed random production
CNNs, with no optimizer, fitting or natural accuracy measurement. Existing layout
tests that fit models were excluded.

This source change followed the v2 worker freeze and is not represented by its
resource binaries. The exact additional source delta, before/after TRX receipts and
hashes are retained under `.artifacts/experiments/layout-load-lifetime-v1/`. It fixes
application failure-path ownership, not the separately traced TorchSharp native
retention.

The [native allocation attribution](nt20-native-allocation-attribution-2026-11-04.md)
remains the root-cause evidence. The subsequent [retained-page recognition replay](nt20-p46-page-resource-2026-11-05.md)
adds three provisional natural pages. Original-resolution/tiled pages, cross-platform
validation and actual supervisor deployment remain separate acceptance work.

The later [scalar-user/command-ownership investigation](scalar-user-queries-2026-11-05.md)
also identifies application-side SQLite command-registry growth. Its deterministic
disposal correction follows both frozen worker-resource snapshots. Their RSS includes
the earlier application command lifetimes as well as runtime/allocator effects;
do not attribute all observed worker RSS change to the separately isolated native
TorchSharp leak roots or present these snapshots as measurements of that later fix.
