# Capture relocation integration review — 2026-11-05

## Findings and corrections

Review covered the bounded `CaptureRelocation` implementation and the CLI's
`migrate rebind-images` path against the November 5 project scope. Candidate
image hashes are checked before one optimistic project save. Capture identities,
frames, rights references, text, geometry and original attachments remain intact;
relocation receipts are appended in the same save. No additional high-confidence
defect was found in that candidate-verification and atomic-save path.

The review found that constructing a normal `LocalStore` made a failed dry run
write to the selected store before relocation validation. A missing path created
a database. An existing zero-byte file was initialized, and an actual v1 store
without the `projects.version` column was migrated. Separate regressions reproduced
the latter two byte changes before the fix. Checking only `File.Exists` addressed
the missing-file case and left the initialization/migration defects.

`LocalStore.OpenExisting` now uses explicit SQLite `ReadWrite` or `ReadOnly`,
disables connection pooling, requires the current schema, and skips initialization,
migrations and version repairs. It does not create parent directories. Read-only
peers retain read-only mode. The original constructor's initialization behavior
and writable peer semantics remain available. The relocation CLI uses this factory,
with read-only mode for dry runs; legacy stores require an explicit upgrade through
the ordinary initialization workflow before relocation. Domain schema errors use
the existing CLI exit code 2.

## Generated-fixture verification

`CaptureRelocationCommandTests` exercises a real LocalStore containing a generated
PNG, Greek text, region/line geometry, a declared baseline, a rights reference and
an original source attachment. The test backs up the store, restores it, moves
the restored image tree, removes the old image source, rebinds the restored
capture, and exports diplomatic text, PAGE geometry and a bundle. It verifies
the image's exact bytes, native text/geometry/frame and capture identity,
original source attachment, project version, and checksummed relocation receipt.
Other controls verify dry-run persistence equality, missing/empty/actual-legacy
store preservation and rejection of malformed flags before store creation.

`ExistingStoreTests` independently covers missing, empty and actual-v1 stores in
both modes, current-schema read-only opening without zero-version repair,
rejected writes through both store and peer, and writable optimistic peer saves.

Final results, with no compile/analyzer warnings:

- Complete Infrastructure suite: **37/37 passed**, including eight new cases.
- Selected CLI relocation/backup/migration suite: **19/19 passed**, including eight
  new relocation cases.
- `git diff --check` passed for owned implementation and test files.

Both commands used isolated output at
`/Users/johnbaima/Code/LeviathanText/.artifacts/midday-relocation-review`, single
MSBuild process and `UseSharedCompilation=false`. Only the named non-training
CLI tests and the Infrastructure suite ran; no model fitting, source collection
access, scholarly adjudication or recognition-accuracy measurement occurred.

## Limits

Relocation requires stable local files during verification. It does not copy or
manage the lifetime of the moved image tree, supply rights evidence, infer image
alignment, or reinterpret frame geometry. Relative and remote capture references
remain outside this command's bounded path contract. This review is not a
general filesystem-race proof or exhaustive platform/filesystem certification.
