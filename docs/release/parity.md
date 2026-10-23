# Reference capability matrix (honest status)

Date: 2026-10-19. This is **not a claim of full Kraken parity**. Each row states what exists,
where the evidence is, and what is missing. Acceptance thresholds remain unfrozen and labels
are not specialist-adjudicated.

| Capability | Status | Evidence | Missing / limits |
| --- | --- | --- | --- |
| PAGE XML import/export | implemented (bounded) | 24 format tests; round-trips; loss reports | unsupported elements reported, not mapped |
| TEI/EpiDoc import/export | implemented (bounded) | format tests; ISicily records parse | bounded profile; no full TEI coverage |
| ALTO v4 / hOCR | implemented (bounded) | adapter tests + legacy import | no word segmentation synthesis; ALTO does not carry direction |
| IIIF Presentation 3 import | implemented (bounded) | real manifest test; remote image cache | no IIIF hosting |
| PDF / ABBYY XML | PDF pages + optional L2 text: partial; ABBYY: not implemented | `PdfRenderer`/`PdfTextExtractor` tests + CLI/API smoke | page rasterization/L2 text with gap-based line segmentation; no reading-order/column inference; ABBYY undecided |
| Plain text + loss report | implemented | export tests | lossy by design |
| Automatic layout | projection baseline + opt-in CNN | P 0.403/R 0.473; learned M02a 0.965/0.987; page CER 0.971→0.942 | single-column only; out-of-domain P01 weak; no columns/rotation |
| Manual layout | move/resize/draw/delete/split/merge/order + audit | API/UI tests | no entity-level geometry revisions |
| Region/reading-order learning | not implemented | manual/order only | training data/contracts pending |
| C# recognition (CRNN+CTC) | implemented | pilot CER reports; L0 provenance + confidence; banded line decode for large JPEG/PNG | data-limited accuracy; CPU only; TIFF fallback full-frame |
| Training workflow | from scratch, resume, package, model card | ML/pilot tests | no distributed/GPU; no wide hyper-search |
| Recognition model distribution | not shipped | — | rights + capability review required |
| Inscription support | EpiDoc bootstrap + manual placement | ISic001151 smoke | no expert labels; no automated line detection benchmark |
| Evaluation | harness, per-track reports, page CER, one-to-one matching | docs/evaluation/* | frozen acceptance sheet + fresh witnesses pending |
| Experiment registry | recognition/layout/evaluation runs with digests, configs, metrics, curves, artifacts; per-line error CSV/JSON + Markdown report; `runs regress` and caller-supplied acceptance gates | CLI/API/UI + tests | no specialist-owned frozen threshold sheet |
| Active learning/review | persisted queue + double review/adjudication | tests + UI | no automatic prioritization model beyond confidence |
| AI proposals | propose/accept → L2, opt-in | AI tests; local Ollama smoke | hosted providers unproven; spend controls partial |
| Durable jobs | layout/import/recognize jobs, leases, cancel, persisted priority, optional per-owner quota | worker/API tests + live smoke | no distributed scheduler or rate limiter |
| Web workspace | editor, geometry, review, search, jobs, auth, deep links, conflict rebase prompt; optional packaged static hosting at /ui | 41 specs | no automatic concurrent-edit merge; advisory store-backed presence |
| Auth/roles | accounts + roles + audit + opt-in per-project membership + admin panels | role/audit/membership/UI tests | no SSO/session policy or invitation tokens |
| Presence | advisory per-surface heartbeat + display, shared SQLite store, 60 s TTL | service/API/UI tests | not a distributed registry; no merge/lock |
| Invitations | admin-issued one-time project tokens, SHA-256 hashed, 1–168 h expiry, membership on accept | API/UI tests | accepted account lives in the same local auth store; no email delivery |
| Backup/restore | checksummed snapshot/restore | infra tests + smoke | no automated restore rehearsal/rollback UI |
| Packaging | publish + fresh-install smoke + osx-arm64 package/manifest | `scripts/package-release.sh` PASS; backup rehearsal PASS | no installers/updaters; Linux/Windows untested; models not bundled |
| Migration from Kraken | public formats only | import paths | no automatic project migration |
| RTL/vertical metadata | partial | native/PAGE/hOCR/TEI direction+script fields, API/UI, tests | no full BiDi layout or vertical recognition; ALTO direction unmapped |
| Searchable PDF / distributed execution | not implemented | — | separately estimated extensions |

**Verdict:** usable pilot for Greek transcription on curated data; not a drop-in Kraken
replacement and not release-ready. The critical path is adjudicated data and fresh held-out
witnesses, not more UI surface.
