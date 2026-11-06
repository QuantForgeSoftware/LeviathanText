# TIFF RGB8 validation buffer reuse: isolated v19 proposal

Status (2026-11-06): source-only, uncompiled and unassessed. Main retains ImageSharp and SEC-01 remains open. Frozen 16/17/18 remain unchanged. V19 is a separate baseline 16 source copy, not a combined pixel-writer/workspace-policy candidate.

## Problem and hypothesis

Independent exact-A16 resource evidence reports a remaining archive-validation slowdown/allocation cost for the generated 6MP RGB deflate-tiled TIFF relative to old ImageSharp TargetSize1. This is a hypothesis for reducing that cost, not measured improvement. The vendor fork's `LibTiff/Tiff.cs` `ReadRGBATile` creates `TiffRgbaImage` per invocation; `TiffRgbaImage.cs`'s contiguous tile reader creates a new tile byte buffer (`new byte[img.tif.TileSize()]`, source line 868). Validation discards the RGBA output after decompression, yet pays conversion and repeated wrapper/buffer allocation. The sample-precision validator already demonstrates direct encoded block/scanline reading with bounded byte buffers.

## Exact source scope

Only `TiffSampleDecoder.cs` in `.artifacts/experiments/sec01-adapter-v1/candidate-v19/` changes. Its validation entry first tries a new private validation-only reader. Decode, DecodeBounded, DecodeBand, metadata and every rendering/color path remain unchanged. The new route requires all of:

- BitsPerSample8, SamplesPerPixel3, PhotometricRGB, PlanarConfigCONTIG and unsigned SampleFormat1.
- No extra samples, including unused/unspecified extras. Their existing generic route remains available.
- Compression 1(NONE), 5(LZW), 8(ADOBE_DEFLATE), 32946(DEFLATE), or 32773(PACKBITS). JPEG 6/7, fax and every unlisted compression remain on their existing paths.

The helper returnsfalse for all layouts outside this conjunction. It does not guess RGB normalization, alpha, orientation or color transforms: no pixels are produced. It validates complete decompression through the same fork's codecs. For tiles it requires `TileSize == tileWidth*tileHeight*3`, reuses one decoded tile buffer, reads every first-IFD tile and requires the full decoded byte count. For strips it checks `ScanlineSize == width*3`, reuses one row buffer and reads every row in order. Predictor processing remains the existing fork codec's responsibility. The enclosing validation still rejects sink errors and applies unchanged source metadata/count/offset/resource caps, first-IFD policy and dimensions checks.

`AllocatedFullRasterDestination` retains the existing conservative source-block semantics: a tile whose area covers the page or rows-per-strip covering the page sets it true. A row buffer is not proof of row-only native/vendor memory; ReadScanline can retain a whole encoded/decompressed strip. The 512MiB decoded-block policy and all encoded/metadata limits are unchanged. Returned/native/vendor/total process memory are not globally bounded by this proposal.

The source proposal is sealed in `candidate-v19-source-proposal-freeze.json`, SHA-256 `366b29eb3cce6cd9704d006a7eafea27df4fc86c85220117e4a8e5d950aed97c`; the patch is `candidate-v19-rgb8-validation.patch`. No outputs were assessed before this registration.

## Required functional registration before execution

The independent evaluator must register exact inputs and baseline 16 decisions before any19 result is read. Preserve existing fixtures and negative evidence; add new fixture versions rather than mutate them.

1. Reuse the complete 154 snapshot-validation controls and 17 variant controls from exact A16 (including 118 TIFFs, 31 archive cases and 5 nested-JPEG controls), with exact acceptance/refusal, raw dimensions, source hashes and allocation flags. The known recoverable JPEG negatives remain decodability compatibility observations, not strict-validity passes.
2. Enumerate every existing eligible RGB8 NONE/LZW/DEFLATE/PackBits stripped/tiled fixture, including II/MM, Classic/BigTIFF, partial bottom/right tiles, predictor2, orientations1–8, first-IFD/multipage, omitted/UINTMAX RowsPerStrip, small/whole-page segments. A v19 result must preserve every baseline-supported case. Record which cases exercise the new route; do not infer coverage from a generic format count.
3. Freeze additional eligible fixtures if a cross-axis is missing: raw, LZW, both deflate tag values, PackBits; strip/tile, partial last row/tile, whole-frame block, predictor1/2, byte order and BigTIFF. Freeze baseline 16 outcomes before 19. No decoder output changes are approved by this proposal.
4. Keep ineligible controls for RGB16, RGBA8/16, gray/palette, planar RGB, sample-format signed/float, extrasamples, JPEG7, deferred gray+alpha, old-JPEG uncertainty and unsupported codecs. If candidate decisions change unexpectedly, report a failure; do not widen the allowlist to hide it.
5. Public `ImageDecoder.ValidateEncodedSnapshot` late-data controls: valid complete multi-strip/multi-tile RGB8; missing last encoded block; last compressed block truncated within a length-consistent source; corrupted final compressed payload/checksum; partial final tile; malformed decompressed-byte-count/predictor layouts. Structural preflight rejection and actual final-block decompression rejection must be reported separately. Earlier valid blocks cannot substitute for the last block. Metadata-only calls must not decode payloads.
6. Compare unchanged fullDecode/Bounded/Band outputs on existing eligible and ineligible matrix to exact 16 pixels, source checksum and dimensions under already-approved source/budget exceptions. This proposal changes only validation dispatch, so any rendering difference is a regression. Do not use a missing private helper as fail-before evidence.
7. Confirm source snapshot bytes remain unchanged and all source/dependency/native binaries are sealed before/after. Build Imaging only against exact frozen 16 dependency DLLs if approved; change only Imaging DLL/PDB in the matched runtime. This source does not include later main crop/ownership patches.

## Later resource gate

Functional parity comes first. Resource execution requires a separate parent-approved quiet window, fixed fixtures/order/caps and preregistration. Reuse the exact independently frozen 6MP tiled-deflate archive input, smaller counterpart and oldTarget1/exact16 context. Measure managed cumulative allocations, peak process RSS and completion time with source/runtime closure. A19 improvement over 16 would not erase oldTarget1 tradeoffs or prove universal speed/RSS improvement. No resource run is authorized by this source document.

## Acceptance and remaining limits

No format/variant dropping, new global input cap, tolerance relaxation or silently accepted malformed final blocks is authorized. Unexpected acceptance/refusal changes require an explicit policy review. If direct decompression cannot preserve the admitted validation contract, retain the old path and report the hypothesis as rejected. Vendor maintenance, fork identity/license inventory, OJPEG coverage/nested-frame risks, human security-monitoring ownership and whole-product dependency adoption remain separate gates.

## Compilation handoff

After the source proposal and independent static review, parent authorized Imaging-only compilation. It succeeded with zero warnings/errors against exact frozen A16 Core/Contracts/fork DLLs; no vendor or dependency was rebuilt. `candidate-v19-matched-runtime/` preserves the20-file baseline layout with only Imaging DLL/PDB replaced. `candidate-v19-compiled-freeze.json` SHA-256 is `1eef05f297a37d762565c26e0aafd56fbd2081eb424b4939087abb27c0e4ef50`; the Imaging DLL hash is `d6c79f052d4cccad53f0b12553f0f3c0f4a8e78f4a89691aec45cdc663d16e10`. The frozen proposal source remains unchanged. No candidate outputs have been assessed; independent baseline fixture registration and parent approval remain required before that step. Compilation does not establish acceptance or resource improvement.
