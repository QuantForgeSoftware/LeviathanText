# P46 VLM repeatability diagnostics — 2026-11-04

**Final frozen revision r6: all 6 repeatability conditions are closed.** The extension retains all 918 planned row slots: 895 Completed, 16 Truncated, 1 Cancelled and 6 explicit NotAttempted. There are no absent JSONL rows. These are repeated requests over the same 51 lines; the [first sweep](vlm-pilot-results-2026-11-04.md) remains separate and unchanged.

The Qwen2.5 V2 condition reached its 480-second deadline and ended at approximately 09:44:16 CST, before the global stop. It attempted 147 of 153 requests: 140 Completed, 6 Truncated and 1 Cancelled; the remaining 6 slots are recorded NotAttempted. No extension, retry or replacement of missing outputs was performed. Those budget-limited outcomes are separate from model errors, and all 153 slots remain in its reported denominator.

**These are raw distances to provisional source references, not diplomatic Greek transcription accuracy.** All 51 lines are previously exposed; 44 trained R0 and 7 were repeatedly used for development. The five proposed physical-leaf/unit groups derive from one biblical witness; folio/source-caption mapping remains pending, and all 7 development lines belong to one proposed group. Three runs do not make 153 independent lines, and stable model output can be consistently wrong, editorialized, memorized or verbose. No confidence interval, model ranking, selection or promotion is justified.

No collection, P46 or VLM model was trained. Two earlier integration-test invocations each fitted one temporary synthetic black-rectangle fixture epoch; [the deviation receipt](../../.artifacts/vlm-benchmark/vlm-bench-1/root/synthetic-test-training-deviation.json) preserves that exception. These offline report programs perform no fitting, downloads or inference.

## Registered conditions and full denominators

The [registration](../../.artifacts/vlm-benchmark/vlm-bench-1/root/repeatability-registration.json) froze unchanged prompts, cohort, packages and sampling settings before the extension. Each condition has three seeded orders (42, 1729, 2026), output limit 512 and at most 20 minutes, additionally bounded by the global 09:45 CST natural-run stop. Failed/truncated text stays complete; missing slots in a closed plan count as deletions. Entirely unstarted/unfrozen conditions have null scores. The historical R0 replay is context, not newly fitted or newly inferred here.

Actual closed-condition wall caps (minutes): Mistral Small 3.1 24B V1: 20; Mistral Small 3.1 24B V2: 20; Qwen3-VL 8B V1: 20; Qwen3-VL 8B V2: 20; Qwen2.5-VL 7B V1: 12; Qwen2.5-VL 7B V2: 8. The actual caps were reduced for the last two conditions to respect the global stop. Unequal wall caps preclude a clean comparison of complete-condition quality or response rates when a deadline tail occurs. `NotAttempted` slots caused by that deadline are distinct from model errors; their full-deletion distance is an accounting rule, not an observed recognition failure.

| Condition | Reps | Completed / planned | Training (44) distance range | Development (7) distance range | Pooled (51) distance range | Recorded NotAttempted | Missing rows |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Frozen R0 | 3 | 153/153 | 8.18% | 81.74% | 17.53% | 0 | 0 |
| Mistral Small 3.1 24B V1 repeatability | 3 | 152/153 | 126.20%–152.46% | 171.69% | 131.98%–154.90% | 0 | 0 |
| Mistral Small 3.1 24B V2 repeatability | 3 | 150/153 | 85.97% | 546.12% | 144.46% | 0 | 0 |
| Qwen3-VL 8B V1 repeatability | 3 | 153/153 | 65.82% | 65.75%–66.21% | 65.82%–65.87% | 0 | 0 |
| Qwen3-VL 8B V2 repeatability | 3 | 153/153 | 63.16% | 297.72% | 92.98% | 0 | 0 |
| Qwen2.5-VL 7B V1 repeatability | 3 | 147/153 | 131.32% | 611.87% | 192.40% | 0 | 0 |
| Qwen2.5-VL 7B V2 repeatability | 3 | 140/153 | 136.04%–141.22% | 539.73%–540.64% | 187.46%–191.87% | 6 | 0 |

Distances use NFC scalar edits/reference scalars without case, sigma, whitespace or accent folding. Insertions and prose can make the ratio exceed 100%. Historical references differ from literal visible writing, so these distances cannot isolate recognition correctness from reference-convention differences. Completion is transport completion, not prompt obedience.

