<!-- Copyright (c) 2026 QuantForgeSoftware. All rights reserved. -->
# ImageSharp replacement research and isolated PNG proof — 2026-11-05

## Decision supported by this investigation

The owner requests permissively licensed replacements. The strongest bounded
next step is **reviewing an AI-only strict-PNG replacement using the already
installed SkiaSharp 4.153.1**. Its isolated Release prototype passes all 194
existing AI tests, 20 new PNG corpus cases and one CRC differential test:
**215 passed, one live-provider test skipped**. No provider, training or vendor
license service was contacted. This was initially a partial candidate, not a
resolution of the product security gate. **Subsequent approved adoption:** the
exact reviewed PNG implementation was adopted into AI after
[independent review](imagesharp-png-independent-review-2026-11-05.md). Its live AI
graph no longer references ImageSharp; Imaging retains 3.1.11 and its six
advisories. The registered prototype measurements below remain the historical
evidence for that bounded change.

The TIFF replacement remains unresolved. Stock Magick.NET is actively maintained
and capable, but its native distribution is not entirely permissively licensed.
The two managed TIFF alternatives below have attractive licenses but insufficient
recent maintenance/security evidence to declare them a secure replacement merely
because NuGet lists no advisories. No production source or package reference was
changed during this research stage; both production references were still
3.1.11 then. Subsequent AI-only adoption is recorded above. The remaining Imaging
reference requires a validated replacement and whole-product audit.

## TIFF candidate comparison

