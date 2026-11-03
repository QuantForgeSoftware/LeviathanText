# NT-PILOT-A — provisional training and engineering handoff

Revision **A1.1 / 2026-11-02**, baseline `fc96d5bb2f901f8610ad421d5f95e640c784ea67`. Engineering verification and the timed work cycle are complete. Authorized cutoff: **17:37:39 CST / 23:37:39 UTC**; closure recorded **17:38:10 CST**. This handoff covers the current two-hour study and its engineering/review evidence, not fulfillment of the historical reviewed-corpus or release gates. A1 is preserved before the final resource append in the report artifact folder.

## Outcome and immutable inputs

[The study report](../evaluation/p46-two-hour-study.md) records three training-only controls and nine fixed-budget comparisons: **63,594 substantive optimizer updates**, plus separately identified synthetic/recovery checks. The height 16 normalized control passes at 2/134 CER, but neither normalization condition passes the matched research selection screen. Median selected development CER is **77.17% T0, 79.45% T1, 78.54% T2**; paired wins versus T0 are 0/3 and 1/3. No model is promoted, no extra accuracy search follows and no pending scholarly decision is accepted.

Preserve the original P46 v2 corpus, receipts, specialist packet and all failed/new controls. Frozen data remain 17 train lines on leaf 82/pages 1450/1460 and 7 exposed development lines on leaf 81/page 1440, 25 labels plus CTC blank. All 81 specialist decisions and 18 questions remain pending. There is no blind result, unseen-hand/witness result, Classical Greek result or inscription accuracy claim.

The original source, float inputs, protocol, code snapshots, selected/latest checkpoints, selected per-line strings, full histories and package reload checks remain under `.artifacts/experiments/p46-study-2026-11-02{,-r2,-r3}/`. The saved matrix index explicitly covers **170 files**, independently rehashed without mismatches, and all 8 v2 receipt files match. Independent scoring recomputes all 216 selected predictions. The report distinguishes selected versus final metrics and unavailable final all-line strings.

Stable SHA-256 receipts: R3 `protocol.json` is `0bfc189c65577eece5bdaec4220fb6588d1dcabf84a2f1727e027db68513cc96`; `matrix-summary.json` is `c6a2a76d0dde3fd9431fad6712536824cbbe20adad76628d64258f4c2dced0da`; `matrix-artifact-index.json` is `342f6f8f5ea5699f5c784cbe3636358e3352c040a79e73b1f0d15e3b0ef3df73`.

## Changed engineering and demonstrated benefit

- Opt-in fixed local-background recipe through the shared C# profile contract; optional training-only gamma and control target. Existing default recipes, identity gamma random draws/config hash and package behavior remain compatible. The recipe's negative quality result is retained.
- Bounded full-history sidecar with byte/hash checks while retaining the 1 MiB inference-header limit; original oversized raw control recovers all 4,000 history rows/four strings unchanged. Publication preflight and temporary replacement prevent demonstrated sidecar/path/directory failures.
- First actual target crossing selects the passing checkpoint even when below the ordinary improvement tolerance. A failing-before/passing-after regression preserves the null-target comparison.
- Exact order-statistic selection/reused workspace/cached brackets reduce measured normalized inference latency 20–23%, with all 48 frozen tensor bits and 216 predictions unchanged. Normalization remains substantially slower than raw and is not adopted by default.
- Three disposal scopes bound temporary tensor aliases per training/evaluation batch and recognition call. Five new regressions fail before and pass after; direct synthetic Adam/loss/parameter and optimizer-resume parity pass. Controlled inference peaks fall from 429–433 MiB to 195–199 MiB, with similar latency and all outputs unchanged. These are not training-memory measurements or a permanent-leak claim.

Production ownership is Recognition profile/preprocessor/recognizer and Training augmentation/config/trainer/package/details helpers, with focused ML/application integration tests. The working tree also contains previously authorized diagnostics/preprocessing integration from earlier turns, separately documented in [training engineering](../evaluation/training-engineering.md) and [the shared contract](../evaluation/preprocessing-contract.md). Do not describe unrelated existing worktree edits as part of this study or stage them wholesale.

## Verification and evidence

The full serial solution build passes with **zero warnings/errors**; final regressions pass **475/475**, zero failures/skips: 249 ML, 126 application, 24 CLI, 20 worker, 56 API. Long P01 parameter-search experiment classes are explicitly excluded. This is engineering regression evidence, not a new all-material real-corpus accuracy gate.

Final results are `.artifacts/test-results/two-hour-study/verified-{ml,application,cli,worker,api}.trx` and `solution-build-lifetime.log`. Relevant failing-before/passing-after TRXs are in that folder and `.artifacts/test-results/p46-two-hour/`.

Additional versioned evidence:

- `p46-study-recovery/`: oversized raw package recovery without retraining.
- `p46-resume-verification/`: copied uninterrupted/interrupted branches, exact parameters and 24 strings at update 6,020; 40 branch updates, no new candidate.
- `p46-normalization-optimization/`: 48 float-bit/hash and 216 decoded-string parity checks.
- `tensor-lifetime-audit/`: exact installed runtime, before/after alias counts, synthetic optimizer ownership/recovery tests, post-scope 48/216 parity and limitations.
- `p46-study-resources{,-optimization,-scoped}/`: separate immutable latency/RSS studies, raw repeated timings, process/environment/source/DLL receipts and previous-evidence integrity checks.
- `p46-r3-reproduction-supplement/`: 99-file post-study C# dependency closure, original frozen overlays, restored pre-scope recognizer and per-file origins/hashes; isolated Training build passes. This is not called an original full-tree receipt.
- `p46-study-report/`: learning-curve plots, error stratification, integrity checks, packet QA and a companion provenance correction linked to preserved original attribution files.

