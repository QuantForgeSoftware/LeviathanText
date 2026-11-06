# Exceptional native bitmap ownership follow-up

Source-review contract only; no new allocation/leak experiment or main repair is claimed. Existing DERIV-01 closes outer intermediate ownership and crop overflow, not allocations that a factory never returns. Preserve the retained-parser transfer's frozen output/resource proof and the permissive candidate's isolation.

## Narrow paths to investigate

In main ImageDecoder.cs, inspect the codec-scaled bitmap around GetPixels/Erase, full-decode resampled bitmap around canvas construction/draw, scanline-band bitmap around GetScanlines, and ExtractSubset destination before returning it. In DerivativeFactory, inspect the local CropBitmap and DeskewBitmap canvas allocation/draw lifetimes. These create native destinations before later calls can throw; the outer derivative pipeline cannot dispose an object it never received. The fallback transfer already has a destination catch/dispose path; avoid duplicating or weakening it.

Do not simulate OOM by allocating enormous images or claim a runtime leak solely from source. Separate factory allocation failure before a managed bitmap is returned (native library responsibility), failure after the wrapper is returned but before handoff (our responsibility), invalid native allocation results, source ownership, canvas cleanup and final result ownership. Changing broad OOM/error classification requires a separately reviewed policy; do not silently reclassify every exception as an unsupported image.

## Parallel ownership

Instance A owns the smallest shared ownership helper or local try/finally repair and the exact production call sites. Prefer a clear ownership transfer: hold the destination until all native initialization and result/metadata construction succeeds, dispose it on failure, and return ownership only on success. Keep caller source alive, retain parser/options/first-frame/dimension behavior and exact transform coordinates/pixels. Avoid global factory hooks or a new mutable singleton test switch.

Instance B owns new generated lifetime and public-output controls. Obtain a narrow internal delegate seam only where necessary to make actual production initialization throw after a real tiny native allocation; assert the captured native handle is released, the source is still usable, later cleanup runs and the original failure remains visible. Cover canvas-constructor failure, draw failure, codec-copy failure, invalid/partial result and metadata handoff where the real seam permits it. Do not label a missing new API or a copied helper as an actual old public-path failure.

Freeze actual original binaries first. If exception injection requires an extracted original control-flow helper, preserve that extraction and label it explicitly; pair it with unchanged original public pixel/dimension/transform controls. Report which defects were reproduced through an actual original path and which are source/extraction evidence. Test no-op/source alias and multi-transform failures. Native factories that never return a wrapper and unsupported platform faults remain outside that proof.

## Acceptance and handoff

Require independent source review, generated before/after controls, all existing Imaging/derivative/capture tests, unchanged fallback-transfer outcomes and the fitting-disabled engineering gate. Register finite owned subprocess/native failure probes before execution, preserve all failures and clean only generated resources. No model, corpus, training, provider or LM Studio calls. Root owns integration and plan updates. Do not close native-retention, ImageSharp advisory, permissive decoder adoption or all-platform gates from this repair.