| Candidate | Primary evidence and current package | Packaging / license | Practical cost and acceptance gaps |
| --- | --- | --- | --- |
| Magick.NET Q8 | [NuGet 15.0.0](https://www.nuget.org/packages/Magick.NET-Q8-AnyCPU/15.0.0), released October 10, 2026; [maintainer release](https://github.com/dlemstra/Magick.NET/releases/tag/15.0.0) | net10.0/netstandard2.0; osx-arm64 native library runs here. Wrapper Apache-2.0, ImageMagick has its own license, bundled native delegates include LGPL libraries. The package is about 84 MB; its osx-arm64 native file is about 28 MB. | Low adapter complexity, substantially larger native/security/license surface. Current version explicitly removes Intel macOS support. Does **not** meet an all-permissive dependency preference without a restricted native build and complete per-RID license review. |
| BitMiracle.LibTiff.NET | [NuGet 2.4.660](https://www.nuget.org/packages/BitMiracle.LibTiff.NET), published December 19, 2024; [repository](https://github.com/BitMiracle/libtiff.net) | Managed netstandard2.0, no NuGet dependencies; package about 296 KB. [New BSD license and retained IJG/libtiff/ComponentAce notices](https://raw.githubusercontent.com/BitMiracle/libtiff.net/master/license.txt). | Small integration surface and no new native family, but security/maintenance review still needed. Latest observed main commit December 18, 2024 adds GDAL_NODATA support; preceding main merge November 2023. Absence of advisory entries does not establish that modern TIFF parser defects are covered. |
| TiffLibrary | [NuGet 0.6.65](https://www.nuget.org/packages/TiffLibrary/0.6.65), published November 16, 2021; [moved repository](https://github.com/yigolden-oss/TiffLibrary) | [MIT](https://raw.githubusercontent.com/yigolden-oss/TiffLibrary/main/LICENSE); entirely managed. net6.0 is usable from net10.0; default transitive JpegLibrary 0.4.32 is also MIT/managed. Package about 1.5 MB. No native JPEG dependency in the restored default graph. | Stronger region/async API fit for large TIFF, but latest observed main commit November 20, 2021. Repository push timestamp April 2024 is not evidence of newer main codec fixes. A maintained fork/security review and actual format/resource probes would be required. Do not use its ImageSharpAdapter, which would retain the dependency being removed. |

Dates were checked against official NuGet registration and GitHub commit metadata;
receipts are in `.artifacts/imagesharp-replacement/research/`. These observations
do not prove that either managed project is abandoned or vulnerable; they identify
missing evidence for a security-sensitive adoption decision.

The research-only `probe/Probe.csproj` restores all three candidates from NuGet.
`nuget-audit.json` reports no known vulnerable packages in that graph on November
5. This is advisory coverage, **not** a source audit or native-CVE attestation.

### Stock Magick.NET native/license findings

The actual Release probe reports ImageMagick 7.1.2-33, TIFF support and delegates
including `heic`, `lqr` and `rsvg`. The shipped `Notice.txt` includes LGPL terms for
components including libde265, libheif and liblqr. `nm` finds de265 symbols in the
actual osx-arm64 native binary, so this is not solely a hypothetical README concern.
The wrapper's [Apache license](https://raw.githubusercontent.com/dlemstra/Magick.NET/15.0.0/License.txt)
does not replace these native notices. Copies of the package notice and runtime
probe are retained in the research artifacts.

The [vendor cross-platform guide](https://raw.githubusercontent.com/dlemstra/Magick.NET/15.0.0/docs/CrossPlatform.md)
describes custom statically linked builds. A TIFF/PNG-only build excluding
unneeded delegates is a possible engineering path, but requires maintained build
recipes, complete native SBOM/notices, reproducible assets and CI for each RID.
It is not a quick package substitution. No Ghostscript installation or PDF/PS
delegate was introduced here. Existing PDF rendering should retain its separate
backend.

### TIFF behavior that any replacement must prove

The [ImageMagick format list](https://imagemagick.org/formats/) advertises TIFF
strip/tile/compression support; the restored native notice includes libtiff
4.7.2. BitMiracle exposes BigTIFF and
[ReadRGBAImageOriented](https://bitmiracle.github.io/libtiff.net/help/api/BitMiracle.LibTiff.Classic.Tiff.html).
Its [scanline API](https://bitmiracle.github.io/libtiff.net/help/articles/KB/basic-introduction.html)
cannot simply be used for tiled input. TiffLibrary advertises standard/BigTIFF,
strips/tiles and PackBits, Huffman, T4/T6 fax, LZW, JPEG and Deflate; its APIs expose
region decoding, cancellation, memory pools and parallelism options.

These are vendor capabilities, not project parity results. Required generated
acceptance cases are first IFD only in a multipage file, BigTIFF offsets,
stripped/tiled arrangements, fax/PackBits/LZW/Deflate/JPEG, palette/grayscale/RGB,
associated/unassociated alpha, and all orientation values. Compare coordinates
and pixel semantics against the existing decoder before deciding whether to
preserve stored orientation or transform it. Existing first-page/source-checksum
tests must remain intact. Tile support in a library does not itself implement
the application's tiled cropping workflow.

### Actual tiny TIFF probes and a published-package defect

An isolated net10 Release consumer (`tiff-probe/TiffProbe.csproj`) now compares
the existing `ImageDecoder`, BitMiracle 2.4.660 and TiffLibrary 0.6.65 on ten
independently generated 7×3 fixtures. Source tags verify classic/BigTIFF,
strip/tile arrangement, compression and orientation. See `tiff-results.json`;
the generator is research-only and is not a proposed product dependency.

- Both managed candidates decode classic, BigTIFF, tiled, LZW, Deflate, JPEG and
  the first image of a two-page TIFF in these fixtures.
- **TiffLibrary 0.6.65 inverts the Group 4 sample:** its first pixel is white
  (255), while the existing decoder and BitMiracle both return black (0).
  This prevents adoption of the published package unchanged.
- TiffLibrary defaults to applying orientation 6, changing 7×3 to 3×7. Its
  `IgnoreOrientation=true` preserves the existing decoder's stored 7×3 geometry.
- BitMiracle's RGBA raster yields premultiplied `(100,10,5,128)` for the alpha
  fixture. TiffLibrary with `UndoColorPreMultiplying=true` yields unpremultiplied
  `(200,20,10,128)`; the existing Skia bitmap reports `(199,20,10,128)` after its
  premultiplication round trip. An adapter must account for alpha semantics and
  rounding explicitly.
- TiffLibrary's 2×1 crop matches its own full decode at that location. A supplied
  one-byte scratch pool rejects the tiled fixture, and a cancelled token produces
  `OperationCanceledException`. This does not prove all allocations use that pool
  or that every codec has bounded cancellation latency.

The [official release-commit-to-main comparison](https://github.com/yigolden-oss/TiffLibrary/compare/32f43490e4140563ae380799dd080ca57881daeb...main)
contains 15 unreleased commits, including fixes to BlackIsZero1/WhiteIsZero1
interpretation and FillOrder. A minimal isolated source build of exact main
commit `cec3a858de270a74965abe5b57a19b12c7a0e8d0` was therefore tested on the
same ten fixtures: **the Group 4 inversion remains**. See `tiff-main-results.json`
and `tiff-main-probe.log`. The source was built without upstream build scripts,
using its declared JpegLibrary 0.4.32. Modern .NET 10 compilation required two
explicit `.AsSpan()` overload disambiguations in its LZW encoder and still emitted
29 CS9191 ref/in compatibility warnings. No codec fix was invented to make the
probe pass. This rules out treating the existing unreleased fixes as a ready
solution. A maintained-fork evaluation would have additional correctness and
modernization work. Additional mixed-pixel 3×2 fixtures covering all eight
orientation tags show that the baseline preserves stored pixel order. Setting
BitMiracle's orientation tag to TOPLEFT **in memory only** before reading, or
using TiffLibrary's `IgnoreOrientation=true`, reproduces the baseline pixel arrays
for all eight cases. Default BitMiracle orientation handling differs for six
tags, so a default call would change the existing coordinate contract. Receipts:
`orientation-results.json` and `orientation-probe.log`. Associated alpha,
fax T4, malformed offsets,
large tags/strips/tiles and real-world capture diversity remain unproven.

Preflight dimensions, IFD/tag counts, offsets, arithmetic and encoded size before
large allocations. Bound scratch memory, parallelism, cancellation and malformed
input work. The current fallback loads its first image before its caller checks
the 12,000-pixel dimension ceiling, and bounded TIFF previews currently materialize
that full frame before shrinking it. Replacing a codec without correcting or
explicitly limiting that behavior would not establish a hard memory budget.

Magick's [security policy](https://imagemagick.org/security-policy/) supports
coder/delegate restrictions and memory/disk/time/list limits; memory limits may
spill to disk, and area is not a simple reject-above-pixels control. Its static
resource settings need a process-wide concurrency policy rather than per-request
mutation. Managed memory pools likewise require actual enforcement tests.

## Isolated AI static-PNG candidate

Review files live under
`.artifacts/imagesharp-replacement/prototype/src/LeviathanText.AI/`;
`vision-image-prototype.diff` compares the candidate with production. Only this
copied AI project changes its reference to SkiaSharp 4.153.1. The copied imaging
project remains a baseline, not a proposed Magick adoption.

The candidate copies/hashes the original bytes, retains the 8 MiB encoded and
four-million-pixel ceilings, checks PNG signature/IHDR/chunk lengths/CRCs and
all three APNG control chunk types (`acTL`, `fcTL`, `fdAT`), requires complete IEND framing, then requires
`SKCodec.EncodedFormat == Png` and successful full decoding into the preflighted
bitmap. The input stream and codec have explicit scoped disposal. It never
re-encodes or replaces the transmitted PNG. Both provider transport tests and
corpus probes assert the original bytes/hash are retained.

CRC uses the BCL ARM IEEE implementation behind its `IsSupported` check, with a
portable 8 KiB slicing-by-eight table fallback. This uses the
[documented BCL intrinsic](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.arm.crc32.arm64.computecrc32?view=net-10.0),
not custom P/Invoke. Known empty/123456789/IEND vectors and an independent bitwise
implementation test both paths across lengths 0–1024 and eight starting offsets.
The observed host executes the ARM path. Other hosts retain the tested portable
algorithm; their full decoder execution/performance remains a platform gate.

### Corpus, behavior changes and native contract

Seven valid cases cover RGB/RGBA/grayscale/palette, 16-bit RGB/grayscale and an
unknown ancillary chunk. Assertions verify generated IHDR bit depths/color types,
not only filenames. Thirteen rejection cases cover APNG, orphan frame-control/frame-data chunks, CRC, corrupt IDAT,
duplicate IHDR, missing IEND, truncated chunks, over-budget header dimensions,
JPEG/GIF spoofing and two trailing-data variants.

The baseline accepts missing IEND and bytes/chunks after IEND; the candidate
rejects them. This is deliberate structural tightening, **not** exact acceptance
equivalence. Independent review found that the first prototype accepted orphan
`fcTL`/`fdAT` chunks rejected by the baseline; the final candidate explicitly
rejects both, with the independent generated reproductions retained as regression
fixtures. Acceptance of unknown critical chunks is shared with the baseline;
this is not claimed to be a complete PNG specification validator. All seven valid cases retain dimensions and exact original bytes;
all existing AI expectations remain unchanged. Copied test fixture builders and
the test's independent transport-pixel decoder were changed to Skia, retaining
the expected grayscale pixel assertions.

Release publication and the published executable run on osx-arm64 without
ImageSharp or vendor keys. The emitted Skia native asset is about 15 MB and
contains arm64/x86_64 slices; that does not claim an Intel execution test.
The full application already uses the same Skia version/native family in Imaging,
but a standalone AI consumer now gains that native dependency. The default AI
graph includes macOS/Win32 assets and no Linux assets; Linux deployment needs its
explicit native-package policy and actual execution proof.

SkiaSharp's [wrapper license](https://github.com/mono/SkiaSharp/blob/v4.153.1/LICENSE.txt)
is MIT. Its native package also ships comprehensive `THIRD-PARTY-NOTICES.txt`
covering optional/dual-licensed components. Preserve and review the actual RID's
existing notices/SBOM rather than declaring every native component MIT. This
candidate reuses the existing product asset family and adds no Magick/Ghostscript
runtime. Native decoder scratch/profile allocations and CPU time are not a
proven process-wide hard cap merely because the destination bitmap is bounded.

### Registered throughput/allocation comparison

The first per-bit CRC prototype caused a large slowdown and was discarded.
The final matched experiment registered SHA-256 hashes of fixtures and both
Release harness/AI binaries **before execution**, then ran ABBA three times
(12 separate processes), three warmups per fixture, three rounds per process,
fixed 1,000/30/10 iterations for small/medium/large fixtures. Runtime is .NET
10.0.9 on macOS arm64. The final run was re-registered after the APNG review fix;
see `abba-final/registered-manifest.json` and `abba-final/summary.json`. The earlier
`abba/` receipts are retained for comparison and do not identify the final binary.

| Generated PNG | Baseline median ms | Candidate median ms | Ratio | Managed bytes/image, baseline → candidate |
| --- | ---: | ---: | ---: | ---: |
| 16×8, 585 encoded bytes | 0.007599 | 0.006871 | 0.904 | 6,802 → 2,968 |
| 512×256, 459,033 bytes | 1.7403 | 1.7505 | 1.006 | 476,629 → 461,512 |
| 2048×1024, 7,251,933 bytes | 27.2607 | 28.2516 | 1.036 | 7,428,663 → 7,254,661 |

Per-process ranges overlap. Host load varied between experiments, so these are
registered comparative measurements, not proof of identical performance or a
statistically established regression. Median process peak RSS from macOS
`/usr/bin/time -l` is 123.7 MB baseline versus 116.0 MB candidate; managed
allocation excludes native allocation, and process peak is not a per-image cap.
No training or live inference contributed to this experiment.

## Remaining decision and concrete next work

Review the small AI-only source/test candidate before any adoption. Its isolated
NuGet graph has no known advisories in `ai-audit.json`, but **the six ImageSharp
advisories remain product-wide while Imaging retains 3.1.11**. Removing one direct
reference does not halve or remove the remaining package advisories.

For a wholly permissive TIFF path, **BitMiracle is the next bounded adapter
candidate to investigate**, based on these basic format/orientation controls;
it is not approved for production or declared security-maintained. Define
maintenance/security ownership, preflight malformed IFD/tag/offset/resource
limits, test associated alpha and additional fax/strip/tile cases, and preserve
the existing first-page/source-identity tests. The published and exact-main
TiffLibrary candidates require resolving the demonstrated fax inversion before
they can compete on compatibility. If neither meets those gates, investigate a restricted maintained
native libtiff binding/build with complete RID packaging and notices. Stock
Magick convenience does not settle the licensing requirement. No TIFF adapter,
full imaging parity, Linux/Windows execution or fuzzing result is claimed here.
