# Local VLM pilot results — 2026-11-04

This freezes the **first sweep**: 306 planned natural P46 requests across three models and two routes. All 306 have retained result rows: 301 completed at the transport level and 5 were truncated. The table includes every request. R0 is a separately replayed frozen recognizer, with three identical 51-line repetitions.

This pilot supports continued engineering and specialist review. **It does not support promoting any model as a Greek transcription replacement.** The two Qwen models failed the blank-image control; Mistral passed that narrow control after a verified client parser fix, but that does not establish manuscript accuracy. No specialist decision or acceptance label was created, and the inference commands performed no training.

Separate verification deviation: two earlier CLI regression invocations unintentionally
each ran an existing one-epoch synthetic alphabet fixture test. Its temporary black-rectangle
fixtures were disposed; no P46 or VLM weights were trained or changed. The corrected test
filter excludes both fitting tests. [The retained deviation receipt](../../.artifacts/vlm-benchmark/vlm-bench-1/root/synthetic-test-training-deviation.json)
records exact tests/timestamps; the frozen first-sweep reports below remain unchanged.

Later studies are reported separately: [repeatability](vlm-repeatability-results-2026-11-04.md), [synthetic prompts](vlm-synthetic-prompt-results-2026-11-04.md), [geometry](vlm-geometry-results-2026-11-04.md), [output diagnostics](vlm-output-diagnostics-2026-11-04.md) and [resource observations](vlm-resource-observations-2026-11-04.md). Their outcomes do not revise this frozen first-sweep matrix.

The first-sweep natural runs are closed. Only Qwen2.5's secondary misleading-OCR controls were closed at this report's freeze; Mistral/Qwen3 secondary controls, later prompt probes and later repeatability sweeps require separate versioned reports. Their pending entries below are not model failures.

Raw source-reference distance only, NOT diplomatic transcription accuracy. All P46 labels are provisional, source-exposed lowercase/σς/space-stripped references; literal visible case, lunate sigma, spacing and marks differ. 44 lines trained R0 and 7 were repeatedly used for development. Pooled numbers conceal this exposure split. Five proposed physical-leaf/unit groups of one witness (folio/source-caption mapping still needs review), with only one development group, cannot support model-selection, confidence-interval or generalization claims.

## Natural P46 source-reference distances

The original 44 training and 7 development lines remain separate (1,504 and 219 NFC reference scalars respectively). Each percentage is a per-repetition NFC scalar edit/reference ratio; a range shows variation across repetitions, never independent replication. Nonempty failed/truncated text is retained, missing planned output is a full deletion, and no difficult line is dropped.

| Condition | Repetitions | Completed / planned | Training (44) | Development (7) | Pooled (51) | Missing rows |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Frozen R0 (raw-recognizer) | 3 | 153/153 | 8.18% | 81.74% | 17.53% | 0 |
| Qwen3-VL 8B V1 (image-only) | 1 | 51/51 | 65.82% | 65.75% | 65.82% | 0 |
| Qwen3-VL 8B V2 (ocr-assisted) | 1 | 51/51 | 63.16% | 297.72% | 92.98% | 0 |
| Qwen2.5-VL 7B V1 (image-only) | 1 | 49/51 | 131.32% | 611.87% | 192.40% | 0 |
| Qwen2.5-VL 7B V2 (ocr-assisted) | 1 | 49/51 | 136.04% | 540.64% | 187.46% | 0 |
| Mistral Small 3.1 24B V1 (image-only) | 1 | 51/51 | 126.20% | 171.69% | 131.98% | 0 |
| Mistral Small 3.1 24B V2 (ocr-assisted) | 1 | 50/51 | 85.97% | 546.12% | 144.46% | 0 |

Completion means transport protocol completed, not prompt obedience or correct Greek. Ratios can exceed 100% because all insertions, including prose and repeated text, count without clipping. The provisional reference convention differs from visible text, so these numbers cannot isolate visual-recognition errors from convention differences. R0's pooled figure mostly measures training fit. The development group has only one physical leaf and was repeatedly inspected; it is not a blind test.

### Physical-unit breakdown

R0 repetitions 1–3 were identical; one row per physical unit below represents all three. Complete per-repetition rows remain in `comparison.json`.

