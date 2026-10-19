# Layout post-filter: dev-selected, held-out report

- Date: 2026-10-19T19:21:23.3904730+00:00; contract C0-0.4-draft
- Detector: projection profile (unchanged); filter applied to detected bands
- Selection set: M02a dev pages only (8 pages)
- Reporting set: held-out M02a pages + P01 pages (19 pages); P01 is out-of-domain
- Matching: vertical-band IoU ≥ 0.5

Dev scores for every candidate (selection is on F1):

| Candidate | Precision | Recall | F1 |
| --- | --- | --- | --- |
| none | 0.289 | 0.387 | 0.331 |
| density<0.5x median | 0.31 | 0.387 | 0.344 |
| height outside 0.5-2.5x median | 0.299 | 0.329 | 0.313 |
| density+height | 0.312 | 0.329 | 0.32 |

Selected on dev: **density<0.5x median**.

Held-out comparison:

| Variant | Mean precision | Mean recall |
| --- | --- | --- |
| Detector only | 0.335 | 0.463 |
| Detector + selected filter | 0.381 | 0.463 |

Per-page (filtered):

| Page | GT | Detected | Matches | Precision | Recall |
| --- | --- | --- | --- | --- | --- |
| m02a:15_01_0053_0006_f_3r_res | 13 | 23 | 8 | 0.348 | 0.615 |
| m02a:15_01_0053_0011_f_5v_res | 19 | 24 | 14 | 0.583 | 0.737 |
| m02a:15_01_0053_0016_f_8r_res | 19 | 22 | 9 | 0.409 | 0.474 |
| m02a:15_01_0053_0021_f_10v_res | 19 | 24 | 9 | 0.375 | 0.474 |
| m02a:15_01_0053_0026_f_13r_res | 19 | 20 | 11 | 0.55 | 0.579 |
| m02a:15_01_0053_0031_f_15v_res | 19 | 23 | 10 | 0.435 | 0.526 |
| m02a:15_01_0053_0036_f_18r_res | 19 | 21 | 5 | 0.238 | 0.263 |
| m02a:15_01_0053_0041_f_20v_res | 20 | 21 | 7 | 0.333 | 0.35 |
| m02a:15_01_0053_0046_f_23r_res | 20 | 24 | 16 | 0.667 | 0.8 |
| m02a:15_01_0053_0051_f_25v_res | 21 | 24 | 10 | 0.417 | 0.476 |
| m02a:15_01_0053_0056_f_28r_res | 19 | 23 | 12 | 0.522 | 0.632 |
| p01:P.Cair.Zen.II.59263r | 6 | 3 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59376 | 21 | 9 | 3 | 0.333 | 0.143 |
| p01:P.Cair.Zen.III.59377 | 11 | 13 | 7 | 0.538 | 0.636 |
| p01:P.Cair.Zen.III.59381r | 6 | 8 | 5 | 0.625 | 0.833 |
| p01:P.Cair.Zen.III.59390r | 5 | 9 | 4 | 0.444 | 0.8 |
| p01:P.Cair.Zen.III.59391 | 7 | 12 | 2 | 0.167 | 0.286 |
| p01:P.Cair.Zen.III.59392 | 4 | 10 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59408 | 17 | 12 | 3 | 0.25 | 0.176 |

## Status

The density filter selected here is now the detector default (disable with
`Options.FilterImplausibleLines = false`); this record keeps the unfiltered
baseline explicit. A filter that only looks at the page's own line statistics can trade precision
for recall but cannot recover missed lines. Multi-column, curved and rotated
layouts remain unsupported; the next iteration is patch-level learning.