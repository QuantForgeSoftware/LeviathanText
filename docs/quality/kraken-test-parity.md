# Kraken test parity matrix

Date: 2026-10-21. Source: local Kraken checkout at `bb487219e7d8289274443d1ad0989efb4924dc2a`.

This is a behavioral parity plan, not a claim that every Kraken test is applicable. Tests are reimplemented as C#/xUnit scenarios; no Python test code, fixtures or implementation are copied. Kraken is Apache-2.0.

## Summary

| Module | Tests | Status | Leviathan target |
| --- | ---: | --- | --- |
| `test_align.py` | 9 | feature-gap | forced alignment / region assignment |
| `test_arrow_dataset.py` | 8 | not-applicable | Kraken Apache Arrow dataset backend; out of local C# scope |
| `test_binarization.py` | 3 | partial | image preprocessing thresholds/BW handling |
| `test_cli.py` | 11 | replicate | CLI output modes, segmentation/recognition failures |
| `test_codec.py` | 11 | partial | label codec strictness/unknown labels |
| `test_container.py` | 16 | partial | baseline ↔ bbox geometry + direction |
| `test_dataset.py` | 15 | partial | class-map filtering/merging and target rasterization; image transforms/loader remain |
| `test_ketos_arch.py` | 25 | not-applicable | Kraken architecture registry/Torch checkpoint internals |
| `test_ketos_training.py` | 3 | feature-gap | C# training smoke equivalents |
| `test_layers.py` | 12 | not-applicable | PyTorch layer implementations |
| `test_lineest.py` | 3 | partial | bounded centroid dewarper; full center-normalizer parity remains |
| `test_loaders.py` | 20 | replicate | model package/version/weights validation |
| `test_merging.py` | 2 | feature-gap | line merge/resize behavior |
| `test_models.py` | 1 | replicate | invalid model rejection |
| `test_newpolygons.py` | 24 | not-applicable | legacy Kraken model/polygon compatibility; no Kraken weights |
| `test_pageseg.py` | 2 | partial | bi-level bbox contract; full page-seg model remains |
| `test_plugins.py` | 1 | not-applicable | Python entry-point discovery; no first-party Python |
| `test_readingorder.py` | 15 | partial | geometric reading order + topological sort |
| `test_repo.py` | 3 | feature-gap | remote model registry/catalog |
| `test_rpred.py` | 37 | partial | record ordering primitive; cuts/bbox/baseline records and full BiDi remain |
| `test_segmentation_metrics.py` | 5 | partial | detection metrics empty/extra/tolerance/aggregate |
| `test_serialization.py` | 4 | partial | PAGE/ALTO/hOCR/ABBYY serialization |
| `test_tasks.py` | 34 | feature-gap | recognition/segmentation/forced-alignment task models |
| `test_train_utils.py` | 2 | partial | failure/cancellation checkpoints; Lightning callback parity not applicable |
| `test_vgsl.py` | 7 | not-applicable | Kraken VGSL model format; Leviathan uses its own package |
| `test_xml.py` | 32 | partial | PAGE/ALTO parsing edge cases |

**Total: 305 tests across 26 modules.**

Status vocabulary: `partial` = core scenarios implemented, integration/remaining cases open; `replicate` = behavioral scenarios can be written now; `feature-gap` = requires new capability first; `not-applicable` = Python/Torch/Kraken-internal or intentionally different architecture.

## Full inventory

### `test_align.py`

- `TestKrakenAlign.test_forced_align_deprecation_warning`
- `TestKrakenAlign.test_forced_align_returns_segmentation`
- `TestKrakenAlign.test_forced_align_record_count`
- `TestKrakenAlign.test_forced_align_records_are_baseline_ocr`
- `TestKrakenAlign.test_forced_align_simple`
- `TestKrakenAlign.test_forced_align_xml_document`
- `TestKrakenAlign.test_forced_align_unencodable_text`
- `TestKrakenAlign.test_forced_align_display_order`
- `TestKrakenAlign.test_forced_align_empty_segmentation`

