# P46 expanded-corpus appearance audit — 2026-11-02 evening

Status: **complete, descriptive audit of all 51 exact shared C# inputs**. This is a read-only appearance audit, not a model comparison, label acceptance or corpus-selection step. It leaves the frozen corpus and training inputs unchanged. The new character/leaf coverage is documented, but **faded-condition coverage remains unestablished**.

The [expanded provisional corpus](../data/p46-expanded-provisional-corpus.md) adds 27 source-reference training lines to the earlier 17; the seven original development lines remain fixed. All source identities, labels and geometry remain unadjudicated. Repeating training instances for batch weighting does not create new appearance observations: this audit counts the 17 old training, 27 new training and seven development lines once each.

## Declared measurement method

Before inspecting the new prepared values, retain `method.json` under `.artifacts/experiments/p46-evening-appearance-audit/`. Read only the root-provided **exact raw C# luminance height-16 float inputs**, validating their manifest hashes, dimensions and finite [0,1] values. Do not reconstruct those inputs using a different image library or replace them with quantized previews.

The primary region is all 16 rows and columns **70 through 389 inclusive**. This is the half-scale counterpart of the earlier diagnostic's height-32 columns 140–779; for the 1695-pixel crop beginning at source X480, it samples approximately source X761–2037. The same output coordinates apply to every line. A predeclared full-width measurement serves only as a sensitivity check for outer paper/white margins.

For each unique line, report linear-interpolation fifth percentile, median, 95th percentile, median minus fifth percentile, and the fraction strictly below luminance **0.22**. The threshold is inherited from the earlier diagnostic; it is a dark-pixel intensity proxy, not estimated foreground and not a rule for including/excluding lines. Aggregate line metrics by page, physical leaf and source group, retaining equal weight for unique lines and reporting medians/ranges rather than treating all pixels as independent examples.

The threshold comparison uses the float32 representation of 0.22, approximately `0.2199999988079071`, matching the stored input dtype. This precision disclosure does not change the registered threshold or select any data. Raw per-line values, arithmetic means, medians and ranges are retained. Tables below use arithmetic means of the unique-line metrics; surfaces with more lines therefore have more weight in a pooled source-group mean, and page/leaf tables make that imbalance visible.

No threshold, region, crop, label or model setting is tuned from these results. No earlier metadata-only leaves 78–80 are opened.

## Interpretation limits

Paper brightness/color, text density, word length, side, blank margins, holes, capture/resolution and geometric rectification confound intensity comparisons. A fixed central strip does not contain exactly the same amount of text on each surface. Lower-tail luminance and threshold fractions do **not** segment ink, establish legibility, isolate stroke contrast or prove that faded development conditions are represented in training.

The earlier height-32 statistics used quantized preview rasters. They are historical context, not numerically interchangeable with this exact height-16 audit. The frozen corpus's character counts demonstrate increased reference coverage, while ψ remains represented only once; neither statement establishes appearance or scholarly coverage. This remains one Koine witness and supplies no unseen-hand, Classical Greek or inscription validation.

## Measured exact-input results

Input manifest: `.artifacts/experiments/p46-expanded-study-2026-11-02-evening/input-manifest.json`; raw files: `inputs/<id>.f32`, little-endian float32, row-major 424 × 16. Its SHA-256 is `94f438f2bcf67ecbcb9e70be5497e1652edb6f9e76765298c5eeeede734a995e`. All 51 hashes, shapes, finite [0,1] values and `dataset.json` page/physical-leaf/source-group fields agree. The 24 old inputs and metric records reproduce the frozen R3 raw-input results exactly; the audit does not reinterpret old pixels.

Interior strip, **mean of per-line metrics**:

| Source group | Unique lines | Fifth percentile | Median | Median − fifth percentile | Fraction below 0.22 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Old training, leaf 82 | 17 | 0.1201 | 0.3854 | 0.2653 | 11.65% |
| Exposed development, leaf 81 | 7 | 0.1666 | 0.4072 | 0.2406 | 9.06% |
| New provisional training, leaves 83–85 | 27 | 0.1231 | 0.3619 | 0.2388 | 12.28% |

