# Decoding-strategy and selective-prediction measurement — P01 dev split

- Owner: **Instance B (ML/EVAL)**; contract set **C0-0.4-draft**; baseline `main` @ `0bccf6b`.
- Reproduce: `dotnet test tests/LeviathanText.ML.Tests --filter FullyQualifiedName~P01DecodingComparisonTests` (skipped when the P01 artifact store is absent). Artifact written to `.artifacts/pilot/p01-decoding-comparison.txt`.
- Decision input for todo §5: “decoding strategy is chosen by measurement, not assumption”, and acceptance-plan.md §3 (AI-assisted / uncertainty rows).

## Measured (2026-10-19, macOS arm64 CPU, pilot machine)

Training: P01 v0.9, train 176 / dev 50, 300 epochs, seed 42, train loss 0.084 → 0,
best dev CER 0.835, label vocabulary 45. **The locked test split was not used.**

Latest run:

| Decoder | Dev scalar CER | Perfect lines | Seconds/line |
| --- | --- | --- | --- |
| greedy (default) | 0.8406 | 0/50 | 0.0001 |
| beam-2 | 0.8406 | 0/50 | 0.0007 |
| beam-4 | 0.8396 | 0/50 | 0.0007 |
| **beam-8** | **0.8386** | 0/50 | 0.0012 |

**Run-to-run variation is about ±0.01 CER** (three runs of this configuration measured
greedy 0.8466 / 0.8367 / 0.8406 and beam-4 0.8446 / 0.8337 / 0.8396). Beam width does not
vary systematically; the gain is at or below run-to-run variation.

### Error analysis (greedy, dev, top 5)

| Class | Count | of which marks |
| --- | --- | --- |
| Substitutions | 140 | 0 |
| Insertions | 0 | 0 |
| Deletions | 706 | 2 |

Top substitutions: `α→ι` ×5, `ϲ→ι` ×5, `α→ο` ×4, `ε→α` ×4, `κ→χ` ×4.
Top deletions: `α` ×63, `ο` ×60, `ε` ×57, `ν` ×55, `τ` ×51.

The model **systematically drops characters** (706 deletions, 0 insertions) rather than
hallucinating: an under-trained CRNN collapses frames. That is a training/decoding-volume
failure mode, not an accent-handling failure (only 2 mark deletions, 0 mark substitutions).

### Selective prediction (greedy, min per-grapheme line confidence)

| Entry | Value |
| --- | --- |
| Full-coverage dev CER | 0.8406 |
| Area under the risk–coverage curve (rectangle rule; lower is better) | 0.8632 |
| Text coverage with CER ≤ 0.80 | 0 |
| Text coverage with CER ≤ 0.75 | 0 |

AURC is slightly **above** full-coverage CER: at this data scale the raw per-grapheme
confidence barely orders lines by correctness, so abstaining on low confidence buys
almost no CER reduction. This is a reason to keep uncertainty as an explicit annotation
concept rather than trusting model confidence.

### Sequence-length headroom (greedy)

| Entry | Value |
| --- | --- |
| CTC time steps (T = width/4) | min 4, median 8 |
| Reference scalars per dev line | min 7, median 18 |
| Lines where T < reference scalars (full label sequence unreachable) | **50/50** |
| Resized line-image width | min 16 px, median 35 px |
| Unreachable lines if the width downsample were 2 | 20/50 |

Every dev line has fewer CTC frames than reference scalars, so the decoder is
**structurally unable** to emit the full line; the deletion-dominated error class follows
directly. A 35 px median width for a line with 18 scalars means the line *crops* are
near-square rather than long and thin: the aspect-preserving resize to model height 16
then yields too few columns. The likely root cause is line-region geometry/extraction
(imaging package), not the CTC decoder; reducing the CRNN width downsample from 4 to 2
would remove the structural limit on 30/50 lines and is the ML-side fallback.

## Decision (pilot-scale, reversible)

- **Keep greedy as the default decoder.** Beam mode stays available and tested
  (`CtcDecodeMode.PrefixBeam`); re-run after the annotation-volume gate and on the other
  two tracks before any change.
- **Report the sequence-length limit to IMG as a blocker** before any accuracy claim;
  fix crop geometry first, then re-measure. A model-shape change (width downsample 4 → 2)
  is the ML fallback and is being measured separately.
- Do **not** ship confidence-based abstention as a quality claim. Use it only as a
  review-ordering hint until calibration is demonstrated on adjudicated data.
- Per-grapheme confidence under beam mode uses a forced best alignment
  (`CtcBeamDecoder.BestAlignment`), so values remain real per-label probabilities.

## Honest limits

- Labels are Transkribus-prepared and **not expert-adjudicated** (DATA-03 pending).
- One documentary hand; nothing here generalizes to manuscripts or inscriptions.
- 0.83–0.85 dev CER is a **baseline** from a data-limited model that has memorized its
  training set (train loss 0). No release claim follows.
- The comparison is dev-only; the one-shot locked-test result in
  [pilot-p01-report.md](pilot-p01-report.md) is unaffected and its reuse rules stand.
- Calibration/ECE scoring is implemented and unit-tested
  (`SelectivePredictionScorer.Calibration`) but not yet run on adjudicated per-grapheme
  labels.
