# Decoder ownership, failure and resource source audit

This is a source-only audit during owner B's NativeAI API full24 quiet window. No decoder, test, model, provider, benchmark or large fixture was executed. Frozen v16/v17 remain unchanged; main Imaging still uses ImageSharp3.1.11. Source anchors are `.artifacts/experiments/sec01-failure-audit-v1/source-review-freeze.json`. Findings distinguish current production from isolated candidates and do not establish observed leaks, allocations or exploitation.

## Small ownership and failure semantics

| Finding | Exact source | Scope and proposed correction |
| --- | --- | --- |
| Allocated native scaled bitmap lacks a complete failure owner | Main `src/LeviathanText.Imaging/ImageDecoder.cs:133–134`; isolated v16 `ImageDecoder.cs:126–127` | `GetPixels` is invoked after allocation without an enclosing ownership finally. Known failure *results* explicitly dispose; an exception before result handling does not. A later finalizer may reclaim it, but deterministic disposal is missing. This path predates the replacement. Retain local ownership until a returned handle is successfully constructed; use try/finally/explicit transfer. Do not change pixel result or IncompleteInput policy in that cleanup patch. |
| Canvas/resampled destination lacks failure ownership | Main same file:177–180; isolated v16:170–173 | The full decoded input is scoped, but the new resampled destination is not disposed if canvas construction/drawing throws before return. This also predates the replacement. Keep a local owner until success; dispose on every exception. Wrapper-allocation failure before ownership transfer should also be covered. No runtime leak is claimed. |
| Resource exhaustion can be mislabeled and retried as unsupported format | Main same file:269–275 plus the fallback catch; isolated v16:180–188, primary snapshot catch:239, and `TryDecodeWithFallback`:490–495 | Broad catches include OutOfMemoryException. Snapshot validation explicitly excludes it; other paths may try another codec or convert it to `unsupported-image-format`. Propose a separate reviewed exception policy: dispose local native resources, propagate resource exhaustion unchanged rather than silently retry/rename it. Tests should inject an exception object, never induce actual host OOM. This is not evidence all malformed native failures behave identically. |
| TIFF full decode publishes a bitmap despite recorded handler errors | Isolated `SafeTiffDecoder.cs:101–119`; compare `ValidateFirstFrame`:135–136 and `DecodeBand`:152–153 | Full Decode snapshots diagnostics in finally but does not reject nonzero ErrorCount before returning, unlike the other pixel routes. The source permits inconsistent acceptance if the library records an error and returns success. No concrete real-file instance has yet been executed. Decide and test an explicit nonzero-error policy separately; warnings remain nonfatal and known recoverable native-JPEG compatibility must not be reclassified. Any stricter accepted/refused image behavior requires baseline control evidence. |

The TIFF/primary scanline Band paths are positive ownership controls: each holds its local bitmap until source-length verification succeeds and disposes it in finally on failure. The precise sample/JPEG block walkers also dispose their local destinations on exceptions. v17's pixel writer composes its constructor/pixmap cleanup with the outer owned bitmap and keeps its bitmap alive; R found no high-confidence static blocker. These are source observations, not substitutes for post-quiet controlled failure tests.

Do not bury these changes inside v17's native pixel-transfer proof. Its code/pixel/resource claims remain exactly scoped. A later versioned cleanup patch should preserve successful bitmap bytes, dimensions/checksums and API signatures, then prove injected failure disposal plus public happy paths. No process-global mutable fault hook or real memory-exhaustion attack is needed.

## Actual RGBA workspace policy gap

R independently identified and this review confirms that `SafeTiffDecoder.MaxDecodedBlockBytes` checks packed source samples during metadata preflight. It does not bound the actual RGBA workspace at full Decode:105 or block traversal:185. A12000² bilevel layout has18,000,000 packed bytes but the generic `int[144,000,000]` needs576,000,000 bytes (about549MiB), before its destination/vendor state. The existing512MiB packed-layout check therefore must not be described as a512MiB RGBA/process limit.

