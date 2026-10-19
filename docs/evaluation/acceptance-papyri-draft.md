# Acceptance sheet — papyri track (draft, not frozen)

- Owner: **Instance B (EVAL)**; template: [acceptance-plan.md](acceptance-plan.md) §6.
- Status: **draft for the project owner and Greek reviewer**. No numeric threshold is set;
  blank cells are deliberate, not omissions.
- Corpus evaluated: P01 v0.9 (Zenon archive). This sheet does not cover manuscripts or
  inscriptions, and no pooled three-track number exists.

| Required entry | This track | Status |
| --- | --- | --- |
| Corpus/version, object counts, evaluated lines/signs, unresolved spans, split hashes | P01 v0.9, CC BY 4.0, md5-verified; 27 image/PAGE pairs, **321 lines**; witness-level split train 176 / dev 50 / **test 95 locked**; split hash `f36aaa2ecd742b5c037e2e5ef63c92810abf2ce906b5f1e19953ddf6272297c6`; 5 unannotated images excluded | recorded |
| Transcription profile, Unicode/scoring version, whitespace & uncertainty rules | contract **C0-0.4-draft**; NFC canonical-equivalent scalars/graphemes; case-sensitive scalar CER as primary; combining marks reported separately; no accent stripping/case folding; whitespace preserved; uncertainty scored only on agreed non-excluded spans | recorded; **specialist adjudication pending (BLK-2)** |
| Raw-model results | dev: **0.797–0.805** scalar CER (default height 16 / width 4, selected best checkpoint); **0.783** with training augmentation; locked: **0.828** (second and final use — see [locked-set-reuse.md](locked-set-reuse.md)) | measured, pilot-scale |
| Language-assisted results | none implemented; no lexicon/morphology pass exists | not started |
| AI-assisted results | measurement harness ready (`CorrectionEffortScorer`, calibration, risk–coverage); **no live provider call, credential, or endpoint has been tested** | pending Phase 2 |
| Manual vs automatic layout | all numbers above use **imported PAGE geometry**, never advertised as fully automatic; automatic projection-profile layout was measured inadequate on this corpus (recall ≈0.1, [layout-comparison.md](layout-comparison.md)) | imported: recorded; automatic: blocker |
| Numeric accuracy/coverage thresholds and correction-time target | **unset** — require frozen owner/specialist decision; the locked split is already spent for threshold purposes | pending |
| Hardware, image sizes, throughput, peak RAM/VRAM, training budget | macOS arm64 CPU; TorchSharp CPU (libtorch 2.10.0); train 176 lines in ≈1.1 min (0.364 s/line); greedy inference ≈0.0002 s/line on dev; allocations ≈9.6 GB; peak RAM unmeasured on macOS (platform limitation) | measured |
| Expert-review process, named owners, evidence links | evaluator/custodian and Greek reviewer **not named**; evidence: pilot reports, run manifests, handoff records | pending |
| Per-profile strata (hand/capture/material) | one documentary hand (Zenon archive); **no unseen-hand/unseen-collection result exists**; unsupported strata must be disclosed, not pooled | single stratum |
| Leakage check result | witness-level split by file; object/lineage dedup within P01 assumed by the source structure but not separately audited against other papyrus sources (DATA-02 pending); locked split reused twice and disclosed | partial |
| Independence check | no Kraken, Python or external OCR runtime in the build/test path; TorchSharp + native libtorch only | recorded |
| Confidence / calibration | raw per-grapheme confidence ECE ≈0.151 / MCE ≈0.552 (dev, post-fix); not calibrated enough to display as certainty or to justify abstention claims | measured; not accepted |

## Sign-off (must remain blank until the gates pass)

| Role | Name | Decision | Date |
| --- | --- | --- | --- |
| Project owner |  |  |  |
| Greek reviewer |  |  |  |
| Engineering owner |  |  |  |
| Evaluator / custodian |  |  |  |

**Freeze rule:** this sheet cannot be frozen while thresholds, specialist adjudication,
unseen-hand coverage and a fresh held-out set are missing. A failed quality gate cannot be
relabelled as success by pooling tracks or ignoring unsupported strata.
