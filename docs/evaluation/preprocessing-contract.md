# Shared preprocessing contract — 2026-11-02

Training and inference now use one versioned C# line-pixel transform when a model declares its preparation profile. Existing packages without that declaration retain their historical route behavior. This implements NT-16b; it does not retrain existing Greek models, change scholarly labels or implement P46 page rectification.

## Contract and recipes

`LinePreprocessingProfile` records four required JSON fields. For example, new generic PAGE training declares:

```json
{
  "Version": 1,
  "Recipe": "p01-otsu",
  "Height": 16,
  "Geometry": "line-crop"
}
```

| Version-1 recipe | Exact pixel behavior | Background |
| --- | --- | ---: |
| `p01-otsu` | Legacy linear resize, byte-reader luminance, floor histogram bins, existing Otsu threshold and strict `<` comparison; always binarize | 0 |
| `ink-positive-grayscale` | Legacy linear resize and byte-reader luminance followed by `1 - luminance`; no thresholding | 0 |
| `luminance-grayscale` | Linear resize and ordinary RGB luminance; no thresholding or already-BW shortcut | 1 |
| `luminance-local-background-v1` | Original-crop RGB luminance; fixed tiled background division and float bilinear resize, as specified below | 1 |

All preserve aspect ratio, round width as before and retain a minimum width of eight pixels. Height must match the model architecture and be at least eight/divisible by four. Unknown versions, recipes, geometry values and incomplete explicit JSON profiles are rejected. Training configuration, checkpoint metadata and run manifests retain the profile; it contributes to the training config hash when declared. A null profile preserves the previous hash behavior.

The first two recipes intentionally preserve the original byte-reader's red/blue channel assumption and threshold details. Replacing their math with the newer inclusive/rounded Otsu helper would change old model inputs. Golden tests cover gray 96 at the strict threshold boundary, floor-bin behavior and color-channel values. Ordinary luminance is a separate recipe with its own meaning.

The fourth recipe was added during the [subsequent provisional P46 study](p46-two-hour-study.md). `LocalBackground(height)` uses 64×32 original-crop tiles, nearest-rank 90th percentile (`ceil(0.9*n)-1`), actual partial-tile centers and clamped bilinear interpolation of that background field. Divide luminance by `max(background, 0.05)`, clip to [0,1], then resize with float bilinear half-pixel-center sampling. Constants are fixed by this recipe identifier. It also changes resampling/quantization relative to ordinary luminance; results must be attributed to the whole recipe. It is opt-in and did not improve the provisional matched Greek development screen. Existing recipes and package defaults are unchanged.

The subsequent exact-output implementation uses bounded order-statistic selection with a reused tile buffer and cached horizontal brackets. Recipe values and arithmetic are unchanged: all 48 frozen raw/normalized tensors and 216 saved predictions replay exactly. The measured normalization speedup is local and partial; the study retains both frozen and optimized resource results and the substantial remaining cost relative to raw luminance.

`Geometry: "line-crop"` means the standard application/PAGE line extraction path. Version-1 defaults retain the existing horizontal margin of eight pixels and vertical padding fraction 0.25, capped at 12 total pixels. Training and inference use the same extracted raster under a declared profile; a fixture also freezes the resulting crop/resize dimensions. `Geometry: "externally-prepared-line"` declares that upstream geometry has already been prepared. Pixel preparation accepts such a crop, while generic PAGE, XML and page segmentation routes reject it. The API reports `ModelIncompatible`/422 without creating transcription revisions for this unsupported route.

The profile does not contain a rectification algorithm. For a future P46 package, a luminance profile with external geometry can describe the pixel step after independently prepared crops, but the crop-generation recipe and source coordinate transforms must still accompany that dataset/package. Old P46 free text is not executed as an algorithm.

## Compatibility policy

| Package | Behavior in this runtime |
| --- | --- |
| Old format-1 package without a typed profile | Legacy PAGE loading keeps its original subset/Otsu path; application/image inference keeps inverted grayscale. Free-text descriptions and model names do not select recipes. Model height is honored. |
| New profile-bearing package | Format 2; PAGE training/evaluation and application/CLI/worker/API inference use the same declared transform and height. |
| Checkpoint metadata only | Still loadable; use its explicit profile if present, otherwise legacy defaults. |
| Unknown or conflicting explicit contract | Reject before native weight loading or output-package creation. No fallback to a guessed recipe. |

