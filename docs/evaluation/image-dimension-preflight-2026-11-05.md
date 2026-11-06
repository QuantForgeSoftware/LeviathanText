<!-- Copyright (c) 2026 QuantForgeSoftware. All rights reserved. -->
# Image dimension preflight resource proof — 2026-11-05

`ImageDecoder.Decode` now checks Skia's codec dimensions before allocating a
decoded bitmap. Oversized Skia-supported images retain the existing
`RejectedArchive` error and dimension details. If the remaining decoded-bitmap
check rejects a fallback result, that bitmap is disposed before throwing.

## Registered baseline and genuine allocation evidence

The production source was unchanged from HEAD before this work. Its SHA-256
was `4fe3877739141ace2b923f5d9949694c44447df8ff1ac5499ab7cc77d1a1424d`;
the original file was copied before editing. The baseline harness references the
frozen release assemblies from `.artifacts/releases/midday-runtime/cli/`, rather
than recompiling the patched source as a supposed baseline.

A streaming generator created a **valid white RGB PNG, 12001×4096 pixels**:

- Encoded size: 152,628 bytes.
- Declared RGBA bitmap: 196,624,384 bytes.
- PNG SHA-256: `793315c16d592616d9a7116cb926bd98302b883f6af51dfce2a1e09774789dbb`.
- Independent chunk-CRC/zlib verification checks all 4,096 scanlines, using a
  maximum 36,004-byte decoded verifier buffer. No full decoded verifier image
  was allocated.

The fixture/source/frozen-assembly manifest was recorded in
`.artifacts/image-dimension-preflight/before-registration.json` **before any
production edit**. An initial actual baseline call rejected the file only after
reaching 253,968,384 bytes process peak RSS. This is the resource defect; an
oversized malformed header alone would not establish it.

Both before and after return the same error. The three functional preflight
controls already passed on the baseline, as recorded by the coordinating
instance's `dimension-before-real.trx`. They are not described as failing-before
regressions. The valid-image subprocess measurements distinguish the resource
behavior that those functional assertions cannot observe.

## Candidate and matched process measurements

The candidate opens a scoped stream/`SKCodec`, checks `Info.Width` and
`Info.Height` against the existing 12,000 ceiling, and preserves
`LeviathanException` rather than swallowing it into the fallback. Encoded size,
checksum computation, accepted dimensions, error fields and fallback selection
policy remain intact. The post-decode check caches dimensions and disposes the
owned bitmap before creating the rejection.

Two ABBA sequences produced **four baseline and four candidate fresh processes**.
Each process performs exactly one real decode call. Each sequence registers
fixture/harness/Imaging-assembly/source hashes before execution and rechecks the
measured binary hashes for each run. `/usr/bin/time -l` records actual macOS peak
resident memory; .NET is 10.0.9 on arm64. No fake allocator or large measurement
buffer is used.

| Valid oversized PNG rejection | Baseline | Candidate |
| --- | ---: | ---: |
| Median process peak RSS, four runs each | 253,935,616 bytes | 57,114,624 bytes |
| Peak RSS range | 253,837,312–254,001,152 | 56,934,400–57,327,616 |
| First ABBA actual call times | 77.8, 63.9 ms | 267.0, 12.9 ms |
| Second ABBA actual call times | 82.8, 68.4 ms | 21.8, 13.6 ms |

The median peak reduction is **196,820,992 bytes**, closely matching the avoided
196,624,384-byte bitmap. First-load timing is noisy: the initial standalone
baseline call took 363 ms, and the candidate's first measured load took 267 ms.
The second registered sequence follows loading both artifact paths; its median
call times are 75.6 ms baseline and 17.7 ms candidate. These timings are observations
of this rejection case, not a universal image throughput guarantee or a hard
process memory cap.

Receipts under `.artifacts/image-dimension-preflight/`:

- `generate.py`, `fixture.json`, `oversize-valid-white.png`;
- `before-registration.json`, `before-result.json`, `before-rss.log`;
- `run-registration.json`, `01-A.json` through `04-A.json`, paired `*-rss.log`,
  `summary.json`;
- `abba-repeat/run-registration.json` and its four result/RSS pairs and summary;
- `baseline/` and `candidate/` one-call harnesses; the candidate build outputs are
  isolated under `.artifacts/image-dimension-build/`.

## Verification and limits

All **51 Imaging tests pass**, including the existing valid boundary-width pixel
and exact encoded-checksum control, oversized malformed-width/height controls,
TIFF first-frame behavior, preview/crop and PDF tests. The dependent image
resolution, PAGE image import and capture relocation subset passes **19/19**.
Candidate Release build and these test builds have no warnings. Logs:
`candidate-build.log`, `imaging-tests.log`, `application-tests.log` in the evidence
directory. `git diff --check` passes.

This fixes the Skia-supported full-image path and disposal on the remaining
dimension rejection. **TIFF fallback preallocation remains unresolved:** when
Skia has no codec, ImageSharp still loads the first frame before the caller's
dimension check. Its oversized result is now disposed when rejected, but the
initial allocation has not been prevented. This work does not change TIFF
backend/licensing, general tiled decode, archive/encoded-byte limits, crop
argument policy, derivatives, native decoder scratch allocations or metadata
resource budgets. No model training or live provider calls were made.
