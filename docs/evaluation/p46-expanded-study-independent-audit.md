<!-- Copyright (c) 2026 QuantForgeSoftware. All rights reserved. -->
# Independent audit of the evening P46 expansion study

Audit date: 2026-11-02, America/Chicago. The audit independently recomputed all **450 main prediction records** and **153 leaf-diagnostic prediction records** from frozen references. Their per-line edits, aggregate CER, recorded training budgets, and checkpoint identities agree with the recorded results. This verifies the accounting; it does not establish transcription correctness.

**The expanded corpus fails the prespecified improvement screen.** Final-checkpoint median development CER moves from **81.2785% to 80.8219%**, an improvement of **0.4566 percentage points**, with two of three paired seed wins. The required median improvement was at least five percentage points as well as at least two paired wins. No model promotion follows from these results. The separately registered held-leaf diagnostic also remains poor: **751 edits / 926 reference scalars = 81.1015% CER**.

## Method and provenance

The read-only audit scripts are `audit.py` and `audit_leaf.py` under `.artifacts/experiments/p46-evening-results-audit/`. Each uses an independent Python dynamic-programming edit-distance implementation after Unicode NFC normalization. Python characters represent Unicode scalars here; every reference, prediction length, total distance, and insertion/deletion/substitution count was compared with the C# output. Equal-cost traceback uses match, substitution, deletion, then insertion, matching the declared scoring convention. CER is summed scalar edits divided by summed reference scalars, rather than mean line CER. Per-line recomputations and consumed-file hash indexes are retained beside the scripts.

The audit verified the main registration preceded the six serial runs, the frozen source snapshots and seven managed binaries, 132 corpus content hashes, all 51 unique shared input rasters, crop hashes, and source/reference identities. The new producer allowlist and independent review allowlist have the same 27 IDs. All accepted-visible-text and human-review fields remain blank or false. Old development page 1440 stays seven lines / 219 reference scalars; no new line enters that development set. The 25-label alphabet is derived from training references, with no development out-of-vocabulary labels.

These are provisional source transcriptions, not specialist-adjudicated visible text. Proposed leaf groups and crop/source associations retain their documented limits. Existing source and image rights records are preserved; this audit makes no model-weight licensing determination. The authoritative runtime provenance is the frozen snapshot and binary hashes. A subsequent worktree resource-loader fix does not retroactively change these fits.

## Main comparison

Both conditions use the registered architecture, preprocessing profile, seeds, batch size four, constant learning rate, and fixed **5,984 optimizer updates / 23,936 sample presentations per run**. Neither condition early-stops. Training losses and history counters are finite and complete, and the common diagnostic interval supplies 32 reports per run.

| Condition | Unique train lines / reference scalars | Uniform whole-list copies | Training instances | Full batches / epoch | Epochs | Presentations per unique line |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Old | 17 / 578 | 4 | 68 | 17 | 352 | 1,408 |
| Expanded | 44 / 1,504 | 1 | 44 | 11 | 544 | 544 |

Copies create training instances, not additional unique lines. Equal update and sample budgets therefore do not give each unique line equal exposure across conditions. Each expanded model contains the original 17 lines and the 27 new lines.

The primary endpoint is the final checkpoint at the fixed update budget. All rows below use the same 219 development reference scalars; positive improvement means fewer expanded-condition edits.

| Seed | Old final edits / CER | Expanded final edits / CER | Improvement, percentage points |
| --- | ---: | ---: | ---: |
| 42 | 173 / 78.9954% | 179 / 81.7352% | −2.7397 |
| 1729 | 179 / 81.7352% | 177 / 80.8219% | +0.9132 |
| 2026 | 178 / 81.2785% | 177 / 80.8219% | +0.4566 |
| Median | 178 / 81.2785% | 177 / 80.8219% | +0.4566 |

The final-checkpoint range is **78.9954–81.7352%** for old and **80.8219–81.7352%** for expanded. The small exposed development set supplies no reliable evidence of a five-point improvement. Final training CER is much lower: old **1.7301–3.1142%**, expanded **6.4495–8.1782%**. Those training percentages use different reference sets and cannot themselves rank generalization.

The secondary endpoint selects a checkpoint using the exposed development set. Its old seed scores are **77.1689%, 78.5388%, 77.1689%**; expanded scores are **79.9087%, 78.0822%, 75.3425%**. Secondary medians are **77.1689% old versus 78.0822% expanded**, so the expanded median is **0.9132 percentage points worse**. The single favorable expanded seed-2026 secondary score must not replace the registered final-checkpoint endpoint. Selection has 352 old versus 544 expanded epoch-validation opportunities, another reason to keep it secondary.

