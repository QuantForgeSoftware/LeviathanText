# Actual TIFF RGBA workspace guard proposal

Status: prospective source policy, **not applied** to frozen v16/v17 or main. Root must approve the newly changed acceptance boundary before a versioned candidate is built/evaluated. No test, image, benchmark or large allocation was executed. Source/helper/test/patch artifacts are `.artifacts/experiments/tiff-rgba-workspace-policy-v1/`.

R's independently identified gap is real in source: `MaxDecodedBlockBytes=512MiB` bounds packed source-layout bytes, whereas generic RGBA arrays use four bytes per pixel. A12000² bilevel source's packed18,000,000 bytes pass that check; its `int[144,000,000]` would require576,000,000 bytes. This proposal does not relabel that source as already refused or claim observed RSS/exploitation.

## Explicit proposed policy

Retain the512MiB limit for **each extra owned RGBA workspace**, calculated in checked unsigned64-bit arithmetic before its allocation. This covers the generic full-page `int[]`, generic source-block `int[]`, and eligible JPEG7 segment BGRA bitmap. It does not cover the returned bitmap, encoded source/staging arrays, precise sample buffers or vendor/native state; it is not a whole-process memory budget. Existing packed-layout/sample budgets remain separately named evidence.

A workspace exceeding the limit produces controlled `RejectedArchive` with reason `tiff-rgba-workspace-budget`, workspace kind, exact requested bytes and limit. A dedicated internal exception must survive fallback catch-all boundaries and be translated at each public pixel/validation route. This avoids misleading users with `unsupported-image-format`. No OOM/diagnostic acceptance policy is changed in this patch; those are separate proposals.

This is an **intentional new refusal for some otherwise valid images within the existing encoded/dimension caps**. It preserves no-loss output for admitted cases but reduces pixel-route admission at this boundary. Root should approve that tradeoff explicitly or choose another registered limit/algorithm. It must not be silently adopted as mere equivalent validation.

| Route | Actual guarded storage | Consequence |
| --- | --- | --- |
| Generic TIFF Decode | width×height×4 page scratch | May refuse a large page even when source strips are small; source/returned data are never silently downsampled. |
| Generic TIFF DecodeBounded fallback | Same full-page scratch before existing resampling | A small requested output budget does not eliminate the current full fallback scratch. Refusal is explicit; no claim of codec-level bounded TIFF scaling. |
| Generic TIFF Band | One actual source strip/tile RGBA workspace | Small ROI can still refuse a whole-image source segment. Smaller source segments can permit Band even when full Decode would refuse page scratch. |
| Generic TIFF snapshot decodability | Same source-block scratch without assembled page destination | Traverses all first-frame blocks within the guard, not a header or thumbnail. |
| Eligible JPEG7 Decode/Band | Extra full native segment BGRA destination | Precheck known segment geometry before even allocating the returned page/band bitmap. |
| Eligible JPEG7 snapshot rows | No segment-raster workspace when native scanlines succeed | Existing row traversal stays permitted; if the native codec requires the existing full-segment compatibility destination, guard it before allocation. Native scratch is not thereby bounded. |
| Dimensions | Existing metadata policy only | No additional RGBA refusal. Existing metadata/packed-layout guards still apply. |

For a page-scratch refusal, use a smaller-image copy or an actually suitable region route. For a source-block refusal, re-encoding a copy with smaller strips/tiles can help. Neither action mutates/replaces original captures automatically. Do not promise smaller strips repair the current full-page generic Decode allocation.

The generic/owned JPEG rules apply to the managed TIFF fallback. Universal native dispatch/oldJPEG refusal requires the separate early-admission review; no platform-dependent Skia capability is inferred from this helper.

## Proposed implementation and deterministic controls

`TiffRgbaWorkspace.cs` calculates the RGBA element count and has a per-call optional allocator seam. Normal production calls allocate a zero-initialized `int[]`, preserving existing behavior. Tests pass a sentinel allocator and never allocate a large array. No process-global fault hook, package or public API is added.

Fixed mathematical cases, declared before candidate evaluation:

- 11585²:536,848,900 bytes admits;11586²:536,941,584 rejects.
- 12000×11184:536,832,000 admits;12000×11185:536,880,000 rejects.
- 12000²:576,000,000 rejects before the sentinel allocator is called.
- Exact536,870,912-byte scalar boundary is tested using16384×8192 **only as arithmetic-helper input**, not public image-dimension admission. The source12,000-axis rule is unchanged.
- Invalid/nonpositive and huge integer dimensions never reach the allocator; true resource arithmetic must not wrap into a small accepted length.
- A2×3 actual array remains six zero-initialized elements.

The proposed reflection-based test source exercises the actual internal helper without widening the runtime public surface. Under-budget large cases deliberately throw an allocator sentinel after validating count, proving dispatch without allocation; over-budget cases prove the allocator is never reached. It does not claim a malformed header is a valid complete image, nor run the unguarded baseline raster against a large header.

`workspace-policy.proposed.patch` is based on frozen v16 and touches only its relevant SafeTiffDecoder/TiffJpegDecoder allocation sites, internal ImageDecoder propagation/translation, ImageValidation translation and the new helper. v17's pixel writer remains a distinct source/performance proof. Final integration must merge against current source and preserve every other change.

## Acceptance still required

Root/R policy/source review precedes application to a new candidate. After quiet, compile and run the arithmetic/sentinel controls and all small registered functional/gray-alpha/JPEG-recovery decisions. Verify exact controlled reasons across public routes without unsafe large allocations; a bounded allocation seam/external guard can be registered for end-to-end proof if needed. Preserve existing admitted pixels, checksums, metadata/source bytes and no-store/authorization boundaries elsewhere.

Any resource run needs its own approved input/binary/limits/order registration. This proposal neither closes the v13 archive/RSS negatives nor establishes v16 exact-A-binary coverage, unqualified OJPEG support, a host admission lane, native cancellation, security maintenance ownership or a production dependency migration.
