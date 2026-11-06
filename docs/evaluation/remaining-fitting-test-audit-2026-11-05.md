# Remaining mixed-suite fitting and data-entry audit — 2026-11-05

This is a **source-only TEST-01a inventory**, not guard implementation or test
execution. It indexes 74 method declarations in the mixed ML/Pilot projects:
57 reach fitting APIs through direct calls or helpers, including three negative
training-API controls expected to fail before an update. The other 54 can execute
optimization. None of these indexed methods has the shared discovery-time fitting
opt-in. Dataset presence is the usual real-data skip condition, not authorization.

No test, loader, optimizer, model, corpus asset or local label file was executed or
opened by this audit. Only tracked C# / project / engineering-script source and the
audit's own JSON were read. The preceding language review has separate execution
receipts; no additional tests ran during this audit.

## Scope and source identity

Inspected 43 ML and 18 Pilot C# source files, the two project files, shared fitting
attributes, engineering runner and the four training/layout/dataset implementation
helpers listed below. All 69 source/project/script hashes are recorded in
`.artifacts/experiments/remaining-fitting-test-audit-v1/inventory.json`. Each indexed
entry includes exact class/method, source path/line/SHA-256, current attribute/skip
conditions, data source and direct/helper call chains. Hashes describe this snapshot,
not a guarantee that future source is unchanged.

The declaration index is not a full C# compiler/call-graph proof. Theory declarations
count once regardless of parameter combinations. Manual source checks traced shared
helpers and constructors; absence from the index does not make another ML test safe
to add to the engineering allowlist.

## Call paths and traps

- `CtcTrainer` construction creates Adam state; `Fit` calls `TrainEpoch`, then
  validation. `TrainEpoch` runs loss backward and optimizer step. Validation failure
  and cancellation tests can therefore fit before their expected exception.
- `PreprocessingPackageTests.Package()` fits a generated model before ten nominal
  package/loader assertions. Names such as “fails before loading weights” describe
  the production assertion, not the earlier fixture preparation.
- `TrainerLifetimeTests.Disposed_operations_fail_before_native_state_or_checkpoint_mutation`
  fits in setup before checking every disposed operation. The separate
  `Disposed_Fit_does_not_create_its_output_directory` control truly expects rejection
  before an update; both remain native-training-API entrypoints for classification.
- `TensorLifetimeTests.Scoped_training_has_stable_live_aliases_and_preserves_reference_adam_and_resume_parameters`
  directly calls reference `loss.backward()` and `Adam.step()` as well as TrainEpoch.
- `LearnedLineDetector.Train` performs CPU row-logistic gradient updates. Training
  does not require TorchSharp. `PatchLayoutModel.Train` also performs backward/step.
- Model/package helpers that only serialize untrained random weights are not labelled
  fitting here. Creating tensors, evaluating a random model and constructing an
  optimizer are distinct capabilities from performing updates; none is newly allowed
  by this audit.

There are no MemberData/ClassData or module/assembly fixture hooks found in the two
source trees by this bounded search. Two constructors create temporary directories,
and P01PilotTests has a static data-directory lookup; future guards must prevent
unapproved construction/helpers rather than checking only immediately before Fit.

## ML generated-fixture fitting/API entrypoints

Data in this table is generated synthetic lines/images or temporary packages/PAGE.
Current attributes are ordinary Fact/Theory, without discovery-time opt-in.
“Negative” means the selected path expects rejection before an optimizer update;
it does not make the containing mixed class generally safe.