### Each repetition remains visible

| Condition | Repetition | Training distance | Development distance | Pooled distance | Status counts |
| --- | ---: | ---: | ---: | ---: | --- |
| Frozen R0 | 1 | 8.18% | 81.74% | 17.53% | completed=51 |
| Frozen R0 | 2 | 8.18% | 81.74% | 17.53% | completed=51 |
| Frozen R0 | 3 | 8.18% | 81.74% | 17.53% | completed=51 |
| Mistral Small 3.1 24B V1 repeatability | 1 | 152.46% | 171.69% | 154.90% | completed=50, truncated=1 |
| Mistral Small 3.1 24B V1 repeatability | 2 | 126.20% | 171.69% | 131.98% | completed=51 |
| Mistral Small 3.1 24B V1 repeatability | 3 | 126.20% | 171.69% | 131.98% | completed=51 |
| Mistral Small 3.1 24B V2 repeatability | 1 | 85.97% | 546.12% | 144.46% | completed=50, truncated=1 |
| Mistral Small 3.1 24B V2 repeatability | 2 | 85.97% | 546.12% | 144.46% | completed=50, truncated=1 |
| Mistral Small 3.1 24B V2 repeatability | 3 | 85.97% | 546.12% | 144.46% | completed=50, truncated=1 |
| Qwen3-VL 8B V1 repeatability | 1 | 65.82% | 65.75% | 65.82% | completed=51 |
| Qwen3-VL 8B V1 repeatability | 2 | 65.82% | 66.21% | 65.87% | completed=51 |
| Qwen3-VL 8B V1 repeatability | 3 | 65.82% | 66.21% | 65.87% | completed=51 |
| Qwen3-VL 8B V2 repeatability | 1 | 63.16% | 297.72% | 92.98% | completed=51 |
| Qwen3-VL 8B V2 repeatability | 2 | 63.16% | 297.72% | 92.98% | completed=51 |
| Qwen3-VL 8B V2 repeatability | 3 | 63.16% | 297.72% | 92.98% | completed=51 |
| Qwen2.5-VL 7B V1 repeatability | 1 | 131.32% | 611.87% | 192.40% | completed=49, truncated=2 |
| Qwen2.5-VL 7B V1 repeatability | 2 | 131.32% | 611.87% | 192.40% | completed=49, truncated=2 |
| Qwen2.5-VL 7B V1 repeatability | 3 | 131.32% | 611.87% | 192.40% | completed=49, truncated=2 |
| Qwen2.5-VL 7B V2 repeatability | 1 | 136.04% | 540.64% | 187.46% | completed=49, truncated=2 |
| Qwen2.5-VL 7B V2 repeatability | 2 | 136.04% | 540.64% | 187.46% | completed=49, truncated=2 |
| Qwen2.5-VL 7B V2 repeatability | 3 | 141.22% | 539.73% | 191.87% | cancelled=1, completed=42, notAttempted=6, truncated=2 |

### Proposed unit-group ranges over the three repetitions

