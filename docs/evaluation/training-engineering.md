# Training diagnostics, modes and padding — 2026-11-02

Implemented the owner's authorized engineering subset of P46 D2: training diagnostics, explicit training/evaluation modes and safe padding, with regression tests. This work uses synthetic fixtures and saved-checkpoint inference; it does not adjudicate manuscript labels, retrain P46 or establish better recognition accuracy.

## Changes

- `CtcTrainer.TrainEpoch` temporarily enables training mode. Evaluation and `TorchLineRecognizer` temporarily enable evaluation mode. Each restores the caller's original mode in `finally`, including error paths. This fixes dropout-sensitive recognition/evaluation and training resumed from an evaluation-mode model.
- `LineImage.BackgroundValue` explicitly declares fill outside the image. The existing three-argument constructor and its default zero background remain available; a four-argument constructor accepts finite values in `[0,1]`. JSON round trips preserve the value, and old JSON without it defaults to zero.
- `LineBatcher` fills only each sample's padded columns using its background; valid pixels and CTC lengths stay unchanged. `LineAugmenter` fills exposed rows the same way and preserves the declaration. Existing ink-positive P01/synthetic/application paths remain at zero; the shared luminance transform declares white (`1`) without inverting any pixels.
- `EvaluateDetailed` reports NFC scalar edit counts, CER, reference/prediction lengths and ratio, valid-frame blank posterior/argmax statistics, and deterministic input-order reference/prediction samples. Blank totals exclude padding. Empty-reference CER agrees with `RecognitionScorer`: empty/empty is zero; nonempty output against an empty reference is one.
- Evaluation decodes the same bounded minibatch logits used for loss and blank statistics, using `CtcOutputDecoder`. It no longer performs an additional full-dataset recognition forward. Greedy/beam decoding and confidence semantics are shared with ordinary recognition.
- `DiagnosticsEveryNEpochs` defaults to zero, disabling scheduled diagnostics. When enabled, `Fit` samples every N absolute completed epochs and the final epoch, including early stopping. It evaluates unaugmented training inputs only at these intervals. `DiagnosticsSampleCount` is bounded to 0–100; zero omits strings. Global optimizer steps and optional training/validation evaluations persist in history and model manifests. Legacy history remains readable.

Telemetry settings do not change the model-meaning config hash; manifests and CLI run config record their values separately. Tests confirm enabling telemetry does not change dropout training losses or checkpoint selection for the same seeded control.

## Usage

```sh
leviathan --store pilot.db train lines /abs/page-data --out ./packages/model-a --epochs 120 --diagnostics-every 10 --diagnostics-samples 3
```

The existing CLI positional order for `--out` and `--epochs` is retained. Optional diagnostic flags are validated before dataset loading. Inspect `History[].TrainingEvaluation`, `ValidationEvaluation` and `GlobalStep` in `run-manifest.json`. Prediction samples contain source strings; request zero samples when only aggregate measurements should be recorded. The CLI options do not change PAGE splitting or implement the P46-specific crop transform.

C# callers can use `TrainingConfig.DiagnosticsEveryNEpochs`, `DiagnosticsSampleCount` and `CtcTrainer.EvaluateDetailed`. Diagnostic CER and error counts use canonical-equivalent Unicode scalars, not UTF-16 code units or NFD mark attribution. Sample strings retain their original encoding.

## Regression evidence

Before fixing behavior, tests reproduced dropout-dependent recognition (greedy and beam), dropout-dependent trainer evaluation, incorrect resumed training mode, wrong white/gray padding and shift borders, and JSON constructor ambiguity introduced by the additive constructor. Trainer before-fix results are retained at `.artifacts/test-results/d2-engineering/training-mode-before.trx`; other before-fix failures were observed in focused runs before their corresponding fixes.

Final checks:

| Check | Result |
| --- | --- |
| ML suite excluding the long real-data `P01*` matrices | **125 passed**, zero failed/skipped |
| CLI diagnostics + existing image command tests | **17 passed**, zero failed/skipped |
| Application training-data export and streaming checks | **13 passed**, zero failed/skipped |
| Entire solution build, `-m:1 --no-restore -p:UseSharedCompilation=false` | **0 warnings, 0 errors** |
| Saved P46 v2 input/prediction compatibility | **24/24 exact decoded strings match** |
| Saved P46 v2 receipt and weights | unchanged |

The first unconstrained parallel solution build reported premature MSBuild worker exits; the serial build passed. No code defect was inferred from that infrastructure failure. The full long-running real-data experiment matrices were not rerun, and no throughput or general accuracy improvement is claimed.

Tests cover both background conventions and a gray background, source immutability, both shift directions, padding masks, decomposed Greek confidence spans, empty inputs, caller mode restoration after failure, resumed optimizer updates, disabled logging, scheduled/final/early-stop cadence, fixed samples and old/new manifest serialization. Relevant tests: [training diagnostics](../../tests/LeviathanText.ML.Tests/TrainingDiagnosticsTests.cs), [training modes](../../tests/LeviathanText.ML.Tests/TrainingModeTests.cs), [recognition modes](../../tests/LeviathanText.ML.Tests/RecognitionModeTests.cs), [padding](../../tests/LeviathanText.ML.Tests/SafePaddingTests.cs), [shared decoder](../../tests/LeviathanText.ML.Tests/CtcOutputDecoderTests.cs) and [CLI](../../tests/LeviathanText.Cli.Tests/TrainingDiagnosticsCommandTests.cs).

Final TRX artifacts are under `.artifacts/test-results/d2-engineering/`: `ml-final.trx`, `cli-final.trx` and `application-regression.trx`. The separate `.artifacts/experiments/p46-d2-verification/` C# runner reconstructs inputs from retained crops and uses the new detailed minibatch evaluator. Its `results.json` records train **67/578 = 11.59% CER** and development **162/219 = 73.97% CER**, with every saved prediction unchanged. The inference check preserves the loaded model's initial evaluation mode; it does not change weights or accepted text.

## Remaining boundary

This completed the requested diagnostics/mode/padding work. The broader D2 preparation task was subsequently implemented through [the shared preprocessing contract](preprocessing-contract.md): new declared profiles unify training/inference, while legacy packages retain their original defaults. Neither these fixes nor their synthetic tests replace specialist review, corpus expansion, the reviewed training-only fit control, or the next frozen comparison in [the plan](../../project-plan.md#p46-next-run-protocol--2026-11-02).