Missing metadata cannot resolve the historical training/inference mismatch reliably, so this change does not automatically migrate old packages. Existing format-1 models remain supported. New typed packages use format 2 so older runtimes, which cannot execute the profile, reject them rather than silently apply grayscale. The profile schema's version 1 and model package format 2 serve different purposes. Removing a manifest from a typed checkpoint does not make it suitable for an older runtime.

The loader verifies checkpoint/manifest profile agreement and both input-height declarations. The package writer preserves a checkpoint profile if the caller omitted it from the manifest, validates before copying, and promotes typed packages to format 2. Resume requires the training configuration's profile to match checkpoint metadata exactly, including null versus declared; legacy metadata is not silently reinterpreted.

The old three-argument `LineImage` constructor, old PAGE-loader compiled signature and static application conversion API remain available. The general `LineInputTransform` API retains historical fixed-width, channel, forced-Otsu and automatic-BW behavior; its ordinary single-channel non-BW grayscale path now delegates to the shared luminance transform. These legacy options are not falsely described as one of the other recipes.

## Integration

- `P01LineDataset` and new CLI training declare a profile before preparing images. CLI training records it in config, checkpoint and manifest.
- Package-based `evaluate lines` loads the model before preparing its dataset. It selects the declared profile and height, retaining the legacy dataset path when no profile exists.
- Application surface recognition, recognition/forced-alignment task wrappers, project/surface jobs, the API surface endpoint, image OCR and XML OCR propagate the profile. Image `--line` accepts an already prepared crop; generic page routes enforce the geometry requirement.
- Pipeline provenance now identifies the actual selected profile instead of claiming Otsu for the old grayscale-only path.

Core implementation: [profile](../../src/LeviathanText.Recognition/LinePreprocessingProfile.cs), [pixel transform](../../src/LeviathanText.Recognition/LinePreprocessor.cs), [package contract validation](../../src/LeviathanText.Training/PreprocessingContract.cs). C# factories are `P01Otsu(height)`, `LegacyInkPositive(height)`, `Luminance(height)` and `LocalBackground(height)`; `LoadedRecognitionModel.PrepareLine` prepares a supplied line crop. Raw `LineImage` recognition remains supported for already prepared data.

## Verification

Tests cover golden legacy pixels, exact per-float training/inference tensor equality for all three recipes, custom heights, profile/background serialization, old packages, checkpoint-only loading, resume compatibility, unsupported/missing fields, writer validation before copy, and CLI/worker/API route propagation.

| Final check | Result |
| --- | --- |
| ML regression suite, excluding only the long `LeviathanText.ML.Tests.P01*` experiment classes | 162 passed |
| Entire CLI suite | 24 passed |
| Application preprocessing/task/export/streaming checks | 24 passed |
| Worker recognition/profile checks | 5 passed |
| API profile and legacy model endpoint checks | 4 passed |
| Total selected tests | **219 passed; zero failures/skips** |
| Full solution build, serial, cached dependencies | **Zero warnings/errors** |
| Preserved P46 v2 replay | **24/24 exact predictions unchanged** |

Final TRX files are `.artifacts/test-results/preprocessing/final-{ml,cli,application,worker,api}.trx`; `solution-build.txt` records the successful `dotnet build LeviathanText.slnx --no-restore -m:1 -p:UseSharedCompilation=false`. Unconstrained parallel MSBuild workers exited prematurely in this environment, so final compilation used one worker. No code failure was inferred from that infrastructure issue.

The existing `.artifacts/experiments/p46-d2-verification/` runner was rebuilt serially and executed with `--no-build --no-restore`. Its detailed evaluation reproduces all 17 training and seven development strings, with the same 67/578 and 162/219 edit counts. Frozen experiment receipt hashes remain unchanged. This replay uses the original prepared crops and luminance path, not the generic fallback for an ambiguous old package; it does not validate automatic P46 rectification or new recognition accuracy.

A genuine baseline regression reproduced the old two-argument task wrapper passing height-16 images to a height-32 model. Its failing TRX is `.artifacts/test-results/preprocessing/preprocessing-height-before.trx`; the restored shared implementation passes that case. Test fixtures and training used for engineering checks are synthetic, not newly accepted Greek data. Long real-data training matrices are outside this verification scope.
