# P01 pilot evaluation (pilot-scale; locked set used multiple times)

- Date: 2026-10-19T20:54:39.0345190+00:00; contract C0-0.4-draft
- **Locked-set status:** this split has been scored multiple times (pre-fix and post-fix re-baselines); it is spent for threshold purposes. See `docs/evaluation/locked-set-reuse.md`.
- Dataset: P01 Zenon Papyri HTR v0.9 (CC BY 4.0), md5-verified, 27 pairs, 321 lines
- Split: witness-level (file = unit); train 176 / dev 50 / **test 95 (locked)**
- Split hash (sha256 of sorted assignment): `f36aaa2ecd742b5c037e2e5ef63c92810abf2ce906b5f1e19953ddf6272297c6`
- Model: eval-pilot-crnn CRNN(h=64), 300 epochs, lr 0.003, seed 42; train loss 1.07 → 0.014
- Labels: Transkribus-prepared diplomatic text, grapheme-level label set (45 classes), NOT yet expert-adjudicated (DATA-03 pending)
- Layout: imported (PAGE geometry) — this is recognition-on-manual-layout, never advertised as fully automatic

## Raw-model results (no AI assistance)

| Metric | Value |
| --- | --- |
| Scalar CER (canonical-equivalent, case-sensitive) | 0.834 |
| Perfect lines | 0/95 |
| Mark insertions (wrongly supplied marks) | 0 |
| Mark deletions (missing marks) | 5 |
| Mark substitutions | 0 |

## Operating limits measured (EVAL-04, pilot machine: macOS arm64, CPU inference/training)

| Entry | Value |
| --- | --- |
| Training wall time | 0.8 min |
| Training seconds per line | 0.286 s |
| Peak RAM (process) | unmeasured on macOS (platform limitation); working set at report time 1101 MB |
| Total allocations | 49433 MB |
| Backend | TorchSharp CPU (libtorch-cpu-osx-arm64 2.10.0) |

## Status and caveats

- **Pilot-scale**: generalization is data-limited (176 training lines); 0.834 is a measured baseline, not a threshold or a pass/fail result.
- The locked split has been used multiple times; under acceptance-plan §1 it is spent for threshold purposes and any further claim requires fresh held-out witnesses and disclosure.
- Missing tracks: manuscripts and inscriptions are not evaluated here; the combined initial scope is not complete (acceptance-plan §9).