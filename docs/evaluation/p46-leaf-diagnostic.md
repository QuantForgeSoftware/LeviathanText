# P46 prespecified leave-one-new-leaf diagnostic

Registered **2026-11-02 18:29:26 CST / 2026-11-03 00:29:26 UTC**, before the evening old/expanded comparison scores or any held-new-leaf score was consumed. This is an artifact-only C# diagnostic, not another parameter search, blind acceptance evaluation or model-promotion gate.

The question is whether a model trained on the old leaf82 and the other new leaves recognizes an omitted new proposed leaf differently from the already exposed development leaf81. Better performance on the omitted new leaves would support investigating a leaf-specific appearance/coverage gap; it would not prove faintness as the cause. Label, geometry, text-content and training-composition differences remain confounds. Poor performance on all omitted leaves would support a broader within-witness generalization problem. Report every fold without selecting a favorable one.

## Immutable preregistration

Artifact folder: `.artifacts/experiments/p46-evening-leaf-diagnostic/`.

`registration.json` SHA-256: **`5fe4e1646d9d16cec2886e55f7b7fe51425c65593854b15b8ef7dde51bfd7bf1`**. It records exact training, omitted-leaf and original-development IDs, settings, stopping boundaries and source/QA receipts. The original registration is not revised after scores.

| Omitted proposed leaf | Omitted surfaces | Training leaves | Unique train lines | Held lines | Whole-list copies | Batches/epoch | Epochs | Updates / presentations |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
|83|1470,1480|82,84,85|32|12|1|8|663|5,304 / 21,216|
|84|1490,1500|82,83,85|34|10|2|17|312|5,304 / 21,216|
|85|1510,1520|82,83,84|39|5|4|39|136|5,304 / 21,216|

Both surfaces of each omitted proposed leaf remain excluded from that model's training. The original17 training lines on leaf82 are included in every fold. Page1440/leaf81 remains the same seven exposed validation lines, never training. Uniform whole-list copies keep full four-line batches; they do not increase unique corpus counts or change each unique training line's relative weight. They do change sampling/within-batch repetition, which is part of the disclosed fixed workload.

All folds use seed1729, CPU threads2, raw luminance height16 with external geometry, hidden64, width downsample4, dropout0, batch4, Adam LR0.003, weight decay0 and no augmentation. The 5,304-update budget is the common multiple of8,17,39 batches below6,000. No epoch target or early stopping truncates it: patience exceeds the fixed epoch budget and the CER target is absent. The original17 training lines alone define the25-label alphabet plus blank; report OOVs and abort rather than expand vocabulary, relabel or discard an evaluation line. Repeat-aware CTC feasibility is checked before fitting.

The source/crop material is the exact [independently checked provisional expansion](p46-expanded-corpus-review.md):27 new reference rows /926 scalars from the staged Michigan images. The corpus132-file freeze, independent QA allowlist and original v2 receipt are checked in full. Per-ID reference/crop/source digests must agree with the independent QA record. Original source notices, geometry/maps and exclusion reasons remain separate from accepted diplomatic labels, which are still blank.

## Execution and scoring boundary

Execution waits until the main raw-height16 technical control passes, all six registered old/expanded runs complete their5,984 updates, root reviews this harness, and a separate execution receipt is frozen. This dependency checks only structural completion and reload parity, not whether the main scores pass a screen. The diagnostic is performed regardless of the direction of the completed main comparison if its prerequisites and time budget permit.

`prepare` verifies sources and freezes51 input hashes, source-code closure, managed DLL snapshots, dataset/registration hashes, main protocol/control-result hashes and all six main-result hashes. It constructs no model. `run 83|84|85` verifies that receipt before fitting and refuses to overwrite an existing run folder. It does not start at/after20:03 CST and checks the absolute20:07:20 CST cutoff while accessing training/validation lines.

During `Fit`, **only the original exposed leaf81** is validation input. Its development-selected checkpoint is a secondary diagnostic. The omitted new leaf is scored only after all5,304 updates, at the final checkpoint; it is never used to select an epoch, change a recipe or stop a run. The primary retained outputs are final training, original-development and held-new-leaf canonical scalar edits/CER, detailed diagnostics and per-line predictions. Latest-checkpoint reload must reproduce all final decoded strings. The selected original-development checkpoint may be reported separately, with its update count, but the omitted leaf is not evaluated at that selected state.

Full histories, final/latest and secondary-best checkpoints, final per-line strings, exact input/profile hashes and actual update/presentation counts remain in the artifact folder. Runtime peak-memory zero is explicitly unavailable; any OS resource measurement must state its scope. No checkpoint becomes a production default or receives a redistribution license from this diagnostic.