| Condition | Rep | Unit | Original split | Lines | Reference scalars | Edits | Raw distance |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: |
| Frozen R0 | 1–3 | unit-01 | development | 7 | 219 | 179 | 81.74% |
| Frozen R0 | 1–3 | unit-02 | training | 17 | 578 | 38 | 6.57% |
| Frozen R0 | 1–3 | unit-03 | training | 12 | 418 | 45 | 10.77% |
| Frozen R0 | 1–3 | unit-04 | training | 10 | 332 | 23 | 6.93% |
| Frozen R0 | 1–3 | unit-05 | training | 5 | 176 | 17 | 9.66% |
| Qwen3-VL 8B V1 | 1 | unit-01 | development | 7 | 219 | 144 | 65.75% |
| Qwen3-VL 8B V1 | 1 | unit-02 | training | 17 | 578 | 339 | 58.65% |
| Qwen3-VL 8B V1 | 1 | unit-03 | training | 12 | 418 | 228 | 54.55% |
| Qwen3-VL 8B V1 | 1 | unit-04 | training | 10 | 332 | 262 | 78.92% |
| Qwen3-VL 8B V1 | 1 | unit-05 | training | 5 | 176 | 161 | 91.48% |
| Qwen3-VL 8B V2 | 1 | unit-01 | development | 7 | 219 | 652 | 297.72% |
| Qwen3-VL 8B V2 | 1 | unit-02 | training | 17 | 578 | 401 | 69.38% |
| Qwen3-VL 8B V2 | 1 | unit-03 | training | 12 | 418 | 215 | 51.44% |
| Qwen3-VL 8B V2 | 1 | unit-04 | training | 10 | 332 | 219 | 65.96% |
| Qwen3-VL 8B V2 | 1 | unit-05 | training | 5 | 176 | 115 | 65.34% |
| Qwen2.5-VL 7B V1 | 1 | unit-01 | development | 7 | 219 | 1340 | 611.87% |
| Qwen2.5-VL 7B V1 | 1 | unit-02 | training | 17 | 578 | 291 | 50.35% |
| Qwen2.5-VL 7B V1 | 1 | unit-03 | training | 12 | 418 | 269 | 64.35% |
| Qwen2.5-VL 7B V1 | 1 | unit-04 | training | 10 | 332 | 273 | 82.23% |
| Qwen2.5-VL 7B V1 | 1 | unit-05 | training | 5 | 176 | 1142 | 648.86% |
| Qwen2.5-VL 7B V2 | 1 | unit-01 | development | 7 | 219 | 1184 | 540.64% |
| Qwen2.5-VL 7B V2 | 1 | unit-02 | training | 17 | 578 | 345 | 59.69% |
| Qwen2.5-VL 7B V2 | 1 | unit-03 | training | 12 | 418 | 229 | 54.78% |
| Qwen2.5-VL 7B V2 | 1 | unit-04 | training | 10 | 332 | 1275 | 384.04% |
| Qwen2.5-VL 7B V2 | 1 | unit-05 | training | 5 | 176 | 197 | 111.93% |
| Mistral Small 3.1 24B V1 | 1 | unit-01 | development | 7 | 219 | 376 | 171.69% |
| Mistral Small 3.1 24B V1 | 1 | unit-02 | training | 17 | 578 | 556 | 96.19% |
| Mistral Small 3.1 24B V1 | 1 | unit-03 | training | 12 | 418 | 427 | 102.15% |
| Mistral Small 3.1 24B V1 | 1 | unit-04 | training | 10 | 332 | 432 | 130.12% |
| Mistral Small 3.1 24B V1 | 1 | unit-05 | training | 5 | 176 | 483 | 274.43% |
| Mistral Small 3.1 24B V2 | 1 | unit-01 | development | 7 | 219 | 1196 | 546.12% |
| Mistral Small 3.1 24B V2 | 1 | unit-02 | training | 17 | 578 | 484 | 83.74% |
| Mistral Small 3.1 24B V2 | 1 | unit-03 | training | 12 | 418 | 412 | 98.56% |
| Mistral Small 3.1 24B V2 | 1 | unit-04 | training | 10 | 332 | 271 | 81.63% |
| Mistral Small 3.1 24B V2 | 1 | unit-05 | training | 5 | 176 | 126 | 71.59% |

## Synthetic image and grounding controls

Five rendered images include four known Greek strings and one blank. Three repetitions are repeated views of the same five fixtures, not fifteen independent examples. Misleading-OCR hints are deliberately invented controls, separate from actual R0-assisted natural V2.

