# Automatic layout: learned vs projection (pilot)

- Date: 2026-10-19T22:49:29.6418800+00:00; contract C0-0.4-draft
- Learned: `row-logit-v2` (per-row logistic regression; 13 ink-profile features; deterministic training)
- Baseline: projection profile (Otsu, trim, adaptive valleys)
- Training: M02a train pages (35); threshold tuned on M02a dev pages (8); evaluation: held-out M02a pages + P01 pages (19 pages)
- Selected threshold: 0.68; matching: vertical-band IoU ≥ 0.5; confidence: none (no probability claim)

| Detector | Mean precision | Mean recall |
| --- | --- | --- |
| Learned (`row-logit-v2`) | 0.101 | 0.04 |
| Projection baseline | 0.416 | 0.46 |

Per-page learned results:

| Page | GT | Detected | Matches | Precision | Recall |
| --- | --- | --- | --- | --- | --- |
| m02a:15_01_0053_0006_f_3r_res | 13 | 8 | 1 | 0.125 | 0.077 |
| m02a:15_01_0053_0011_f_5v_res | 19 | 7 | 3 | 0.429 | 0.158 |
| m02a:15_01_0053_0016_f_8r_res | 19 | 6 | 0 | 0 | 0 |
| m02a:15_01_0053_0021_f_10v_res | 19 | 4 | 0 | 0 | 0 |
| m02a:15_01_0053_0026_f_13r_res | 19 | 9 | 1 | 0.111 | 0.053 |
| m02a:15_01_0053_0031_f_15v_res | 19 | 3 | 0 | 0 | 0 |
| m02a:15_01_0053_0036_f_18r_res | 19 | 10 | 6 | 0.6 | 0.316 |
| m02a:15_01_0053_0041_f_20v_res | 20 | 5 | 1 | 0.2 | 0.05 |
| m02a:15_01_0053_0046_f_23r_res | 20 | 4 | 0 | 0 | 0 |
| m02a:15_01_0053_0051_f_25v_res | 21 | 5 | 1 | 0.2 | 0.048 |
| m02a:15_01_0053_0056_f_28r_res | 19 | 4 | 1 | 0.25 | 0.053 |
| p01:P.Cair.Zen.II.59263r | 6 | 4 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59376 | 21 | 3 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59377 | 11 | 2 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59381r | 6 | 3 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59390r | 5 | 3 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59391 | 7 | 2 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59392 | 4 | 3 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59408 | 17 | 4 | 0 | 0 | 0 |

## Status

**Measured outcome: the learned row model does NOT beat the corrected projection
baseline on this corpus** (0.04 vs 0.46 mean recall).
Diagnosis: a linear model over whole-row ink statistics cannot reliably separate
faint letter rows from parchment texture at the windows that fit within one line;
the projection detector with background-relative splitting is the stronger baseline
here. Next iteration: patch-level CNN features (or learned baselines) with the same
harness and split discipline. Multi-column, curved and rotated layouts remain
unsupported and must not be presented as solved.