### `test_arrow_dataset.py`

- `TestKrakenArrowCompilation.test_build_path_dataset`
- `TestKrakenArrowCompilation.test_build_xml_dataset`
- `TestKrakenArrowCompilation.test_build_xml_bbox_dataset`
- `TestKrakenArrowCompilation.test_build_seg_dataset`
- `TestKrakenArrowCompilation.test_forced_type_dataset`
- `TestKrakenArrowCompilation.test_build_empty_dataset`
- `TestKrakenArrowCompilation.test_force_type_mismatch_warning`
- `TestKrakenArrowCompilation.test_build_image_error`

### `test_binarization.py`

- `TestBinarization.test_not_binarize_empty`
- `TestBinarization.test_not_binarize_bw`
- `TestBinarization.test_binarize`

### `test_cli.py`

- `TestCLI.test_binarize_color`
- `TestCLI.test_binarize_bw`
- `TestCLI.test_segment_color`
- `TestCLISegmentation.test_segment_baseline_native_output`
- `TestCLISegmentation.test_segment_baseline_alto_output`
- `TestCLISegmentation.test_segment_baseline_text_direction`
- `TestCLIRecognition.test_ocr_no_segmentation_mode`
- `TestCLIRecognition.test_ocr_pipeline_segment_then_ocr`
- `TestCLIRecognition.test_ocr_output_formats`
- `TestCLIRecognition.test_ocr_xml_input_linetype`
- `TestCLIRecognition.test_ocr_missing_model_fails`

### `test_codec.py`

- `TestCodec.test_encode`
- `TestCodec.test_decode`
- `TestCodec.test_decode_invalid_nonstrict`
- `TestCodec.test_encode_invalid_nonstrict`
- `TestCodec.test_decode_invalid_strict`
- `TestCodec.test_encode_invalid_strict`
- `TestCodec.test_codec_add_simple`
- `TestCodec.test_codec_add_multiple`
- `TestCodec.test_codec_merge_both`
- `TestCodec.test_codec_merge_add`
- `TestCodec.test_codec_merge_remove`

### `test_container.py`

- `TestBaselineToBBox.test_boundary_extents`
- `TestBaselineToBBox.test_falls_back_to_baseline`
- `TestBaselineToBBox.test_falls_back_on_empty_boundary`
- `TestBaselineToBBox.test_preserves_shared_fields`
- `TestBaselineToBBox.test_text_direction_is_respected`
- `TestBaselineToBBox.test_does_not_mutate_input`
- `TestBBoxToBaseline.test_placement`
- `TestBBoxToBaseline.test_preserves_shared_fields`
- `TestBBoxToBaseline.test_degenerate_bbox`
- `TestBBoxToBaseline.test_does_not_mutate_input`
- `TestSegmentationCasts.test_baselines_to_bbox_round_trip`
- `TestSegmentationCasts.test_bbox_to_baselines_round_trip`
- `TestSegmentationCasts.test_to_baselines_delegates_topline`
- `TestSegmentationCasts.test_passthrough_fields`
- `TestSegmentationCasts.test_casts_produce_independent_segmentations`
- `TestSegmentationCasts.test_empty_lines`

### `test_dataset.py`

- `TestBaselineSet.test_baselineset_simple_xml`
- `TestBaselineSet.test_baselineset_filter_baselines`
- `TestBaselineSet.test_baselineset_filter_regions`
- `TestBaselineSet.test_baselineset_merge_baselines`
- `TestBaselineSet.test_baselineset_merge_and_filter_baselines`
- `TestBaselineSet.test_baselineset_merge_and_filter_regions`
- `TestBaselineSet.test_baselineset_canonical_class_mapping_no_merging`
- `TestBaselineSet.test_baselineset_canonical_class_mapping_with_merging`
- `TestBaselineSet.test_baselineset_merged_classes_empty`
- `TestBaselineSet.test_baselineset_merged_classes_with_merging`
- `TestBaselineSet.test_baselineset_empty_baselines_and_regions`
- `TestBaselineSet.test_baselineset_invalid_class_mapping`
- `TestInputTransforms.test_imageinputtransforms`
- `TestInputTransforms.test_imageinputtransforms_channel_height`
- `TestInputTransforms.test_imageinputtransforms_invalid_channels`

