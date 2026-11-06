# Strict returned-pixel budget proposal

This is a separate registered correction to a pre-existing baseline/candidate bug. Existing exact pixel-comparison results remain unchanged until root approves the policy. No model, labels or natural accuracy run is involved.

Frozen observations already show `DecodeBounded(maxPixels:1)` returning4 pixels for the prior16×12 JPEG,2 for an8×2 PNM, and3 for a15×2 P4. The new tiny fixed fixtures in `.artifacts/experiments/sec01-independent-v1/fixtures/budget-manifest.json` add valid12000×1 PNG/JPEG shapes at budget128. All source hashes and literal expected targets are fixed before a candidate budget change. These are safe small rasters, not decompression bombs.

Proposed integer-fit policy:

1. A positive budget is mandatory. Invalid0/negative values receive a typed StageContractViolation before source decoding/allocation; preserve existing missing-file prechecks where the path API already returns404. No new error enum is needed.
2. Preserve existing full/native-scaled/fallback output pixels and dimensions whenever the actual returned pixel count already fits the budget. Do not change safe legacy output just to choose a different rounding convention.
3. For an oversized fallback/resample target, compute the uniform ideal scale from original width/height and budget. Use floored positive dimensions; if minimum-one clamping still exceeds budget on a thin image, reduce the longer dimension by integer division so width×height<=budget. Keep dimensions positive and no larger than the source. This applies only when old output would overshoot.
4. If a native codec's minimum supported scale still exceeds the budget, decode that admitted codec intermediate, then resample it to the same integer-fit target. This corrects returned size; it does **not** claim the native intermediate or scratch is proportional to the tiny budget. Existing input/dimension/block guards and failure disposal remain mandatory.
5. Fallback codecs resample the original full raster once into the corrected target; avoid an unnecessary legacy-rounded intermediate. The codec-scaled JPEG case resamples the old native intermediate once. Sampling stays the existing Skia linear mode. Source checksum and original dimensions remain exact; ScaleX/ScaleY describe actual independent output axes.

Literal targets are1×1 for the three budget1 fixtures and128×1 for both12000×1 budget128 fixtures. Existing in-budget outputs remain exact controls. Derived pixel oracles will use the approved source-correct PNM/P4 raster where applicable, the original raster for fallback paths, and the actual frozen native JPEG intermediate for the JPEG path. No generic pixel tolerance is introduced.

This intentionally changes overshooting outputs and invalid-budget acceptance. It does not establish unchanged accuracy for models previously consuming those unsafe shapes; promotion/training/natural evaluation gates remain separate. TIFF/PNG temporary full-frame fallback and native codec minimum-scale allocation remain open resource limitations even after strict returned dimensions pass.

Approval pending; no candidate implementation or adoption claim.
