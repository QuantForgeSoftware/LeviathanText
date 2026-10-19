# Packaging and independence evidence (pilot)

Date: 2026-10-19. This records what was **actually run**, not a release claim.

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
