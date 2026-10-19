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
- Shared-server authentication is **not** implemented; do not expose the API beyond localhost.
