# P46 development-error investigation — 2026-11-02

The high development CER reflects poor generalization and severe under-emission. The strongest explanation to test is learning from an extremely small, visually unrepresentative training sample: 578 reference characters on one physical leaf, while the development leaf has visibly fainter strokes. The observed training/development gap is confirmed; the causal contribution of contrast, geometry, label errors and model design is not yet isolated. More epochs alone are not the next remedy.

This investigation reads the preserved [v2 experiment](p46-provisional-training.md), independently audits its C# path and runs inference against the saved checkpoint. It does not train, change weights, accept specialist decisions or promote a decoder/model.

## Measured failures

| Partition | Reference characters | Output characters | Deletions | Substitutions | Insertions | Greedy CER |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Training | 578 | 514 | 64 | 3 | 0 | 11.59% |
| Development | 219 | 123 | 96 | 66 | 0 | 73.97% |

Development emits only 56.16% of reference length, versus 88.93% on training. Every development line is poor (66.67–80.00% CER); one outlier does not explain the score. Even training fit remains imperfect, almost entirely through deletions. Page 1450 training CER is 15.51% (38/245); page 1460 is 8.71% (29/333). These are reference-label diagnostics, not adjudicated manuscript accuracy.

The learning curves show optimization of training data without comparable development success. Minimum development loss occurs at epoch 114 (2.9805, CER 1.0). At selected epoch 1,164, training evaluation loss is 0.4503 and development loss 4.4723. The final epoch lowers training CER to 9.52% while development CER worsens to 78.08%. Development CER first falls below 1.0 at epoch 666. This supports overfitting and delayed useful decoding; it does not establish a defective CTC implementation.

Only 578 training characters support 25 nonblank classes: examples include ψ once, ξ twice, χ/φ three times each and γ six times. The seven development lines contain seven γ occurrences. All development characters are in the alphabet, but alphabet membership is much weaker than adequate visual/context coverage.

## Inference-only decoder check

Reconstruct exact float inputs from the retained RGB line crops using the original C# transform. Reload the packaged selected checkpoint and first require equality with all 24 saved greedy predictions. Then compare greedy with the existing prefix beam decoder at fixed width 8, using identical logits and no language model or biblical edition.

| Diagnostic | Training | Development |
| --- | ---: | ---: |
| Greedy edit count / CER | 67 / 11.59% | 162 / 73.97% |
| Beam-8 edit count / CER | 56 / 9.69% | 163 / 74.43% |
| Greedy argmax-blank frame fraction | 82.10% | 89.56% |
| Mean blank posterior | 79.72% | 84.89% |

