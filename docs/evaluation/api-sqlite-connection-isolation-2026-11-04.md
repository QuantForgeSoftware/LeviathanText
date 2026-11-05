# API SQLite connection isolation — 2026-11-04

The API registered a single `LocalStore` for all requests. Its SQLite connection was shared by project writes, jobs and middleware. Microsoft.Data.Sqlite requires a separate connection instance for concurrent access. [Provider concurrency guidance](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors#locking-retries-and-timeouts).

## Baseline reproduction

The disposable HTTP harness starts a new API process and SQLite store per condition. It uses the fresh loopback address announced by that child, 96 measured HTTP requests per process and two repetitions with reversed route order. Serial and parallel mixed requests alternate independent project creation and cancellation of distinct seeded jobs. Parallel conditions use six waves of 16 simultaneous clients. Each request has a 10-second timeout. The registration pins the executable, dependencies and harness.

| Condition | Project creation responses per repeat | Cancellation responses per repeat | Persisted per repeat |
| --- | --- | --- | --- |
| Serial mixed | 48 HTTP 201 | 48 HTTP 200 | 48 projects, 48 cancelled jobs |
| Parallel cancellation only | No project requests | 96 HTTP 200 | 96 cancelled jobs |
| Parallel mixed | 7 HTTP 201, 41 HTTP 500 | 48 HTTP 200 | 7 projects, 48 cancelled jobs |

Both mixed parallel repetitions failed on 41 of 48 project creations. Server stacks point to `LocalStore.SaveProject` and include nested-transaction exceptions from both the .NET provider and SQLite. The other conditions distinguish concurrent cancellation of distinct jobs from the mixed request failures. All six processes exited normally when signalled, and the final read-only `PRAGMA integrity_check` passed for each disposable store.

The baseline files in `.artifacts/experiments/nt20-api-singleton-race-v1/` contain all 576 measured request results and six server logs. The short worker soak used serialized project setup, with six correct terminal jobs and normal process shutdowns. [Worker transition checks](durable-worker-transitions-2026-11-04.md).

## Request-scoped candidate

The API holds an initialization anchor per host. The anchor performs schema migration and legacy project-version repair once. Each request uses a separate SQLite connection and checks the initialized schema version. A uniquely named shared-memory database retains the prior `:memory:` option across requests while isolating different hosts. The in-memory job table, revision chain and advisory event bus keep their existing host lifetimes.

The exact registered HTTP workload was replayed on six fresh candidate processes. Both parallel mixed repetitions persisted all 48 projects and 48 cancelled jobs, with HTTP 201 and 200 respectively. Both serial mixed and parallel cancellation-only controls also completed all requests as expected. All 576 measured responses reconciled to the expected stored state, all six integrity checks passed, and each owned process exited zero.

The replay files are in `.artifacts/experiments/nt20-api-scoped-store-v1/`. Concurrent idempotent creation and policy quotas involve separate multi-operation rules and remain outside this workload.

The idle SSE regression reproduced a five-second scope-disposal timeout after a client consumed the first event and disconnected. Passing the request cancellation token into the channel wait releases the iterator subscription and request scope. The regression now passes alongside the in-memory host isolation and mixed-request checks.

## Contention and latency

The first scoped candidate corrected project creation, but the matched cancellation-only control showed a large latency regression. Separate connections compete for SQLite write locks. Microsoft.Data.Sqlite's busy handling includes a 150 ms retry interval, which contributed long delays under this burst workload. [Provider implementation](https://github.com/dotnet/efcore/blob/main/src/Microsoft.Data.Sqlite.Core/SqliteDataReader.cs#L169-L184).

Four isolated builds from one source snapshot tested the same registered workload. All 576 requests in each variant received the expected status and matched the persisted counts. The scoped connection control, native wait alone, and process write gate alone are retained with the combined result. Timing is pooled over the two process repeats per condition. The 95th percentile uses nearest rank.

| Variant | Parallel cancellation p50 / p95 | Parallel mixed creation p95 | Parallel mixed cancellation p95 |
| --- | ---: | ---: | ---: |
| Historical singleton API | 4.49 / 9.48 ms | 82 project failures across both repeats | 7.41 ms |
| Request peers | 759.81 / 1,521.17 ms | 1,826.48 ms | 1,823.99 ms |
| Peers with 50 ms native busy timeout | 38.18 / 223.32 ms | 218.22 ms | 222.52 ms |
| Peers with process write gate | 155.79 / 308.11 ms | 322.04 ms | 322.97 ms |
| Peers with both controls | 6.09 / 11.80 ms | 11.87 ms | 8.98 ms |

The combined variant lets peer connections use a 50 ms connection-local SQLite busy timeout before the provider's retry path. Peers share a process-local lock for mutating SQL and existing short project transactions. Request processing and reads run independently, and separately launched workers continue to rely on SQLite's database locking. The busy handler is connection-local. [SQLite busy timeout](https://www.sqlite.org/c3ref/busy_timeout.html).

Serial mixed medians on the combined variant are 0.53 ms for creation and 0.54 ms for cancellation. The combined isolated cancellation p95 is 2.32 ms above the historical singleton control. This is bounded loopback evidence, with broader performance parity and release acceptance still open.

The comparison sources and builds are under `.artifacts/experiments/nt20-api-native-busy-source-v1/`. The four matched run roots are `nt20-api-native-busy-control-v1`, `nt20-api-native-busy50-v1`, `nt20-api-write-gate-v1` and `nt20-api-write-gate-busy50-v1` beneath `.artifacts/experiments/`.

The integrated production-source replay again completed all 576 requests with expected statuses and durable counts. Parallel cancellation p95 was 11.02 ms, mixed creation p95 was 12.24 ms, and mixed cancellation p95 was 8.06 ms. The API, Infrastructure and Worker suites passed 87, 29 and 26 tests respectively. The API suite excluded the existing layout fixture that fits a model. The final raw responses, hash verification and independent database reconciliation are under `.artifacts/experiments/nt20-api-write-gate-final-v1/`.