| Page / role | Lines | Fifth percentile | Median | Median − fifth percentile | Fraction below 0.22 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1440 / old development | 7 | 0.1666 | 0.4072 | 0.2406 | 9.06% |
| 1450 / old training | 7 | 0.1211 | 0.3815 | 0.2604 | 11.42% |
| 1460 / old training | 10 | 0.1194 | 0.3881 | 0.2688 | 11.81% |
| 1470 / new training | 8 | 0.1070 | 0.3658 | 0.2588 | 12.49% |
| 1480 / new training | 4 | 0.0901 | 0.3096 | 0.2195 | 17.82% |
| 1490 / new training | 4 | 0.1178 | 0.3665 | 0.2487 | 12.93% |
| 1500 / new training | 6 | 0.1395 | 0.3580 | 0.2186 | 10.77% |
| 1510 / new training | 3 | 0.1448 | 0.3687 | 0.2239 | 9.98% |
| 1520 / new training | 2 | 0.1821 | 0.4432 | 0.2611 | 6.99% |

| Proposed physical leaf / role | Lines | Fifth percentile | Median − fifth percentile | Fraction below 0.22 |
| --- | ---: | ---: | ---: | ---: |
| 81 / old development | 7 | 0.1666 | 0.2406 | 9.06% |
| 82 / old training | 17 | 0.1201 | 0.2653 | 11.65% |
| 83 / new training | 12 | 0.1014 | 0.2457 | 14.27% |
| 84 / new training | 10 | 0.1308 | 0.2306 | 11.64% |
| 85 / new training | 5 | 0.1597 | 0.2387 | 8.79% |

The added material is not uniformly similar to development on these measurements. Its average fifth-percentile value remains close to old training, while its median-minus-fifth-percentile average is numerically close to development. Its median luminance is darker than either old group. These mixed directions cannot identify a common physical cause: darker paper, text density and actual stroke appearance all affect the same statistics.

Page 1520's two rows have a higher fifth percentile and a smaller fraction below 0.22 than many training rows, with some numerical overlap with development. Two unadjudicated lines do not establish coverage of faded writing. Per-line fifth-percentile ranges are **0.0963–0.1511** old training, **0.1340–0.2077** development and **0.0804–0.1844** new training. Median-minus-fifth-percentile ranges overlap substantially: **0.1928–0.3521**, **0.1829–0.2796** and **0.1724–0.3264**, respectively. Overlap of mixture statistics is not overlap of annotated ink/legibility conditions.

## Margin sensitivity and conclusion

The full-width sensitivity view includes outer paper/white background. For example, development's mean line 95th percentile rises from **0.5083** in the interior strip to **0.9837** over the full width; page 1480 rises from **0.4005 to 0.9866**. Even the interior strip includes some white edge/background in short or torn rows: `P46-1490-L18` has interior p95 approximately 0.9588. Thus p95 is not a measured paper background, and a global quantile or dark-pixel fraction must not be called segmented ink.

Use this corpus as a **provisional character/leaf expansion**, not proof that the faint development appearance has been represented or corrected. A reviewed condition/legibility inventory, source/crop checks and genuinely varied training examples are still needed. No row is added/removed, no threshold is fitted, no transform is changed and no training recommendation is selected from these measurements.

## Evidence and verification

Artifact folder: `.artifacts/experiments/p46-evening-appearance-audit/`:

- `method.json`: method declared before new input analysis, SHA-256 `11e32da7e1ded272209d6b5143921627fad86dc058a30a89ff08b4207cc9c4de`.
- `metrics.py`: read-only float-file/hash/metadata analysis; no image preprocessing or training code.
- `baseline-old24.json` and `appearance-results.json`: all per-line values and page/leaf/source-group aggregates, with runtime/precision/input receipts.
- `validation.json`: all 51 hash/metadata checks, exact old24 record parity and independent standard-library calculation over all **102 line/region pairs**. Maximum numerical difference is approximately `1.11e-16`, below the declared `1e-7` comparison tolerance; threshold fractions agree exactly.

No source/corpus/model/input files were changed, no new material was opened and no model was trained or evaluated by this audit. Its output is descriptive evidence for interpretation of the separately registered experiment; it is not a new acceptance criterion or causal explanation.
