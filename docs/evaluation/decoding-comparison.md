# Decoding-strategy measurement — P01 dev split (greedy vs prefix beam)

- Owner: **Instance B (ML/EVAL)**; contract set **C0-0.4-draft**; baseline `main` @ `ac6479d`.
- Reproduce: `dotnet test tests/LeviathanText.ML.Tests --filter FullyQualifiedName~P01DecodingComparisonTests` (skipped when the P01 artifact store is absent). Artifact written to `.artifacts/pilot/p01-decoding-comparison.txt`.
- Decision input for todo §5: “decoding strategy is chosen by measurement, not assumption”.

## Measured (2026-10-19, macOS arm64 CPU, pilot machine)

Training: P01 v0.9, train 176 / dev 50, 300 epochs, seed 42, train loss 0.084 → 0,
best dev CER 0.827, label vocabulary 45. **The locked test split was not used.**

| Decoder | Dev scalar CER | Perfect lines | Seconds/line |
| --- | --- | --- | --- |
| greedy (default) | 0.8466 | 0/50 | 0.0001 |
| beam-2 | 0.8466 | 0/50 | 0.0007 |
| **beam-4** | **0.8446** | 0/50 | 0.0007 |
| beam-8 | 0.8456 | 0/50 | 0.0012 |

Beam decoding is scored on the *same* alignment-summed objective as greedy
(`CtcBeamDecoder.PrefixBeamSearch`); the improvement is 0.002 CER absolute.

## Decision (pilot-scale, reversible)

- **Keep greedy as the default decoder.** At this data scale the beam gain is within
  noise of the annotation volume (176 training lines), while decode cost rises ~6–12×.
- Beam mode stays available and tested (`CtcDecodeMode.PrefixBeam`); re-run this
  measurement after the annotation-volume gate is met and on the other two tracks.
- Per-grapheme confidence under beam mode uses a forced best alignment of the chosen
  label sequence, so confidence values remain real per-label probabilities, not
  fabricated scores.

## Honest limits

- Labels are Transkribus-prepared and **not expert-adjudicated** (DATA-03 pending).
- One documentary hand; the result does not generalize to manuscripts or inscriptions.
- 0.84 dev CER is a **baseline**, not a threshold; the model is memorizing a small set
  (train loss 0). No release claim follows from this table.
- The comparison is dev-only; the one-shot locked-test result in
  [pilot-p01-report.md](pilot-p01-report.md) is unaffected and its reuse rules stand.