These prefixes are under `.artifacts/experiments/`, are Git-ignored local evidence and do not distribute models. [The provenance correction](../evaluation/p46-source-provenance-correction.md) must accompany research-package transfers because copied original attribution notes incorrectly describe rectified crops as untouched rectangles. Keep all original notices/hashes unchanged. No model redistribution license is assigned.

The original runner's study path/cutoff and source/binary guards are hardcoded. Reproduction requires a new isolated tree, future cutoff/output path, adaptation diff and preparation/receipt, as the report explains. Do not overwrite old evidence or bypass guards. CPU seeds do not guarantee cross-process bit identity.

## Corpus handoff review and next dependencies

Root/integrator reviewed [NT-PILOT-B's current source-expansion handoff](NT-PILOT-B.md) as **source preparation only**. Root independently verified 47 content sizes/hashes, 56 local HTML links, six page records, 11 pending questions, 48 archive entries/CRC and the ZIP digest. Native contact-sheet inspection checks presentation; no browser execution or specialist identity/text acceptance is claimed. The 6 new full-page images/TEIs remain source-exposed, unreviewed, unassigned and ineligible for training.

[Earlier leaves 78–80](../data/p46-earlier-leaves-candidates.md) add proposed metadata/rights crosswalks only: no image pixels/TEI bodies were opened and text license/content/availability remain unverified. The evaluator owns the decision whether metadata exposure suits a future protocol. They are not already blind acceptance data.

The [B2 corpus brief](../tasks/p46-reviewed-corpus.md) owns the new reviewed source/target/geometry release. The [A2 training brief](../tasks/p46-next-training.md) owns the loading contract and subsequent registered control/comparison. Both have disjoint paths and independent work while waiting; real fitting depends on an exact accepted dataset handoff. Alphabet/learned preprocessing are training-only; development OOV items are reported rather than silently dropped. Reserved contents stay evaluator-owned.

Open dependencies are a named Greek reviewer/second reviewer/evaluator, adjudicated visible labels and crop geometry, more faded/rare-character/witness coverage, reviewed physical-leaf partitions and fresh acceptance material. NT-15/17/18 and scholarly thresholds remain open. NT-20 separately records actual training peak RSS, final optimizer/trainer lifecycle, sustained/concurrent and supported-platform resource checks. The scoped inference benchmark and alias counts do not fill those gaps.

No institution/reviewer was contacted and no deployment, submission or release occurred. Changes remain in the shared working tree for human review; no human DCO certification is added by the agent. The completed timed-session task is archived at closure; reviewed-data tasks remain open.

## Independent engineering review

A separate AI instance reviewed the exact study/handoff against retained run receipts, final TRXs/build log, tensor/Adam evidence and scoped resource JSON. At 17:19 CST it found no actionable discrepancy and accepted the **scoped engineering handoff**. It confirmed all three R3 receipt hashes, 63,594-update arithmetic, 475 passing tests, resource limits and unresolved specialist/corpus gates. This is peer review of engineering evidence, not human maintainer approval, scholarly adjudication or model acceptance.

`p46-study-report/session-artifact-index.json` independently records **779 stable evidence files / 76,627,026 bytes**, excluding disposable `bin`/`obj` outputs and mutable closing documentation. SHA-256: `62d69b21afb1b3097ab91bd616909c4aadf817b04592651866d56c63601c9cd4`. Source/data packets have their separate receipts. Recheck the index before an authorized transfer; it does not grant distribution rights.

## A1.1 bounded training-resource append

Before execution, register a separate six-process TRAIN-only resource workload, frozen R3 versus current T0/T1/T2, seed 1729/threads 1/height 16/hidden 64/batch 4/LR 0.003, 200 `TrainEpoch` calls and 1,000 updates per process. All finish by 17:27:25 CST, with 102 prepared-input checks, 1,200 finite losses and **6,000 additional engineering updates**. No development data, evaluation, CER, recognizer, checkpoint or quality candidate is used. This is separate from the 63,594 accuracy-experiment updates and 40 copied-branch recovery updates.

Frozen/current whole-process RSS is 308.41→203.13 MiB T0, 306.42→193.39 T1, 312.52→193.39 T2; training timers are 6.040→6.119s, 6.069→6.116s, 6.781→6.319s. The current bundle also has exact normalization optimization, so this is not isolated scope causality. One process per condition/variant, initialization/preprocessing included in peaks and a 1,000-update/no-validation workload do not reconstruct the original full-budget matrix's unknown training peaks. NT-20 remains partial for those and broader lifecycle/platform checks.

`p46-training-resources/` preserves registration, 17-ID/34-row train-only manifest, source/DLL/config hashes, 50 aggregate evidence files, raw process output and actual OS peaks/time. All 101 earlier resource files remain unchanged. A separate AI reviewer verifies registration before execution, all 14 DLL hashes, 6,000 updates, 1,200 finite losses, 102 input checks, absent checkpoints and exact table arithmetic, and accepts this bounded engineering append without findings. No new quality result or promotion is inferred.