| Class.method | Source line | Attribute | Static assessment / helper |
| --- | ---: | --- | --- |
| `CheckpointIntegrityTests.Loading_with_a_different_label_set_fails_as_model_incompatible` | 15 | Fact | Fit / TrainEpoch reaches optimization |
| `ModelPackageTests.Package_contains_artifacts_manifest_and_card_and_reloads` | 24 | Fact | Fit / TrainEpoch reaches optimization |
| `ModelPackageTests.Package_without_manifest_uses_the_single_best_metadata` | 88 | Fact | Fit / TrainEpoch reaches optimization |
| `ModelPackageTests.Missing_labels_in_metadata_are_rejected` | 118 | Fact | Fit / TrainEpoch reaches optimization |
| `ModelPackageTests.Manifest_name_mismatch_is_reported_as_model_incompatible` | 147 | Fact | Fit / TrainEpoch reaches optimization |
| `ModelPackageTests.Tampered_weights_are_rejected_by_the_manifest_digest` | 175 | Fact | Fit / TrainEpoch reaches optimization |
| `PhotometricAugmentationTests.Gamma_affects_only_opted_in_training_and_round_trips_in_the_manifest` | 133 | Fact | Fit / TrainEpoch reaches optimization |
| `PreprocessingPackageTests.Declared_recipe_round_trips_and_training_inference_tensors_match` | 49 | Theory | Package() fits a generated one-line model before the nominal loader/metadata assertion |
| `PreprocessingPackageTests.Legacy_package_does_not_guess_recipe_from_description_and_raw_tensor_loading_is_preserved` | 96 | Fact | Package() fits a generated one-line model before the nominal loader/metadata assertion |
| `PreprocessingPackageTests.Checkpoint_only_package_retains_explicit_profile` | 116 | Fact | Package() fits a generated one-line model before the nominal loader/metadata assertion |
| `PreprocessingPackageTests.Unsupported_or_conflicting_explicit_contract_fails_before_loading_weights` | 133 | Theory | Package() fits a generated one-line model before the nominal loader/metadata assertion |
| `PreprocessingPackageTests.Missing_required_profile_field_is_not_a_legacy_fallback` | 144 | Fact | Package() fits a generated one-line model before the nominal loader/metadata assertion |
| `PreprocessingPackageTests.Typed_package_cannot_claim_legacy_format_that_old_runtimes_would_misinterpret` | 153 | Fact | Package() fits a generated one-line model before the nominal loader/metadata assertion |
| `PreprocessingPackageTests.Resume_rejects_changed_or_unspecified_profile_and_preserves_matching_profile` | 163 | Fact | Package() fits a generated one-line model before the nominal loader/metadata assertion |
| `PreprocessingPackageTests.Writer_rejects_profile_conflict_before_creating_output_package` | 180 | Fact | Package() fits a generated one-line model before the nominal loader/metadata assertion |
| `PreprocessingPackageTests.Writer_checks_manifest_height_when_profile_is_inherited_from_checkpoint` | 191 | Fact | Package() fits a generated one-line model before the nominal loader/metadata assertion |
| `PreprocessingPackageTests.Writer_rejects_an_unsupported_package_format_before_copying` | 207 | Fact | Package() fits a generated one-line model before the nominal loader/metadata assertion |
| `TensorLifetimeTests.Collation_failure_releases_already_allocated_inputs_and_restores_mode` | 70 | Theory | negative pre-update path |
| `TensorLifetimeTests.Scoped_training_has_stable_live_aliases_and_preserves_reference_adam_and_resume_parameters` | 90 | Fact | Direct reference loss.backward() and Adam.step() occur in addition to TrainEpoch |
| `TrainerLifetimeTests.Dispose_releases_optimizer_state_immediately_and_preserves_caller_resources` | 28 | Fact | Fit / TrainEpoch reaches optimization |
| `TrainerLifetimeTests.Disposed_operations_fail_before_native_state_or_checkpoint_mutation` | 63 | Theory | Fits setup before checking disposed-operation failures; method name is not evidence of no fitting |
| `TrainerLifetimeTests.Disposed_Fit_does_not_create_its_output_directory` | 103 | Fact | negative pre-update path |
| `TrainerLifetimeTests.A_new_trainer_can_resume_on_the_callers_model_after_old_trainer_disposal` | 122 | Fact | Fit / TrainEpoch reaches optimization |
| `TrainingDiagnosticsTests.Interval_final_epoch_resume_and_manifest_preserve_diagnostics` | 92 | Fact | Fit / TrainEpoch reaches optimization |
| `TrainingDiagnosticsTests.Early_stopped_final_epoch_is_measured_even_off_interval` | 130 | Fact | Fit / TrainEpoch reaches optimization |
| `TrainingDiagnosticsTests.Enabling_telemetry_does_not_change_dropout_training_or_checkpoint_selection` | 149 | Fact | Local Run helper executes Fit twice |
| `TrainingEndToEndTests.Train_SelectsBestCheckpoint_ThenReloadsAndRecognizes` | 25 | Fact | Fit / TrainEpoch reaches optimization |
| `TrainingEndToEndTests.Fit_ResumesFromCheckpoint_ContinuingTheSchedule` | 81 | Fact | Fit / TrainEpoch reaches optimization |
| `TrainingModeTests.Failed_training_restores_the_callers_mode` | 15 | Theory | negative pre-update path |
| `TrainingModeTests.Resumed_training_uses_training_mode_even_if_caller_left_model_in_eval` | 49 | Fact | Fit / TrainEpoch reaches optimization |
| `TrainingRecoveryTests.Failure_during_validation_writes_reloadable_failure_checkpoint` | 16 | Fact | Fit / TrainEpoch reaches optimization |
| `TrainingRecoveryTests.Cancellation_writes_failure_checkpoint_and_rethrows` | 60 | Fact | Fit / TrainEpoch reaches optimization |
| `TrainingRunManifestTests.Manifest_captures_architecture_split_and_checkpoint_digest` | 23 | Fact | Fit / TrainEpoch reaches optimization |
| `TrainingTargetTests.Reaching_control_target_stops_at_first_epoch_and_records_final_diagnostics` | 27 | Fact | Fit / TrainEpoch reaches optimization |
| `TrainingTargetTests.Missing_or_unreached_target_keeps_the_fixed_epoch_budget` | 63 | Theory | Fit / TrainEpoch reaches optimization |
| `TrainingTargetTests.First_target_crossing_is_selected_even_inside_the_ordinary_improvement_tolerance` | 123 | Theory | Fit / TrainEpoch reaches optimization |

