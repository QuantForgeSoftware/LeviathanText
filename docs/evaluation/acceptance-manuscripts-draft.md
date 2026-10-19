# Acceptance sheet — manuscripts track (draft, not frozen)

- Owner: **Instance B (EVAL)**; template: [acceptance-plan.md](acceptance-plan.md) §6.
- Status: **draft**. Cross-witness numbers are transfer measurements, not competence
  claims; labels are not adjudicated. No numeric threshold is set.
- Sources: [manuscripts.md §10–11](../data/manuscripts.md), ledgers
  [`m02a.json`](../../data/manifests/manuscripts/m02a.json),
  [`m01.json`](../../data/manifests/manuscripts/m01.json).

| Required entry | This track | Status |
| --- | --- | --- |
| Corpus/version, object counts, evaluated lines/signs, unresolved spans, split hashes | M02a v1.0 (Stavronikita 53, CC BY 4.0): zip md5 verified, 111 files; page-level split **571 train / 131 dev / 179 page-holdout test lines**. M01 v1.0: 120/120 image+XML pairs, **2,272 TextLines**; used as an independent cross-witness evaluation sample (568 lines, deterministic 1-in-4). Book-level split; no line-level randomization of the publisher's splits | recorded |
| Transcription profile, Unicode/scoring version, whitespace & uncertainty rules | contract **C0-0.4-draft**, NFC canonical-equivalent scalar CER; grapheme label set; overbarred nomina-sacra composites present (129 classes for M02a); canonical-equivalence only | recorded; **specialist adjudication pending (DATA-03)** |
| Raw-model results | M02a page-holdout: **CER 0.811** (150 epochs, no M01 data/tuning). Cross-witness M02a→M01: **CER 0.838** on 568 sampled lines; perfect-line rate 0. **Measured before the `0c6c768` crop fix; re-measure before accepting.** | measured, pilot-scale, pre-fix extraction |
| Language-assisted results | none implemented | not started |
| AI-assisted results | none; no provider call was run on this track | pending Phase 2 |
| Manual vs automatic layout | imported PAGE geometry only; automatic layout was not evaluated for manuscripts | imported recorded; automatic not started |
| Numeric accuracy/coverage thresholds and correction-time target | **unset**; requires owner/specialist decision and adjudicated labels | pending |
| Hardware, image sizes, throughput, peak RAM/VRAM, training budget | same macOS arm64 CPU / TorchSharp CPU pilot machine; per-track wall time and peak RAM not separately recorded | partial |
| Expert-review process, named owners, evidence links | reviewer/custodian **not named**; evidence in sight, cross-witness, model card/run manifests | pending |
| Per-profile strata (hand/capture/material) | M02a = **single codex**; M01 contains at least two hands (Camillos Zanettus ff.104r–174v vs Antonius Eparchos) but **no hand-holdout has been measured yet** | unsupported hand strata disclosed |
| Leakage check result | M02a/M01 are disjoint books; M01 was never trained on in this measurement; the 1-in-4 M01 sample is an evaluation sample, not a locked test with a custodian | partial |
| Independence check | no Kraken/Python runtime; TorchSharp + native libtorch only | recorded |
| Confidence / calibration | not measured for the manuscript model | pending |

## Honest interpretation

- 0.81 within-book and 0.84 cross-witness CER are near-chance for scholarly use at this
  data volume; the important result is that the **witness-level evaluation machinery**
  works and the cross-witness gap is small in relative terms.
- M02a is one codex; M01 is a second book. This is not evidence of broad manuscript
  coverage, and early Greek majuscule hands are absent.
- A manuscript acceptance claim needs adjudicated labels, a hand-level holdout, a named
  custodian and fresh unseen-book witnesses.

## Sign-off (blank until gates pass)

| Role | Name | Decision | Date |
| --- | --- | --- | --- |
| Project owner |  |  |  |
| Greek reviewer |  |  |  |
| Engineering owner |  |  |  |
| Evaluator / custodian |  |  |  |