Latest metadata matches the final epoch and update count; selected metadata matches the selected update. Final and selected checkpoint SHA-256 values and packaged selected weight digests agree. Both reload-parity flags are true for every run. The audit verifies recorded parity flags and file identities; it does not rerun native forward passes or retrain a model. The prerequisite four-line training-only control passed at 2/134 scalar edits after 1,655 updates, but that is not the reviewed 17-line gate.

## Separately registered held-leaf diagnostic

The diagnostic registration predates the main comparisons. Execution depends on the completed control and all six main results by hash. The same 51 reference/crop/input identities agree with the main study. Each fold retains the old 17 training lines and excludes all new lines assigned to one proposed physical leaf, keeping siblings together. The alphabet is fixed from the old 17 training lines alone. There is one registered seed, 1729.

| Held proposed leaf | Held lines / scalars | Unique train lines | Copies / training instances | Batches / epochs | Final train CER | Original dev CER | Final held edits / CER |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 83 | 12 / 418 | 32 | 1 / 32 | 8 / 663 | 4.3278% | 80.3653% | 355 / 84.9282% |
| 84 | 10 / 332 | 34 | 2 / 68 | 17 / 312 | 6.9966% | 79.4521% | 259 / 78.0120% |
| 85 | 5 / 176 | 39 | 4 / 156 | 39 / 136 | 5.8735% | 85.3881% | 137 / 77.8409% |

Every fold completes **5,304 updates / 21,216 sample presentations**, for **15,912 updates / 63,648 presentations** in total. Per-unique-line presentations differ: 663, 624, and 544 respectively. The held pooled CER is **751/926 = 81.1015%**; the unweighted mean of the three leaf CERs is **80.2604%**. The pooled score comprises 27 unique lines predicted by three different models. Repeated evaluation of the original seven development lines across the three models does not create 21 independent development lines.

Frozen harness ordering confirms only the original exposed development set is evaluated during fitting. Held-new loss/CER is first calculated after the fixed final update count, and held-new lines are never scored at the secondary selected checkpoint. Latest checkpoint hashes, metadata counters, and recorded reload-parity flags agree for all three folds. All 153 partitioned per-line records independently match their references and C# scalar edit counts.

“Held” means excluded from optimizer training: the source pages had already been inspected, and all input content was decoded, prepared, and checked against the alphabet before fitting. This is not a blind unseen-content evaluation. Folds differ in training content, size, per-line exposure, and 663/312/136 original-dev validation opportunities. They do not isolate a single physical cause. High held-leaf error broadens the evidence of a generalization problem within this witness; it does not identify faded ink as the cause or show that appearance, provisional labels, geometry, content, and model capacity have been separated.

## Resource accounting

Darwin `/usr/bin/time -l` maximum resident set size is **bytes**, converted here using 1 MiB = 1,048,576 bytes. The runtime peak-working-set API reports zero; that value means unavailable, not zero memory use.

| Main run | Whole-process seconds | Training timer seconds | Maximum RSS, MiB |
| --- | ---: | ---: | ---: |
| Old 42 | 46.89 | 46.1302 | 212.7031 |
| Expanded 42 | 50.46 | 49.7030 | 213.1875 |
| Old 1729 | 46.33 | 45.7823 | 210.1563 |
| Expanded 1729 | 45.44 | 44.7488 | 197.1719 |
| Old 2026 | 39.28 | 38.7741 | 215.0000 |
| Expanded 2026 | 42.45 | 41.9014 | 222.0938 |

Held-leaf folds 83/84/85 use **37.49/36.18/35.38 whole-process seconds** and peak at **208.2344/216.8125/212.7188 MiB**. These are fresh whole-process peaks, not steady-state memory or training-only memory. Whole-process timing includes preparation, hashing, baseline scoring, fitting with validation, final/secondary scoring, reload checks, and main-study packaging. Different epoch-validation frequencies contribute different work despite identical update budgets. Single runs on one machine do not establish a speed or memory improvement; user CPU time may exceed wall time because the runs use two threads.

## Evidence and remaining limits

Machine-readable audit outputs are `.artifacts/experiments/p46-evening-results-audit/audit-results.json`, `per-line-recomputed.json`, `consumed-file-index.json`, and their `leaf-` counterparts. `audit-receipt.json` records their hashes and the audit document hash. The main study and diagnostic frozen folders were read only; no training, model scoring, image changes, source changes, or experiment-file edits were performed by this audit.

The result remains a small, source-exposed, provisional-label study of one Koine manuscript witness. The separate appearance audit leaves faded-condition coverage unestablished. These results do not establish performance on a new hand, Classical Greek, general papyri, or inscriptions. Specialist review and an independently reviewed, locked evaluation set remain necessary before release or accuracy claims.