## P01 ML experiments

All nine use SkippableFact and skip only when the P01 artifact directory is absent.
They call the full `P01LineDataset.Load`, then fit train/dev (some through
`TrainAndEvaluate`). Their comments explicitly describe dev experiments; merely
loading their snapshot still materializes the named locked partition.

| Class.method | Source line | Source / fitting |
| --- | ---: | --- |
| `P01AugmentationTests.Compare_training_with_and_without_augmentation` | 20 | P01 v0.9; Fit via full snapshot |
| `P01BatchSizeTests.Compare_batch_sizes` | 19 | P01 v0.9; Fit via full snapshot |
| `P01DecodingComparisonTests.Compare_greedy_and_beam_on_the_dev_split` | 23 | P01 v0.9; Fit via full snapshot |
| `P01DropoutTests.Compare_dropout_values` | 19 | P01 v0.9; Fit via full snapshot |
| `P01HiddenSizeTests.Compare_hidden_sizes` | 19 | P01 v0.9; Fit via full snapshot |
| `P01InputHeightTests.Height_32_restores_text_and_time_resolution` | 23 | P01 v0.9; Fit via full snapshot |
| `P01LearningRateTests.Compare_learning_rates` | 19 | P01 v0.9; Fit via full snapshot |
| `P01RegularizationTests.Compare_weight_decay_values` | 19 | P01 v0.9; Fit via full snapshot |
| `P01WidthDownsampleTests.Reducing_width_downsample_improves_reachability_and_should_not_hurt_cer` | 23 | P01 v0.9; Fit via full snapshot |

## Pilot data-backed fitting entrypoints

These methods have runtime file/directory/content-presence checks, not discovery
fitting/corpus authorization. The named locked-evaluation method additionally checks
`LEVIATHANTEXT_RUN_LOCKED=1` inside its body; that flag currently also enables its
training path rather than independently controlling fitting and scoring.