| Condition | Unit | Historical split | Lines per rep | Reference scalars per rep | Raw distance range |
| --- | --- | --- | ---: | ---: | ---: |
| Frozen R0 | unit-01 | development | 7 | 219 | 81.74% |
| Frozen R0 | unit-02 | training | 17 | 578 | 6.57% |
| Frozen R0 | unit-03 | training | 12 | 418 | 10.77% |
| Frozen R0 | unit-04 | training | 10 | 332 | 6.93% |
| Frozen R0 | unit-05 | training | 5 | 176 | 9.66% |
| Mistral Small 3.1 24B V1 repeatability | unit-01 | development | 7 | 219 | 171.69% |
| Mistral Small 3.1 24B V1 repeatability | unit-02 | training | 17 | 578 | 96.19% |
| Mistral Small 3.1 24B V1 repeatability | unit-03 | training | 12 | 418 | 102.15% |
| Mistral Small 3.1 24B V1 repeatability | unit-04 | training | 10 | 332 | 130.12%–249.10% |
| Mistral Small 3.1 24B V1 repeatability | unit-05 | training | 5 | 176 | 274.43% |
| Mistral Small 3.1 24B V2 repeatability | unit-01 | development | 7 | 219 | 546.12% |
| Mistral Small 3.1 24B V2 repeatability | unit-02 | training | 17 | 578 | 83.74% |
| Mistral Small 3.1 24B V2 repeatability | unit-03 | training | 12 | 418 | 98.56% |
| Mistral Small 3.1 24B V2 repeatability | unit-04 | training | 10 | 332 | 81.63% |
| Mistral Small 3.1 24B V2 repeatability | unit-05 | training | 5 | 176 | 71.59% |
| Qwen3-VL 8B V1 repeatability | unit-01 | development | 7 | 219 | 65.75%–66.21% |
| Qwen3-VL 8B V1 repeatability | unit-02 | training | 17 | 578 | 58.65% |
| Qwen3-VL 8B V1 repeatability | unit-03 | training | 12 | 418 | 54.55% |
| Qwen3-VL 8B V1 repeatability | unit-04 | training | 10 | 332 | 78.92% |
| Qwen3-VL 8B V1 repeatability | unit-05 | training | 5 | 176 | 91.48% |
| Qwen3-VL 8B V2 repeatability | unit-01 | development | 7 | 219 | 297.72% |
| Qwen3-VL 8B V2 repeatability | unit-02 | training | 17 | 578 | 69.38% |
| Qwen3-VL 8B V2 repeatability | unit-03 | training | 12 | 418 | 51.44% |
| Qwen3-VL 8B V2 repeatability | unit-04 | training | 10 | 332 | 65.96% |
| Qwen3-VL 8B V2 repeatability | unit-05 | training | 5 | 176 | 65.34% |
| Qwen2.5-VL 7B V1 repeatability | unit-01 | development | 7 | 219 | 611.87% |
| Qwen2.5-VL 7B V1 repeatability | unit-02 | training | 17 | 578 | 50.35% |
| Qwen2.5-VL 7B V1 repeatability | unit-03 | training | 12 | 418 | 64.35% |
| Qwen2.5-VL 7B V1 repeatability | unit-04 | training | 10 | 332 | 82.23% |
| Qwen2.5-VL 7B V1 repeatability | unit-05 | training | 5 | 176 | 648.86% |
| Qwen2.5-VL 7B V2 repeatability | unit-01 | development | 7 | 219 | 539.73%–540.64% |
| Qwen2.5-VL 7B V2 repeatability | unit-02 | training | 17 | 578 | 59.69%–62.28% |
| Qwen2.5-VL 7B V2 repeatability | unit-03 | training | 12 | 418 | 54.78%–62.20% |
| Qwen2.5-VL 7B V2 repeatability | unit-04 | training | 10 | 332 | 384.04%–393.67% |
| Qwen2.5-VL 7B V2 repeatability | unit-05 | training | 5 | 176 | 111.93% |

## Exact outcome repeatability

Exact raw string equality uses ordinal comparison without Unicode normalization, trimming or extraction. A triplet is fully observed only when all three planned outcomes have retained rows and none is NotAttempted; failed/truncated recorded text stays included. Exact-text/status/joint stability is null for unobserved triplets. All-completed exact stability is also reported separately; repeated failures are not recognition success. Every summary denominator is the full planned unique-line cohort. Three repetitions do not create new manuscripts or independent accuracy samples.

