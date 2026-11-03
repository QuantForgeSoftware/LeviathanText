# Review corrections — 2026-11-03

All eleven defects from [the 12-hour review](review-2026-11-03.md) have source fixes and regression coverage. The owner authorized implementation, starting with data loss and registry security. Baseline: `9262c57`. The candidate is committed as `9ff4a94`; the [verification receipt](../../.artifacts/review-fixes-2026-11-03/verification/receipt.json) identifies all 16 changed C# source/test files by SHA-256 and records the environment and result hashes. Root's independent rerun is in [the integration verification](review-integration-2026-11-03.md).

## Dispositions

| Finding | Correction | Regression evidence |
| --- | --- | --- |
| 1: colliding TEI IDs | A full GUID namespaces every imported line, revision and annotation. All native links are remapped together; original XML IDs remain available for export and provenance. Long IDs retain a digest when bounded. | Same/cross-project repeated IDs, long IDs, revision/annotation links and a correction to the second import that leaves the first unchanged. |
| 2: migrated projects bypass version checks | Real old-schema rows start at version 1. Opening an already-current database also repairs persisted zero versions transactionally. | Genuine schema-7 table without a version column and schema-9 zero row both reject the stale writer; legacy unversioned calls retain their documented behavior. |
| 3 and 10: unsafe automatic merges | A null parent means the editor observed no revision. A concurrent first revision causes a conflict. A named base must belong to the target line, layer and actual ancestry. | Concurrent initial saves, including a race during retry; foreign line/layer, disconnected and cyclic history; valid multi-generation merge and unrelated-metadata retry controls. |
| 4, 7, 8 and 9: TEI text/span corruption | Render contained ranges recursively, preserve inline boundary spaces, emit milestones at their scalar positions and avoid generated indentation in witness text. Honor explicit editorial/expansion mappings; report ambiguous cross-layer locations instead of selecting the first match. | Nested/equal/adjacent spans, inline spaces, interior/end markers, erased/supplied text, repeated readings, equal full layers with different spans, supplementary scalars and overlapping matches. |
| 5: registry destination escape | Validate cross-platform single-component IDs in both parsing and downloading, verify a direct-child destination, extract into an invocation-owned staging directory and publish atomically. | Absolute paths, traversal, separators, control/device names, Unicode names, concurrent publication, checksum failure and cleanup. |
| 6: registry redirect bypass | API and CLI transports disable automatic redirects. Every hop is checked before requesting it, with a five-hop limit. Pull validates index trust before any request. Preserve the full index timeout across headers and body. | Real local HTTP API tests verify forbidden index/package targets receive no request; permitted relative redirects still install verified bytes. CLI trust-order tests and application redirect/timeout tests cover the shared helper. |
| 11: format-2 packages rejected | Catalogs accept supported formats 1 and 2 together and reject unknown formats. | Mixed catalog and API format-2 listing tests; existing legacy controls retained. |

## Before and after

The initial regressions compiled and failed for the intended behavior before production edits:

| Scope | Baseline result | Evidence |
| --- | --- | --- |
| Concurrency | 2 migration failures; 6 merge failures with 3 controls passing | [Receipt and commands](../../.artifacts/review-fixes-2026-11-03/concurrency/receipt.json) |
| Interchange | 11 Formats failures / 13 controls passed; 4 Application failures / 3 controls passed | [Detailed handoff](../../.artifacts/review-fixes-2026-11-03/interchange/handoff.md) |
| Registry | 22 failures / 16 controls passed | [Baseline TRX](../../.artifacts/review-fixes-2026-11-03/registry/baseline/baseline.trx) |
| HTTP API | 4 failures / 1 allowed-redirect control passed | [Baseline TRX](../../.artifacts/review-fixes-2026-11-03/api/baseline/baseline.trx) |
| CLI integration | Old binary attempts the connection before checking missing/disallowed hosts; the candidate returns the trust error first | [Missing-host baseline](../../.artifacts/review-fixes-2026-11-03/cli/baseline-no-allow-host.log), [disallowed-index baseline](../../.artifacts/review-fixes-2026-11-03/cli/baseline-disallowed-index.log). These are equivalent command-level reproductions for the two new CLI tests. |

Independent cross-review also found and corrected candidate errors in API exception mapping, whole-body index timeout, milestone nesting and equal-layer abbreviation/editorial mapping. Intermediate failed runs remain in the evidence directory; they are not presented as final passing results.

There are **87 additional test cases** across the affected suites. The final Release solution build has **0 warnings and 0 errors**. All **434 tests** in the six affected suites passed, with none failed or skipped:

| Suite | Passed |
| --- | ---: |
| Application | 201 |
| Formats | 87 |
| Infrastructure | 29 |
| API | 64 |
| CLI | 33 |
| Worker | 20 |

Reproduce from the repository root using the installed .NET 10 SDK and restored dependencies:

```sh
dotnet build LeviathanText.slnx --no-restore -m:1 -p:UseSharedCompilation=false -c Release
for suite in Application Formats Infrastructure Api Cli Worker; do
  dotnet test "tests/LeviathanText.$suite.Tests/LeviathanText.$suite.Tests.csproj" --no-build --no-restore -c Release
done
```

[Combined build/test logs](../../.artifacts/review-fixes-2026-11-03/verification) and the receipt link the final Formats/Infrastructure runs performed by their owners. No frontend or recognition/training behavior changed, so the earlier review's Angular/ML checks were not repeated; costly corpus training and platform profiling are outside this correction's evidence. Fixtures use synthetic text, images, archives, databases and loopback HTTP servers.

## Remaining limits

- Persisted projects with duplicate native IDs are not silently rewritten, and previously corrupted exports or overwritten readings are not automatically restored. Audit and recovery must use backups, retained source attachments and revision history while preserving subsequent human edits; this is tracked separately in `todo.md`.
- Ambiguous L1-to-L2 locations produce `annotation-mapping` loss. Nested structures outside the bounded TEI importer produce explicit `nested-annotation` loss. This is not arbitrary TEI round-trip support.
- Library callers supplying their own `HttpClient` must disable automatic redirects, as documented; the product's API and CLI clients do so. Registry package structure still requires the model loader after the download's checksum/path checks.
- Greek-specialist adjudication, P46 promotion gates and the existing RSS investigation remain open. This work supplies no new recognition-quality or cross-platform resource claim.
