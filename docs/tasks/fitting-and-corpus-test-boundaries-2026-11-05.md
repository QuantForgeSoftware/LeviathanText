# TEST-01 fitting, corpus and locked-test boundaries — parallel implementation briefs

Status: implemented under the subsequent overnight authorization; no fitting or actual
corpus/locked-content reads were used for verification. See the
[implementation and generated proof report](../evaluation/fitting-and-corpus-test-boundaries-2026-11-05.md).
The role split and proposed contracts below preserve the historical planning brief;
the report states final interfaces (including named Required on Skippable attributes),
actual counts and remaining integration limits. Source baseline:
[read-only audit](../evaluation/remaining-fitting-test-audit-2026-11-05.md) and
`.artifacts/experiments/remaining-fitting-test-audit-v1/inventory.json`, with exact
source hashes/method lines. The audit indexed 74 declarations, 57 fitting-API paths
(three negative pre-update controls) and 54 update-capable paths. Do not interpret
these counts as test-case counts or a complete compiler call-graph proof.

The original brief authorized planning only. A later explicit overnight instruction
authorized the source implementation and harmless sentinel/generated-IO verification
recorded in the linked report. That authorization did not permit model fitting,
real corpus/locked-content reads, corpus scoring or a new locked claim. Actual fitting
still needs its own distinct authorization even after an opt-in mechanism exists.
Environment flags are execution gates, not permission.

## Shared contract to approve before edits

A publishes the attribute/API specification; B reviews it before either changes a
consumer. Root approves the loader's intentional safe-default behavior and caller
integration. Keep all existing FittingFact/FittingTheory names, namespace and
`LEVIATHANTEXT_RUN_FITTING=1` compatibility.

Three independent capabilities are proposed:

| Capability | Proposed exact flag | Grants | Does not grant |
| --- | --- | --- | --- |
| Fitting | `LEVIATHANTEXT_RUN_FITTING=1` | Explicit selected synthetic optimizer/gradient work | Corpus or locked access |
| Corpus | `LEVIATHANTEXT_RUN_CORPUS=1` | Explicit selected real image/XML/label reads in permitted partitions | Fitting or locked access |
| Locked evaluation | Existing `LEVIATHANTEXT_RUN_LOCKED=1` | Explicit preregistered selected reserved evaluation | Fitting, corpus access or checkpoint selection |

A owns the shared `TestCapabilities` flags (Fitting, Corpus, LockedEvaluation) and
single-discoverer `CapabilityFactAttribute(requiredCapabilities)` /
`CapabilityTheoryAttribute(requiredCapabilities)`. Existing FittingFact/Theory remain
compatible wrappers for the Fitting capability. Real-data fitting requires
Fitting|Corpus; reserved scoring requires Corpus|LockedEvaluation; a retained legacy
method doing both requires all three until separated. Do not stack multiple Fact
attributes on a method. Do not replace the independent locked flag with a blanket
“run expensive tests” flag.

Discovery must set Skip before test-class construction, static/helper setup, theory
data providers, native model allocation, file reads or output creation. Attribute
construction reads only exact flag values; it must not probe dataset directories,
load a model, create an output directory or infer authorization from installed data.
Pure codec tests remain ordinary tests and do not require these flags.

Root owns `scripts/verify-engineering.sh`, its Python verification tests, README,
solution/test-runner integration, user-facing opt-in instructions and shared state.
Preserve the runner's exact two class selections: `LabelCodecTests.` and
`LabelCodecInvariantTests.`. Neither instance expands those filters or runs the mixed
ML/Pilot projects wholesale.

## Instance A — attributes and ML generated fitting ownership

Own only:

- `tests/Shared/FittingTestAttributes.cs` and any new shared attribute-only file in
  that directory; no dependency/version change.
- `tests/LeviathanText.ML.Tests/LeviathanText.ML.Tests.csproj` for linking shared
  attributes, without changing existing packages.
