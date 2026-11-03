# Trainer lifecycle and training-alphabet fixes — 2026-11-02

The evening engineering work fixes two separate defects: the trainer had no explicit route to release its owned Adam state, and the CLI enlarged its alphabet using development labels. Neither correction changes the meaning of existing model weights, the shared pixel recipes, source images, scholarly labels or frozen experiments. No P46 fit or quality evaluation was run for these checks.

Baseline repository commit is `fc96d5bb2f901f8610ad421d5f95e640c784ea67`, with the already authorized dirty-worktree changes from earlier sessions. Exact before sources, narrow patches and current source hashes are retained under `.artifacts/experiments/trainer-lifecycle/`; the commit alone is not represented as the complete pre-fix code state.

## Explicit optimizer ownership

`CtcTrainer` creates and owns its Adam optimizer, while its model is supplied by the caller. The exact installed TorchSharp 0.107.0.0/libtorch 2.10.0.0 runtime probe warms a tiny synthetic model: there are 36 live tensor aliases, then direct disposal of the trainer-owned Adam releases 20 immediately to 16 without garbage collection. Recognition on the caller model remains usable. Before this fix, the trainer did not implement `IDisposable`, so callers could not express that final-use cleanup through its public API.

The trainer now implements idempotent `IDisposable`, disposing **only its optimizer**. The model remains caller-owned. `TrainEpoch`, evaluation, fitting, checkpoint saving/loading and restoring best reject a disposed trainer with `ObjectDisposedException` before model-mode changes, counters or filesystem operations. Read-only run metadata remains available. There is no new finalizer, forced production GC, forward arithmetic change or concurrent-use guarantee.

The CLI's training branch now declares its model and trainer with separate `using` declarations. Reverse disposal order releases the trainer before the model, including exception paths. Existing clients remain source/binary compatible; they must opt into `using`/`Dispose` to obtain deterministic final-use cleanup. Existing per-minibatch/recognition-call temporary tensor scopes remain intact.

`TrainerLifetimeTests` has ten cases. Against the old public API, the disposal helper uses `IDisposable` only when implemented, allowing the same regression source to compile before the fix. Nine fail for the intended defects: 20 optimizer aliases remain live, seven native/checkpoint operations continue, and a disposed fitting attempt creates its output directory. The checkpoint-resume control passes both versions. After the fix, all ten pass. Assertions cover no forced GC after disposal, idempotence, caller tensor/model usability, mode restoration, checkpoint byte preservation, absence of new output directories, exact subsequent loss/parameter agreement and optimizer resume on the same caller model. A nullability warning in the new test fixture was corrected; final candidate builds contain no warnings.

The retained-state case is the resource-defect reproduction. The remaining eight baseline failures show that the old API cannot express the newly defined disposed-state contract; they are not eight additional independently discovered memory leaks.

This demonstrates prompt release of owned tensor aliases, not a count of unique native allocations or a measured RSS reduction. Optimizer/trainer concurrent disposal, GPU behavior and large sustained workloads remain separately scoped resource work. The caller must still dispose its model after its own final use.

## Train-only alphabet and explicit development OOV

The CLI previously built vocabulary from `snapshot.Train.Concat(snapshot.Dev)`. A development-only glyph therefore became a supported output label before training, hiding a corpus-readiness problem. This is independent of the model's existing unknown-label checks and violates the training-only alphabet policy.

New CLI training builds the ordered alphabet from **training lines only**. Before model construction or checkpoint-path creation, inspect development grapheme clusters against that fixed alphabet. Any OOV aborts with exit 1, affected dataset-local source IDs, line/occurrence/distinct-grapheme counts, original glyphs and Unicode scalar codes. It does not drop or relabel lines, fold Greek case/sigma/marks, expand vocabulary from held-out labels or silently normalize text. A future alternative OOV scoring policy would need an explicit separately frozen design.

`LabeledLine` gains an optional `SourceId` init property; its existing two-argument constructor remains unchanged and null metadata is omitted in JSON. `P01LineDataset` populates `XML filename#imported line-ID suffix`, preserving identity through `with` copies. The suffix follows the existing importer's ID sanitization; this is a dataset-local diagnostic identity, not a global object ID or a new lossless external-ID contract. Pixel preparation, split assignment and label contents do not change.

The synthetic PAGE regression uses training labels `β,α`, two development lines containing three OOV occurrences (`χ` twice and decomposed `ά` once), and locked-only `ζ`. Before the fix, the CLI returns 0 and produces a package containing the development-only labels. Afterward it returns 1, produces no package and reports both exact source IDs plus the 3/2 occurrence/distinct counts and scalar codes. A compatible development subset still packages the original training order `β,α`, with no locked-only label. Source-ID propagation, null/old-constructor JSON compatibility and round trips pass. These fixtures are synthetic engineering examples, not Greek manuscript ground truth.

Existing packages/loaders retain their alphabets and behavior. The changed readiness check applies when creating a new CLI training run; it does not reinterpret an old checkpoint. Previously generated frozen artifacts remain unchanged.

## Verification and reproduction

| Check | Result |
| --- | --- |
| Lifecycle baseline, new ten-case fixture | 9 intended failures; 1 resume control passed |
| Lifecycle/mode/temporary tensor/recovery/end-to-end group | 23 passed |
| Alphabet baseline, first two CLI cases | 1 intended OOV failure; 1 compatible control passed |
| Alphabet/source/diagnostic/preprocessing CLI group | 14 passed |
| Full ML suite excluding long `LeviathanText.ML.Tests.P01*` parameter-search classes | **259 passed**, zero failures/skips |
| Entire CLI suite | **27 passed**, zero failures/skips |
| Final CLI build | **Zero warnings/errors** |

TRX receipts are `.artifacts/test-results/trainer-lifecycle/{trainer-lifetime-before,trainer-lifetime-final,training-alphabet-before,training-alphabet-after,ml-final,cli-final}.trx`. Full build/test output, synthetic exact-runtime probe and source receipts are in `.artifacts/experiments/trainer-lifecycle/` and `.artifacts/experiments/training-alphabet/`. `trainer-disposal.patch`, `cli-lifecycle.patch` and `cli-alphabet.patch` distinguish the narrow changes from earlier worktree edits.

Use cached restore, serial builds (`-m:1 -p:UseSharedCompilation=false`) and the named test filters to reproduce. To reproduce baseline failures, restore the preserved pre-fix trainer/CLI source in an isolated checkout, retaining the new regression source; do not modify the shared working tree or frozen training artifacts. The baseline alphabet test predates the separate new SourceId API test, so run its first two cases against the old API rather than present a missing-property compile failure as defect evidence.

No long P01 accuracy-search experiments or new manuscript/papyrus/inscription quality measurements are claimed. The changes provide lifecycle/readiness correctness; the reviewer-owned dataset and model acceptance gates remain open. No submission, release, human DCO certification or model promotion occurred.