| Class.method | Source line | Data source | Current extra gate |
| --- | ---: | --- | --- |
| `AutoLayoutRecognitionPilotTests.Image_only_page_gets_layout_then_recognition` | 38 | P01 train/dev via full snapshot plus page image | presence only |
| `LabelHygienePilotTests.Excluding_out_of_profile_lines_is_measured_on_dev` | 30 | P01 train/dev via full snapshot | presence only |
| `LayoutImpactPageCerTests.Learned_layout_improves_page_cer_over_projection` | 133 | M02a training/held-out PAGE plus M01 comparison | presence only |
| `LearnedLayoutPilotTests.Learned_vs_projection_on_held_out_pages` | 28 | M02a and P01 page profiles/held-out truth | presence only |
| `LockedTestEvaluationTests.OneShot_ScoreLockedTest_AndPublishReport` | 20 | P01 full snapshot and named TestLocked scoring | runtime RUN_LOCKED=1; still no fitting opt-in |
| `ManuscriptAutoLayoutRecognitionTests.Manuscript_page_gets_layout_then_recognition` | 38 | M02a full snapshot and page image | presence only |
| `ManuscriptPilotTests.Ingest_Train_AndScore_OnPageHoldout` | 37 | M02a full snapshot; second method also M01 all splits | presence only |
| `ManuscriptPilotTests.CrossWitness_Generalization_TrainOnM02a_ScoreM01` | 90 | M02a full snapshot; second method also M01 all splits | presence only |
| `P01RealTrainingTests.TrainsOnRealLines_ThenReloadsAndDecodes` | 22 | P01 full snapshot, train/dev fitting | presence only |
| `PageEndToEndPilotTests.Page_cer_includes_layout_misses_across_seeds` | 179 | M02a train/dev and held-out PAGE/image truth | presence only |
| `PatchLayoutPilotTests.Patch_cnn_is_measured_against_projection_on_held_out_pages` | 51 | M02a train/dev/test; P01 and M01 comparisons | presence only |
| `PipelineIntegrationTests.Train_ThenRecognizeThroughPipeline_ThenScore` | 30 | P01 full snapshot train/dev fit then recognition | presence only |

## Other data/loader entrypoints in the index

No fitting call was traced for these methods. Corpus-backed pilot checks still need
explicit data access authorization; four ML loader controls use generated temporary
fixtures and should remain distinguishable from real-corpus tests. These are not new
engineering allowlist approvals.

| Class.method | Source line | Declared source | Current gate |
| --- | ---: | --- | --- |
| `BaselineDatasetTests.Loader_builds_normalized_image_and_multiclass_target` | 10 | generated synthetic lines/images or temporary package/PAGE fixtures | plain Fact; no discovery-time opt-in |
| `BaselineDatasetTests.Missing_image_is_reported_not_silently_skipped` | 59 | generated synthetic lines/images or temporary package/PAGE fixtures | plain Fact; no discovery-time opt-in |
| `PreprocessingPackageTests.Legacy_PAGE_loader_retains_strict_threshold_golden_pixels` | 218 | generated synthetic lines/images or temporary package/PAGE fixtures | plain Fact; no discovery-time opt-in |
| `PreprocessingPackageTests.Dataset_rejects_an_explicit_height_that_conflicts_with_its_profile` | 244 | generated synthetic lines/images or temporary package/PAGE fixtures | plain Fact; no discovery-time opt-in |
| `AutoLayoutPilotTests.Detect_layout_on_a_real_page_and_refuse_overwrite` | 24 | P01 artifact page image/PAGE | runtime dataset/fixture presence only; no explicit opt-in |
| `IiifPilotTests.Real_manifest_parses_to_canvases_and_rights` | 19 | bundled I.Sicily manifest/image fixtures | runtime dataset/fixture presence only; no explicit opt-in |
| `IiifPilotTests.Views_become_separate_captures_never_merged` | 39 | bundled I.Sicily manifest/image fixtures | runtime dataset/fixture presence only; no explicit opt-in |
| `IiifPilotTests.Fetched_smoke_image_matches_the_manifest_view` | 67 | bundled I.Sicily manifest/image fixtures | runtime dataset/fixture presence only; no explicit opt-in |
| `InscriptionPilotTests.Audit_GreekRecords_WithCommentAwareImageAvailability` | 29 | I01/I.Sicily repository EpiDoc and image assets | runtime dataset/fixture presence only; no explicit opt-in |
| `InscriptionPilotTests.SmokeImage_Decodes_WithChecksum` | 60 | I01/I.Sicily repository EpiDoc and image assets | runtime dataset/fixture presence only; no explicit opt-in |
| `InscriptionPilotTests.Real_EpiDoc_Imports_ThroughBoundedProfile` | 75 | I01/I.Sicily repository EpiDoc and image assets | runtime dataset/fixture presence only; no explicit opt-in |
| `LayoutComparisonPilotTests.CompareAutomaticVsImportedLayout_OnBothTracks` | 27 | P01 and M02a PAGE/images | runtime dataset/fixture presence only; no explicit opt-in |
| `LayoutPostFilterPilotTests.Dev_selected_postfilter_is_reported_on_held_out_pages` | 103 | M02a dev/held-out and P01 images/PAGE | runtime dataset/fixture presence only; no explicit opt-in |
| `ManifestAuditPilotTests.Repository_manifests_have_no_audit_errors` | 18 | repository dataset manifests | runtime dataset/fixture presence only; no explicit opt-in |
| `P01PilotTests.All_27_page_xmls_import_as_diplomatic_lines` | 31 | P01 PAGE, images, extracted source-labelled crops | runtime dataset/fixture presence only; no explicit opt-in |
| `P01PilotTests.Page_and_image_files_pair_by_name` | 58 | P01 PAGE, images, extracted source-labelled crops | runtime dataset/fixture presence only; no explicit opt-in |
| `P01PilotTests.Extracted_real_line_crops_match_the_declared_geometry` | 75 | P01 PAGE, images, extracted source-labelled crops | runtime dataset/fixture presence only; no explicit opt-in |