- The 12 generated fitting/API class files and 36 indexed methods below. The full
  JSON identifies helper chains and negative controls. `Package()` fits before ten
  preprocessing assertions; disposed-operation tests can fit in setup. Zero learning
  rate or “expected exception” names do not establish no fitting.
- Generated loader controls in `BaselineDatasetTests.cs` and nonfitting methods of
  `PreprocessingPackageTests.cs` remain unchanged unless an approved fixture/API
  adaptation is necessary. B must not edit these files.
- New focused A regression/source-fixture files, with unique names agreed with B.

| Owned class file | Exact fitting/API method declarations |
| --- | --- |
| `tests/LeviathanText.ML.Tests/CheckpointIntegrityTests.cs` | `Loading_with_a_different_label_set_fails_as_model_incompatible` |
| `tests/LeviathanText.ML.Tests/ModelPackageTests.cs` | `Package_contains_artifacts_manifest_and_card_and_reloads`; `Package_without_manifest_uses_the_single_best_metadata`; `Missing_labels_in_metadata_are_rejected`; `Manifest_name_mismatch_is_reported_as_model_incompatible`; `Tampered_weights_are_rejected_by_the_manifest_digest` |
| `tests/LeviathanText.ML.Tests/PhotometricAugmentationTests.cs` | `Gamma_affects_only_opted_in_training_and_round_trips_in_the_manifest` |
| `tests/LeviathanText.ML.Tests/PreprocessingPackageTests.cs` | `Declared_recipe_round_trips_and_training_inference_tensors_match`; `Legacy_package_does_not_guess_recipe_from_description_and_raw_tensor_loading_is_preserved`; `Checkpoint_only_package_retains_explicit_profile`; `Unsupported_or_conflicting_explicit_contract_fails_before_loading_weights`; `Missing_required_profile_field_is_not_a_legacy_fallback`; `Typed_package_cannot_claim_legacy_format_that_old_runtimes_would_misinterpret`; `Resume_rejects_changed_or_unspecified_profile_and_preserves_matching_profile`; `Writer_rejects_profile_conflict_before_creating_output_package`; `Writer_checks_manifest_height_when_profile_is_inherited_from_checkpoint`; `Writer_rejects_an_unsupported_package_format_before_copying` |
| `tests/LeviathanText.ML.Tests/TensorLifetimeTests.cs` | `Collation_failure_releases_already_allocated_inputs_and_restores_mode`; `Scoped_training_has_stable_live_aliases_and_preserves_reference_adam_and_resume_parameters` |
| `tests/LeviathanText.ML.Tests/TrainerLifetimeTests.cs` | `Dispose_releases_optimizer_state_immediately_and_preserves_caller_resources`; `Disposed_operations_fail_before_native_state_or_checkpoint_mutation`; `Disposed_Fit_does_not_create_its_output_directory`; `A_new_trainer_can_resume_on_the_callers_model_after_old_trainer_disposal` |
| `tests/LeviathanText.ML.Tests/TrainingDiagnosticsTests.cs` | `Interval_final_epoch_resume_and_manifest_preserve_diagnostics`; `Early_stopped_final_epoch_is_measured_even_off_interval`; `Enabling_telemetry_does_not_change_dropout_training_or_checkpoint_selection` |
| `tests/LeviathanText.ML.Tests/TrainingEndToEndTests.cs` | `Train_SelectsBestCheckpoint_ThenReloadsAndRecognizes`; `Fit_ResumesFromCheckpoint_ContinuingTheSchedule` |
| `tests/LeviathanText.ML.Tests/TrainingModeTests.cs` | `Failed_training_restores_the_callers_mode`; `Resumed_training_uses_training_mode_even_if_caller_left_model_in_eval` |
| `tests/LeviathanText.ML.Tests/TrainingRecoveryTests.cs` | `Failure_during_validation_writes_reloadable_failure_checkpoint`; `Cancellation_writes_failure_checkpoint_and_rethrows` |
| `tests/LeviathanText.ML.Tests/TrainingRunManifestTests.cs` | `Manifest_captures_architecture_split_and_checkpoint_digest` |
| `tests/LeviathanText.ML.Tests/TrainingTargetTests.cs` | `Reaching_control_target_stops_at_first_epoch_and_records_final_diagnostics`; `Missing_or_unreached_target_keeps_the_fixed_epoch_budget`; `First_target_crossing_is_selected_even_inside_the_ordinary_improvement_tolerance` |