| Condition | Cohort | Planned lines | Fully observed triplets | All completed | Exact text stable | Status stable | Text + status stable | All-completed exact text stable | Unobserved triplets | Missing rows | Explicit not attempted |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Frozen R0 (historical replay) | all | 51 | 51 | 51 | 51 | 51 | 51 | 51 | 0 | 0 | 0 |
| Frozen R0 (historical replay) | dev | 7 | 7 | 7 | 7 | 7 | 7 | 7 | 0 | 0 | 0 |
| Frozen R0 (historical replay) | train | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 0 | 0 | 0 |
| Mistral Small 3.1 24B V1 repeatability | all | 51 | 51 | 50 | 50 | 50 | 50 | 50 | 0 | 0 | 0 |
| Mistral Small 3.1 24B V1 repeatability | dev | 7 | 7 | 7 | 7 | 7 | 7 | 7 | 0 | 0 | 0 |
| Mistral Small 3.1 24B V1 repeatability | train | 44 | 44 | 43 | 43 | 43 | 43 | 43 | 0 | 0 | 0 |
| Mistral Small 3.1 24B V2 repeatability | all | 51 | 51 | 50 | 51 | 51 | 51 | 50 | 0 | 0 | 0 |
| Mistral Small 3.1 24B V2 repeatability | dev | 7 | 7 | 6 | 7 | 7 | 7 | 6 | 0 | 0 | 0 |
| Mistral Small 3.1 24B V2 repeatability | train | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 0 | 0 | 0 |
| Qwen3-VL 8B V1 repeatability | all | 51 | 51 | 51 | 50 | 51 | 50 | 50 | 0 | 0 | 0 |
| Qwen3-VL 8B V1 repeatability | dev | 7 | 7 | 7 | 6 | 7 | 6 | 6 | 0 | 0 | 0 |
| Qwen3-VL 8B V1 repeatability | train | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 0 | 0 | 0 |
| Qwen3-VL 8B V2 repeatability | all | 51 | 51 | 51 | 51 | 51 | 51 | 51 | 0 | 0 | 0 |
| Qwen3-VL 8B V2 repeatability | dev | 7 | 7 | 7 | 7 | 7 | 7 | 7 | 0 | 0 | 0 |
| Qwen3-VL 8B V2 repeatability | train | 44 | 44 | 44 | 44 | 44 | 44 | 44 | 0 | 0 | 0 |
| Qwen2.5-VL 7B V1 repeatability | all | 51 | 51 | 49 | 51 | 51 | 51 | 49 | 0 | 0 | 0 |
| Qwen2.5-VL 7B V1 repeatability | dev | 7 | 7 | 6 | 7 | 7 | 7 | 6 | 0 | 0 | 0 |
| Qwen2.5-VL 7B V1 repeatability | train | 44 | 44 | 43 | 44 | 44 | 44 | 43 | 0 | 0 | 0 |
| Qwen2.5-VL 7B V2 repeatability | all | 51 | 45 | 42 | 44 | 44 | 44 | 42 | 6 | 0 | 6 |
| Qwen2.5-VL 7B V2 repeatability | dev | 7 | 6 | 4 | 5 | 5 | 5 | 4 | 1 | 0 | 1 |
| Qwen2.5-VL 7B V2 repeatability | train | 44 | 39 | 38 | 39 | 39 | 39 | 38 | 5 | 0 | 5 |

Physical-unit stability and all 51 opaque sample identities are retained in `stability.json`; source text and model text are not duplicated. A stable response can be consistently incorrect, contain prose, restore a memorized passage, or repeat a transport failure. These counts do not establish image grounding, prompt obedience, diplomatic correctness or readiness.

### Timing by repetition

Nearest-rank quantiles use rank ceil(p×n), without interpolation. All recorded finite durations, including failures, stay included; absent durations are not zero. The separate comparison report retains first-request timing and resource caveats. R0 is inference-only after warmups; provider timings include HTTP/encoding, so these are not comparable end-to-end performance measurements.

| Condition | Rep | Timed outcomes | p50 ms | p95 ms |
| --- | ---: | ---: | ---: | ---: |
| Frozen R0 (historical replay) | 1 | 51 | 1.15 | 1.20 |
| Frozen R0 (historical replay) | 2 | 51 | 1.14 | 1.23 |
| Frozen R0 (historical replay) | 3 | 51 | 1.16 | 1.25 |
| Mistral Small 3.1 24B V1 repeatability | 1 | 51 | 808.11 | 2157.10 |
| Mistral Small 3.1 24B V1 repeatability | 2 | 51 | 832.83 | 2268.38 |
| Mistral Small 3.1 24B V1 repeatability | 3 | 51 | 874.80 | 2805.67 |
| Mistral Small 3.1 24B V2 repeatability | 1 | 51 | 1201.16 | 1661.63 |
| Mistral Small 3.1 24B V2 repeatability | 2 | 51 | 1102.96 | 1680.50 |
| Mistral Small 3.1 24B V2 repeatability | 3 | 51 | 1065.09 | 1666.68 |
| Qwen3-VL 8B V1 repeatability | 1 | 51 | 532.01 | 632.43 |
| Qwen3-VL 8B V1 repeatability | 2 | 51 | 449.78 | 542.75 |
| Qwen3-VL 8B V1 repeatability | 3 | 51 | 467.17 | 581.34 |
| Qwen3-VL 8B V2 repeatability | 1 | 51 | 600.26 | 717.24 |
| Qwen3-VL 8B V2 repeatability | 2 | 51 | 586.10 | 728.20 |
| Qwen3-VL 8B V2 repeatability | 3 | 51 | 533.60 | 741.36 |
| Qwen2.5-VL 7B V1 repeatability | 1 | 51 | 453.86 | 764.68 |
| Qwen2.5-VL 7B V1 repeatability | 2 | 51 | 467.67 | 1241.85 |
| Qwen2.5-VL 7B V1 repeatability | 3 | 51 | 958.03 | 4972.71 |
| Qwen2.5-VL 7B V2 repeatability | 1 | 51 | 2778.48 | 5941.65 |
| Qwen2.5-VL 7B V2 repeatability | 2 | 51 | 684.85 | 7613.18 |
| Qwen2.5-VL 7B V2 repeatability | 3 | 45 | 2681.56 | 6997.17 |

