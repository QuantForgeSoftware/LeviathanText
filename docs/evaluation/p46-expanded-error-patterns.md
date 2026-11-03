<!-- Copyright (c) 2026 QuantForgeSoftware. All rights reserved. -->
# Closed-checkpoint P46 error patterns — 2026-11-02

The saved final predictions are consistently shorter than their provisional source references, with many substitutions as well as deletions. **None of the 69 evaluated prediction records is empty, and none is an exact full-line match.** These descriptions do not identify a cause, change a model-selection decision, or demonstrate new Greek text coverage. The prespecified main final-checkpoint improvement screen remains failed, as recorded in the [independent study audit](/Users/johnbaima/Code/LeviathanText/docs/evaluation/p46-expanded-study-independent-audit.md).

## Fixed scope and scoring

This audit reads only six main **final-checkpoint development** outputs and the three diagnostic **final-checkpoint newly held leaf** outputs. It excludes selected checkpoints, training predictions, and the original development outputs from the leaf models. It recomputes **69 line-evaluation records representing 34 unique source lines**:

- Main comparison: the **same seven page-1440 lines / 219 reference scalars** evaluated by each of six models. The 42 evaluations are not 42 independent source lines. Each condition's three-seed confusion pool repeats those seven references three times, giving 657 reference-scalar positions.
- Held-leaf diagnostic: **27 distinct new lines / 926 reference scalars**, partitioned across three different models with seed 1729. Each proposed leaf's siblings are held together. The pooled 27-line result is not a single model or a repeated-seed estimate.

Scoring uses NFC Unicode scalars without case folding, sigma substitution, accent stripping, or additional whitespace changes. Minimum-edit traceback uses the existing deterministic order **match, substitute, delete, insert**. Every recomputed per-line and aggregate I/D/S count matches the corresponding record in the prior independent 603-record audit. The [method](/Users/johnbaima/Code/LeviathanText/.artifacts/experiments/p46-evening-error-patterns/method.json), [per-line strings and alignments](/Users/johnbaima/Code/LeviathanText/.artifacts/experiments/p46-evening-error-patterns/per-line.json), and [full results](/Users/johnbaima/Code/LeviathanText/.artifacts/experiments/p46-evening-error-patterns/results.json) preserve all denominators, per-seed/per-surface summaries, scalar frequencies, and confusion counts.

References remain unadjudicated source transcriptions, not independently accepted visible text. “Blank line” below means an empty decoded string; no CTC blank-frame probability is measured by this string audit.

## Final-checkpoint totals

R and P are NFC reference and prediction scalar counts. I, D, S are aligned insertions, deletions, substitutions. P/R measures decoded length only.

| Model | Lines | R | P | I | D | S | CER | P/R |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Old 42 | 7 | 219 | 169 | 2 | 52 | 119 | 78.9954% | 77.17% |
| Old 1729 | 7 | 219 | 175 | 4 | 48 | 127 | 81.7352% | 79.91% |
| Old 2026 | 7 | 219 | 173 | 3 | 49 | 126 | 81.2785% | 79.00% |
| Expanded 42 | 7 | 219 | 133 | 1 | 87 | 91 | 81.7352% | 60.73% |
| Expanded 1729 | 7 | 219 | 162 | 1 | 58 | 118 | 80.8219% | 73.97% |
| Expanded 2026 | 7 | 219 | 154 | 1 | 66 | 110 | 80.8219% | 70.32% |
| Held leaf 83, seed 1729 | 12 | 418 | 329 | 7 | 96 | 252 | 84.9282% | 78.71% |
| Held leaf 84, seed 1729 | 10 | 332 | 199 | 1 | 134 | 124 | 78.0120% | 59.94% |
| Held leaf 85, seed 1729 | 5 | 176 | 129 | 2 | 49 | 86 | 77.8409% | 73.30% |

Every row has zero blank prediction lines and zero exact lines. The length identity **P = R − D + I** holds for every line and group. Each expanded main seed emits fewer scalars and has more aligned deletions than its paired old seed. Across the three repeated-reference main evaluations, old has 149 deletions / 372 substitutions / 9 insertions; expanded has 211 / 319 / 3. This descriptive substitution/deletion tradeoff does not replace the prespecified median final-CER endpoint.

The held pool has **10 insertions, 279 deletions, 462 substitutions**, totaling **751/926 = 81.1015% CER**, with P/R **657/926 = 70.95%**. Both substitutions and shortening contribute; decoded strings alone do not distinguish segmentation, appearance, label, alignment, architecture, or fitting explanations.

## Held surfaces remain separate

All rows use the registered seed-1729 model that holds the listed proposed leaf out of training. These six surfaces do not provide six independently trained models.

| Surface / held leaf | Lines | R | P | I | D | S | CER | P/R |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1470 / 83 | 8 | 293 | 213 | 1 | 81 | 158 | 81.9113% | 72.70% |
| 1480 / 83 | 4 | 125 | 116 | 6 | 15 | 94 | 92.0000% | 92.80% |
| 1490 / 84 | 4 | 146 | 83 | 1 | 64 | 49 | 78.0822% | 56.85% |
| 1500 / 84 | 6 | 186 | 116 | 0 | 70 | 75 | 77.9570% | 62.37% |
| 1510 / 85 | 3 | 109 | 75 | 1 | 35 | 51 | 79.8165% | 68.81% |
| 1520 / 85 | 2 | 67 | 54 | 1 | 14 | 35 | 74.6269% | 80.60% |