A's work:

1. Implement discovery attributes only after shared interface approval. Apply precise
   capabilities to actual fitting methods and helper-triggered setup. Keep negative
   pre-update/native-only paths classified explicitly; do not broaden default native
   execution merely because a path is not expected to update weights.
2. Prefer untrained serialization fixtures where fitting is incidental to a package
   metadata assertion. Declare them untrained engineering fixtures; do not substitute
   them for a test whose purpose genuinely needs an optimizer step.
3. After B freezes loader interfaces, adapt A-owned generated PAGE/package fixtures
   explicitly. Their generated “locked” paths can opt into a generated partition in
   an isolated sentinel test; that is not access to reserved corpus labels.
4. Keep both codec class files unchanged. Leave model/CTC behavior, checkpoints,
   source labels and production training policy untouched.

A acceptance, validated without fitting a real model:

- Default discovery produces skipped fitting cases before constructor/setup/output
  sentinels; shared public names and existing opted-in flag remain compatible.
- Guarding is method-accurate, including shared Package/local Run helpers. No model,
  gradient or optimizer call is executed simply to demonstrate that it would skip.
- B independently verifies the same attribute source with managed sentinels in a
  host containing no trainer/model references.
- Source/binary hashes, precise guarded declaration list, defaults and deliberately
  unsupported paths are handed off. No “all tests passed” statement from zero
  discovered/executed cases.

## Instance B — independent validation and data/locked loader ownership

Own only:

- All `tests/LeviathanText.Pilot.Tests/*.cs` and that project's `.csproj` for corpus/
  fitting/locked declaration changes; use A's shared attributes rather than editing
  the shared file.
- The nine data-backed ML P01 experiment files below. A does not edit these files.
- `src/LeviathanText.Training/P01LineDataset.cs`; a new narrowly scoped
  `DatasetPartitionAccess.cs` / `P01DatasetIo.cs` if approved; Training assembly friend
  metadata solely if needed for the generated internal IO boundary tests.
- New `tests/LeviathanText.ML.Tests/DatasetReadBoundaryTests.cs` (generated data only),
  with no edits to the A-owned ML project file. Its execution remains separately
  selected/reviewed, not added to the engineering runner automatically.
- A new independent `tests/LeviathanText.TestPolicy.Tests/` managed sentinel project,
  subject to root's project-integration approval. It links the shared attribute
  source, xUnit and ordinary managed IO only; no ML/Pilot/Training/Recognition/model
  reference. B owns this project and all sentinel fixtures.

Exact data-backed method ownership:

| Owned class file | Exact indexed declarations |
| --- | --- |
| `tests/LeviathanText.ML.Tests/P01AugmentationTests.cs` | `Compare_training_with_and_without_augmentation` |
| `tests/LeviathanText.ML.Tests/P01BatchSizeTests.cs` | `Compare_batch_sizes` |
| `tests/LeviathanText.ML.Tests/P01DecodingComparisonTests.cs` | `Compare_greedy_and_beam_on_the_dev_split` |
| `tests/LeviathanText.ML.Tests/P01DropoutTests.cs` | `Compare_dropout_values` |
| `tests/LeviathanText.ML.Tests/P01HiddenSizeTests.cs` | `Compare_hidden_sizes` |
| `tests/LeviathanText.ML.Tests/P01InputHeightTests.cs` | `Height_32_restores_text_and_time_resolution` |
| `tests/LeviathanText.ML.Tests/P01LearningRateTests.cs` | `Compare_learning_rates` |
| `tests/LeviathanText.ML.Tests/P01RegularizationTests.cs` | `Compare_weight_decay_values` |
| `tests/LeviathanText.ML.Tests/P01WidthDownsampleTests.cs` | `Reducing_width_downsample_improves_reachability_and_should_not_hurt_cer` |
| `tests/LeviathanText.Pilot.Tests/AutoLayoutPilotTests.cs` | `Detect_layout_on_a_real_page_and_refuse_overwrite` |
| `tests/LeviathanText.Pilot.Tests/AutoLayoutRecognitionPilotTests.cs` | `Image_only_page_gets_layout_then_recognition` |
| `tests/LeviathanText.Pilot.Tests/IiifPilotTests.cs` | `Real_manifest_parses_to_canvases_and_rights`; `Views_become_separate_captures_never_merged`; `Fetched_smoke_image_matches_the_manifest_view` |
| `tests/LeviathanText.Pilot.Tests/InscriptionPilotTests.cs` | `Audit_GreekRecords_WithCommentAwareImageAvailability`; `SmokeImage_Decodes_WithChecksum`; `Real_EpiDoc_Imports_ThroughBoundedProfile` |
| `tests/LeviathanText.Pilot.Tests/LabelHygienePilotTests.cs` | `Excluding_out_of_profile_lines_is_measured_on_dev` |
| `tests/LeviathanText.Pilot.Tests/LayoutComparisonPilotTests.cs` | `CompareAutomaticVsImportedLayout_OnBothTracks` |
| `tests/LeviathanText.Pilot.Tests/LayoutImpactPageCerTests.cs` | `Learned_layout_improves_page_cer_over_projection` |
| `tests/LeviathanText.Pilot.Tests/LayoutPostFilterPilotTests.cs` | `Dev_selected_postfilter_is_reported_on_held_out_pages` |
| `tests/LeviathanText.Pilot.Tests/LearnedLayoutPilotTests.cs` | `Learned_vs_projection_on_held_out_pages` |
| `tests/LeviathanText.Pilot.Tests/LockedTestEvaluationTests.cs` | `OneShot_ScoreLockedTest_AndPublishReport` |
| `tests/LeviathanText.Pilot.Tests/ManifestAuditPilotTests.cs` | `Repository_manifests_have_no_audit_errors` |
| `tests/LeviathanText.Pilot.Tests/ManuscriptAutoLayoutRecognitionTests.cs` | `Manuscript_page_gets_layout_then_recognition` |
| `tests/LeviathanText.Pilot.Tests/ManuscriptPilotTests.cs` | `Ingest_Train_AndScore_OnPageHoldout`; `CrossWitness_Generalization_TrainOnM02a_ScoreM01` |
| `tests/LeviathanText.Pilot.Tests/P01PilotTests.cs` | `All_27_page_xmls_import_as_diplomatic_lines`; `Page_and_image_files_pair_by_name`; `Extracted_real_line_crops_match_the_declared_geometry` |
| `tests/LeviathanText.Pilot.Tests/P01RealTrainingTests.cs` | `TrainsOnRealLines_ThenReloadsAndDecodes` |
| `tests/LeviathanText.Pilot.Tests/PageEndToEndPilotTests.cs` | `Page_cer_includes_layout_misses_across_seeds` |
| `tests/LeviathanText.Pilot.Tests/PatchLayoutPilotTests.cs` | `Patch_cnn_is_measured_against_projection_on_held_out_pages` |
| `tests/LeviathanText.Pilot.Tests/PipelineIntegrationTests.cs` | `Train_ThenRecognizeThroughPipeline_ThenScore` |

### Proposed loader interface, frozen before consumer edits

