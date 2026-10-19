# Decoding, confidence and error measurement — P01 dev split

- Owner: **Instance B (ML/EVAL)**; contract set **C0-0.4-draft**; baseline `main` @ `0842cec`.
- Reproduce:
  - `dotnet test tests/LeviathanText.ML.Tests --filter FullyQualifiedName~P01DecodingComparisonTests`
  - `dotnet test tests/LeviathanText.ML.Tests --filter FullyQualifiedName~P01WidthDownsampleTests`
  - `dotnet test tests/LeviathanText.ML.Tests --filter FullyQualifiedName~P01InputHeightTests`
  - skipped automatically when the P01 artifact store is absent.
- Artifacts: `.artifacts/pilot/p01-decoding-comparison.txt`, `.artifacts/pilot/p01-width-downsample.txt`,
  `.artifacts/pilot/p01-decode-run-manifest.json`.
- Decision input for todo §5 (“decoding strategy chosen by measurement”) and
  acceptance-plan.md §3 (uncertainty, AI-assisted rows).

## Runs and reproducibility

Config: P01 v0.9, train 176 / dev 50 / **test 95 locked (not used here)**, 300 epochs,
seed 42, hidden 64, vocabulary 45. The run manifest records architecture
(`widthDownsampleFactor`, `inputHeightPixels`), config hash, label-set hash, split hash, best-checkpoint
SHA-256 and the full 300-epoch learning curve. Its split hash
`f36aaa2ecd742b5c037e2e5ef63c92810abf2ce906b5f1e19953ddf6272297c6` equals the one-shot
[pilot-p01-report.md](pilot-p01-report.md), so both records refer to the same split.

**Run-to-run variation is about ±0.02 dev CER** (this configuration has measured greedy
0.8367–0.8536 across runs). Conclusions below are limited to effects larger than that.

## Decoding (representative run)

| Decoder | Dev scalar CER | Perfect lines | Seconds/line |
| --- | --- | --- | --- |
| greedy (default) | 0.8536 | 0/50 | 0.0001 |
| beam-2 | 0.8536 | 0/50 | 0.0007 |
| beam-4 | 0.8576 | 0/50 | 0.0007 |
| beam-8 | 0.8576 | 0/50 | 0.0012 |

Beam width does not move dev CER beyond run-to-run variation, at ~7–12× decode cost.

## Confidence is not calibrated (representative run)

| Entry | Value |
| --- | --- |
| Produced-grapheme samples | 261 (deleted reference graphemes have no produced value and are excluded) |
| Expected calibration error (10 bins) | 0.237 |
| Maximum calibration error | 0.737 |
| AURC (line risk–coverage, rectangle rule) | 0.875 |
| Full-coverage dev CER | 0.854 |
| Text coverage with CER ≤ 0.80 | 0 |

Raw per-grapheme confidence is **badly miscalibrated**: ECE ≈ 0.24 and the worst bin is
off by ≈ 0.74. Line-level risk–coverage is flat/negative, so abstaining on low confidence
buys almost nothing. Confidence must not be presented as scholarly certainty or used as a
quality claim.

## Error analysis (representative run)

| Class | Count | of which marks |
| --- | --- | --- |
| Substitutions | 114 | 0 |
| Insertions | 0 | 0 |
| Deletions | 745 | 2 |

Top deletions: `α` ×71, `ο` ×70, `ε` ×62, `ν` ×61, `τ` ×52.

## Root cause: sequence-length headroom

| Entry | Value |
| --- | --- |
| CTC time steps (T = width/4) | min 4, median 8 |
| Reference scalars per dev line | min 7, median 18 |
| Lines where T < reference scalars (full label sequence unreachable) | **50/50** |
| Resized line-image width | min 16 px, median 35 px |
| Unreachable lines if the width downsample were 2 | 20/50 |

Every dev line has fewer CTC frames than reference scalars: the decoder is structurally
unable to emit the full line, which is exactly the deletion-dominated profile above.

The padding is a *contract requirement* (marks/interlinear additions), but it doubles the
crop height; at input height 16 the text band is then only ~8 px tall and the
aspect-preserving resize halves the width. So this is a **normalization** problem as much
as a geometry problem: re-cropping at height 32 restores both text rows and width.

## ML fallback A/B: input height and width downsample (measured)

| Input height | Width downsample | Dev scalar CER | Reachable lines | Min T | Median T | Median width | Seconds/line |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 16 (default) | 4 (default) | 0.8576 | 0/50 | 4 | 8 | 35 | 0.0001 |
| **32** | **4** | **0.8297** | 29/50 | 8 | 17 | 71 | 0.0002 |
| 32 | 2 | 0.8675 | 50/50 | 16 | 35 | 71 | 0.0003 |

Height 32 with the original width downsample is the best measured candidate (~0.028 CER
improvement, ~2× decode cost). Height 32 + width 2 reaches every line but is **worse** at
this data volume: too many frames for 176 training lines. Width 2 at height 16 measured
0.8257 in a separate run (within ±0.02 run variation of height 32); height 32 is preferred
because it also restores vertical text resolution.

Architecture is configurable (`CrnnModel(numClasses, hiddenSize, widthDownsampleFactor,
inputHeight)`); the **defaults stay 16/4** so existing checkpoints and the one-shot
locked-test report remain valid. The next frozen candidate should use height 32 with fresh
held-out witnesses (acceptance-plan.md §2 reuse rule).

## Decision (pilot-scale, reversible)

- Keep **greedy** as the default decoder; beam mode stays available and tested
  (`CtcDecodeMode.PrefixBeam`). Re-run after the annotation-volume gate and on the other
  two tracks before changing.
- Do **not** ship confidence-based abstention or a calibration claim from this data.
- **Report the padding/normalization interaction to IMG**: the mandatory vertical padding
  doubles crop height, so height-16 normalization leaves an ~8 px text band. IMG owns the
  padding fraction; ML measures height 32 as the candidate fix. Re-measure after either
  change.
- Next model candidate: **input height 32, width downsample 4**; width 2 at height 16 is
  the alternative. Defaults remain 16/4 for checkpoint/report compatibility.
- Per-grapheme confidence under beam mode uses a forced best alignment
  (`CtcBeamDecoder.BestAlignment`), so values remain real per-label probabilities.

## Honest limits

- Labels are Transkribus-prepared and **not expert-adjudicated** (DATA-03 pending).
- One documentary hand; nothing here generalizes to manuscripts or inscriptions.
- The model has memorized its small training set (train loss 0); 0.82–0.85 dev CER is a
  **baseline**, not a threshold, and no release claim follows.
- The comparison is dev-only; the locked-test reuse rules in
  [pilot-p01-report.md](pilot-p01-report.md) stand unchanged.