All six surfaces have zero blank and zero exact lines. Page 1480 illustrates why length alone is insufficient: four predictions total 116/125 reference scalars, yet incur 115 edits, predominantly substitutions. Its four-line denominator is too small to support a broader page, hand, or appearance claim.

## Scalar distributions and exploratory alignments

Concentration is calculated separately for each observed reference/prediction distribution. “Top five” means the five most frequent scalars in that distribution, not a common fixed five-label set. The machine-readable results also retain full per-surface distributions, each scalar's occurrence denominator, and HHI (sum of squared scalar shares). No concentration threshold was selected.

| Model | Reference distinct scalars / top-five share | Prediction distinct scalars / top-five share | Most frequent predicted scalar |
| --- | ---: | ---: | --- |
| Old 42 | 21 / 47.03% | 20 / 61.54% | ο, 31/169 = 18.34% |
| Old 1729 | 21 / 47.03% | 21 / 64.57% | ε, 39/175 = 22.29% |
| Old 2026 | 21 / 47.03% | 19 / 65.32% | ο, 28/173 = 16.18% |
| Expanded 42 | 21 / 47.03% | 18 / 57.14% | ι, 24/133 = 18.05% |
| Expanded 1729 | 21 / 47.03% | 21 / 63.58% | ι, 31/162 = 19.14% |
| Expanded 2026 | 21 / 47.03% | 19 / 60.39% | ι, 27/154 = 17.53% |
| Held 83 | 24 / 51.20% | 20 / 62.92% | ε, 59/329 = 17.93% |
| Held 84 | 22 / 49.40% | 20 / 58.29% | α, 41/199 = 20.60% |
| Held 85 | 21 / 51.70% | 16 / 58.14% | ε, 19/129 = 14.73% |

Predicted distributions put more mass on their five most frequent scalars in all nine model summaries. They still contain 16–21 distinct scalars, and the most frequent label varies. These counts do not demonstrate a single-label output collapse or establish why a distribution differs from its source reference.

Some common **aligned substitutions** are listed below. The parenthetic denominator is the number of occurrences of the reference scalar in that pool, including matches and other errors; it is not the total number of substitutions.

| Descriptive pool | Common scalar pairs, aligned count / reference-scalar occurrences |
| --- | --- |
| Old main, same references repeated over three seeds | ε→ο 9/72; ι→ο 7/42; ρ→ε 7/36 |
| Expanded main, same references repeated over three seeds | ν→ι 13/66; ε→ο 9/72; ε→ι 7/72; ο→α 7/66 |
| New held lines, three different models | π→ε 9/30; τ→ε 9/63; α→υ 7/108; α→ω 7/108; ε→α 7/100; ε→ο 7/100 |

Common aligned deletions are ο 17, υ 15, and ε/π 12 each in the old main pool; υ 20, ο 19, and α 18 in the expanded main pool; and ι 32, α 30, and τ 22 in the new held pool. Full frequency and reference-occurrence denominators remain in the results JSON. High-frequency reference scalars have more opportunities for errors. These are deterministic minimum-edit alignments of complete strings: alternative equal-cost paths can move substitutions and deletions. They are not independently localized glyph-classifier confusions or evidence for changing Greek spellings, sigma conventions, or labels.

## Future checks to register after review

The following are a proposed review-and-registration sequence, **not claims that new experiments have been registered or executed**. Preserve the closed results and make the complete future protocol reviewable before fitting:

1. Obtain specialist decisions on visible reference text and source/line association for the existing development and held examples. Freeze accepted revisions, uncertainty/exclusion rules, source/crop hashes, physical-object grouping, and train-only alphabet/OOV handling. Re-score all affected closed examples under the reviewed reference version as a separately named sensitivity report; retain the provisional scores.
2. Establish independently reviewed evaluation lines outside the repeatedly exposed seven-line development set. Keep sibling surfaces and derivatives in the same physical-object group. Register which data are exposed and which remain locked, and freeze the baseline/candidate, seeds, preprocessing, update/sample budgets, checkpoint endpoint, and acceptance rule before another fit.
3. Require the next registered comparison to report every fixed seed and surface with R/P, I/D/S, blank/exact lines, label counts, and scalar distributions, together with valid-frame CTC blank diagnostics from the existing instrumentation. String shortening and blank-frame behavior must remain separate observations; choose no new threshold from this audit.
4. If appearance is the proposed factor, first have reviewers define usable appearance strata independently of model errors and document reference/geometry confidence. Register a matched one-factor comparison and its quality/resource checks before execution. The present scalar counts and earlier intensity proxies cannot identify faintness as the cause, establish faded-condition coverage, or separate it from content and source-label uncertainty.

No inference, fitting, image inspection, crop/source/label changes, production edits, or new selection occurred. All inputs and the analysis script are hashed in the separate [audit receipt](/Users/johnbaima/Code/LeviathanText/.artifacts/experiments/p46-evening-error-patterns/audit-receipt.json). The material remains a small Koine P46 study; this audit supplies no Classical Greek, new-hand, general papyrus, or inscription coverage evidence.
