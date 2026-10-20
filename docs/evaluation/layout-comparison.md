# Automatic vs imported layout comparison (pilot)

- Date: 2026-10-20T00:22:27.6399310+00:00; contract C0-0.4-draft
- Automatic: ProjectionProfileLayout (single-column projection profile; multi-column/curved unsupported and recorded, not silently mis-segmented)
- Matching: vertical-band IoU ≥ 0.5 (disclosed tolerance)
- Confidence semantics: None (heuristic module has no probability claim)

| Witness | GT lines | Detected | Matches | Precision | Recall | Mean IoU |
| --- | --- | --- | --- | --- | --- | --- |
| p01:P.Cair.Zen.II.59263r | 6 | 3 | 0 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59376 | 21 | 10 | 2 | 0.2 | 0.095 | 0.735 |
| p01:P.Cair.Zen.III.59377 | 11 | 13 | 7 | 0.538 | 0.636 | 0.602 |
| p01:P.Cair.Zen.III.59381r | 6 | 8 | 3 | 0.375 | 0.5 | 0.569 |
| p01:P.Cair.Zen.III.59390r | 5 | 9 | 4 | 0.444 | 0.8 | 0.7 |
| p01:P.Cair.Zen.III.59391 | 7 | 12 | 2 | 0.167 | 0.286 | 0.691 |
| m02a:15_01_0053_0006_f_3r_res | 13 | 20 | 7 | 0.35 | 0.538 | 0.6 |
| m02a:15_01_0053_0007_f_3v_res | 19 | 23 | 9 | 0.391 | 0.474 | 0.632 |
| m02a:15_01_0053_0008_f_4r_res | 19 | 21 | 12 | 0.571 | 0.632 | 0.6 |
| m02a:15_01_0053_0009_f_4v_res | 19 | 18 | 10 | 0.556 | 0.526 | 0.563 |
| m02a:15_01_0053_0010_f_5r_res | 20 | 19 | 10 | 0.526 | 0.5 | 0.601 |
| m02a:15_01_0053_0011_f_5v_res | 19 | 18 | 13 | 0.722 | 0.684 | 0.57 |

**Mean precision 0.403, mean recall 0.473** (pilot sample).

Status: measurement baseline only; per-hand/capture strata and full acceptance thresholds await Phase 0 completion.