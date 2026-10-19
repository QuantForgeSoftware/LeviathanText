# End-to-end page CER (layout + recognition), held-out manuscript pages

- Date: 2026-10-19T19:44:19.4869380+00:00; contract C0-0.4-draft
- Model: `e2e-page-crnn` (CRNN h=64, 80 epochs max, trained on M02a train/dev lines only)
- Pipeline: image-only surface → automatic layout → recognition; no imported layout
- Matching: detected↔PAGE line vertical IoU ≥ 0.5; **missed GT lines count as full deletions**
- Evaluation pages: 6 held-out M02a pages (file split i%5==0); *same books as training — workflow measurement, not a generalization claim*
- Aggregate page CER (layout + recognition, misses as deletions): **0.956** over 3474 scalars
- Aggregate control CER (same model, PAGE boxes = perfect layout): **0.965** over 3474 scalars
- Layout-induced CER increase: **-0.009** absolute

| Page | GT lines | Detected | Matched | Detection recall | CER (matched auto boxes) | Page CER (incl. misses) | Control CER (PAGE boxes) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 15_01_0053_0006_f_3r_res | 13 | 23 | 8 | 0.615 | 0.924 | 0.953 | 0.963 |
| 15_01_0053_0011_f_5v_res | 19 | 24 | 14 | 0.737 | 0.874 | 0.908 | 0.962 |
| 15_01_0053_0016_f_8r_res | 19 | 22 | 9 | 0.474 | 0.93 | 0.967 | 0.962 |
| 15_01_0053_0021_f_10v_res | 19 | 24 | 9 | 0.474 | 0.924 | 0.965 | 0.963 |
| 15_01_0053_0026_f_13r_res | 19 | 20 | 11 | 0.579 | 0.946 | 0.968 | 0.97 |
| 15_01_0053_0031_f_15v_res | 19 | 23 | 10 | 0.526 | 0.948 | 0.973 | 0.967 |

## Reading

Page CER is substantially worse than line-crop CER because layout misses (and false
lines) are counted. That is the honest number for the image-only workflow: improving
recognition alone cannot fix lost lines. Multi-column/curved layouts remain
unsupported; layout quality is the current workflow bottleneck alongside label noise.