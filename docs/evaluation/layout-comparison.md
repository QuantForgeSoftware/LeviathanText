# Automatic vs imported layout comparison (pilot)

- Date: 2026-10-19T16:54:49.1011960+00:00; contract C0-0.4-draft
- Automatic: ProjectionProfileLayout (single-column projection profile; multi-column/curved unsupported and recorded, not silently mis-segmented)
- Matching: vertical-band IoU ≥ 0.5 (disclosed tolerance)
- Confidence semantics: None (heuristic module has no probability claim)

| Witness | GT lines | Detected | Matches | Precision | Recall | Mean IoU |
| --- | --- | --- | --- | --- | --- | --- |
| p01:P.Cair.Zen.II.59263r | 6 | 4 | 0 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59376 | 21 | 9 | 3 | 0.333 | 0.143 | 0.666 |
| p01:P.Cair.Zen.III.59377 | 11 | 15 | 7 | 0.467 | 0.636 | 0.602 |
| p01:P.Cair.Zen.III.59381r | 6 | 12 | 5 | 0.417 | 0.833 | 0.545 |
| p01:P.Cair.Zen.III.59390r | 5 | 10 | 4 | 0.4 | 0.8 | 0.7 |
| p01:P.Cair.Zen.III.59391 | 7 | 12 | 2 | 0.167 | 0.286 | 0.691 |
| m02a:15_01_0053_0006_f_3r_res | 13 | 23 | 8 | 0.348 | 0.615 | 0.588 |
| m02a:15_01_0053_0007_f_3v_res | 19 | 29 | 9 | 0.31 | 0.474 | 0.633 |
| m02a:15_01_0053_0008_f_4r_res | 19 | 26 | 9 | 0.346 | 0.474 | 0.591 |
| m02a:15_01_0053_0009_f_4v_res | 19 | 22 | 10 | 0.455 | 0.526 | 0.581 |
| m02a:15_01_0053_0010_f_5r_res | 20 | 28 | 11 | 0.393 | 0.55 | 0.618 |
| m02a:15_01_0053_0011_f_5v_res | 19 | 28 | 14 | 0.5 | 0.737 | 0.563 |

**Mean precision 0.345, mean recall 0.506** (pilot sample).

Status: measurement baseline only; per-hand/capture strata and full acceptance thresholds await Phase 0 completion.