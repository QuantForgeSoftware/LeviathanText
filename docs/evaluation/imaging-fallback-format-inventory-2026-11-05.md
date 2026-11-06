# Current imaging fallback format inventory — 2026-11-05

A TIFF-only replacement would remove other demonstrated working imports on this
osx-arm64 build: **PBM, TGA and QOI also require the current ImageSharp fallback**.
All nine generated fixtures decode successfully through the current production
`ImageDecoder.Decode`. This is a small compatibility inventory, not replacement
adoption, a security gate closure or a complete format/variant certification.

## Pinned baseline and generated inputs

The actual Imaging project pins ImageSharp **3.1.11** and SkiaSharp **4.153.1**.
The pinned [ImageSharp 3.1.11 default configuration](https://raw.githubusercontent.com/SixLabors/ImageSharp/v3.1.11/src/ImageSharp/Configuration.cs)
registers PNG, JPEG, GIF, BMP, PBM, TGA, TIFF, WebP and QOI. Newer documentation for
formats such as EXR/ANI does not establish this baseline's installed coverage.
The configuration was consulted for API inventory only; no decoder algorithm was
copied or vendored.

An isolated Release probe references the current production Imaging project and
uses its already cached encoder dependency. It generates nine 16×12 opaque RGB
gradient inputs through the old encoders: JPEG quality 95, lossless WebP, PBM
black/white and other encoder defaults. The PBM fixture is the plain P1 variant;
GIF is one frame. These are tiny trusted fixtures, not acquired corpus assets.

Input SHA-256 values were fixed before evaluation. Current production source,
project, harness, managed assemblies and copied native libraries were also hashed
before evaluation and verified unchanged afterward. Actual hashes include:

- `SixLabors.ImageSharp.dll`: `27ef3f04651914be319d3c75a890e985f7e77f9c340ad9bc7bb0b337a9730588`
- `SkiaSharp.dll`: `2c9bbbe1a3d0e9407894c319821b5cdcade1f302466a950a2e5d6024aee32422`
- osx `libSkiaSharp.dylib`: `11a672e8df0701967aa041b449bb4db6f9af040a5e8b0ed9ea44a922ec0effaa`
- current `LeviathanText.Imaging.dll`: `40849d644f25c112000532eea792fb56a47aa4de40d0eb063a81b1ce655a665d`

Other copied RID assets were hashed for package identity only; they were not executed.
No dependency version, production source, test assertion or licensing policy changed.
This baseline research does not authorize keeping or redistributing a replacement
under different terms, or resolve the existing ImageSharp release blocker.

## Observed availability and pixel preservation

| Generated format | Bytes | SKCodec created | Direct SKBitmap decoded | Production decoded | Observed path |
| --- | ---: | --- | --- | --- | --- |
| PNG | 137 | yes | yes | yes | Skia primary |
| JPEG | 767 | yes | yes | yes | Skia primary |
| GIF | 1,022 | yes | yes | yes | Skia primary |
| BMP | 630 | yes | yes | yes | Skia primary |
| PBM (P1) | 394 | no | no | yes | ImageSharp fallback |
| TGA | 606 | no | no | yes | ImageSharp fallback |
| TIFF | 792 | no | no | yes | ImageSharp fallback |
| WebP (lossless) | 56 | yes | yes | yes | Skia primary |
| QOI | 787 | no | no | yes | ImageSharp fallback |

Every production result has the expected 16×12 frame, exact original encoded-byte
count and exact source SHA-256 checksum. Canonical pixels are ordered `GetPixel`
RGBA bytes, independent of backing bitmap channel layout. The five primary-path
results match direct Skia pixels exactly. The four fallback results match the old
ImageSharp reference pixels exactly. Every input/output pixel digest is retained.

JPEG's production pixels match direct Skia exactly; they differ from an independent
ImageSharp JPEG decode (maximum channel delta 87, mean 5.5625 on this small colored
gradient). No cross-decoder lossy tolerance was declared or applied. This is not a
production regression: the unchanged primary Skia result is the current baseline.
A future candidate must declare its lossy/color comparison policy before measuring,
rather than treating a different decoder's JPEG pixels as interchangeable.

For unsupported primary formats, `SKCodec.Create` returns null and this installed
`SKBitmap.Decode` throws `ArgumentNullException(codec)`. Production already catches
primary exceptions and reaches its fallback successfully. The first inventory
runner called direct Skia without capturing that exception and exited 134; its
failure and original registration are retained. The corrected runner records the
exception per format, pins its revised harness/binary before evaluation, and exits
zero. Both generation and final evaluation exited zero. Final build: zero warnings
and zero errors. This runner correction is not a production decoder change.

## Handoff and limits

Evidence root: `.artifacts/experiments/imaging-fallback-format-inventory-v1/`.
`registration.json` pins source/binary/input-manifest identity; `fixtures/inputs.json`
contains all nine full input hashes; `fixtures/results.json` retains source checksums,
frame dimensions, pixel hashes, decoder availability and reference deltas;
`verification.json` records the final invariants and clean process exits. The explicit
Release probe source/project is in `probe/`, with outputs isolated under
`/private/tmp/lt-midday-fallback-inventory`.

This inventory concretizes the coverage requirement in the
[parallel replacement brief](../tasks/permissive-imaging-replacement-2026-11-05.md).
Preserve PBM/PBM-family, TGA and QOI working routes as well as TIFF, or make any
proposed removal explicit for owner review before dependency replacement. Existing
primary PNG/JPEG/GIF/BMP/WebP behavior remains a required baseline.

Remaining coverage includes P2–P6 PBM-family encodings/PGM/PPM, TGA variants, QOI
alpha, complex TIFF compression/bit depths/orientations/directories, animated formats,
metadata/color profiles, malformed and oversized resources, DecodeBounded/DecodeBand,
actual corpus captures, lifecycle/resource measurements and every other RID. No
untrusted decompression probe, model, fitting, OCR/VLM/provider call or performance
claim was part of this bounded task.