### `test_ketos_arch.py`

- `TestUserSuppliedParams.test_seeded_defaults_are_filtered`
- `TestUserSuppliedParams.test_cli_values_are_explicit`
- `TestUserSuppliedParams.test_yaml_values_are_explicit`
- `TestUserSuppliedParams.test_cli_overrides_yaml`
- `TestArchRegistry.test_resolve_arch`
- `TestArchRegistry.test_resolve_arch_segmentation`
- `TestArchRegistry.test_arch_names_are_task_scoped`
- `TestArchRegistry.test_resolve_unknown_arch`
- `TestArchRegistry.test_resolve_arch_wrong_task`
- `TestArchRegistry.test_trainer_classvars`
- `TestArchDetection.test_checkpoint_detection_ppocr`
- `TestArchDetection.test_checkpoint_detection_vgsl`
- `TestArchDetection.test_checkpoint_detection_pretrain_decoy`
- `TestArchDetection.test_checkpoint_without_config`
- `TestArchDetection.test_weights_detection_ppocr`
- `TestArchDetection.test_weights_detection_vgsl`
- `TestArchDetection.test_weights_detection_shared_model_class`
- `TestArchDetection.test_weights_detection_task_scoped`
- `TestArchDetection.test_weights_detection_legacy_fallback`
- `TestKetosArchCLI.test_foreign_option_vgsl_rejected_for_ppocr`
- `TestKetosArchCLI.test_foreign_option_ppocr_rejected_for_vgsl`
- `TestKetosArchCLI.test_arch_conflict_with_weights`
- `TestKetosArchCLI.test_arch_conflict_with_checkpoint`
- `TestKetosArchCLI.test_non_recognition_checkpoint_rejected`
- `TestKetosArchCLI.test_ketos_train_ppocr_smoke`

### `test_ketos_training.py`

- `TestKetosTraining.test_ketos_train_recognition_smoke`
- `TestKetosTraining.test_ketos_train_segmentation_smoke`
- `TestKetosTraining.test_ketos_train_reading_order_smoke`

### `test_layers.py`

- `TestLayers.test_maxpool`
- `TestLayers.test_1d_dropout`
- `TestLayers.test_2d_dropout`
- `TestLayers.test_rnn_layer`
- `TestLayers.test_linsoftmax`
- `TestLayers.test_linsoftmax_aug`
- `TestLayers.test_actconv2d_lin`
- `TestLayers.test_actconv2d_sigmoid_logits`
- `TestLayers.test_actconv2d_tanh`
- `TestLayers.test_actconv2d_softmax`
- `TestLayers.test_actconv2d_relu`
- `TestLayers.test_output_layer_resize`

### `test_lineest.py`

- `TestLineest.test_dewarp`
- `TestLineest.test_dewarp_fail_color`
- `TestLineest.test_dewarp_bw_undewarpable`

### `test_loaders.py`

- `TestCreateModel.test_create_model_invalid_name`
- `TestCreateModel.test_create_model_torch_vgsl`
- `TestLoadModels.test_load_models_coreml`
- `TestLoadModels.test_load_models_invalid_file`
- `TestLoadModels.test_load_models_nonexistent_file`
- `TestLoadModels.test_load_models_task_filter`
- `TestLoadModels.test_load_models_task_filter_mismatch`
- `TestLoadModels.test_load_fp32`
- `TestLoadModels.test_load_fp16`
- `TestLoadModels.test_load_mixed`
- `TestLoadModels.test_load_safetensors_invalid_tasks_metadata`
- `TestLoadModels.test_load_coreml_missing_model_type_metadata`
- `TestLoadModels.test_load_coreml_legacy_no_kraken_meta`
- `TestVersionCompatibility.test_all_compatible`
- `TestVersionCompatibility.test_all_incompatible`
- `TestVersionCompatibility.test_incompatible_model_warns`
- `TestVersionCompatibility.test_compatible_model_has_weights`
- `TestWriteModels.test_write_read_roundtrip_safetensors`
- `TestWriteModels.test_roundtrip_fp16`
- `TestWriteModels.test_roundtrip_mixed`

