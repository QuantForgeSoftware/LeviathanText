# Reference capability matrix (honest status)

Date: 2026-11-02. This is **not a claim of full Kraken parity**. Each row states what exists,
where the evidence is, and what is missing. Acceptance thresholds remain unfrozen and labels
are not specialist-adjudicated.

| Capability | Status | Evidence | Missing / limits |
| --- | --- | --- | --- |
| PAGE XML import/export | implemented (bounded) | format tests; round-trips; loss reports | unsupported elements reported, not mapped |
| TEI/EpiDoc import/export | implemented (bounded, layered) | [profile tests + evidence](../evaluation/interchange-v2.md): `<lb>` splitting, L1/L2 supplied+expansion links, orig/reg, add/del/handShift, facsimile, provenance, attachment, loss report; ISicily records parse | no XSD validation; not all EpiDoc constructs; attachments capped at 2 MiB |
| ALTO v4 / hOCR | implemented (bounded) | adapter tests + legacy import | no word segmentation synthesis; ALTO does not carry direction |
| IIIF Presentation 3 import | implemented (bounded) | real manifest test; remote image cache | no IIIF hosting |
| PDF / ABBYY XML | implemented (bounded) | PDF page rasterization + optional coordinate-bearing L2 text with gap-based lines and two-column gutter ordering; ABBYY FineReader 10 import/export with loss reporting; tests + CLI/API | no PDF figures/rotation/full reading-order model; ABBYY schema validation and richer metadata missing |
| Plain text + loss report | implemented | export tests | lossy by design |
| Automatic layout | projection baseline + opt-in CNN + opt-in two-column gutter split | P 0.403/R 0.473; learned M02a 0.965/0.987; page CER 0.971→0.942; [synthetic column tests](../evaluation/layout-columns.md) | default single-column; merged/shallow/curved/rotated layouts unsupported; out-of-domain P01 weak |
| Manual layout | move/resize/draw/delete/split/merge/order + audit | API/UI tests | no entity-level geometry revisions |
| Region/reading-order learning | geometric helpers only | bounded column grouping + opt-in gutter split | learned region/reading-order model and training data pending |
| C# recognition (CRNN+CTC) | implemented | pilot CER reports; L0 provenance + confidence; banded line decode for large JPEG/PNG | data-limited accuracy; CPU only; TIFF fallback full-frame |
| Training workflow | from scratch, resume, package, model card | ML/pilot tests | no distributed/GPU; no wide hyper-search |
| Recognition model distribution | not shipped; local/remote registry plumbing exists | strict index parser, bounded fetch, checksum-required download, safe extraction, host allow-list, CLI + admin API/UI catalog/pull | no public model weights; rights/capability review required |
| Inscription support | EpiDoc bootstrap + manual placement | ISic001151 smoke | no expert labels; no automated line detection benchmark |
| Evaluation | harness, per-track reports, page CER, one-to-one matching | docs/evaluation/* | frozen acceptance sheet + fresh witnesses pending |
| Experiment registry | recognition/layout/evaluation runs with digests, configs, metrics, curves, artifacts; per-line error CSV/JSON + Markdown report; `runs regress` and caller-supplied acceptance gates | CLI/API/UI + tests | no specialist-owned frozen threshold sheet |
| Active learning/review | persisted queue + double review/adjudication | tests + UI | no automatic prioritization model beyond confidence |
| AI proposals | propose/accept → L2, opt-in | AI tests; local Ollama smoke | hosted providers unproven; spend controls partial |
| Durable jobs | layout/import/recognize jobs, leases, cancel, persisted priority, optional per-owner quota | worker/API tests + live smoke | no distributed scheduler or rate limiter |
| Web workspace | editor, geometry, review, search, jobs, auth, deep links, three-way merge on save, surface and workspace session panels; optional packaged static hosting at /ui | 50 specs + [concurrency evidence](../evaluation/collaboration-concurrency.md) | no edit locks; geometry/order writes not merged |
| Auth/roles | accounts + roles + audit + opt-in per-project membership + admin panels | role/audit/membership/UI tests | no SSO/refresh tokens; session policy is the bounded TTL registry below |
| Presence | advisory per-surface heartbeat + display, shared SQLite store, 60 s TTL | service/API/UI tests | still advisory; session binding is optional |
| Editing sessions | server-issued, store-backed, TTL + heartbeat + revoke, node/surface binding, presence identity from principal; surface view creates/revokes its session and a panel lists/revokes visible sessions | service/API/UI + [evidence](../evaluation/collaboration-concurrency.md) | no push/broker or deployment-wide console |
| Invitations | admin-issued one-time project tokens, SHA-256 hashed, 1–168 h expiry, membership on accept | API/UI tests | accepted account lives in the same local auth store; no email delivery |
| Backup/restore | checksummed snapshot/restore | infra tests + smoke | no automated restore rehearsal/rollback UI |
| Packaging | publish + fresh-install smoke + osx-arm64 package/manifest | `scripts/package-release.sh` PASS; backup rehearsal PASS | no installers/updaters; Linux/Windows untested; models not bundled |
| Migration from Kraken | public formats + read-only inventory | `migrate inventory` smoke on M01/291-file reference checkout; import paths | no automatic project migration or product comparison |
| RTL/vertical metadata | partial | native/PAGE/hOCR/TEI direction+script fields, API/UI, tests | no full BiDi layout or vertical recognition; ALTO direction unmapped |
| Searchable PDF / distributed execution | searchable PDF: bounded; distributed execution: not implemented | capture + invisible L1 line text, CLI/API, loss report/tests | no distributed execution or automatic fragment joining; advanced imaging remains separate |

**Verdict:** usable pilot for Greek transcription on curated data; not a drop-in Kraken
replacement and not release-ready. The critical path is adjudicated data and fresh held-out
witnesses, not more UI surface.