| Condition | Family | Completed / planned | Text exact / planned | Blank pass / planned | Blank fail | Missing |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Qwen3-VL 8B synthetic | synthetic | 15/15 | 6/12 | 0/3 | 3 | 0 |
| Qwen3-VL 8B misleading OCR | synthetic-misleading | pending | — | — | — | — |
| Qwen2.5-VL 7B synthetic | synthetic | 15/15 | 3/12 | 0/3 | 3 | 0 |
| Qwen2.5-VL 7B misleading OCR | synthetic-misleading | 15/15 | 3/12 | 0/3 | 3 | 0 |
| Mistral Small 3.1 24B synthetic | synthetic | 15/15 | 6/12 | 3/3 | 0 | 0 |
| Mistral Small 3.1 24B misleading OCR | synthetic-misleading | pending | — | — | — | — |

A failed blank control blocks the proposed grounding/adoption gate. Even perfect finite synthetic controls cannot demonstrate manuscript competence. Masked/damaged natural controls and specialist audits are still missing.

**Retained client-diagnostic appendix — Mistral pre-fix synthetic:** Old client incorrectly required a content token for valid empty SSE responses. The 3 empty blank responses were marked InvalidOutput. All 15 requests were rerun as synthetic-v2 after a regression-tested parser fix; these original statuses are retained. Producer snapshot cli-live-v2-hashes.json. Original statuses: Completed=12, InvalidOutput=3. These statuses are not retroactively rewritten and are not evidence of model grounding failure.

## Failure and profile diagnostics

| Condition | Status counts | Rows with ASCII Latin letters | ASCII Latin letters |
| --- | --- | ---: | ---: |
| Frozen R0 / p46 | Completed=153 | 0 | 0 |
| Qwen3-VL 8B V1 / p46 | Completed=51 | 1 | 1 |
| Qwen3-VL 8B V2 / p46 | Completed=51 | 1 | 358 |
| Qwen3-VL 8B synthetic / synthetic | Completed=15 | 3 | 9 |
| Qwen2.5-VL 7B V1 / p46 | Completed=49, Truncated=2 | 6 | 134 |
| Qwen2.5-VL 7B V2 / p46 | Completed=49, Truncated=2 | 2 | 106 |
| Qwen2.5-VL 7B synthetic / synthetic | Completed=15 | 3 | 9 |
| Qwen2.5-VL 7B misleading OCR / synthetic-misleading | Completed=15 | 3 | 6 |
| Mistral Small 3.1 24B V1 / p46 | Completed=51 | 3 | 476 |
| Mistral Small 3.1 24B V2 / p46 | Completed=50, Truncated=1 | 1 | 70 |
| Mistral Small 3.1 24B synthetic / synthetic | Completed=15 | 3 | 9 |
| Mistral pre-fix synthetic / synthetic-client-diagnostic | Completed=12, InvalidOutput=3 | 3 | 9 |

ASCII Latin counts are a narrow code-point diagnostic, not a judgement of all language/script content. They can include prose or Greek-looking Latin letters; no substitution, case/sigma folding, whitespace repair or accent removal was applied. Review exact raw outputs and the specialist packet before interpreting any apparent variant correction.

## Timing and resource observations

### Local runtime and model identities

The measurement host reports Apple M5 Max, 64GiB unified RAM, macOS27.0.1 and
.NET10.0.301 SDK. LM Studio0.4.26+4 used
`llama.cpp-mac-arm64-apple-metal-advsimd@2.65.1`, one loaded model and one in-flight
request. All three candidates used Q4_K_M weights, their matching F16 vision projector,
8,192-token context, full requested GPU offload and parallelism1. The saved API load
configurations also record batching, flash attention and KV-cache options; do not infer
unreported runtime defaults from the package name.

| Candidate | LM Studio request identifier | Pre-load estimate |
| --- | --- | ---: |
| Qwen3-VL 8B | `lt-vlm-qwen3-8b` | 6.71GiB |
| Qwen2.5-VL 7B | `lt-vlm-qwen25-7b` | 6.21GiB |
| Mistral Small3.1 24B | `lt-vlm-mistral-24b` | 15.57GiB |

These are load estimates, not measured peaks. Exact upstream/conversion revisions,
downloaded weight/projector SHA-256 values and pinned model cards are retained in
`.artifacts/vlm-benchmark/vlm-bench-1/root/model-receipts/`. All six repository cards
declare Apache2.0; those revisions do not contain standalone `LICENSE` files, so the
receipt distinguishes card declarations from the separately retained standard license
text. No model or corpus redistribution was performed. Model-file hashes identify
embedded tokenizer/template bytes; the effective runtime template and internal image
tiling were not independently dumped, and seed honoring was not independently verified.
These remain reproducibility limits, not assumed facts.