B proposes an explicit `DatasetPartitionAccess` value with TrainDev and
TrainDevAndLocked choices. Keep both existing public Load signatures callable,
including the original compiled five-argument overload. They delegate to a safe
TrainDev default. Add a distinct explicit method, avoiding ambiguous optional
parameter overloads:

`Snapshot LoadPartitions(string datasetDirectory, DatasetPartitionAccess access, string imageExtension = ".jpg", int testEveryNth = 5, int devEveryNth = 6, int? targetHeight = null, LinePreprocessingProfile? preprocessingProfile = null)`.

The loader must not consult test environment flags. Entry points combine the
approved capability with an explicit partition request; library callers retain a
clear API rather than inheriting process-global test policy. TrainDevAndLocked is
never chosen because files happen to exist. An explicit partition argument itself
is not authorization to perform a real reserved-data read in this assignment.

Safe default semantics are intentional: existing callers still compile/link, but
TestLocked is empty because its contents were not opened. Do not retain an unsafe
legacy full-read default under a compatibility alias. Root must approve/document
this behavior change before edits. Keep filename ordering, split assignment and
preprocessing identical for admitted witnesses: assign the original witness index
and partition **before filtering**, never renumber after excluding reserved files.

Add a non-positional nullable `Snapshot.LoadedPartitions` property of type
`DatasetPartitionAccess?`, preserving the existing positional constructor. Public
loader results set it explicitly; null means a legacy/manually constructed snapshot
has unknown read scope and grants no capability. It must say whether reserved
contents were loaded. TotalLines counts admitted loaded lines;
zero loaded locked lines does not mean the corpus has no locked lines. Root owns
CLI/manifests/readme followup so training summaries do not invent reserved line
counts, silently write “zero test lines”, or claim a new evaluation. No native
project schema/label migration is part of this interface.

For generated safety tests propose an internal `P01DatasetIo` boundary with exactly
four injectable operations:

- Enumerate XML witness paths, in a deterministic sorted list.
- Resolve a witness's image path without opening XML/image content.
- Read XML text for an admitted path.
- Decode an admitted image into the existing owned DecodedImage handle.

IO dependencies are per call, never process-global mutable hooks. Production
delegates preserve the current file operations. Recording delegates
return generated path descriptors/PAGE strings/tiny images and record opens; a
reserved-path read/decode delegate throws a sentinel immediately. B freezes exact
signatures with A/root before implementation. No recording test searches the real
.artifacts tree, checks whether real datasets are present, loads a checkpoint or
uses original corpus labels.

B's work:

1. Apply discovery capabilities to real-data reads, including read-only audits and
   report-writing comparisons. Preserve RUN_LOCKED as independent; a locked scorer
   that still retrains needs all relevant flags, or separate it into fitting and
   frozen-checkpoint evaluation entrypoints under root approval.
2. Move the partition admission before ReadXml/Decode. Default TrainDev must make
   zero calls on reserved XML/image paths, even if a full snapshot or count is later
   requested. Return only admitted labels/images; do not load then discard.
3. Keep source data/partition membership unchanged. Existing spent P01 remains
   spent. No guard, new method or receipt makes it fresh confirmation data.
4. Independently verify A's attributes with generated constructor/body/provider/
   output sentinels. Flag combinations run only this managed sentinel host, never
   real fitting/data methods. Simulated “fit reached” counters are not model fits.

B acceptance without model fitting or reserved labels:

- With all flags absent, sentinels show no constructor/helper/provider/body/output
  entry. Test discovery reports intentional skips; a zero-discovery run is failure.
- In the sentinel-only host, each flag alone enables only its own capability;
  Fitting does not grant Corpus/Locked, Corpus does not grant Fitting/Locked, and
  Locked alone does not grant Corpus/Fitting. Combinations enable only the exact
  generated combined fixture. Real experiment tests remain unexecuted.
