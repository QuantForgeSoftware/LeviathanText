# Learned layout (patch/row CNN) vs projection — held-out report

- Date: 2026-10-20T02:05:16.3833260+00:00; contract C0-0.4-draft
- Model: `patch-row-cnn-v2` v2; input 256px high, max width 256; 100 epochs, seed 31
- Training: 35 M02a train pages (page-level split); threshold selected on 8 M02a dev pages; **test pages never trained on**
- Held-out: 17 pages (M02a test i%5==0 + 6 P01 test pages, reported separately as out-of-domain)
- Matching: vertical-band IoU ≥ 0.5; confidence: none (detector has no calibrated probability claim)
- Training wall time: 77.1s on the pilot CPU; package: `.artifacts/models/layout-patch-v1`
- Threshold selection table (dev only):

| Threshold | Precision | Recall | F1 |
| --- | --- | --- | --- |
| 0.20 | 0.981 | 0.98 | 0.981 |
| 0.30 | 0.981 | 0.98 | 0.981 |
| 0.40 | 0.994 | 1 | 0.997 |
| 0.50 | 0.994 | 1 | 0.997 |
| 0.60 | 0.994 | 1 | 0.997 |
| 0.70 | 0.994 | 1 | 0.997 |
| 0.80 | 0.994 | 1 | 0.997 |

Selected on dev: **0.40**.

| Subset | Detector | Precision | Recall |
| --- | --- | --- | --- |
| All held-out | patch CNN | 0.744 | 0.692 |
| All held-out | projection | 0.457 | 0.415 |
| M02a held-out | patch CNN | 0.983 | 1 |
| M02a held-out | projection | 0.523 | 0.545 |
| P01 (out-of-domain) | patch CNN | 0.306 | 0.126 |
| P01 (out-of-domain) | projection | 0.337 | 0.176 |

Per-page learned results:

| Page | GT | Detected | Matches | Precision | Recall |
| --- | --- | --- | --- | --- | --- |
| m02a:15_01_0053_0006_f_3r_res | 13 | 16 | 13 | 0.813 | 1 |
| m02a:15_01_0053_0011_f_5v_res | 19 | 19 | 19 | 1 | 1 |
| m02a:15_01_0053_0016_f_8r_res | 19 | 19 | 19 | 1 | 1 |
| m02a:15_01_0053_0021_f_10v_res | 19 | 19 | 19 | 1 | 1 |
| m02a:15_01_0053_0026_f_13r_res | 19 | 19 | 19 | 1 | 1 |
| m02a:15_01_0053_0031_f_15v_res | 19 | 19 | 19 | 1 | 1 |
| m02a:15_01_0053_0036_f_18r_res | 19 | 19 | 19 | 1 | 1 |
| m02a:15_01_0053_0041_f_20v_res | 20 | 20 | 20 | 1 | 1 |
| m02a:15_01_0053_0046_f_23r_res | 20 | 20 | 20 | 1 | 1 |
| m02a:15_01_0053_0051_f_25v_res | 21 | 21 | 21 | 1 | 1 |
| m02a:15_01_0053_0056_f_28r_res | 19 | 19 | 19 | 1 | 1 |
| p01:P.Cair.Zen.II.59263r | 6 | 3 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59391 | 7 | 6 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59412 | 26 | 14 | 9 | 0.643 | 0.346 |
| p01:P.Cair.Zen.III.59420 | 2 | 1 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59428 | 21 | 3 | 1 | 0.333 | 0.048 |
| p01:P.Cair.Zen.III.59433 | 33 | 14 | 12 | 0.857 | 0.364 |

## Decision

- **M02a codex model: adopted as an opt-in per-material detector** (API
  `LeviathanText:Layout:Models:<kind>`, CLI `--model`). It clearly beats projection
  on held-out pages of the same book.
- **P01 model: measured, not adopted.** It beats projection in aggregate on the P01
  test split but fails catastrophically on some dense pages (1–2 bands for 21–33 GT
  lines); a specialist-defined papyrus layout profile and more training material
  are required before enabling it.
- Projection remains the default. Neither detector is a scholarly layout claim.
- Matching is one-to-one greedy by vertical IoU (a merged detection cannot match two
  GT lines), so precision/recall are comparable and bounded by 1.

## Scope

Single-column pages only: the width axis is collapsed, so columns are not modeled.
The P01 split is the recognition locked split, already spent; this layout
measurement makes no locked-test claim.
## Papyrus-track model (P01 train/dev/test split)

- Training: 17 P01 train pages; threshold selected on 4 dev pages; evaluation on 6 P01 test pages
- Selected threshold: **0.85** (dev P=0.375, R=0.229)
- Package: `.artifacts/models/layout-patch-p01-v1`

| Detector | Precision | Recall |
| --- | --- | --- |
| patch CNN (P01-trained) | 0.444 | 0.324 |
| patch CNN (P01-trained, augmented) | 0.25 | 0.163 |
| projection | 0.337 | 0.176 |

| Page | GT | Detected | Matches | Precision | Recall |
| --- | --- | --- | --- | --- | --- |
| p01:P.Cair.Zen.II.59263r | 6 | 5 | 5 | 1 | 0.833 |
| p01:P.Cair.Zen.III.59391 | 7 | 6 | 4 | 0.667 | 0.571 |
| p01:P.Cair.Zen.III.59412 | 26 | 2 | 1 | 0.5 | 0.038 |
| p01:P.Cair.Zen.III.59420 | 2 | 2 | 1 | 0.5 | 0.5 |
| p01:P.Cair.Zen.III.59428 | 21 | 1 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59433 | 33 | 2 | 0 | 0 | 0 |

Augmentation did **not** help the P01-trained model (likely too little
training data: 17 pages); the P01 variant remains disabled. The P01 split is the
recognition locked split, already spent; this layout measurement makes no
locked-test claim. It shows whether the architecture learns a second material.
## Augmentation A/B (same seed and epochs)

Augmentation: vertical scale 0.85–1.15,
shift ±8 rows, intensity noise 0.030;
threshold selected on dev (0.70).

| Detector | M02a P | M02a R | P01 P | P01 R | All P | All R |
| --- | --- | --- | --- | --- | --- | --- |
| No augmentation | 0.983 | 1 | 0.306 | 0.126 | 0.744 | 0.692 |
| Augmented | 0.988 | 1 | 0.325 | 0.201 | 0.754 | 0.718 |

One seed, one material pair: a direction check, not a tuned result. Augmentation
improved both subsets here, so the **CLI training path enables it by default**
(`--no-augment` to disable); the library option stays explicit. A second corpus is
still required before any generalization claim.
## Fresh-book layout check: M01 (never used for layout training)

- Evaluation: 20 M01 pages (first sorted subset), single codex hand; no M01 data in layout training
- Same thresholds selected on M02a dev; no M01 tuning

| Detector | Precision | Recall |
| --- | --- | --- |
| Projection | 0.918 | 0.918 |
| Patch CNN (no augmentation) | 1 | 1 |
| Patch CNN (augmented) | 0.994 | 0.991 |

This is cross-book layout evidence within the same corpus project; it is not a
hand-level or broad-manuscript generalization claim. Layout labels remain
PAGE-derived, not specialist-adjudicated.