### `test_merging.py`

- `TestMerging.test_no_resize_fails`
- `TestMerging.test_merging`

### `test_models.py`

- `TestModels.test_load_invalid`

### `test_newpolygons.py`

- `TestNewPolygons.test_rpred_from_old_model`
- `TestNewPolygons.test_rpred_from_old_model_force_new`
- `TestNewPolygons.test_rpred_from_new_model`
- `TestNewPolygons.test_krakencli_ocr_old_model`
- `TestNewPolygons.test_krakencli_ocr_old_model_force_new`
- `TestNewPolygons.test_krakencli_ocr_new_model`
- `TestNewPolygons.test_krakencli_ocr_bbox_model_default`
- `TestNewPolygons.test_ketoscli_test_old_model`
- `TestNewPolygons.test_ketoscli_test_old_model_force_new`
- `TestNewPolygons.test_ketoscli_test_new_model`
- `TestNewPolygons.test_ketoscli_test_bbox_model_default`
- `TestNewPolygons.test_ketoscli_train_new_model`
- `TestNewPolygons.test_ketoscli_train_new_model_force_legacy`
- `TestNewPolygons.test_ketoscli_train_old_model`
- `TestNewPolygons.test_ketoscli_train_old_model_force_legacy`
- `TestNewPolygons.test_ketoscli_pretrain_new_model`
- `TestNewPolygons.test_ketoscli_pretrain_new_model_force_legacy`
- `TestNewPolygons.test_ketoscli_pretrain_old_model`
- `TestNewPolygons.test_ketos_old_arrow_train_new`
- `TestNewPolygons.test_ketos_new_arrow`
- `TestNewPolygons.test_ketos_new_arrow_force_legacy`
- `TestNewPolygons.test_ketos_old_arrow_old_model`
- `TestNewPolygons.test_ketos_new_arrow_old_model`
- `TestNewPolygons.test_ketos_mixed_arrow_train_new`

### `test_pageseg.py`

- `TestPageSeg.test_segment_color`
- `TestPageSeg.test_segment_bw`

### `test_plugins.py`

- `test_entry_point_group`

### `test_readingorder.py`

- `TestReadingOrder.test_is_in_region`
- `TestReadingOrder.test_is_in_region2`
- `TestReadingOrder.test_is_in_region3`
- `TestReadingOrder.test_order_simple_over_under`
- `TestReadingOrder.test_order_simple_over_under_touching`
- `TestReadingOrder.test_order_simple_left_right`
- `TestReadingOrder.test_order_simple_left_right_touching`
- `TestReadingOrder.test_order_simple_right_left`
- `TestReadingOrder.test_order_simple_right_left_touching`
- `TestReadingOrder.test_order_real_reverse`
- `TestReadingOrder.test_order_real_in_order`
- `TestReadingOrder.test_topsort_ordered`
- `TestReadingOrder.test_topsort_ordered_no_self`
- `TestReadingOrder.test_topsort_unordered`
- `TestReadingOrder.test_topsort_unordered_no_self`

### `test_repo.py`

- `TestRepo.test_listing`
- `TestRepo.test_get_description`
- `TestRepo.test_prev_record_version_get_description`

### `test_rpred.py`

