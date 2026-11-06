# Read-only native validation before restore publication

Root approved this bounded source-only correction during the long quiet window. No backup, restore, SQLite operation, build, test, HTTP, model or corpus workload has run. The preserved LocalStore source and verifier are under `.artifacts/experiments/restore-native-validation-v1/before`.

The existing verifier checks SQLite quick_check, newer schema and a projects LIMIT0 query. It can accept checksum-valid but unreadable native JSON or current-schema missing tables/columns before --force replaces a healthy target. Integrity hashes establish copied bytes, not application readability.

R owns only LocalStore.VerifyBackupDatabase and new Infra/CLI regression files. The proposed verifier remains read-only. It rejects negative/newer schemas, and streams the existing minimum project columns to validate current NativeJson envelope loading, non-null Collection identity, exact row/native ID agreement and current row/envelope contract metadata. It never rewrites payloads, normalizes versions, drops duplicate inner IDs or performs a migration.

Only current schema9 requires the existing nine table/query shapes, including projects.version. Table matching retains SQLite identifier-case semantics. Current project SQL versions must be nonnegative integers; zero remains untouched for the already established later normal-open repair. Old schemas retain the minimum project shape, including genuine v7 without a version column. This is not full old-schema migration validation, index/constraint verification, job/auth JSON forensics or proof that every malformed native graph is detected.

Prepared tests:19 direct read-only cases cover malformed/null/unsupported payloads, row/native mismatch, missing current shapes, bad/zero versions, table-name case, duplicate inner IDs and a genuine old7 source.14 actual CLI cases include11 checksum-valid corrupt --force inputs, each retaining healthy target DB and cache bytes, plus byte-identical old7 restore/later migration and duplicate-ID preservation. CLI fail-before diagnostics record actual target/image before/after hashes and exit code, rather than merely manufacturing a checksum mismatch. All remain unexecuted; root will run coherent before/after and integration checks after quiet releases.

Verify only the staged owned copy before any image publication. Positive controls must show source bytes unchanged and no migration during verification/restore. Any older migration rehearsal occurs only when the test deliberately opens the restored copy afterward. Existing source/target checks, ordinary-error rollback and stopped-service recovery preconditions remain unchanged.


## Source-only v2 correction before first execution

Root review found that v1 accidentally dropped name from the legacy minimum query while streaming id/contract/json. Complete v1 source/tests/brief were retained under candidate-v1 with the original freeze unchanged. V2 restores the full id/name/contract_version/json shape for every schema and rejects null required metadata. Old7 missing-name is a regression/control against weakening the preexisting shape check.

V2 also explicitly requires projects.version for schema0: the current migrator cannot add that column to an already existing schema-zero table. A missing-version0 backup is refused before publication. A genuine minimum schema0 with version remains accepted and byte-identically restored, and migrates only on deliberate later open; genuine old7 without version remains accepted. These narrow cases do not establish all older migrations safe. No version normalization, JSON repair or duplicate-ID removal was added. All sources remain uncompiled/unexecuted.
