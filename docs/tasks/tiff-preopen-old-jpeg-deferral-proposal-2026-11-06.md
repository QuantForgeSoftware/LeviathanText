# First-IFD old-JPEG deferral before managed TIFF open

Status: separately versioned source-only v20 proposal, uncompiled and unassessed. Main remains unchanged; frozen16/17/18/19, forkv4/v5 and the earlier insufficient draft remain preserved. No new old-JPEG pixel input has been decoded by this work.

## Why a post-open guard is insufficient

The owned fork registers `OJpegCodec` for Compression6. Its `ReadDirectory`/scanline-size metadata initialization can query YCbCr subsampling and enter OJPEG header parsing during `Tiff.ClientOpen`. The old draft's guard after ClientOpen therefore cannot promise avoidance of that parser. The draft remains explicitly marked insufficient at `.artifacts/experiments/tiff-ojpeg-deferral-v1/`.

The source chain also recognizes raw Compression7 as old-style6 when any of these **exact nine first-IFD tags** is present: JPEGPROC512, JPEGIFOFFSET513, JPEGIFBYTECOUNT514, JPEGRESTARTINTERVAL515, JPEGLOSSLESSPREDICTORS517, JPEGPOINTTRANSFORM518, JPEGQTABLES519, JPEGDCTABLES520, JPEGACTABLES521. This is the fork's existing `FIX_JPEG_IS_OJPEG` normalization in `Internal/Tiff_Internal.cs`, not an invented all-JPEG-tag rule. JPEG7/JPEGTABLES347 alone is not on this list.

The old OJPEG SOF path's upper-dimension/resource behavior remains unqualified. This proposal defers that codec rather than attempting an owned JPEG entropy decoder or claiming that outer TIFF dimensions bound nested JPEG frames.

## Proposed managed boundary

V20 copies baseline16 and changes only `SafeTiffDecoder.cs` and the internal fallback Header record. The existing bounded first-IFD parser derives codec-policy flags while it already owns validated entries/offsets/counts. It does not enter any vendor codec, follow later IFDs/SubIFDs or decode payloads.

- Compression absent retains the existing default non-OJPEG path.
- Integer representations are inspected with their actual byte width and the fork's low16-bit normalization. Signed integer storage is treated as raw bits for this classification, not numerically guessed.
- Raw6, or raw7 plus any exact legacy tag above, marks old-JPEG deferral.
- Noninteger storage types, zero or over64 element counts, or conflicting per-sample compression values mark **unqualified codec metadata**. The source proposal intentionally refuses that metadata on managed pixel routes; this additional controlled policy requires explicit review, not implicit acceptance. Count inspection is bounded64. This is not a whitelist of codec values: scalar0 or an unknown integer codec value remains on its existing downstream path unless low16-bit normalization selects the named old-JPEG rule. The proposed metadata-representation refusal must not be described as rejecting all unknown codec numbers.
- Identify/Dimensions retain their prior admission and raw dimensions; flags are metadata only. There is no old-JPEG pixel claim attached to successful metadata inspection.

Decode, Band and exact-snapshot validation check the flags **before each managed Tiff.ClientOpen**. DecodeBounded reaches the same guarded fallback Decode. Old-JPEG pixel refusal is `UnsupportedImageVariantException` with reason `unsupported-tiff-old-jpeg`; unqualified codec metadata uses `unsupported-tiff-compression-metadata`. Existing public translation must preserve these controlled reasons. No source image is modified or converted in place; a caller may convert a separate copy through an independently qualified tool.

## Native-first/public scope: explicit limitation

The source proposal guards **the owned managed fallback only**. Public ImageDecoder currently tries Skia before fallback, and this proposal does not move a general TIFF metadata policy ahead of Skia. It therefore does not claim that every native codec/RID avoids old-JPEG parsing, or that all public calls universally return the new reason. Current fallback observations do not prove native behavior on other platforms or future packages.

If the intended product policy is universal old-JPEG deferral before *any* native parser, parent must separately approve pre-native TIFF admission. Reusing the complete fallback preflight there could introduce its existing tag/segment caps/refusals for TIFFs a native backend previously accepted; that is a real behavior change requiring native-first baseline inventory and cross-platform evidence. A second independent TIFF header scanner would add maintenance and still need malformed-type/offset/count coverage. Neither policy is silently adopted here. CaptureDisplay's existing PNG/JPEG-only format gate is unchanged.

## Registration and acceptance before execution

Source freeze `.artifacts/experiments/sec01-adapter-v1/candidate-v20-source-proposal-freeze.json` SHA-256 is `8c8ca7ae2930fa31c8b78df22e2db5145ee77b17d18135a8615b167e39a1886a`; patch `candidate-v20-preopen-ojpeg.patch`. This is not a combined forkv5, pixel-writer or RGBA-budget candidate.

Before any candidate result, freeze:

1. Pure first-IFD metadata cases: raw6, each of the nine raw7 legacy-tag cases, raw7+JPEGTABLES347-only control, no Compression default, BYTE/SHORT/LONG/LONG8 and signed raw integer forms, inline/external and II/MM/Classic/BigTIFF. Metadata admission/raw dimensions must stay exact. Bad offset/count/type inputs retain bounded controlled metadata failure; do not call a format valid merely because an independent tool tolerates it.
2. Tiny independently validated old-JPEG source(s), with baseline accepted/refused inventory from the original ImageSharp backend and exact16. No prior intentional Compression6 acceptance fixture exists in the independent matrix. Unexecuted builder sources are not evidence of validity or old support.
3. Managed pixel entry controls for Decode, both Bounded overloads, Band and ValidateEncodedSnapshot: intended deferrals occur before managed ClientOpen, source bytes/hash unchanged. Header-only invalid JPEG payloads can distinguish the policy reason from an ordinary downstream codec failure, but do not constitute valid OJPEG acceptance evidence. No oversized-SOF payload or old-IJG decompression is authorized without a separate tiny bounded registration.
4. Full existing non-OJPEG matrix and known deferred variants, source identity, first-frame/raw orientation, late-data and allocation flags. Standard JPEG7 must remain on its existing qualified segment path. Per-sample/type ambiguity changes are named policy exceptions, not reported as strict parity.
5. Public native-first dispatch behavior on each tested platform explicitly, and a clear managed-only versus universal policy conclusion. No blanket JPEG/TIFF safety or cross-RID claim.

Production adoption, fork ownership/security monitoring, valid OJPEG support and universal native-first refusal remain separate decisions. This source proposal establishes no resource or pixel-performance result.