- `TestBBoxRecords.test_arabic_bbox_record_cuts`
- `TestBBoxRecords.test_arabic_bbox_record_display_identity`
- `TestBBoxRecords.test_arabic_bbox_record_logical_identity`
- `TestBBoxRecords.test_arabic_bbox_record_display`
- `TestBBoxRecords.test_arabic_bbox_record_logical`
- `TestBBoxRecords.test_arabic_bbox_record_slicing`
- `TestBBoxRecords.test_arabic_bbox_record_step_slicing`
- `TestBBoxRecords.test_latin_bbox_record_display_identity`
- `TestBBoxRecords.test_latin_bbox_record_logical_identity`
- `TestBBoxRecords.test_latin_bbox_record_slicing`
- `TestBBoxRecords.test_latin_bbox_record_step_slicing`
- `TestBaselineRecords.test_arabic_baseline_record_construction`
- `TestBaselineRecords.test_arabic_baseline_record_display_identity`
- `TestBaselineRecords.test_arabic_baseline_record_logical_identity`
- `TestBaselineRecords.test_arabic_baseline_record_display_to_logical`
- `TestBaselineRecords.test_arabic_baseline_record_logical_to_display`
- `TestBaselineRecords.test_arabic_baseline_record_roundtrip`
- `TestBaselineRecords.test_arabic_baseline_short_record_logical`
- `TestBaselineRecords.test_arabic_baseline_record_slicing`
- `TestBaselineRecords.test_arabic_baseline_record_step_slicing`
- `TestBaselineRecords.test_arabic_baseline_logical_order_slicing`
- `TestBaselineRecords.test_latin_baseline_record_display_identity`
- `TestBaselineRecords.test_latin_baseline_record_logical_identity`
- `TestBaselineRecords.test_latin_baseline_record_slicing`
- `TestBaselineRecords.test_latin_baseline_record_step_slicing`
- `TestRecognition.test_rpred_bbox_outbounds`
- `TestRecognition.test_rpred_bl_outbounds`
- `TestRecognition.test_simple_bbox_rpred`
- `TestRecognition.test_simple_bl_rpred`
- `TestRecognition.test_mm_rpred_bbox_missing_tags`
- `TestRecognition.test_mm_rpred_bl_missing_tags`
- `TestRecognition.test_mm_rpred_bbox_ignore_tags`
- `TestRecognition.test_mm_rpred_bbox_default_tags`
- `TestRecognition.test_mm_rpred_bl_ignore_tags`
- `TestRecognition.test_mm_rpred_bl_default_tags`
- `TestRecognition.test_mm_rpred_bl_nobidi`
- `TestRecognition.test_mm_rpred_bbox_nobidi`

### `test_segmentation_metrics.py`

- `TestSegmentationMetrics.test_compute_detection_metrics_empty_cases`
- `TestSegmentationMetrics.test_compute_detection_metrics_simple_match`
- `TestSegmentationMetrics.test_compute_detection_metrics_extra_prediction_penalizes_precision`
- `TestSegmentationMetrics.test_compute_detection_metrics_monotonicity`
- `TestSegmentationMetrics.test_aggregate_detection_metrics`

### `test_serialization.py`

- `TestSerializations.test_serialize_alto`
- `TestSerializations.test_serialize_pagexml`
- `TestSerializations.test_serialize_abbyyxml`
- `TestSerializations.test_serialize_hocr`

### `test_tasks.py`

