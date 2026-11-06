# Bounded native AI proposal discovery — 2026-11-05

`NativeAiProjectResolver.ForProposals` now scans candidate projects once per batch and parses each issued-role attachment once per project. Previously each requested proposal independently reloaded every project and reparsed records. The new lookup retains only up to two matching **project IDs** for each of the at most 128 requested proposal IDs, plus one bounded per-project set; it does not retain all native project payloads or cache across requests. Only the selected native project is reloaded for return.

## Registered generated benchmark

Registration and receipts: `.artifacts/experiments/native-ai-resolver-scan-v1/`. `registration.json` predates production edits and records the baseline resolver source SHA-256 `47084c7384eed3a0cf0a80fe7948af3621ad6ec4973aff2aa4a3917fdea746ec`. Before/after resolver source copies, the final fixture source and final source hashes are retained beside the measurements.

All fixtures are generated SQLite stores containing one or six projects, each with 128 checksummed minimal issued-role candidate JSON records and 16 unrelated-role attachments. Candidate records include 384 padding characters. The target project contains the requested IDs; the other five projects contain issued-role records with different IDs. This exercises relevant-role parsing and irrelevant-project/nonissued filtering without real source data. These records support candidate discovery only and are not represented as authoritative valid proposals.

Cases use 10, 64 and 128 requested IDs, one warmup and three measured samples each. Measurements include request-scope setup and resolver execution, but exclude fixture creation, providers, native proposal validation and acceptance. Elapsed time uses `Stopwatch`; allocation uses current-thread allocated-byte deltas. Before and final after benchmarks ran separately under the same isolated Release build settings, without concurrent HTTP-fixture work. Receipts contain every sample; medians follow.

| Projects | Requested IDs | Before ms | After ms | Before allocated bytes | After allocated bytes |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 10 | 4.397 | 0.715 | 4,034,424 | 832,360 |
| 1 | 64 | 21.961 | 0.902 | 26,120,952 | 894,408 |
| 1 | 128 | 53.493 | 1.237 | 53,020,896 | 968,208 |
| 6 | 10 | 23.180 | 2.705 | 25,200,600 | 2,912,672 |
| 6 | 64 | 160.047 | 3.953 | 161,577,104 | 2,975,488 |
| 6 | 128 | 172.724 | 2.987 | 323,637,328 | 3,049,288 |

The largest registered case reduces measured allocations by about **99.1%**. These finite same-host samples demonstrate a useful bounded engineering improvement; they are not a production latency guarantee, a monotonic timing assertion or an end-to-end AI benchmark. No timing thresholds were added to ordinary tests.

## Preserved lookup and authorization contract

Batch-size and duplicate-ID rejection still happen before store lookup. An explicit project restricts discovery to that project. More than one matching project is still ambiguous for each ID; duplicate matching records within one project do not create project ambiguity. Missing, ambiguous, authorization and cross-project outcomes are evaluated in original request order. In particular an earlier denied target takes precedence over a later missing target, and a denied second project takes precedence over the cross-project batch refusal.

Requested IDs are converted to strings once and compared ordinally. JSON object/member and string-ID requirements remain exact: malformed JSON, null/nonobject proposal records and numeric/boolean/object IDs cannot identify a target. Null attachments or null content are skipped as unreadable records, including records after a valid match that the old short-circuit lookup would not inspect. Checksums, source context and complete proposal graphs remain the native service's responsibility; candidate discovery does not grant acceptance authority.

Current account/membership checks and request binding remain in place for each resolved ID. The selected project is then reloaded once through `ReloadAuthorized`, refreshing authorization and binding. The authoritative native service validation, optimistic project version checks and transaction-time permission guard are unchanged. `ForRevision` keeps its existing lookup behavior.

Discovery is **not a database-wide transaction snapshot**: projects are read sequentially, and another writer may change the store while discovery runs. The selected reload provides a fresh project snapshot; native record/source validation and optimistic saves remain required. Concurrent newly introduced ambiguity after a project was scanned is not prevented. No cross-request cache or retained authorization decision is introduced.

## Verification and reproduction

Final registered resolver run: **7 passed**, including the measured benchmark, size/duplicate validation, scope/missing/ambiguity/cross-project behavior, denied-membership precedence, malformed/nonstring JSON and two unreadable-record controls. Baseline run: **5 passed**, with the same registered benchmark and original semantic controls. The **21 existing native HTTP tests passed unchanged**, using generated provider doubles rather than external providers; their separate final receipt is `resolver-http-final.trx`. Final verification therefore covers 28 passing resolver/HTTP cases, with zero failures/skips and no compiler/analyzer warnings.

Use the opt-in environment variable only to write benchmark results to a disposable evidence path:

```sh
LEVIATHANTEXT_RESOLVER_BENCH_OUTPUT=/absolute/disposable/after.json \
dotnet test tests/LeviathanText.Api.Tests/LeviathanText.Api.Tests.csproj \
  -c Release -m:1 -p:UseSharedCompilation=false \
  -p:ArtifactsPath=/private/tmp/leviathan-resolver-scan \
  --filter FullyQualifiedName~NativeAiResolverScanTests
```

Without the opt-in path, the benchmark method reports that it was not requested; the semantic tests still run. Source ownership is limited to `NativeAiProjectResolver.cs` and new `NativeAiResolverScanTests.cs`; historical HTTP fixtures were not edited. No production dependency, schema, provider/model request, recognition, training or Greek-specialist decision changed.
