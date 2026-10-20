# Reference capability matrix (honest status)

Date: 2026-10-19. This is **not a claim of full Kraken parity**. Each row states what exists,
where the evidence is, and what is missing. Acceptance thresholds remain unfrozen and labels
are not specialist-adjudicated.

| Capability | Status | Evidence | Missing / limits |
| --- | --- | --- | --- |
| PAGE XML import/export | implemented (bounded) | 24 format tests; round-trips; loss reports | unsupported elements reported, not mapped |
| TEI/EpiDoc import/export | implemented (bounded) | format tests; ISicily records parse | bounded profile; no full TEI coverage |
| ALTO v4 / hOCR | implemented (bounded) | adapter tests + legacy import | no word segmentation synthesis |
| IIIF Presentation 3 import | implemented (bounded) | real manifest test; remote image cache | no IIIF hosting |
| PDF / ABBYY XML | not implemented | — | ingestion/backends undecided |
| Plain text + loss report | implemented | export tests | lossy by design |
| Automatic layout | projection baseline + opt-in CNN | P 0.403/R 0.473; learned M02a 0.965/0.987; page CER 0.971→0.942 | single-column only; out-of-domain P01 weak; no columns/rotation |
| Manual layout | move/resize/draw/delete/split/merge/order + audit | API/UI tests | no entity-level geometry revisions |
| Region/reading-order learning | not implemented | manual/order only | training data/contracts pending |
| C# recognition (CRNN+CTC) | implemented | pilot CER reports; L0 provenance + confidence | data-limited accuracy; CPU only |
| Training workflow | from scratch, resume, package, model card | ML/pilot tests | no distributed/GPU; no wide hyper-search |
| Recognition model distribution | not shipped | — | rights + capability review required |
| Inscription support | EpiDoc bootstrap + manual placement | ISic001151 smoke | no expert labels; no automated line detection benchmark |
| Evaluation | harness, per-track reports, page CER, one-to-one matching | docs/evaluation/* | frozen acceptance sheet + fresh witnesses pending |
| Experiment registry | recognition/layout runs with digests, configs, metrics, curves, artifacts | CLI/API/UI + tests | no error-analysis drilldown or automated report publishing |
| Active learning/review | persisted queue + double review/adjudication | tests + UI | no automatic prioritization model beyond confidence |
| AI proposals | propose/accept → L2, opt-in | AI tests; local Ollama smoke | hosted providers unproven; spend controls partial |
| Durable jobs | layout/import/recognize jobs, leases, cancel | worker/API tests + live smoke | no scheduler/priority/quota |
| Web workspace | editor, geometry, review, search, jobs, auth | 29 specs | no concurrent workspaces, no deep links |
| Auth/roles | accounts + roles + audit + opt-in per-project membership | role/audit/membership tests | no SSO/session policy or invitation/presence UI |
| Backup/restore | checksummed snapshot/restore | infra tests + smoke | no automated restore rehearsal/rollback UI |
| Packaging | publish + fresh-install smoke | script PASS | no installers/updaters; target matrix untested |
| Migration from Kraken | public formats only | import paths | no automatic project migration |
| RTL/vertical scripts | not implemented | — | Phase 4 |
| Searchable PDF / distributed execution | not implemented | — | separately estimated extensions |

**Verdict:** usable pilot for Greek transcription on curated data; not a drop-in Kraken
replacement and not release-ready. The critical path is adjudicated data and fresh held-out
witnesses, not more UI surface.