Beam decoding reduces development deletions from 96 to 92 but increases substitutions from 66 to 71. It does not rescue recognition; retain greedy as the documented baseline. This rules out a simple greedy-to-beam replacement as the current remedy, not every possible decoder. CTC includes a blank class and sums multiple alignments; a high blank fraction is not itself an error. Compare blank statistics with emitted length and edit types rather than targeting zero blanks. See the [original CTC paper](https://www.cs.toronto.edu/~graves/icml_2006.pdf).

## Appearance and geometry

Visual inspection of retained crops and actual network inputs shows fainter/thinner strokes on development page 1440 than on training pages 1450/1460. Descriptive image statistics support this difference:

| Page | Mean fifth-percentile luminance | Fraction below luminance 0.22 | Median minus fifth percentile |
| --- | ---: | ---: | ---: |
| 1440 development | 0.1641 | 9.34% | 0.2403 |
| 1450 training | 0.1176 | 11.73% | 0.2627 |
| 1460 training | 0.1165 | 12.11% | 0.2702 |

These measurements use the quantized grayscale previews, all 32 rows and the same interior columns 140–779, avoiding the outer field. They are intensity proxies, not segmented foreground or proof that contrast caused the error. A matched normalization experiment and more faded training examples are needed to test that hypothesis. Per-line global Otsu had already failed visual QA and was not used during training; do not reintroduce it as an assumed fix.

Geometry and labels remain provisional. Broad common horizontal bounds include nontext margins; resampling reduces faint strokes, and damaged edges can omit ink. Nevertheless, no systematic wrong-row association or clipping across all seven development crops has been demonstrated. Average absolute center refinement is smaller on development (5.10 px) than training (7.28/7.93 px). Review crop-to-line identity and full visible extent before attributing the score to geometry, and preserve any corrected records as a new dataset revision. Higher input resolution is a later controlled comparison, not an established cure.

## Checks that did not explain this result

- All 24 prepared inputs are 848 × 32, giving 212 CTC frames for 28–38 target characters. The repeat-aware feasibility gate passes. No variable-width padding occurs. The [runtime-matched CTCLoss documentation](https://docs.pytorch.org/docs/2.10/generated/torch.nn.CTCLoss.html) explains that impossible short inputs can produce infinite losses hidden by `zero_infinity`; that mechanism is not supported here.
- Train and development use the same grayscale transform, polarity, label ordering and input height. Reconstructed-input predictions match the saved outputs exactly; no train/inference transform mismatch was found.
- Folding `ς` to `σ` recovers zero edit errors on either partition. Sigma policy still needs scholarly agreement but does not explain the present score. Labels contain no diacritic/mark errors in this plain subset.
- Source rights, leaf grouping, checkpoint selection and package reload are consistent. There is no evidence that the wrong model was loaded. Keeping both sides of leaf 82 together is correct; moving page 1440 into training would remove this development check.

## Engineering prerequisites for future experiments

**Subsequent engineering status:** the owner authorized the diagnostics, mode and padding fixes; [implementation and regression evidence](training-engineering.md) records their completion. The [shared preprocessing contract](preprocessing-contract.md) was subsequently implemented as well. The paragraphs below describe the original investigation findings; P46's saved recognition output remains unchanged.

The audit found two issues that were inactive in v2 and must not be misreported as its causes:

1. `CtcTrainer` and `TorchLineRecognizer` do not explicitly switch training/evaluation modes. With dropout 0 and no batch normalization this does not change v2 results. Before testing dropout, add explicit mode handling with restoration of caller state and a focused failing-before/passing-after test for deterministic inference and resumed training behavior.
2. `LineBatcher` and `LineAugmenter` fill uncovered pixels with zero. Here zero means black; all widths match and augmentation is disabled, so neither affected this run. Before using variable-width crops or shifts, declare the background/polarity contract and test white-background fill, valid CTC lengths and absence of false border ink. Check all callers before changing shared defaults.

The next run should log training/development CER, deletions/substitutions/insertions, prediction/reference length, blank statistics and fixed sample strings at declared intervals. Prior history records only loss/CER. The earlier claim that predictions were blank at every intermediate epoch was too strong: selected v1 predictions are blank and all recorded development CER values are 1.0, but intermediate strings/posteriors were not saved. The historical report and plan have been corrected.

## Reproduction and evidence

The local inference-only diagnostic is in `.artifacts/experiments/p46-diagnosis-2026-11-02/`:

- `Program.cs` and `Diagnosis.csproj`: C# reconstruction, reload equality, logits, blank statistics and fixed beam comparison.
- `inference-diagnostics.json`: per-line predictions, edit scores, frame counts and blank statistics.
- `error-summary.json`: aggregate errors, character exposure, learning-curve landmarks and descriptive image statistics. Read-only Python/Pillow/NumPy analysis generated this summary; training/preparation remain C#.

From the repository root:

```sh
dotnet restore .artifacts/experiments/p46-diagnosis-2026-11-02/Diagnosis.csproj -p:NuGetAudit=false
dotnet run --no-restore --project .artifacts/experiments/p46-diagnosis-2026-11-02/Diagnosis.csproj -- /absolute/path/to/LeviathanText
```

The model SHA-256 remains `2ab0f93c3bae03c2fd6dad5a833092e7ae764b2923a21083bfe3510763a3f9c0`. Independent audits agree with aggregate errors and image measurements. The [next-run plan](../../project-plan.md#p46-next-run-protocol--2026-11-02) specifies ownership, prerequisites, a bounded comparison and selection criteria. No fresh acceptance set was evaluated.
