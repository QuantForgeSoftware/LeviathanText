# End-to-end page CER (layout + recognition), held-out manuscript pages

- Date: 2026-10-19T19:30:04.4239880+00:00; contract C0-0.4-draft
- Model: `e2e-page-crnn` (CRNN h=64, 80 epochs max, trained on M02a train/dev lines only)
- Pipeline: image-only surface → automatic layout → recognition; no imported layout
- Matching: detected↔PAGE line vertical IoU ≥ 0.5; **missed GT lines count as full deletions**
- Evaluation pages: 6 held-out M02a pages (file split i%5==0); *same books as training — workflow measurement, not a generalization claim*
- Aggregate page CER (layout + recognition, misses as deletions): **0.938** over 3474 scalars
- Aggregate control CER (same model, PAGE boxes = perfect layout): **0.881** over 3474 scalars
- Layout-induced CER increase: **+0.056** absolute

| Page | GT lines | Detected | Matched | Detection recall | CER (matched auto boxes) | Page CER (incl. misses) | Control CER (PAGE boxes) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 15_01_0053_0006_f_3r_res | 13 | 23 | 8 | 0.615 | 0.896 | 0.935 | 0.855 |
| 15_01_0053_0011_f_5v_res | 19 | 24 | 14 | 0.737 | 0.944 | 0.959 | 0.866 |
| 15_01_0053_0016_f_8r_res | 19 | 22 | 9 | 0.474 | 0.91 | 0.957 | 0.871 |
| 15_01_0053_0021_f_10v_res | 19 | 24 | 9 | 0.474 | 0.879 | 0.944 | 0.88 |
| 15_01_0053_0026_f_13r_res | 19 | 20 | 11 | 0.579 | 0.841 | 0.906 | 0.902 |
| 15_01_0053_0031_f_15v_res | 19 | 23 | 10 | 0.526 | 0.853 | 0.924 | 0.904 |

## Reading

Page CER is substantially worse than line-crop CER because layout misses (and false
lines) are counted. That is the honest number for the image-only workflow: improving
recognition alone cannot fix lost lines. Multi-column/curved layouts remain
unsupported; layout quality is the current workflow bottleneck alongside label noise.