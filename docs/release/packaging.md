# Packaging and independence evidence (pilot)

Date: 2026-10-20. This records what was **actually run**, not a release claim.

## Fresh-install verification (2026-10-19)

`scripts/fresh-install-smoke.sh` publishes the CLI, API and worker, then:
- creates a project from the published CLI binary with a brand-new store;
- starts the published API on localhost and checks `/` and `/api/v1/projects`;
- runs the published worker once against the same store.
Result: **PASS** (no source-tree or network dependency in the published paths).

Dependency inventory: all runtime dependencies resolve through NuGet (SkiaSharp, ImageSharp for TIFF fallback, TorchSharp +
native libtorch, PDFtoImage/PDFium, PdfPig, ASP.NET/SQLite); no Python or Kraken package/runtime appears
in the graph. ImageSharp uses the Six Labors Split License; confirm distribution terms before a commercial release.
Published outputs are large (~600 MB per binary set) because native libtorch is bundled; a
distribution should ship one shared runtime, not duplicate it.

## Reproducible release directory (2026-10-20)

`scripts/package-release.sh [rid] [output-dir]` publishes the CLI, API and worker for one
framework-dependent runtime identifier, builds the Angular assets, refuses an existing output
directory, checks first-party source for Kraken/Python references, and writes a SHA-256
`release-manifest.json` plus a `README.txt`. It does **not** bundle model weights.

Verified on this machine:

| Target | Status | Evidence / limits |
| --- | --- | --- |
| osx-arm64 | **built + CLI smoke PASS** | 170 files / ~847 MB directory; 202 MB `.tar.gz`; manifest records runtime/commit/dotnet; CLI creates and lists a project from the packaged binary. |
| osx-x64 | not built/tested | requires a matching .NET runtime and native libtorch/PDFium assets. |
| linux-x64 | not built/tested | no Linux host in this environment. |
| win-x64 | not built/tested | no Windows host in this environment. |

The target matrix is deliberately a table of **tested vs untested**, not a compatibility claim.
The package is framework-dependent (the .NET runtime is not bundled). Model packages and corpus
images are separate artifacts. Set `LeviathanText:WebRoot=.../web` to serve the Angular workspace
from the API at `/ui` (SPA fallback; `/` remains the service/version endpoint).

## Publish outputs (verified)

```bash
dotnet publish src/LeviathanText.Cli -c Release -o dist/cli    # succeeded
dotnet publish src/LeviathanText.Api -c Release -o dist/api    # succeeded
```

- Both outputs are framework-dependent builds for `net10.0`; each is ~600 MB because the ML packages carry native libtorch binaries. PDF ingestion adds PDFium native assets per runtime. The CLI starts and creates/loads a store from the publish directory (smoke: `project new` → `col-…`, `project list` → row).
- `dist/` is a local build artifact (gitignored), not a distribution commitment.

## Independence checks performed

- No Kraken package, subprocess call, Python runtime or Python service appears in any `*.csproj` or first-party source (`grep` over project files; the ML backend is TorchSharp + CPU libtorch).
- The CLI and API operate from a fresh directory with a new SQLite store and no network.

## Not yet done (release checklist remains open)

- Installers/updaters; full cross-platform migration/rollback rehearsal (osx-arm64 rehearsal passes).
- Backup/restore rehearsal: **PASS** (2026-10-20, `scripts/backup-restore-rehearsal.sh`: snapshot, mutate, restore into a fresh store, verify pre-snapshot only).
- Store migration coverage: v1→v5 and v2/v3/v4→v5 tests create users/membership/runs after migration; newer schema is refused.
- Target-matrix packaging on Windows/Linux and non-macOS libtorch/PDFium/runtime validation (osx-arm64 is the only packaged target verified here).
- Sustained collection throughput/peak-RAM profiling (single-page startup/import/layout/export/backup profile is recorded in `docs/evaluation/pilot-profile.md`; not a sustained-load benchmark).
- Model artifacts are not bundled; model packages are produced by training and must ship only after rights + capability review.
- TIFF is decoded through the ImageSharp fallback; very large TIFF pages still need full-frame decode and the dependency uses the Six Labors Split License. High-resolution tiled decode remains open.
- Shared-server authentication: local accounts with reader/annotator/reviewer/admin roles,
  SHA-256 hashed keys, rotation/disable, auth audit and opt-in per-project membership exist.
  SSO/session policy and invitation UI remain open; presence is advisory and store-backed; do not expose an unconfigured
  instance beyond localhost.


## Migration/rollback rehearsal

`scripts/migration-rehearsal.sh` builds the pre-v6 CLI (`226a612`), creates a store with it,
opens/migrates it with the current CLI, then verifies the old binary **refuses** the newer store
with `SchemaVersionUnsupported`. Result: **PASS** (2026-10-20). This proves the documented
rollback boundary: restore the pre-upgrade backup and old binaries, not a downgrade of a migrated
store.

## Backup/restore rehearsal

`scripts/backup-restore-rehearsal.sh` creates a store, snapshots it with
`leviathan backup`, adds post-snapshot state, restores into a **different** store, and checks
that only the pre-snapshot project survives. Result: **PASS** (2026-10-20). This exercises the
manifest/checksum restore path; it is not a substitute for a full pre-upgrade backup on real data.

## Upgrade, migration and rollback (pilot)

1. **Stop the API and worker.** Never copy a live SQLite file while writes continue.
2. **Back up first:** `leviathan --store pilot.db backup ./snapshots/pre-upgrade`
   (`VACUUM INTO` + checksummed image copies).
3. Replace binaries/assets, then start the new API. Store schema migrations run on open:
   - v1/v2 → v3 adds `users`/`auth_audit`; v4 adds `project_members`; v5 adds `runs`; v6 adds `presence`; v7 adds `invites`;
   - a store written by a **newer** schema is refused with `SchemaVersionUnsupported` (downgrades
     are unsupported).
4. Verify `/`, `/api/v1/projects`, a login (`/api/v1/me`) and one read/export before resuming work.
5. **Rollback:** stop services, restore the pre-upgrade backup with the old binaries. A store
   already migrated to v3 cannot be opened by a v1/v2 binary, so the backup is mandatory.

Model packages are independent of the store schema: recognition packages verify labels,
architecture and manifest digest; layout packages verify architecture, weights digest and data
digest, and carry a model card. A package that fails verification is rejected, not guessed at.


## Dependency vulnerability check (2026-10-20)

`dotnet list LeviathanText.slnx package --vulnerable --include-transitive` reported **no known
vulnerable packages** at that time. Re-run before a release; this is a snapshot, not a guarantee.
