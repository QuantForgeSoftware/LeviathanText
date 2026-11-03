# Handoff — interchange, concurrency and migration inventory (2026-11-02)

Owner request: continue `todo.md` for eight hours from 2026-11-02 21:00 CST. This handoff
records the code/evidence delivered in that cycle and what was deliberately left open. It is
not a release, acceptance or model-promotion record.

## Delivered

1. **Layered bounded TEI/EpiDoc** (`src/LeviathanText.Formats/TeiXml.cs`,
   `EpiDocImport.cs`, `NativeProject.cs`):
   - line splitting at `<lb>` with `@n`, `@xml:id`, `break="no"`, nested `textpart`;
   - L1 visible text plus linked L2 supplied/expansion/regularized readings;
   - `<app>` (lemma or orig/corr), unclear/illegible, gaps, add/del/spans, handShift,
     standalone `<abbr>`, `<hi rend="overline">` sign, facsimile/provenance/page breaks;
   - word spaces between `<w>` elements preserved (important for NTVMR);
   - unsupported markup aggregated; exact source retained as a checksummed attachment
     (2 MiB UTF-8 cap); machine-readable export loss report.
2. **Product import/export** (`ProjectService.ImportTei`/`ExportTeiWithReport`,
   CLI `import tei`/`export tei`, API endpoints, retained-attachment download).
   `--skip-duplicates` reports an existing object by source hash.
3. **Concurrency** (`NativeProject.Version`, `LocalStore`, `ThreeWayMerge`,
   `TranscriptionService.AppendLineRevisionMerged`, API `merge: true`, editor):
   - optimistic project saves; stale saves fail `revision-conflict`;
   - scalar-level three-way merge of stale text revisions; overlapping edits report both
     sides; annotations/reviews can name their observed revision and fail if stale.
4. **Sessions** (`ISessionStore`, `SessionService`, schema v9, API, surface view):
   - TTL session create/heartbeat/revoke/list, node/surface binding, 20-session cap;
   - presence identity can come from the authenticated session principal.
5. **Opt-in two-column projection split** (`ProjectionProfileLayout`, `LayoutService`, CLI
   `--columns`, API `?columns=true`): clear-gutter split, left-then-right order; synthetic
   tests only.
6. **Read-only migration inventory** (`MigrationInventory`, CLI `migrate inventory`):
   classifies public formats, records size/SHA-256, reports unclassified files; does not
   modify the tree. Smoke: M01 tree 241 files (120 PAGE, 120 images, 1 archive).

## Evidence and checks

- `docs/evaluation/interchange-v2.md` — profile behaviour, tests, real P46 page-1440 smoke
  (30 lines / 11 L2; structure only, not reading acceptance) and limits.
- `docs/evaluation/collaboration-concurrency.md` — CAS/merge/session design, tests and limits.
- `docs/evaluation/layout-columns.md` — synthetic two-column evidence, no accuracy claim.
- `docs/release/parity.md`, `known-issues.md`, `operations.md`, `STATUS.md` updated.
- New tests: TEI profile/import/CLI/API, three-way merge, session service/API, migration
  inventory, CAS/backup. Release build 0 warnings/0 errors.
- 48 Angular specs and production build clean.

## Open / next

- TEI XSD/RelaxNG pinning and the remaining EpiDoc constructs; multi-page `<pb>` splitting.
- Cross-project presence feed/push and a formal presence/session spec (surface and workspace
  panels already list/revoke visible sessions).
- Geometry/order and annotation-retraction conflict checks; edit locks.
- Object/surface/fragment provenance crosswalk; accepted-record comparison and eScriptorium
  round trip.
- Run the migration inventory on the actual reference collection and review the ledger.
- NT-20's repeated-loading RSS growth and supported-platform resource checks remain open;
  no model was promoted and no P46 accuracy search was reopened.
