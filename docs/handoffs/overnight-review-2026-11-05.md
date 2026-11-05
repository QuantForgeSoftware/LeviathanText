# Overnight uncommitted-change review

The review examines the overnight candidate and preserves its original
receipts. Corrections use disposable stores and generated fixtures. The
Greek reviewer and release acceptance gates retain their existing status.

- Bundle export could destroy an existing output when conversion failed,
  or replace a source capture when it was selected as the ZIP destination.
  It also measured size before the ZIP central directory was written.
  The replacement stages and closes the archive, protects capture paths
  including directory aliases, then publishes it. Three baseline assertions
  failed. The focused export and migration set passes after correction.
- Queue quota admission counted only the latest 100 jobs and performed a
  separate check before insertion. Sixteen concurrent submissions passed a
  quota of one. The quota count and insert now share one SQLite statement,
  with shared-peer and independent-connection regressions and a greater-than-
  100 active-job control.
- Late structural editor replies could reload another surface or erase
  drafts made during the request. Order-save acknowledgements recorded live
  arrays instead of the submitted arrays. Context guards and pending-state
  checks now protect structural changes and order acknowledgements. Line-order
  edits also stay bound to the region whose lines were loaded.
- Gray8 luminance arithmetic placed some values in different histogram bins
  from equivalent grayscale BGRA. Two threshold regressions failed before
  the correction. The Gray8 path now preserves the legacy equal-RGB math.
- Rollback selected paths by backup-kind name before timestamp, which could
  restore an older pre-update copy over a newer forced-install backup.
  Selection now compares the generated timestamps. The mixed-kind regression
  failed before the fix and both rollback tests pass afterward.
- Conflicting TEI license targets silently collapsed into a null target with
  partial notice text. The importer now records an explicit provenance loss.
  Exact supplied-text projection now precedes contextual matching. Native
  uncertainty spans interrupted by inserted editorial text produce a loss
  instead of moving onto a later occurrence. Generated-case verification
  reduced 54 unreported offset shifts to zero across 4,095 evaluable cases.
  All nine archived P46 exports retained their exact bytes.

- Project-job visibility and cancellation bypassed enabled membership. Job
  lists, status lists, individual reads, cancellation and event streams now
  resolve the job's actual project before returning data or changing status.
  Cross-creator idempotency collisions return a generic HTTP 409, including
  concurrent submissions. The checks preserve project peer access and the
  configured local, administrative and membership-disabled modes. Ownership
  rules for global jobs and revoking already-admitted work remain open.

Local root evidence is under `.artifacts/overnight-review.vHToJz/`. The API,
geometry/layout and TEI review tracks retain their own fail-before logs and
candidate checks.

The combined Release build passed with zero warnings and errors. Twelve
selected C# suites passed 1,131 tests with one existing skip. All 93 Angular
tests and six script tests passed. The five existing fitting test classes
were excluded. No training or live LM Studio run was part of this review.

A disposable CLI import/export probe refused a source capture as a ZIP
destination without changing its bytes. A normal replacement export produced
a readable ZIP whose actual size exactly matched the CLI's reported size.
The source-protection, failed-export and timestamp tests cover the public
operations involved in the corrections.

The original overnight snapshots and reports remain historical evidence.
This reviewed source needs its own package before deployment. Existing
native TorchSharp retention, ownership of global jobs, revocation of
already-admitted work and permission changes during open streams stay in
todo.md. The review applies project-job visibility to the configured
membership policy and retains the configured shared mode for global jobs.
