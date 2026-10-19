# Learned layout (patch/row CNN) vs projection — held-out report

- Date: 2026-10-19T22:12:52.2022420+00:00; contract C0-0.4-draft
- Model: `patch-row-cnn-v2` v2; input 256px high, max width 256; 100 epochs, seed 31
- Training: 35 M02a train pages (page-level split); threshold selected on 8 M02a dev pages; **test pages never trained on**
- Held-out: 17 pages (M02a test i%5==0 + P01 8 pages, reported separately as out-of-domain)
- Matching: vertical-band IoU ≥ 0.5; confidence: none (detector has no calibrated probability claim)
- Training wall time: 46.2s on the pilot CPU; package: `.artifacts/models/layout-patch-v1`
- Threshold selection table (dev only):

| Threshold | Precision | Recall | F1 |
| --- | --- | --- | --- |
| 0.20 | 0.989 | 1 | 0.994 |
| 0.30 | 0.989 | 1 | 0.994 |
| 0.40 | 0.989 | 1 | 0.994 |
| 0.50 | 0.989 | 1 | 0.994 |
| 0.60 | 0.989 | 1 | 0.994 |
| 0.70 | 0.989 | 1 | 0.994 |
| 0.80 | 0.989 | 1 | 0.994 |

Selected on dev: **0.20**.

| Subset | Detector | Precision | Recall |
| --- | --- | --- | --- |
| All held-out | patch CNN | 0.734 | 0.686 |
| All held-out | projection | 0.457 | 0.415 |
| M02a held-out | patch CNN | 0.965 | 0.987 |
| M02a held-out | projection | 0.523 | 0.545 |
| P01 (out-of-domain) | patch CNN | 0.31 | 0.134 |
| P01 (out-of-domain) | projection | 0.337 | 0.176 |

Per-page learned results:

| Page | GT | Detected | Matches | Precision | Recall |
| --- | --- | --- | --- | --- | --- |
| m02a:15_01_0053_0006_f_3r_res | 13 | 16 | 13 | 0.813 | 1 |
| m02a:15_01_0053_0011_f_5v_res | 19 | 19 | 19 | 1 | 1 |
| m02a:15_01_0053_0016_f_8r_res | 19 | 20 | 19 | 0.95 | 1 |
| m02a:15_01_0053_0021_f_10v_res | 19 | 19 | 19 | 1 | 1 |
| m02a:15_01_0053_0026_f_13r_res | 19 | 19 | 19 | 1 | 1 |
| m02a:15_01_0053_0031_f_15v_res | 19 | 19 | 19 | 1 | 1 |
| m02a:15_01_0053_0036_f_18r_res | 19 | 19 | 19 | 1 | 1 |
| m02a:15_01_0053_0041_f_20v_res | 20 | 19 | 18 | 0.947 | 0.9 |
| m02a:15_01_0053_0046_f_23r_res | 20 | 21 | 20 | 0.952 | 1 |
| m02a:15_01_0053_0051_f_25v_res | 21 | 21 | 20 | 0.952 | 0.952 |
| m02a:15_01_0053_0056_f_28r_res | 19 | 19 | 19 | 1 | 1 |
| p01:P.Cair.Zen.II.59263r | 6 | 6 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59391 | 7 | 6 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59412 | 26 | 12 | 7 | 0.583 | 0.269 |
| p01:P.Cair.Zen.III.59420 | 2 | 2 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59428 | 21 | 3 | 1 | 0.333 | 0.048 |
| p01:P.Cair.Zen.III.59433 | 33 | 17 | 16 | 0.941 | 0.485 |

## Scope and decision rule

Single-column pages only: the width axis is collapsed, so columns are not modeled.
A useful model must beat projection on held-out M02a **without collapsing** on P01;
otherwise the projection detector remains the default and this is recorded as a
negative result. Neither detector is a scholarly layout claim.
## Papyrus-track model (P01 train/dev/test split)

- Training: 17 P01 train pages; threshold selected on 4 dev pages; evaluation on 6 P01 test pages
- Selected threshold: **0.85** (dev P=0.375, R=0.229)
- Package: `.artifacts/models/layout-patch-p01-v1`

| Detector | Precision | Recall |
| --- | --- | --- |
| patch CNN (P01-trained) | 0.444 | 0.324 |
| projection | 0.337 | 0.176 |

| Page | GT | Detected | Matches | Precision | Recall |
| --- | --- | --- | --- | --- | --- |
| p01:P.Cair.Zen.II.59263r | 6 | 5 | 5 | 1 | 0.833 |
| p01:P.Cair.Zen.III.59391 | 7 | 6 | 4 | 0.667 | 0.571 |
| p01:P.Cair.Zen.III.59412 | 26 | 2 | 1 | 0.5 | 0.038 |
| p01:P.Cair.Zen.III.59420 | 2 | 2 | 1 | 0.5 | 0.5 |
| p01:P.Cair.Zen.III.59428 | 21 | 1 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59433 | 33 | 2 | 0 | 0 | 0 |

The P01 split is the recognition locked split, already spent; this layout
measurement makes no locked-test claim. It shows whether the architecture
learns a second material at all.