## Timing and resource limits

The timing table above retains each repetition. The grouped comparison additionally records the first timed request separately, all/subsequent p50/p95 and sampled host RSS. The first request is not assumed to include cold model loading. Failed measured requests stay included and absent durations remain unavailable. Provider durations include encoding/HTTP; R0 durations are inference-only after warmups. Download/build activity, other processes, cache history and run order remain confounds. Approximately one-second summed LM Studio/runner RSS may double-count shared pages and misses GPU allocations and shorter peaks. These are descriptive runtime observations, not a speed or memory improvement claim.

A concrete host-state confound is visible: Qwen3 V1 provider p50 changed from 5,722.71 ms in the first sweep to 477.31 ms in this repeatability phase after model reload and completion of acquisition activity. The registered recognition settings stayed fixed, but host/cache/download state did not. This is not evidence of a code speedup or a clean between-model performance comparison. Root retains before/after load configuration and OS observations separately.

## Evidence, validation and remaining gate

The frozen register pins the original source split, provisional references, real R0 hypotheses, each closed run and its resource receipts. The C# comparison entry point reuses strict CLI scoring and the current bounded scorer; the separate C# stability program validates every predecessor output hash, all six condition identities and each complete 3×51 plan. Raw text equality is ordinal and unnormalized; failed outcomes remain distinguishable from all-completed stability. A null or missing triplet never becomes a stable success.

- [Comparison register](../../.artifacts/vlm-benchmark/vlm-bench-1/b/repeatability-report/scores-r6/register.json): `5bad7dcd262208d0d275fe18eb0c2dcdbef404de72f51381a472f198ea93098f`.
- [Grouped scores](../../.artifacts/vlm-benchmark/vlm-bench-1/b/repeatability-report/scores-r6/comparison.json): `d2a71624f4b0cc536445a5ffdec610ef3e9a9d9fccbd4fb75987fa3dba0c2d21`.
- [Scoring receipt](../../.artifacts/vlm-benchmark/vlm-bench-1/b/repeatability-report/scores-r6/receipt.json): `dcc604dbac6a5523fa3df01fda3eb0583bce9bc2492bd00b3b9166be19b51fd8`.
- [Exact outcome stability](../../.artifacts/vlm-benchmark/vlm-bench-1/b/repeatability-report/stability-r6/stability.json): `bbe22e5c7d263ffb72ac15ff450905bc26067a55fb7c13d5a150e5d15a50f9e9`.
- [Stability receipt](../../.artifacts/vlm-benchmark/vlm-bench-1/b/repeatability-report/stability-r6/receipt.json): `e963394ff3dc300ccd33ed38a54e70c8252373914beb3b10a728b461f8225fa4`.

The comparison entry point passed 13 self-tests and the stability program passed 14, with final builds reporting zero warnings/errors. Both use preserved managed runtimes and make no model calls. Read-only peer review found no concrete denominator or stability defect. Independent report arithmetic/partition validation is retained beside each frozen revision.

All 81 original specialist decisions remain pending. Literal visible targets, geometry/mark adjudication, fresh disjoint manuscript/Classical Greek/inscription material, natural grounding controls and blinded correction effort are still required. Repetition assesses consistency of this exposed diagnostic, not general Greek support.
