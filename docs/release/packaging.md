# Packaging and independence evidence (pilot)

Date: 2026-10-19. This records what was **actually run**, not a release claim.

## Fresh-install verification (2026-10-19)

`scripts/fresh-install-smoke.sh` publishes the CLI, API and worker, then:
- creates a project from the published CLI binary with a brand-new store;
- starts the published API on localhost and checks `/` and `/api/v1/projects`;
- runs the published worker once against the same store.
Result: **PASS** (no source-tree or network dependency in the published paths).

Dependency inventory: all runtime dependencies resolve through NuGet (SkiaSharp, TorchSharp +
native libtorch, ASP.NET/SQLite); no Python or Kraken package/runtime appears in the graph.
Published outputs are large (~600 MB per binary set) because native libtorch is bundled; a
distribution should ship one shared runtime, not duplicate it.

## Publish outputs (verified)

```bash
dotnet publish src/LeviathanText.Cli -c Release -o dist/cli    # succeeded
dotnet publish src/LeviathanText.Api -c Release -o dist/api    # succeeded
```

- Both outputs are self-contained framework-dependent builds for `net10.0`; each is ~600 MB because the ML packages carry native libtorch binaries. The CLI starts and creates/loads a store from the publish directory (smoke: `project new` → `col-…`, `project list` → row).
- `dist/` is a local build artifact (gitignored), not a distribution commitment.

## Independence checks performed

- No Kraken package, subprocess call, Python runtime or Python service appears in any `*.csproj` or first-party source (`grep` over project files; the ML backend is TorchSharp + CPU libtorch).
- The CLI and API operate from a fresh directory with a new SQLite store and no network.

## Not yet done (release checklist remains open)

- Installers/updaters, versioned migration tests, backup/restore rehearsal, rollback checks.
- Target-matrix packaging (Windows/Linux) and non-macOS libtorch/runtime validation.
- Throughput/peak-RAM profiling under sustained collection load (pilot measurements exist in the evaluation records).
- Model artifacts are not bundled; model packages are produced by training and must ship only after rights + capability review.
- **TIFF is not supported by the current imaging backend** (measured 2026-10-19: the SkiaSharp build has no TIFF codec; the decoder now reports `RejectedArchive/unsupported-image-format` instead of crashing). High-resolution photo/TIFF ingestion remains an open Phase 0 item until a backend is selected or conversion is documented.
- Shared-server authentication: a **pilot-grade opt-in guard** exists (`LeviathanText:Auth:ApiKey` → Bearer/`X-Api-Key` required under `/api/v1`; `Auth:ReadOnly=true` blocks mutations). It is **not** a user system: no accounts, roles, per-user audit or key rotation. Without a key the API logs a local-only warning — do not expose it beyond localhost.


## Upgrade, migration and rollback (pilot)

1. **Stop the API and worker.** Never copy a live SQLite file while writes continue.
2. **Back up first:** `leviathan --store pilot.db backup ./snapshots/pre-upgrade`
   (`VACUUM INTO` + checksummed image copies).
3. Replace binaries/assets, then start the new API. Store schema migrations run on open:
   - v1/v2 → v3 adds the `users` and `auth_audit` tables in place;
   - a store written by a **newer** schema is refused with `SchemaVersionUnsupported` (downgrades
     are unsupported).
4. Verify `/`, `/api/v1/projects`, a login (`/api/v1/me`) and one read/export before resuming work.
5. **Rollback:** stop services, restore the pre-upgrade backup with the old binaries. A store
   already migrated to v3 cannot be opened by a v1/v2 binary, so the backup is mandatory.

Model packages are independent of the store schema: recognition packages verify labels,
architecture and manifest digest; layout packages verify architecture, weights digest and data
digest, and carry a model card. A package that fails verification is rejected, not guessed at.
