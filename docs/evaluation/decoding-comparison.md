# P01 decoding, confidence and error measurement (post IMG crop fix)

- Owner: **Instance B (ML/EVAL)**; contract set **C0-0.4-draft**; measured at `main` @ `51eb654`.
- Reproduce:
  - `dotnet test tests/LeviathanText.ML.Tests --filter FullyQualifiedName~P01DecodingComparisonTests`
  - `dotnet test tests/LeviathanText.ML.Tests --filter FullyQualifiedName~P01WidthDownsampleTests`
  - `dotnet test tests/LeviathanText.ML.Tests --filter FullyQualifiedName~P01InputHeightTests`
  - `dotnet test tests/LeviathanText.ML.Tests --filter FullyQualifiedName~P01AugmentationTests`
  - skipped automatically when the P01 artifact store is absent.
- Artifacts: `.artifacts/pilot/p01-*.txt` and `.artifacts/pilot/p01-decode-run-manifest.json`.
- Decision input for todo §5 (“decoding strategy chosen by measurement”) and
  acceptance-plan.md §3 (uncertainty, AI-assisted rows).

All CERs below are for the **selected best checkpoint** (`trainer.RestoreBest()`), not the
last epoch. Dev-only; the locked test split is untouched.

## What changed since the first measurements

The first run found **50/50 dev lines with fewer CTC frames than reference scalars** and a
deletion-dominated error profile. Root cause: the mandatory vertical line padding doubled
crop height, so height-16 normalization left an ~8 px text band and halved the width.
That finding was handed to IMG; A fixed extraction in `0c6c768` (padding fraction 0.5 → 0.25,
capped at 12 px). The tables below are **post-fix**; the earlier h32 recommendation was a
workaround for the pre-fix crops and is superseded.

Same split and data throughout; the run manifest split hash
`f36aaa2ecd742b5c037e2e5ef63c92810abf2ce906b5f1e19953ddf6272297c6` matches the one-shot
[pilot-p01-report.md](pilot-p01-report.md).

## Decoding (representative run)

| Decoder | Dev scalar CER | Perfect lines | Seconds/line |
| --- | --- | --- | --- |
| greedy (default) | 0.7968 | 0/50 | 0.0002 |
| beam-2 | 0.7958 | 0/50 | 0.0011 |
| beam-4 | 0.7998 | 0/50 | 0.0014 |
| beam-8 | 0.7928 | 0/50 | 0.0022 |

Beam widths move CER by less than run-to-run variation at ~6–10× decode cost.
**Greedy stays the default**; beam remains opt-in and tested.

## Error profile (greedy)

| Class | Count | of which marks |
| --- | --- | --- |
| Substitutions | 397 | 0 |
| Insertions | 7 | 0 |
| Deletions | 398 | 2 |

Top substitutions: `α→ο` ×9, `ο→α` ×8, `α→ε` ×7, `ι→ε` ×6, `ο→ε` ×6. The profile is now
**balanced** rather than deletion-dominated — the model is confusing letters with letters,
which is the behaviour expected of a data-limited recognizer. This is the strongest
evidence that the crop fix (not a decoder change) addressed the structural failure.

## Confidence (greedy)

| Entry | Value |
| --- | --- |
| Produced-grapheme samples | 615 (deleted reference graphemes have no produced value) |
| Expected calibration error (10 bins) | 0.151 |
| Maximum calibration error | 0.552 |
| AURC (line risk–coverage, rectangle rule) | 0.803 |
| Full-coverage dev CER | 0.797 |

Calibration improved from ECE 0.237 pre-fix, but is still not good enough to present
confidence as certainty or to justify abstention claims. Line-level AURC is roughly equal
to full-coverage CER.

## Architecture / augmentation A/B (all post-fix, best checkpoint)

| Configuration | Dev scalar CER | Reachable lines (T ≥ scalars) | Seconds/line |
| --- | --- | --- | --- |
| **height 16, width 4 (defaults)** | **0.799–0.805** | 7/50 | 0.0002 |
| height 16, width 2 | 0.838 | 50/50 | 0.0004 |
| height 32, width 4 | 0.827 | 50/50 | 0.0005 |
| height 32, width 2 | 0.861 | 50/50 | 0.0007 |
| **height 16, width 4 + augmentation** | **0.783** | 7/50 | 0.0002 |
| height 32, width 4 + augmentation | 0.801 | 50/50 | 0.0005 |

Augmentation (training-only: horizontal stretch ±10%, vertical shift ±1 px, 1%
salt-and-pepper) improved dev CER in three independent pre/post-fix runs; the post-fix gain
is 0.805 → 0.783. Extra time resolution (height 32 or width 2) is **worse** at this data
volume: it does not fix the remaining errors once crop geometry is sane.

## Regularization A/B (height 16, width 4, augmentation on)

| Weight decay | Dev scalar CER |
| --- | --- |
| **0 (default)** | **0.7908** |
| 1e-4 | 0.8018 |
| 1e-3 | 0.8227 |

Weight decay hurts monotonically at this data volume; keep it **0**. The knob exists
(`TrainingConfig.WeightDecay`) and is recorded in the run manifest for future data scales.

## Dropout A/B (height 16, width 4, augmentation on)

| Dropout | Dev scalar CER |
| --- | --- |
| **0 (default)** | **0.7908** |
| 0.1 | 0.8068 |
| 0.2 | 0.8008 |

Dropout on the LSTM output does not help at this data volume; keep it **0**. The
configuration exists (`CrnnModel(..., dropout:)`) and is recorded in the run manifest.

## Learning-rate A/B (height 16, width 4, augmentation on)

| Learning rate | Dev scalar CER |
| --- | --- |
| 1e-3 | 0.8028 |
| **3e-3 (default)** | **0.7829** |
| 5e-3 | 0.7898 |

3e-3 remains the best measured rate; no change.

## Decision (pilot-scale, reversible)

- Keep **greedy** as the default decoder; keep `AugmentTraining` **off by default** so
  existing runs/checkpoints remain comparable.
- Best measured next-candidate configuration: **height 16, width 4, augmentation on,
  weight decay 0, dropout 0**. The measured run is packaged under
  `.artifacts/pilot/model-p01-h16-aug/` (weights, metadata/label set, run manifest, model
  card; dev CER 0.783). The package is an evaluation artifact, not a release model.
  A future frozen candidate must use fresh held-out witnesses (acceptance-plan.md §2).
- Do **not** claim calibrated confidence or use confidence-based abstention from this data.
- The earlier height-32 recommendation is withdrawn for now; re-test it after the
  annotation volume grows.

## Honest limits

- Labels are Transkribus-prepared and **not expert-adjudicated** (DATA-03 pending).
- One documentary hand; nothing here generalizes to manuscripts or inscriptions.
- 176 training lines; 0.78–0.80 dev CER is a **baseline**, not a threshold.
- The comparison is dev-only; the locked-test reuse rules in
  [pilot-p01-report.md](pilot-p01-report.md) stand unchanged.