- A recording loader with generated filenames checks all default train/dev reads,
  zero reserved XML/image opens, preserved original split indexes and deterministic
  preprocessing. Explicit full-partition sentinel mode demonstrates controlled
  generated reads only. Both missing-image and exception cleanup paths are bounded.
- Supplied reserved filenames, counts and checksum expectations do not trigger
  speculative reads. No ground truth is opened merely to prove the gate is safe.
- Corpus/locked rejection precedes native allocation, output mutation and model
  loading. Fresh output/refusal checks use temporary generated fixtures only.

## Fresh outputs and deliberate compatibility

Each explicitly selected experiment must receive a fresh absolute output path,
proposed as `LEVIATHANTEXT_TEST_OUTPUT_DIR`. Refuse an existing file/directory; do
not delete/reuse it, overwrite docs/evaluation or write into frozen package/corpus
locations. Reserve a unique child for each variant within that fresh experiment.
Concurrent experiment methods do not share one path without an approved reservation
protocol. Discovery/skip itself creates no output. Receipt paths and production
experiment artifacts remain distinct.

The existing RUN_FITTING and RUN_LOCKED spellings are retained. RUN_CORPUS and fresh
output selection are new explicit requirements; do not silently reinterpret an old
locked flag as permission for the fitting it currently happens to trigger. Preserve
legacy test method names where practical, with clear discovery skip reasons.

Illustrative **future commands only**, after distinct authorization; do not run them
as part of this brief:

- Default safety probe: unset all three flags and run only the new managed sentinel
  project with a fresh TRX destination. It must demonstrate skips before setup.
- Positive safety probe: set one/combined flags only for the managed sentinel host;
  counters/throwing recording delegates replace every real fitting/data operation.
- A deliberately authorized synthetic fit can retain the old invocation pattern:
  `LEVIATHANTEXT_RUN_FITTING=1 dotnet test tests/LeviathanText.ML.Tests --filter FullyQualifiedName=LeviathanText.ML.Tests.TrainingEndToEndTests.Train_SelectsBestCheckpoint_ThenReloadsAndRecognizes`, with a fresh output path. Do not enable Corpus or Locked.
- A separately authorized corpus-only audit requires RUN_CORPUS=1 and an exact named
  method. It must not set fitting/locked flags. Reserved scoring requires both
  Corpus and Locked, an explicit frozen-checkpoint/partition request and a fresh
  preregistered evidence destination; fitting remains disabled.

An inherited environment flag does not authorize work. Root's engineering runner
must keep the explicit pure-codec selection and cannot infer permission to fit from
these examples, a dataset folder, a previous run or green discovery counts.

## Dependencies, handoff and integration

1. Both instances read the hashed inventory; record source hashes and current dirty
   state. Agree the shared attribute and dataset IO/partition interfaces with root.
2. A freezes attribute names/flags/skip semantics first. B can prepare sentinel source
   and loader contracts in parallel, but does not edit A's files or consumers until
   that interface is stable.
3. B freezes the loader interface; A then adapts only its own generated fixture
   callers. B updates its nine P01/Pilot consumers. Root owns CLI callers, manifest
   scope wording, solution wiring, docs and runner selection.
4. Exchange concrete diffs, method ownership lists, hash manifests and disabled/
   sentinel receipts. Independent B checks must compile the actual current shared
   source; no retained before source may shadow it through default Compile globs.
5. Root reviews default-off behavior and both no-read/no-write proofs before any
   opt-in can be used on real work. A future separately authorized fitting or locked
   evaluation run gets its own plan, registered inputs/outputs and cleanup budget.

The subsequent authorized implementation now provides capability gates, explicit
TrainDev loader admission, generated IO proofs and fresh output reservations. Root owns
final engineering-runner integration and central verification. No real-data test,
model fitting, reserved scoring, training-label edit or fresh-confirmation claim was
performed to validate these changes. The linked report is the implementation handoff;
this historical brief alone never grants an opt-in scientific run.
