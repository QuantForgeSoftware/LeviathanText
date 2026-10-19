# Decoding, confidence and error measurement — P01 dev split

- Owner: **Instance B (ML/EVAL)**; contract set **C0-0.4-draft**; baseline `main` @ `0842cec`.
- Reproduce:
  - `dotnet test tests/LeviathanText.ML.Tests --filter FullyQualifiedName~P01DecodingComparisonTests`
  - `dotnet test tests/LeviathanText.ML.Tests --filter FullyQualifiedName~P01WidthDownsampleTests`
  - skipped automatically when the P01 artifact store is absent.
- Artifacts: `.artifacts/pilot/p01-decoding-comparison.txt`, `.artifacts/pilot/p01-width-downsample.txt`,
  `.artifacts/pilot/p01-decode-run-manifest.json`.
- Decision input for todo §5 (“decoding strategy chosen by measurement”) and
  acceptance-plan.md §3 (uncertainty, AI-assisted rows).

## Runs and reproducibility

Config: P01 v0.9, train 176 / dev 50 / **test 95 locked (not used here)**, 300 epochs,
seed 42, hidden 64, vocabulary 45. The run manifest records architecture
(`widthDownsampleFactor`), config hash, label-set hash, split hash, best-checkpoint
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
unable to emit the full line, which is exactly the deletion-dominated profile above. A
35 px median width for an 18-scalar line means the *crops* are near-square; the likely
root cause is line-region geometry/extraction (imaging package), not the CTC decoder.

## ML fallback: width-downsample A/B (measured)

| Width downsample | Dev scalar CER | Reachable lines (T ≥ scalars) | Min T | Median T | Seconds/line |
| --- | --- | --- | --- | --- | --- |
| 4 (default) | 0.8496 | 0/50 | 4 | 8 | 0.0001 |
| **2 (candidate)** | **0.8257** | **30/50** | 8 | 17 | 0.0002 |

Factor 2 removes the structural limit on 30/50 dev lines and improves dev CER by ≈0.024
absolute. The architecture is configurable (`CrnnModel(numClasses, hiddenSize,
widthDownsampleFactor)` with 1/2/4). The **default stays 4** so existing checkpoints and
the one-shot locked-test report remain valid; factor 2 is recommended for the next frozen
candidate, which must use fresh held-out witnesses (acceptance-plan.md §2 reuse rule).

## Decision (pilot-scale, reversible)

- Keep **greedy** as the default decoder; beam mode stays available and tested
  (`CtcDecodeMode.PrefixBeam`). Re-run after the annotation-volume gate and on the other
  two tracks before changing.
- Do **not** ship confidence-based abstention or a calibration claim from this data.
- **Report the sequence-length limit to IMG as a blocker**: fix P01 crop geometry first,
  then re-measure; the factor-2 model shape is the ML fallback.
- Per-grapheme confidence under beam mode uses a forced best alignment
  (`CtcBeamDecoder.BestAlignment`), so values remain real per-label probabilities.

## Honest limits

- Labels are Transkribus-prepared and **not expert-adjudicated** (DATA-03 pending).
- One documentary hand; nothing here generalizes to manuscripts or inscriptions.
- The model has memorized its small training set (train loss 0); 0.82–0.85 dev CER is a
  **baseline**, not a threshold, and no release claim follows.
- The comparison is dev-only; the locked-test reuse rules in
  [pilot-p01-report.md](pilot-p01-report.md) stand unchanged.
