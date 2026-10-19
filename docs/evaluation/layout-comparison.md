# Automatic vs imported layout comparison (pilot)

- Date: 2026-10-19T15:21:07.9740950+00:00; contract C0-0.4-draft
- Automatic: ProjectionProfileLayout (single-column projection profile; multi-column/curved unsupported and recorded, not silently mis-segmented)
- Matching: vertical-band IoU ≥ 0.5 (disclosed tolerance)
- Confidence semantics: None (heuristic module has no probability claim)

| Witness | GT lines | Detected | Matches | Precision | Recall | Mean IoU |
| --- | --- | --- | --- | --- | --- | --- |
| p01:P.Cair.Zen.II.59263r | 6 | 4 | 0 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59376 | 21 | 3 | 0 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59377 | 11 | 16 | 6 | 0.375 | 0.545 | 0.621 |
| p01:P.Cair.Zen.III.59381r | 6 | 4 | 0 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59390r | 5 | 6 | 1 | 0.167 | 0.2 | 0.714 |
| p01:P.Cair.Zen.III.59391 | 7 | 3 | 0 | 0 | 0 | 0 |
| m02a:15_01_0053_0006_f_3r_res | 13 | 20 | 4 | 0.2 | 0.308 | 0.615 |
| m02a:15_01_0053_0007_f_3v_res | 19 | 5 | 0 | 0 | 0 | 0 |
| m02a:15_01_0053_0008_f_4r_res | 19 | 10 | 1 | 0.1 | 0.053 | 0.508 |
| m02a:15_01_0053_0009_f_4v_res | 19 | 3 | 0 | 0 | 0 | 0 |
| m02a:15_01_0053_0010_f_5r_res | 20 | 5 | 1 | 0.2 | 0.05 | 0.575 |
| m02a:15_01_0053_0011_f_5v_res | 19 | 4 | 0 | 0 | 0 | 0 |

**Mean precision 0.087, mean recall 0.096** (pilot sample).

Status: measurement baseline only; per-hand/capture strata and full acceptance thresholds await Phase 0 completion.