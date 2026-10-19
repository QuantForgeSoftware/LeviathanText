# Layout post-filter: dev-selected, held-out report

- Date: 2026-10-19T20:18:39.1167800+00:00; contract C0-0.4-draft
- Detector: projection profile (unchanged); filter applied to detected bands
- Selection set: M02a dev pages only (8 pages)
- Reporting set: held-out M02a pages + P01 pages (19 pages); P01 is out-of-domain
- Matching: vertical-band IoU ≥ 0.5

Dev scores for every candidate (selection is on F1):

| Candidate | Precision | Recall | F1 |
| --- | --- | --- | --- |
| none | 0.311 | 0.387 | 0.345 |
| density<0.5x median | 0.387 | 0.387 | 0.387 |
| height outside 0.5-2.5x median | 0.301 | 0.31 | 0.305 |
| density+height | 0.342 | 0.31 | 0.325 |
| merge gap<=0.15x height | 0.33 | 0.393 | 0.359 |
| merge gap<=0.30x height | 0.383 | 0.407 | 0.395 |
| merge gap<=0.50x height | 0.344 | 0.303 | 0.322 |
| merge 0.30 + density | 0.439 | 0.401 | 0.419 |

Selected on dev: **merge 0.30 + density**.

Held-out comparison:

| Variant | Mean precision | Mean recall |
| --- | --- | --- |
| Detector only | 0.358 | 0.483 |
| Detector + selected filter | 0.337 | 0.3 |

Split by domain (M02a is in-domain; P01 is out-of-domain for this detector):

| Subset | Variant | Precision | Recall |
| --- | --- | --- | --- |
| M02a held-out | detector only | 0.424 | 0.545 |
| M02a held-out | + selected filter | 0.582 | 0.518 |
| P01 (out-of-domain) | detector only | 0.268 | 0.398 |
| P01 (out-of-domain) | + selected filter | 0 | 0 |

Per-page (filtered):

| Page | GT | Detected | Matches | Precision | Recall |
| --- | --- | --- | --- | --- | --- |
| m02a:15_01_0053_0006_f_3r_res | 13 | 12 | 8 | 0.667 | 0.615 |
| m02a:15_01_0053_0011_f_5v_res | 19 | 17 | 12 | 0.706 | 0.632 |
| m02a:15_01_0053_0016_f_8r_res | 19 | 18 | 10 | 0.556 | 0.526 |
| m02a:15_01_0053_0021_f_10v_res | 19 | 17 | 9 | 0.529 | 0.474 |
| m02a:15_01_0053_0026_f_13r_res | 19 | 16 | 10 | 0.625 | 0.526 |
| m02a:15_01_0053_0031_f_15v_res | 19 | 13 | 10 | 0.769 | 0.526 |
| m02a:15_01_0053_0036_f_18r_res | 19 | 18 | 5 | 0.278 | 0.263 |
| m02a:15_01_0053_0041_f_20v_res | 20 | 18 | 9 | 0.5 | 0.45 |
| m02a:15_01_0053_0046_f_23r_res | 20 | 20 | 13 | 0.65 | 0.65 |
| m02a:15_01_0053_0051_f_25v_res | 21 | 22 | 13 | 0.591 | 0.619 |
| m02a:15_01_0053_0056_f_28r_res | 19 | 15 | 8 | 0.533 | 0.421 |
| p01:P.Cair.Zen.II.59263r | 6 | 2 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59376 | 21 | 1 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59377 | 11 | 2 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59381r | 6 | 3 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59390r | 5 | 1 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59391 | 7 | 1 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59392 | 4 | 2 | 0 | 0 | 0 |
| p01:P.Cair.Zen.III.59408 | 17 | 2 | 0 | 0 | 0 |

## Decision

**The dev-selected rule was NOT adopted.** Its combined held-out scores regress
(0.337/0.3 vs 0.358/0.483):
the dev set (8 M02a pages) is not representative of the out-of-domain P01 pages,
where the merge rule collapses most detected bands (zero matches: P=0, R=0)
and destroys valid short lines. The simpler density filter
from the first experiment remains the default (`Options.FilterImplausibleLines`;
disable to reproduce the unfiltered baseline).

This is a recorded methodological result: single-corpus dev selection can
overfit; held-out reporting across domains caught it.

## Status

A filter that only looks at the page's own line statistics can trade precision
for recall but cannot recover missed lines. Multi-column, curved and rotated
layouts remain unsupported; the next iteration is patch-level learning.