Each model's `*-api-config.json`, `*-loaded.json`, load logs, hardware survey and per-run
resource records are in the same artifact root. The host already had about25GiB of swap
in use before the larger model loaded. An RSS sample or an unchanged swap-use total cannot
prove absence of GPU peaks or paging; raw VM counters are retained for separate analysis.

Nearest-rank quantile: sort n finite durations and choose rank ceil(p*n), 1-based; no interpolation. First recorded provider request is shown separately, not asserted to be cold model loading. Subsequent durations are not assumed isolated warm measurements. Failed attempts with measured duration stay included; absent durations remain unavailable. Provider timing includes encoding/HTTP but excludes pre-request crop reads/validation and model loading. R0 timings are inference-only after three separate warmups; not equivalent end-to-end work. Its separately recorded initial model load was 1,962.60 ms; the three warmups were 10.55, 1.27 and 1.21 ms and were excluded from the timing table. Downloads/builds and host processes confound this pilot.

| Condition | Timed attempts | First ms | All p50 ms | All p95 ms | Subsequent p50 ms | Subsequent p95 ms | Sampled summed RSS MiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Frozen R0 / p46 | 153 | 1.26 | 1.15 | 1.23 | 1.15 | 1.23 | — |
| Qwen3-VL 8B V1 / p46 | 51 | 6657.96 | 5722.71 | 7555.07 | 5708.04 | 7555.07 | 5918.42 |
| Qwen3-VL 8B V2 / p46 | 51 | 6661.69 | 4961.13 | 6661.69 | 4932.26 | 6468.51 | 6628.62 |
| Qwen3-VL 8B synthetic / synthetic | 15 | 2222.96 | 921.88 | 2309.50 | 459.25 | 2309.50 | 5943.69 |
| Qwen2.5-VL 7B V1 / p46 | 51 | 4396.86 | 676.67 | 4396.86 | 658.07 | 4056.13 | 5890.58 |
| Qwen2.5-VL 7B V2 / p46 | 51 | 656.02 | 645.73 | 1195.73 | 642.39 | 1195.73 | 6611.77 |
| Qwen2.5-VL 7B synthetic / synthetic | 15 | 1528.04 | 498.40 | 2067.38 | 440.08 | 2067.38 | 7197.58 |
| Qwen2.5-VL 7B misleading OCR / synthetic-misleading | 15 | 366.21 | 163.05 | 366.21 | 161.58 | 343.96 | 6580.47 |
| Mistral Small 3.1 24B V1 / p46 | 51 | 1448.33 | 1005.12 | 2348.36 | 1004.16 | 2348.36 | 15998.48 |
| Mistral Small 3.1 24B V2 / p46 | 51 | 1831.26 | 1276.57 | 1831.26 | 1276.12 | 1682.42 | 15782.30 |
| Mistral Small 3.1 24B synthetic / synthetic | 15 | 822.31 | 152.14 | 822.31 | 143.10 | 293.36 | 14369.44 |
| Mistral pre-fix synthetic / synthetic-client-diagnostic | 15 | 859.67 | 200.23 | 859.67 | 196.10 | 588.00 | 16698.66 |

The RSS column sums approximately one-second samples of LM Studio app/helpers/backends and the child runner. Shared pages can be double-counted; GPU allocations and sub-second peaks are not measured. Before/during/after swap and OS records are retained separately. R0 has three excluded warmups and a separately recorded model load; its second resource replay measured 177.875 MiB for that process alone, under different cache/host conditions. No speed, memory-stability or correction-time improvement is established.

### Producer and input differences

The recorded requests all use the frozen `vlm-diplomatic-1` prompt, temperature 0, top-p 1, output-token limit 512 and seeded sample order. V1 receives an image only. Natural V2 receives the same image plus the immutable R0 suggestion; reference labels are never sent. The misleading synthetic hints are invented wrong strings and are explicitly separate from R0.

R0 uses its original prepared 424×16 grayscale float inputs and fixed-budget seed-42 checkpoint (epoch 544, step 5,984). VLMs receive the corresponding 1,695×64 PNG crops. This is a pipeline diagnostic with different input recipes, not an isolated architectural benchmark. See [the frozen corpus](../data/vlm-benchmark-corpus.md) and [scoring/R0 details](vlm-benchmark-scoring.md).

Live producer snapshots pin all project assemblies, including the AI parser and evaluator. A CLI assembly hash alone would not identify every dependency. Original live files were not rewritten after fixes.

