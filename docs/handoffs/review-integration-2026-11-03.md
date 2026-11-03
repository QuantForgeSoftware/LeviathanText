# Review-fix integration verification — 2026-11-03

Root verification of the eleven corrections in [the fix handoff](review-fixes-2026-11-03.md),
against the committed candidate `9ff4a94` (baseline `9262c57`). This records an independent
rerun, not a new correction claim.

## Commands and results

```sh
dotnet build LeviathanText.slnx --no-restore -m:1 -p:UseSharedCompilation=false -c Release
for suite in Application Formats Infrastructure Api Cli Worker; do
  dotnet test "tests/LeviathanText.$suite.Tests/LeviathanText.$suite.Tests.csproj" \
    --no-build --no-restore -c Release
done
```

- Release solution build: **0 warnings, 0 errors** (32.66 s).
- Application **201/201**, Formats **87/87**, Infrastructure **29/29**, API **64/64**,
  CLI **33/33**, Worker **20/20** — **434/434**, no failures/skips.
- Targeted review filters: Formats TEI/profile **32/32**, Application TEI/merge **29/29**,
  LocalStore **22/22**, API registry **5/5**, CLI registry **2/2**.
- Angular headless Chrome: **51/51**.
- The earlier review's ML checks were not repeated here because the fixes touch no recognition,
  training or preprocessing code; no ML result is inferred from this run.
- The full solution test run, ML suite and corpus training remain outside this receipt.

## Review dispositions

| Finding | Verified behaviour |
| --- | --- |
| 1 colliding TEI IDs | Import namespaces native line/revision/annotation IDs and remaps links; source `@xml:id` remains metadata. Same/cross-project repeated IDs, correction of the second import, and long-ID digest tests pass. |
| 2 migrated zero versions | Migration writes version 1 and repairs persisted zero rows; genuine schema-7 and schema-9 zero-row stale writers are rejected. |
| 3 concurrent first revisions | Null parent is treated as "observed no revision"; a concurrent first save returns `revision-conflict`, including retry races. |
| 4 nested TEI | Contained spans render recursively; unsupported nested structures report `nested-annotation` while preserving text. |
| 5 registry destination escape | IDs must be safe single components and destination must be a direct child; staging/publication and cleanup tests pass. |
| 6 redirect bypass | API/CLI transports disable automatic redirects; every hop is trust-checked with a five-hop limit; forbidden hops receive no request. |
| 7 inline spacing | Boundary whitespace on nonempty inline text nodes is preserved while runs collapse. |
| 8 milestones | Zero-width milestones render at their scalar position, before contained atomic spans when required. |
| 9 repeated L1→L2 spans | Repeated spans are resolved by surrounding context where unique; only tied candidates are reported as `annotation-mapping` loss, never attached to the first occurrence. Equal layers keep exact scalar coordinates. |
| 10 foreign merge base | Base must belong to the target line, layer and actual ancestry; foreign/disconnected/cyclic bases are rejected. |
| 11 format-2 registry | Catalogs accept formats 1 and 2 together; unknown formats remain rejected. |

## Remaining limits

- Duplicate native IDs already persisted are not silently rewritten; recovery uses backups,
  source attachments and revision history. Tracked separately in `todo.md`.
- Ambiguous L1→L2 locations and unsupported nesting are reported, not reconstructed.
- Library callers supplying their own `HttpClient` must disable automatic redirects.
- Greek-specialist adjudication, P46 promotion gates and the repeated-loading RSS investigation
  remain open. No recognition-quality or cross-platform resource claim follows from this work.
