# Complete permissive imaging replacement — parallel implementation brief

The owner selected investigation of permissively licensed replacements. The AI
strict-PNG path now uses the existing Skia dependency and passes its independent
review/tests. Imaging still references ImageSharp3.1.11 and its six advisories
remain a release blocker. This brief does not authorize declaring that gate closed
from a clean package scan or a few successful photographs.

Read [the candidate research](../evaluation/imagesharp-replacement-research-2026-11-05.md),
[PNG adoption review](../evaluation/imagesharp-png-independent-review-2026-11-05.md)
and [dimension admission evidence](../evaluation/image-dimension-preflight-2026-11-05.md).
The [actual pinned-format inventory](../evaluation/imaging-fallback-format-inventory-2026-11-05.md)
already proves that PBM(P1), TGA, TIFF and QOI use the fallback on this machine;
its nine generated inputs and exact pixel/checksum results are the starting
baseline, with broader variants still to add.
Retain all frozen models, source images, labels and historical benchmark binaries.
No Greek decision, natural accuracy experiment or training is required here.

## Instance A — adapter and lifecycle ownership

Own the candidate Imaging project, decoder adapter, project dependencies and
source/byte/pixel/frame resource contracts. Begin in an isolated source copy.

1. Inventory the **actual pinned** ImageSharp3.1.11 fallback formats against the
   current Skia build before removing a reference. The pinned [default format
   configuration](https://raw.githubusercontent.com/SixLabors/ImageSharp/v3.1.11/src/ImageSharp/Configuration.cs)
   includes PBM, TGA and QOI as well as TIFF and common formats. Current vendor docs
   describe additional newer formats and are not proof of the3.1.11 baseline.
   A TIFF-only replacement must not silently remove another working import format.
2. Prototype BitMiracle.LibTiff.NET2.4.660 as the next bounded TIFF option. Its
   published BSD-style terms and bundled notices need retention. Keep explicit
   maintenance/security review ownership: current advisory absence is insufficient.
3. Preserve first-directory behavior, raw stored orientation/frame dimensions,
   exact encoded checksum, alpha premultiplication/channel order and pixel output.
   The research matrix shows all eight orientation tags match baseline when only
   the in-memory orientation is reset before decoding. Never rewrite the source.
4. Reject encoded/dimension limits before allocating the frame/raster. Bound tag
   counts/offset arithmetic and metadata allocation; do not treat output dimensions
   alone as a parser resource limit. Define cancellation/failure cleanup and avoid
   process-global error/configuration changes that race other decodes.
5. Preserve Decode, DecodeBounded and DecodeBand semantics. Distinguish actual
   region/scaled decoding from full-frame fallback and disclose temporary peak RAM.
   Keep every non-TIFF path and the existing Skia admission preflight working.
6. If extra permissive decoders or a restricted native build are needed, write a
   separate dependency/notice/maintenance/RID proposal before adding production
   dependencies. No quiet fallback to a differently licensed binary distribution.

Deliver a source/dependency diff, package graph and complete license files,
compatibility/resource evidence, exact unsupported cases and commands to rebuild.
Do not alter the reference baseline to make the candidate pass.

## Instance B — independent formats, failure and resource evaluation

Own a versioned generated fixture manifest, runner, expected results and independent
review. Coordinate adapter interfaces with A, but do not edit A's production files.

- Pin hashes before evaluation. Include classic/BigTIFF; stripped/tiled; faxG3/G4,
  LZW, Deflate, PackBits and JPEG; grayscale/bilevel/palette/RGB/RGBA/high bit depths;
  all orientations; multiple directories; ICC/EXIF metadata and malformed/truncated
  inputs. Add PBM/PGM/PPM, TGA and QOI fallback coverage if baseline accepts them.
- Compare current production output against candidate bytes/dimensions/frame and
  checksums, not only whether decode returned a bitmap. Declare acceptable lossy
  codec/color tolerances before measuring; never hide white/black inversion.
- Repeat the known fax inversion against any proposed TiffLibrary fork. Both the
  published0.6.65 and inspected latest source failed that fixture; a different
  commit must provide actual corrected output, not just a relevant commit title.
- Exercise oversized headers, offsets/counts, cycles, compressed metadata, huge
  declared dimensions, cancellation and allocation failure in owned subprocesses
  with registered limits and cleanup. Do not run unbounded decompression attacks
  in the API or on corpus assets.
- Run registered baseline/candidate ABBA resource comparisons, separating managed
  allocations, native/OS RSS, cold startup and steady state. Retain failures and
  negative results. No arbitrary timing thresholds in ordinary unit tests.
- Run the complete engineering gate, relevant existing image/import tests unchanged
  and packaged osx-arm64 operations. Other RIDs need their own evidence.

Handoff includes every fixture hash, source/assembly identity, expectation,
per-case outcome and process exit, plus a concise defects/gaps report. No training,
LM Studio calls, source-label acceptance or model promotion is part of this slice.

## Integration and release criteria

A consumes B's findings and fixes independently reproducible defects; B reruns the
frozen cases and reviews the final adapter. Root integrates only after approval of
the concrete dependency/behavior diff. Remove all production ImageSharp references
and verify the final graph and copied notices. Preserve working baseline formats,
source frames/pixels and prior models; unresolved gaps keep the item open.

Required evidence is functional compatibility, safe failure/resource boundaries,
maintainer ownership of the parser dependency and exact per-RID notices. Stock
Magick.NET's LGPL-bearing native distribution does not meet the selected
all-permissive replacement preference. A custom restricted build remains a separate
costed option, not an assumed solution. Even a clean full audit is not a complete
native SBOM, security proof, rights grant or Greek recognition acceptance.