| Live conditions | Producer snapshot under `.artifacts/vlm-benchmark/vlm-bench-1/root/` |
| --- | --- |
| Qwen3 first synthetic controls | `cli-probe-v1-hashes.json` |
| Qwen3 first P46 V1/V2 | `cli-live-v1-hashes.json` |
| Qwen2.5 first runs; retained Mistral pre-fix synthetic v1 | `cli-live-v2-hashes.json` |
| Corrected Mistral synthetic v2; Mistral first P46 V1/V2 | `cli-live-v3-hashes.json` |

The empty-SSE correction changed the AI parser. The old Mistral run remains a client-diagnostic appendix, and all 15 controls were repeated with the same inputs and prompt. No old status or output was silently repaired. The companion producer snapshots, comparison register and [transport handoff](../handoffs/VLM-A1.md) retain the identities and regression evidence. Later client changes require separate producer receipts and do not retroactively change these snapshots.

## Evidence and next gate

The versioned comparison register pins the original references, source dataset, R0 outputs and each explicitly closed run's manifest/input/results/resource receipts. Finalized CLI scoring validates model/prompt/image/cohort joins; the current C# scorer supplies every planned denominator. Raw artifacts are referenced rather than duplicated. Pending/running conditions are not reported as failed models. No confidence interval, statistical selection, promotion or acceptance decision is computed.

Obtain independently reviewed visible targets, geometry and mark conventions; keep the existing 81 decisions pending until returned. Add fresh material-specific units and appropriate comparators for Classical Greek, other manuscripts and inscriptions. Then preregister adequate sampling, blinded correction effort and isolated resource measurements. These pilot outputs are evidence for that work, not a replacement for it.

## Reproduction and frozen evidence

The offline C# reporter makes no inference calls. It validates each closed VLM run with the CLI scorer, checks hashes before and after scoring, scores real R0 outputs directly, and joins original source split assignments without changing them. It preserves failed text, every planned denominator and the distinction between an unstarted condition and a missing output from a closed plan.

- [Frozen comparison register](../../.artifacts/vlm-benchmark/vlm-bench-1/b/comparison-report/run-r2/register.json): `89c83aed83bceb02ee2bf24037d67b84bf280aa8ff425ea905427d06a0e79dfa`.
- [Machine-readable grouped comparison](../../.artifacts/vlm-benchmark/vlm-bench-1/b/comparison-report/run-r2/comparison.json): `96babe1f416051f924ef7ce97fe049643accdd0eb25ff3aba2dc77c3a328c865`.
- [Offline report receipt](../../.artifacts/vlm-benchmark/vlm-bench-1/b/comparison-report/run-r2/receipt.json): `ecf9248b8f7d062b9cb112bf0efa244f6a555fd468785cc601016503f8164904`.
- [Reporter source and runtime snapshot](../../.artifacts/vlm-benchmark/vlm-bench-1/b/comparison-report/producer-r2/receipt.json), [candidate self-test log](../../.artifacts/vlm-benchmark/vlm-bench-1/b/comparison-report/edge-candidate-test.log), and [independent arithmetic validation](../../.artifacts/vlm-benchmark/vlm-bench-1/b/comparison-report/run-r2-validation.log).

The reporter compiled without warnings/errors and passed 13 self-tests. Independent validation passed 209 checks of original train/development partitions and physical-unit aggregates against pooled counts, edits and coverage. Read-only peer review caught a pending-client-diagnostic rendering defect; its failing reproduction is retained and the corrected reporter preserves null pending denominators. The current Evaluation scorer's 39 focused and 78 full-suite tests are documented separately in [VLM-B1](../handoffs/VLM-B1.md).

Reproduce into a new output directory, using the preserved reporter runtime and frozen register:

```sh
dotnet .artifacts/vlm-benchmark/vlm-bench-1/b/comparison-report/producer-r2/runtime/Report.dll \
  /absolute/repository \
  /absolute/repository/.artifacts/vlm-benchmark/vlm-bench-1/b/comparison-report/register-r2.json \
  /absolute/new-output-directory
```

The [frozen runtime receipt](../../.artifacts/vlm-benchmark/vlm-bench-1/b/comparison-report/producer-r2/runtime-receipt.json) pins the managed assemblies required for offline reporting. Native training/image runtimes are omitted; this artifact performs no inference or image loading. The frozen copy passes the same 13 self-tests. Rebuilding later against changed project dependencies creates a new producer and requires a new receipt. Large raw outputs remain in their original artifact directories referenced by the frozen register; this document does not duplicate them.