## Independent locked-data boundary

`P01LineDataset.Load` assigns a split to each witness and then reads its XML and
decodes its image before returning Train/Dev/TestLocked. A dev-only caller cannot
claim it avoided locked labels merely because it only scores Dev or reads
TestLocked.Count. Future authorized train/dev loading must skip locked witness
contents **before** XML/image reads, not materialize and discard them afterward.

Only `LockedTestEvaluationTests` has the explicit runtime locked flag. In source,
`ManuscriptPilotTests.Ingest_Train_AndScore_OnPageHoldout` scores TestLocked, and
`CrossWitness_Generalization_TrainOnM02a_ScoreM01` combines M01 TestLocked/Dev/Train
for sampled scoring without that flag. Other page-layout comparisons select held-out
truth manually and also lack an independent evaluation opt-in. These are source
observations; no labels or measured historical results were re-examined here.

Some P01 comments already disclose its old locked split as spent. New test guards
must not relabel that split as fresh, reopen review decisions or manufacture a new
locked evaluation claim. A locked-scoring flag also must not silently authorize Fit;
separate checkpoint fitting/selection from a preregistered frozen-checkpoint scoring
entrypoint, or require both independently authorized capabilities when a legacy test
still does both.

## Recommended future discovery policy — not implemented here

1. Keep the existing engineering runner's exact two-class codec allowlist unchanged:
   LabelCodecTests and LabelCodecInvariantTests. Do not run either mixed project
   wholesale or broaden filters based only on this text search.
2. Extend the existing shared FittingFact/FittingTheory discovery skip to all actual
   synthetic fitting methods/helpers, or replace nonessential fitted package fixtures
   with explicitly untrained fixture serialization. Guard construction/setup before
   it can fit or write artifacts. An environment value is an implementation gate,
   never a substitute for the user's authorization.
3. Add separate discovery-time corpus-access gates for real dataset/image/label
   methods, including read-only audits. A present .artifacts directory must not opt
   a machine into training, scoring, crop extraction or report overwrites.
4. Preserve an independent locked-evaluation gate. Require explicit selected scope,
   checkpoint/source hashes and a fresh receipt destination; train/dev access and
   fitting opt-ins must not imply locked access. Refactor full snapshot loading so
   unauthorized locked contents are never opened.
5. Review and classify native model/tensor-only and negative pre-update tests
   separately. Do not infer fitting from a constructor alone or infer safety from
   a method whose assertion expects an error after fitted fixture setup.
6. Verify guard discovery in a fresh subprocess with no training flag, assert tests
   are skipped before constructors/helpers, and instrument a deterministic sentinel
   at fitting/data boundaries. Verify opt-in behavior only under separately granted
   authorization; do not execute fitting to validate the default-off audit today.

Several pilot methods write evaluation reports/checkpoints when executed, including
tracked docs/evaluation paths. Future experiment entrypoints should require fresh
versioned evidence destinations; ordinary verification must not silently replace
historical reports. This report and JSON add no production/test guard, change no
current skip policy and close no TEST-01 implementation or locked-evaluation gate.
