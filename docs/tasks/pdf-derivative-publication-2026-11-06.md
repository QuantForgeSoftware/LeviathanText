# Non-overwriting PDF derivative publication

Source-only preparation during the long API measurement quiet window. Root approved the narrow fix contract; no PDF, build, test, HTTP or model operation has run. The preserved source is `.artifacts/experiments/pdf-publication-safety-v1/before/PdfRenderer.cs.txt`.

The existing renderer writes predictable page names with File.WriteAllBytes into an explicitly reusable directory. Reimporting a different same-stem PDF can replace an older capture's bytes while the native frame/checksum still identify the original; a later project save conflict does not undo that file damage. Defaults use a fresh directory, but both CLI `--out` and API `outputDirectory` accept existing directories. This is source-derived, not an executed failure yet.

R owns only PdfRenderer.cs and a new Imaging regression file. The candidate preserves page rotation, geometry, naming, output paths and limits. It preflights all requested target names before rendering any page, rejecting existing files/directories/links with StageContractViolation and reason `pdf-output-exists`. Every publication independently opens with FileMode.CreateNew; competing creation or permission/open failure receives StageContractViolation/reason `pdf-output-not-created`. There is no overwrite or rollback deletion.

Publication is not an all-files/database transaction. A later render/write/save failure can leave earlier new derivatives or a partial new file. Existing user files are never deleted to clean up because another writer could have replaced a pathname. These safe leftovers must be reported/reconciled explicitly, not presented as atomic rollback. Project import already saves only after rendering completes; application-level optimistic conflicts remain a separate boundary.

Proposed proof after quiet releases:

- Public renderer baseline/candidate: tiny generated PDF plus a preexisting first-page sentinel; later-page file/directory obstruction must preserve the sentinel and create no first-page output. These three public cases compile against the original renderer.
- Candidate-only actual exclusive publisher: a target created after the caller's absence check survives unchanged; two own tiny-byte publishers produce one whole winner and one controlled refusal. No concurrent PDFium rendering or large allocation is involved. Reflection accesses only this private publication primitive; tests requiring it are not baseline proof.
- Root's two actual-host HTTP regressions import a first generated PDF, then refuse a same-stem import with exact prior PNG and complete project JSON unchanged; later-page collision likewise leaves no first-page capture/project write.
- CLI inherits the same renderer path. Add/retain a generated explicit-`--out` collision case if a dedicated CLI contract receipt is needed; do not imply it was run from API/renderer coverage.

Then run existing PDF rotation/text/reference tests, focused public/candidate checks, the exact safe engineering gate and package verification. No training, corpus labels, natural accuracy or real provider work is required. Keep every genuine fail-before receipt and do not count helper absence as a successful behavioral before-proof.
