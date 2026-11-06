# Derivative intermediate ownership

Source-only proposal; no main edit, compile, drawing, tests or performance run. The current owner-B NativeAI API full24 quiet window remains active. Artifacts: `.artifacts/experiments/derivative-ownership-v1/`.

Current public `DerivativeFactory.Apply` replaces its bitmap through a transform sequence without disposing earlier owned intermediates. If a later transform or metadata publication throws, its current intermediate is also left for eventual finalization. The source bitmap belongs to its caller. Current production LineCropper normally uses one crop, so this public-SDK source finding is not a measured corpus/session leak.

## Separate baseline and proposed fix

`ImageDecoder.original-main.cs` preserves the exact current main source. `ImageDecoder.extraction-baseline.cs` is an explicitly labeled **pure extraction**, not the original compiled binary: it moves the existing transform switch unchanged into a private method and retains the old Apply algorithm inside an internal per-call transform seam. The proposed tests must run against this compiled extraction baseline and the candidate. A missing new helper in the original binary proves nothing and must not be counted as fail-before evidence.

`ImageDecoder.ownership-proposed.cs` uses that same seam, tracks whether the current bitmap is owned, disposes a replaced owned intermediate only after the next transform returns, and releases its last owned intermediate on every later exception. It constructs final metadata/result before relinquishing ownership. Caller-owned source and an unchanged current-object alias are never disposed by the pipeline; final successful output belongs to its caller. No-transform returns the exact original source reference and empty metadata, preserving current semantics.

`ownership-only.patch` isolates the lifetime change against the extraction baseline. `combined-ownership.proposed.patch` is the prospective merge hunk against current main. Only the Apply/core/default-transform block changes: ImageDecoder itself, crop bounds, native CropBitmap/DeskewBitmap bodies and transform mapping/metadata remain unchanged. Root's independent crop-overflow fix must have its own actual-public before/after proof and be merged separately.

The per-call callback is internal and exists to observe real bitmap lifetime/failures without process-global hooks. It is not a new public plugin/callback API. The core cannot dispose a bitmap allocated inside a transform that throws before returning that bitmap; native crop/canvas exception ownership remains a separate finding. No stronger claim is made.

## Proposed regression sources

Eight safe controls use only2×2/1px native buffers. They observe actual native handles, dispose all recorded objects in test finally blocks even when old-algorithm assertions fail, and preserve caller source:

- Actual crop/zero-deskew/crop operations expose all intermediates through the seam: earlier handles must close, final output stays alive until caller disposal, exact pixel/metadata/source bytes remain.
- A real later unsupported nonlinear transform must propagate its original typed error and release the prior actual crop.
- A controlled description/publication exception after returned outputs releases the last owned bitmap and preserves the original exception; no actual OOM is induced.
- Internal identity/source-alias controls prevent accidentally disposing a still-used output or the caller's original source.
- First-step failure, real public no-transform semantics and real public valid transform output remain controls.

No test is run in the current quiet window, and no pass/fail or performance count is predicted as measured. Root/R review precedes post-quiet compile and the labeled extraction-baseline/candidate comparison. The real public pixel/metadata controls should also run against the preserved original compiled SDK. Then run full Imaging and the central no-fitting gate after any reviewed main integration. No training/model/provider/corpus workload is involved.