A separate source-only proposal will guard actual checked RGBA scratch sizes before allocation and state the resulting intentional refusal policy. No existing refusal is claimed. Source counts/metadata remain readable within their existing limits; returned bitmap, encoded snapshot, planar buffers and native workspace are separate costs. JPEG segment bitmaps and precise-sample buffers must each be inventoried rather than hidden behind the generic array's cap. A full-page Decode scratch and a selected source-block Band/validation scratch are different workloads: small source segments can permit ROI/validation even where a page scratch would exceed a proposed cap.

## Compression6/OJPEG nested-frame admission gap

This finding is isolated-fork source behavior, not a new production dependency or a runtime exploit demonstration:

1. `TiffJpegDecoder.cs:24–29` handles only Compression7, contiguous8-bit three-component RGB/YCbCr. Compression6 reaches generic `SafeTiffDecoder.cs:106`/`:195` RGBA calls.
2. The fork's `LibTiff/Internal/Tiff_Codec.cs:31` registers `OJpegCodec` for Compression6.
3. `LibTiff/Internal/OJpegCodec.cs:1528` explicitly notes the absent upper-bound check. Its SOF parser rejects only dimensions smaller than expected at1532/1541, then retains larger height/width at1537/1546.
4. `OJPEGWriteHeaderInfo` at1083 reads a JPEG header at1098 and starts managed decompression at1173. The TIFF metadata dimension guard does not independently constrain this larger nested frame.

The currently frozen intentional JPEG fixtures are Compression7; R confirms no valid Compression6 capability/oracle exists in the matrix. Do not infer either baseline support or baseline refusal from common TIFF passes. Root favors proposing an explicit typed Compression6 pixel-route deferral until valid old-JPEG baseline inventory and nested-frame/resource qualification exist. That policy needs a separately frozen proposal and controls across Decode/Bounded/Band/snapshot validation, with metadata availability explicit. No old-IJG decompression or large allocation may run during quiet. A metadata-shaped malformed control is not evidence a complete old-JPEG image is valid.

An early admission guard must also consider dispatch order: the primary Skia probe currently precedes the managed fallback. A guard placed only after vendor `ClientOpen` does not prove no unsupported parser was entered, and cross-platform/native codec capabilities must not be assumed identical. Before claiming a universal Compression6 refusal, specify where the first-IFD compression is checked and verify all public routes on the actual native build. No DNG/other primary-codec support is inferred here.

## Source and host-resource limits

Exact encoded snapshots used for full/Bounded decode bind hashes to the decoded supplied bytes. Streaming Dimensions/Band uses one handle and checks admitted length/growth; it is not a proof against same-length in-place filesystem edits. Byte-array callers must preserve their snapshot during synchronous traversal. Do not advertise universal immutable-filesystem or totalRSS/time guarantees.

Current source caps, metadata/segment checks and bounded diagnostics are useful but are not a host admission lane, process-memory governor or cancellation of synchronous native work. Largest admitted strips/tiles, encoded staging plus joined JPEG bytes, page destinations and vendor/native scratch can coexist. Their actual resource observations remain separate from configured limits. The existing v13 full/archive RSS negatives and the later unmeasured v16 traversal stay open evidence.

## Migration inventory consequence

The base integration remains eight owned runtime files, plus the chosen vendored project/source/notices. v17 introduces a **ninth optional owned writer file**; proposed cap/ownership/error helpers are not silently included in the base eight. Stage and hash each separately before any adoption.

`ImageDecoder.cs` also contains `ImageTransform`/`DerivativeFactory`, outside this decoder replacement scope. Merge reviewed decoder-class hunks into current source; never overwrite the file from a stale prototype and erase root's independent budget/dimension work or unrelated derivatives. Contracts/Core clones and artifact paths remain build scaffolding, not migration payload. Fork identity/security-maintenance ownership, exact provenance/notices, native platform coverage and source-to-published-DLL proof remain independent release gates.
