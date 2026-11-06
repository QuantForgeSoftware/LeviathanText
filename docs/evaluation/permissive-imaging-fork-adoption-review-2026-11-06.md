# Independent fork adoption review

This is a source-only audit during the long API measurement quiet window. No build, test, browser, decoder, large-image allocation, model, or corpus operation was run. Reviewed source hashes are in `.artifacts/experiments/sec01-v17-static-review/review.json`. Production migration is not accepted by this document.

## V17 pixel-writer review

No high-confidence blocker was found in the narrow writer change. One owned Bgra8888/Premul bitmap view is acquired per actual fallback decode. Checked row/span extents cover the destination; each existing walker supplies bounded coordinates. The ref struct remains local/ref-passed rather than captured or copied. Constructor failure disposes its acquired view, the outer decoder still disposes a failed bitmap, and finally releases the view while keeping the bitmap owner alive. A null destination still validates without a native destination. Native SKPMColor.PreMultiply avoids an invented rounding approximation.

This is not pipeline parity or throughput evidence. The earlier copy-only oracle does not prove PNM/TGA/QOI decode, alpha/palette/RLE/orientation, resampling or exception behavior. All such proof must use a newly frozen final binary after quiet releases.

## Provenance and functional gates still open

| Gate | Required evidence before adoption |
|---|---|
| Exact published assembly | Stage A's frozen Imaging/Core/Contracts/Fork DLLs without rebuilding them, pin every native/helper dependency, and exercise the actual intended binary. V16's154+17 successful independent cases used a separately built source-equivalent Imaging DLL; its different hash remains explicit and unexplained, not attributed to build paths without PE evidence. |
| Final consolidated source | Pin final source, cumulative fork diff, owned decoder changes, build properties/toolchain, package graph, per-RID native hashes and a reproducible binary manifest. Historical v8/v13/v16/v17 results cannot be silently relabeled as one final package. |
| Complete pixel/decision replay | Retain raw source checksum/frame/first-IFD semantics, exact lossless results and only already approved named exceptions. Recheck all owned-writer formats/variants and accepted TIFF paths against fixed oracles. Keep gray-alpha typed deferral and recoverable-JPEG decodability policy explicit. |
| Corrected bounded outputs | Freeze derived expectations for every prior operation whose old output exceeded its declared pixel budget before assessing the corrected final candidate. Preserve original strict negatives and all in-budget outputs exactly. The five tiny controls do not automatically replace every historical matrix oracle. |
| Archive decisions | Compare actual old Identify+Load(TargetSize1,MaxFrames1,SkipMetadata) decisions against the new same-byte validator, including late strips/tiles, malformed nested JPEGs and retained recoverable JPEG observations. Band deliberately skips unused pixels and is not archive validation. |
| Whole request path | Fresh safe engineering/build checks, import/bundle no-write-on-refusal controls and real relevant API behavior must use the final candidate. Removing a package reference alone does not remove every use or qualify migration. |

## Resource caps require distinct accounting

The prototype's 512 MiB encoded snapshot cap, 12,000-pixel axis limits, first-IFD tag/metadata/segment caps and nested-JPEG staging limits constrain different resources. None is a process-RSS guarantee. The returned pixel budget constrains the returned bitmap, not native intermediate scale, decoder scratch, encoded array, color metadata, or other concurrent decodes. Metadata-only dimensions and raster routes also have different validation semantics.

A concrete unexecuted source example exposes the generic workspace gap: a 12,000×12,000 bilevel whole-image segment is about18,000,000 packed bytes and passes the packed sample layout calculation, but a generic `int[width*height]` RGBA workspace is576,000,000 bytes, about549 MiB, before destination and vendor scratch. `MaxDecodedBlockBytes=512 MiB` therefore is not an actual generic RGBA allocation cap. The conservative full-segment flag correctly reports risk but does not prevent allocation. Root has requested a separately versioned cap/refusal proposal, with explicit compatibility review and metadata-safe tests; no large allocation should be attempted to establish this arithmetic. Per-buffer and aggregate/process caps must not be conflated.

The measured v13 archive TIFF allocation/RSS regression remains a blocker until exact final streaming-validator comparison qualifies it. Register matched old TargetSize1 versus final same-byte workloads, not only old full Decode; preserve ABBA order, every visit, warnings, decisions, source/native hashes, CPU/heap/RSS, mode/flag and owned cleanup. Whole-image strip/tile, tiny segments, high segment counts, long metadata, JPEG/alpha and full-native fallback are separate resource cases. Success on one tiled6MP case is not universal TIFF acceptance. No metric-selected repeat or relaxed threshold should erase earlier negatives.

## Exception and source-ownership boundaries

The validator deliberately lets OutOfMemoryException propagate, while several existing Decode/Bounded/fallback broad catches can convert allocation failure into unsupported-format errors or attempt a fallback after primary failure. The adoption decision must review this discrepancy explicitly; a successful bitmap does not establish absence of decoder error diagnostics. Full Decode attaches the bounded sink while Band/validation additionally reject positive ErrorCount, so corresponding policy and generated error controls must be stated before assuming uniform behavior. Proposed tests should inject or use small deterministic error paths, not exhaust the host to manufacture OOM.

Single admitted-handle reads, bounded source views, exact-length snapshot reads and growth/truncation checks are useful. They do not establish protection from same-length external rewrites. Caller-owned snapshot arrays must remain private/unmodified during validation and subsequent use; a byte-array parameter does not itself create immutability. Record checksum evidence for the actual bytes used rather than claiming a filesystem race has globally been eliminated.

Subprocess watchdog/RSS limits in research do not create an API cancellation guarantee. Synchronous native/managed decoder work cannot be preempted merely by checking a cancellation token before and after it. Production admission/concurrency, permitted input/layout budgets and any process-isolation policy need their own route-level acceptance.

## Maintenance and distribution obligations

The existing prototype report records the exact older source pin, cumulative BigTIFF/diagnostic fork edits, complete notices and the material vendored TIFF/JPEG/compression footprint. A project-reference fork is not covered merely because NuGet reports no advisory for its package graph. Production needs a named maintenance/security-monitoring owner, review of relevant upstream/native issues against this managed implementation, exact third-party source-component inventory, complete distributed notices and a fork-specific assembly/package identity linked to its source and hashes. Preserve all original BSD-style/libtiff/IJG/ComponentAce texts; do not call the dependency uniformly MIT or substitute a current website notice for pinned bytes.

These are unresolved adoption gates and source-derived risks, not a security attestation, legal clearance, new accuracy result, universal memory bound or proof that current main is affected by the isolated prototype.

The separate workspace-cap proposal has now received independent source review in `tiff-rgba-workspace-policy-independent-review-2026-11-06.md`. Its precise new refusal requires root policy approval; neither frozen v16/v17 nor the resource results were changed. A also identified an untested Compression6/OJPEG nested-frame path: the owned JPEG7 SOF guard does not cover the generic vendor OJPEG route. The registered independent JPEG/TIFF oracles deliberately cover Compression7, so valid-small baseline classification and an explicit defer-or-qualified-support decision are additional gates. No OJPEG decode, oversized fixture or exploit was executed or established by this source observation.
