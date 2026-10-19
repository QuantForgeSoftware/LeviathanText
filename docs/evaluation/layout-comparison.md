# Automatic vs imported layout comparison (pilot)

- Date: 2026-10-19T19:43:17.4734530+00:00; contract C0-0.4-draft
- Automatic: ProjectionProfileLayout (single-column projection profile; multi-column/curved unsupported and recorded, not silently mis-segmented)
- Matching: vertical-band IoU ≥ 0.5 (disclosed tolerance)
- Confidence semantics: None (heuristic module has no probability claim)

| Witness | GT lines | Detected | Matches | Precision | Recall | Mean IoU |
| --- | --- | --- | --- | --- | --- | --- |
| p01:P.Cair.Zen.II.59263r | 6 | 3 | 0 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59376 | 21 | 9 | 3 | 0.333 | 0.143 | 0.666 |
| p01:P.Cair.Zen.III.59377 | 11 | 13 | 7 | 0.538 | 0.636 | 0.602 |
| p01:P.Cair.Zen.III.59381r | 6 | 8 | 5 | 0.625 | 0.833 | 0.545 |
| p01:P.Cair.Zen.III.59390r | 5 | 9 | 4 | 0.444 | 0.8 | 0.7 |
| p01:P.Cair.Zen.III.59391 | 7 | 12 | 2 | 0.167 | 0.286 | 0.691 |
| m02a:15_01_0053_0006_f_3r_res | 13 | 23 | 8 | 0.348 | 0.615 | 0.588 |
| m02a:15_01_0053_0007_f_3v_res | 19 | 29 | 9 | 0.31 | 0.474 | 0.633 |
| m02a:15_01_0053_0008_f_4r_res | 19 | 26 | 9 | 0.346 | 0.474 | 0.591 |
| m02a:15_01_0053_0009_f_4v_res | 19 | 21 | 10 | 0.476 | 0.526 | 0.581 |
| m02a:15_01_0053_0010_f_5r_res | 20 | 24 | 11 | 0.458 | 0.55 | 0.618 |
| m02a:15_01_0053_0011_f_5v_res | 19 | 24 | 14 | 0.583 | 0.737 | 0.563 |

**Mean precision 0.386, mean recall 0.506** (pilot sample).

Status: measurement baseline only; per-hand/capture strata and full acceptance thresholds await Phase 0 completion.