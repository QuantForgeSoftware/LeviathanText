# DERIV-02: narrow destination ownership proposal

Source proposal only; no candidate code/native probe/build has run. Main production remains unchanged. Independent tester A owns the original source/binary freeze and generated tests; root owns approval/integration. Pause all builds/native work during B's journal observation window.

## Chosen repair

Use local ownership flags and try/finally in the existing ImageDecoder.cs allocation sites, rather than a generic bitmap factory framework. A destination stays owned until initialization and the result wrapper/metadata are constructed and the method is about to return that exact destination. On failure or a declined scanline attempt, dispose only the destination owned by that local scope. Never dispose the caller source or transfer a different bitmap by accident.

This distinction matters for codec-scaled overshoot: a fitted bitmap may be returned while the larger codec intermediate must still be disposed. Use separate local ownership flags for scaled and fitted destinations; do not treat any non-null result as ownership transfer of both. For scanline partial results, preserve the current fallback and dispose the rejected band. For ExtractSubset, preserve its current return/boolean handling rather than adding a new invalid-result policy. No parser, pixel format, sampler, frame, coordinates, size limit, fallback or broad exception/OOM classification changes.

Owned sites: codec-scaled bitmap around GetPixels/Erase and possible fitted Resize handoff; full-decode resampled destination around canvas/draw; band destination around GetScanlines; subset destination around ExtractSubset; CropBitmap/DeskewBitmap destination around canvas/draw. The ImageSharp fallback already has catch/dispose ownership and stays untouched. Existing outer DerivativeFactory.ApplyCore intermediate ownership stays intact.

## Per-call observation/fault seam

Proposed internal declarations in Imaging (not public API):

```csharp
internal enum BitmapConstructionStage
{
    DestinationAllocated, CanvasCreated, NativeInitialized, BeforePublish
}
internal delegate void BitmapConstructionProbe(
    BitmapConstructionStage stage, SKBitmap destination, SKCanvas? canvas);
```

Public DecodeBounded(bytes,path,maxPixels) and DecodeBand(path,top,height) call new internal Core variants with a null probe. The Core variants carry the optional probe only through the listed native destination sites. An internal DerivativeFactory.ApplyWithBitmapProbe(source,transforms,probe) routes the actual transform bodies; the existing public Apply and existing ApplyCore signature remain compatible. The existing two-argument transform method can forward to a private probe-aware implementation, avoiding a new closure on ordinary public calls.

Invoke DestinationAllocated inside the protected ownership scope immediately after a native wrapper is returned; CanvasCreated immediately after constructing its real using-scoped SKCanvas; NativeInitialized after real native copy/draw/erase work; BeforePublish after constructing the ordinary result but before relinquishing ownership. A null probe is the only production default. No static/global hooks, dependency replacements, mutable singleton or new native allocator is introduced.

Independent tests capture real tiny allocated destinations/canvases through this per-call callback and throw a generated controlled exception at a selected boundary. The actual initializer/canvas/native copy code runs up to that boundary. The probe does not claim a real platform constructor/OOM/GetPixels fault occurred: it models the exception edge while exercising the actual allocation/initializer/cleanup scopes. Do not call disposed native codecs/bitmaps or allocate huge images to force faults. Allocation failures before a wrapper returns remain native-library responsibility and outside the repair.

## Before evidence and acceptance

A first freezes untouched current public Imaging/Core/Contracts/Skia binaries and source. Preserve original public tiny PNG/JPEG and retained-fallback pixels/dimensions, crop/deskew coordinates, no-op/source alias and successful final-result ownership controls against that exact original binary.

Exception injection requires a separately labeled matched extraction: copy the exact original factory control flow into an isolated source tree and add only the same internal per-call probe plumbing/stage observations, without any ownership repair. That extracted baseline and the candidate share identical seams; compare destination/canvas handles and original failure behavior. A missing new API must never be counted as an old bug. Report untouched public controls separately from extracted-boundary failure evidence. A real original public failure may be added only if safely demonstrated independently.

Generated tiny tests: failures after allocation, within a real canvas lifetime and after native initialization; handoff failure; exact failure type/contract preserved; captured destination handle released; captured canvas disposed; original source pixels/handle usable; rejected partial attempts cleaned where actually exercised; returned successful bitmap remains live until caller disposal. A multi-transform failure must release both the prior owned intermediate and the current factory destination while retaining the caller source. Keep old crop/deskew draw calls, rectangles/pivots/sampling and returned frame metadata byte-for-byte where practical.

DecodeBounded's existing broad catch currently maps ordinary exceptions to its existing unsupported-image contract. Preserve that behavior and compare the same contract before/after; do not promise an unchanged raw exception identity on that path. Crop/deskew/band paths retain their existing exception propagation. Never broaden OOM handling or claim generic native retention/all-platform closure.

Before candidate execution, root reviews the exact extraction diff, local ownership diff, stage locations and source/binary hashes. All native fault probes use owned subprocess deadlines/CPU/RSS/log caps and finally cleanup, no real models, fitting, corpora/labels or providers. After independent controls, root runs the relevant existing Imaging/derivative/capture and fitting-disabled engineering gates. A later resource check is separate; functional failure cleanup does not establish throughput/RSS improvements.
