# Layout impact on the image-only page workflow

- Date: 2026-10-19T22:15:04.8579780+00:00; contract C0-0.4-draft
- Same recognition model/seed, same pages; only the layout detector changes
- Layout model: `patch-row-cnn-v2` trained on 35 M02a train pages, threshold 0.55
- Evaluation: 11 held-out M02a pages (one seed; recognition variance is measured separately)
- Matching: one-to-one greedy by vertical IoU ≥ 0.5; missed GT lines count as full deletions

| Detector | Aggregate page CER | Mean detection recall |
| --- | --- | --- |
| Projection | 0.971 | 0.545 |
| Patch CNN (learned) | 0.942 | 0.996 |

| Page | GT | Projection CER | Patch CER | Projection recall | Patch recall |
| --- | --- | --- | --- | --- | --- |
| 15_01_0053_0006_f_3r_res | 13 | 0.945 | 0.905 | 0.538 | 1 |
| 15_01_0053_0011_f_5v_res | 19 | 0.969 | 0.967 | 0.684 | 1 |
| 15_01_0053_0016_f_8r_res | 19 | 0.97 | 0.952 | 0.474 | 1 |
| 15_01_0053_0021_f_10v_res | 19 | 0.97 | 0.946 | 0.474 | 1 |
| 15_01_0053_0026_f_13r_res | 19 | 0.976 | 0.927 | 0.632 | 1 |
| 15_01_0053_0031_f_15v_res | 19 | 0.987 | 0.968 | 0.526 | 1 |
| 15_01_0053_0036_f_18r_res | 19 | 0.989 | 0.92 | 0.263 | 1 |
| 15_01_0053_0041_f_20v_res | 20 | 0.981 | 0.959 | 0.4 | 1 |
| 15_01_0053_0046_f_23r_res | 20 | 0.973 | 0.936 | 0.75 | 1 |
| 15_01_0053_0051_f_25v_res | 21 | 0.969 | 0.962 | 0.619 | 0.952 |
| 15_01_0053_0056_f_28r_res | 19 | 0.944 | 0.902 | 0.632 | 1 |

## Reading

The learned detector improves page-level text recovery mainly by finding lines the
projection heuristic misses; recognition quality on matched lines is unchanged. This
is a single-book, single-seed workflow measurement — not a generalization or
acceptance claim. The model remains opt-in and per-material.