- `TestRecognitionTaskModel.test_load_model_from_path`
- `TestRecognitionTaskModel.test_instantiation_from_model_list`
- `TestRecognitionTaskModel.test_reject_non_recognition_models`
- `TestRecognitionTaskModel.test_one_channel_mode_attribute`
- `TestRecognitionTaskModel.test_seg_type_attribute`
- `TestRecognitionTaskModel.test_predict_baseline`
- `TestRecognitionTaskModel.test_predict_bbox`
- `TestRecognitionTaskModel.test_predict_empty_segmentation`
- `TestRecognitionTaskModel.test_predict_config_precision`
- `TestRecognitionTaskModel.test_predict_invalid_line_coords`
- `TestRTLRecognitionTaskModel.test_predict_rtl_baseline`
- `TestRTLRecognitionTaskModel.test_predict_rtl_baseline_bidi`
- `TestRTLRecognitionTaskModel.test_predict_rtl_baseline_nobidi`
- `TestRTLRecognitionTaskModel.test_predict_rtl_display_logical_switch`
- `TestRTLRecognitionTaskModel.test_predict_rtl_bidi_nobidi_differ`
- `TestSegmentationTaskModel.test_load_default_model`
- `TestSegmentationTaskModel.test_reject_non_segmentation_models`
- `TestSegmentationTaskModel.test_predict_returns_segmentation`
- `TestSegmentationTaskModel.test_predict_produces_lines`
- `TestSegmentationTaskModel.test_predict_baseline_type`
- `TestSegmentationTaskModel.test_merge_segmentations_single`
- `TestSegmentationTaskModel.test_merge_segmentations_region_only`
- `TestForcedAlignmentTaskModel.test_load_model_from_path`
- `TestForcedAlignmentTaskModel.test_instantiation_from_model_list`
- `TestForcedAlignmentTaskModel.test_reject_non_recognition_models`
- `TestForcedAlignmentTaskModel.test_reject_non_ctc_models`
- `TestForcedAlignmentTaskModel.test_predict_enables_logits`
- `TestForcedAlignmentTaskModel.test_predict_returns_segmentation`
- `TestForcedAlignmentTaskModel.test_predict_record_type`
- `TestForcedAlignmentTaskModel.test_predict_produces_cuts_and_confidences`
- `TestForcedAlignmentTaskModel.test_predict_record_count`
- `TestForcedAlignmentTaskModel.test_predict_display_order`
- `TestForcedAlignmentTaskModel.test_predict_unencodable_text`
- `TestForcedAlignmentTaskModel.test_predict_empty_segmentation`

### `test_train_utils.py`

- `TestTrainUtils.test_kraken_on_exception_checkpoint_saves_file`
- `TestTrainUtils.test_kraken_on_exception_checkpoint_not_lightning_fault_tolerance_callback`

### `test_vgsl.py`

- `TestVGSL.test_save_model`
- `TestVGSL.test_model_roundtrip`
- `TestVGSL.test_append`
- `TestVGSL.test_resize`
- `TestVGSL.test_del_resize`
- `TestVGSL.test_nested_serial_model`
- `TestVGSL.test_parallel_model_inequal`

### `test_xml.py`

- `TestXMLParser.test_page_parsing`
- `TestXMLParser.test_alto_parsing`
- `TestXMLParser.test_auto_parsing`
- `TestXMLParser.test_failure_invalid_documents`
- `TestXMLParser.test_zero_dims_fallback_to_image_size`
- `TestXMLParser.test_alto_basedirection`
- `TestXMLParser.test_alto_language_parsing`
- `TestXMLParser.test_alto_fallback_region_boundaries`
- `TestXMLParser.test_alto_tag_parsing`
- `TestXMLParser.test_alto_split_parsing`
- `TestXMLParser.test_alto_baseline_linetype`
- `TestXMLParser.test_alto_bbox_linetype`
- `TestXMLParser.test_page_basedirection`
- `TestXMLParser.test_page_split_parsing`
- `TestXMLParser.test_page_language_parsing`
- `TestXMLParser.test_alto_ro_string_refs_flattened`
- `TestXMLParser.test_alto_ro_region_level_flattened`
- `TestXMLParser.test_alto_order_levels`
- `TestXMLParser.test_page_explicit_ro_parsing`
- `TestXMLParser.test_page_explicit_ro_region_level`
- `TestXMLParser.test_page_explicit_ro_unordered_group`
- `TestXMLParser.test_page_order_levels`
- `TestXMLParser.test_to_container_line_orders`
- `TestXMLParser.test_to_container_explicit_ro_indices`
- `TestXMLParser.test_get_sorted_lines_by_region`
- `TestXMLParser.test_get_sorted_regions`
- `TestXMLParser.test_missing_region_coords_lines_parsed`
- `TestXMLParser.test_missing_region_coords_region_excluded`
- `TestXMLParser.test_missing_region_coords_implicit_orders`
- `TestXMLParser.test_missing_region_coords_explicit_ro_skips`
- `TestXMLParser.test_missing_region_coords_to_container`
- `TestXMLParser.test_page_missing_region_coords_transkribus_ro`