## Preparation verification

Cached serial build of `LeafDiagnostic.csproj` succeeded with **zero warnings/errors** on2026-11-02. No model was constructed or fit by the build. Its only compile item is `Program.cs`, so source-snapshot copies do not accidentally enter later builds. The artifact project references the C# Training library; there is no Python training dependency.

The registered code includes program guards for the passing main control and all six expected condition/seed results at5,984 updates, and pins those dependencies in `execution.json`. Preparation and training were deliberately deferred pending the main comparison and root's execution instruction. Results and an execution digest are appended only after the registered runs; absent results do not constitute a passed diagnostic.

## Limits

These are proposed physical-leaf groups from source crosswalks, with AI-reviewed geometry and unadjudicated references. All source images/TEIs were already exposed during corpus preparation. One seed,5–12 omitted lines per fold, one witness and likely one hand cannot establish broad Greek accuracy, causal faintness effects, unseen-hand/witness performance, Classical Greek coverage or inscription support. No new earlier-leaf78–80 content is opened. A named Greek specialist, independent scholarly adjudication and fresh evaluator-owned acceptance data remain separate prerequisites for reliable-use claims.

## Completed results — 2026-11-02 evening

All three registered folds completed exactly **5,304 updates /21,216 presentations** with latest-checkpoint reload agreement. No recipe, seed, fold, budget or exclusion changed after registration, and no retry occurred. The three models add **15,912 diagnostic updates /63,648 presentations**, separate from the main old/expanded comparison and technical control. No model is promoted.

| Omitted proposed leaf | Final training edits/scalars / CER | Final held-leaf edits/scalars / CER | Same model on original leaf81 / CER |
| --- | ---: | ---: | ---: |
|83|47/1,086 / **4.33%**|355/418 / **84.93%**|176/219 / **80.37%**|
|84|82/1,172 / **7.00%**|259/332 / **78.01%**|174/219 / **79.45%**|
|85|78/1,328 / **5.87%**|137/176 / **77.84%**|187/219 / **85.39%**|

All three omitted new proposed leaves are poorly recognized, as is original leaf81. This supports investigating **broader within-witness generalization failure** rather than treating page1440's faintness as a complete explanation. It does not determine how much of the error comes from appearance, source/geometry uncertainty, character/context coverage, limited training data, fitting or model design. Training error remains4–7% as well; these results do not prove near-complete fitting of the larger fold datasets.

Pooling the27 distinct omitted rows across their three final models gives **751 edits /926 reference scalars =81.10% CER**. This is a disclosed diagnostic aggregate, not a new test benchmark. The seven original-development lines are scored under three different models; their21 predictions are not21 independent lines or a seed-spread experiment. Per-leaf and per-line results remain authoritative.

| Omitted proposed leaf | Fit time, seconds | Whole-process maximum RSS, MiB |
| --- | ---: | ---: |
|83|35.10|208.23|
|84|35.41|216.81|
|85|34.67|212.72|

These are one fresh macOS CPU process per fold with two Torch threads. Fit time includes the registered Fit work, validation and checkpointing. Darwin `/usr/bin/time -l` peaks cover initialization, source verification/preparation, fit, final evaluations and reload work; they are not isolated optimizer or steady-state memory values. No cross-platform, speed-regression or memory-improvement claim follows from comparisons between these rows.

### Frozen execution and verification

`execution.json` was saved at **18:45:12 CST**, before constructing any diagnostic model. SHA-256 **`ea970f01c825ac5ab2d40d1135a30be1d38888ff805cd2339c4b9d6050283479`** binds97 source files, eight managed DLLs,51 prepared-input hashes, dataset and registration, plus the main protocol/control/six-result digests. Main technical control and all six structural-completion guards passed; their score direction was not an execution gate. The registration SHA remains unchanged.

The final strings in `runs/held-{83,84,85}/final-predictions.json` contain51 uniquely identified train/original-development/held rows per model. Independently recomputing canonical scalar distance and reference length for all **153 final predictions** reproduces every stored scalar score. All latest reloads match. `summary.json` records the per-partition edits/lengths, raw detailed diagnostics, process resources and154 stable evidence file sizes/hashes. Its SHA-256 is **`afa7290d44c428dbfe507af7f4d0daf632a9f337bbc90f28ea0d6c322b72c322`**.

Root/integrator retains independent audit and plan/status ownership. The source labels, proposed leaf groups and geometry remain unadjudicated; these engineering results close this prespecified diagnostic without closing specialist, corpus or scholarly release gates.
