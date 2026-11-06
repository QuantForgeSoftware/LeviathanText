# Existing ImageSharp fallback pixel transfer: isolated backport proof

The selective candidate passes the same36 tiny functional tests as the actual current production Imaging binary. Production source remains unchanged at this handoff. This proposal retains ImageSharp3.1.11 and all of its existing parser behavior, including known legacy PNM decoding quirks. It does not close SEC-01 or introduce a new dependency.

## Exact change

Only the pixel-copy section of `TryDecodeWithImageSharp` changes. For an actual BGRA8888/premultiplied destination with a usable PeekPixels view, one view is held for the copy. The ImageSharp pixel-row callback obtains one destination span and writes explicit BGRA bytes using native `SKPMColor.PreMultiply`, preserving Skia's established arithmetic. It validates row/extent bounds before writes. Other destination representations and an unavailable view retain the original SetPixel loop. The view is disposed after the callback; the returned bitmap remains caller-owned. A transfer exception disposes the newly owned bitmap before the existing exception translation runs.

ImageSharp Identify/Load options, MaxFrames1 behavior, source limits, checksums, public API signatures, budgets, resampling and exceptions are unchanged. The current root crop-overflow and derivative-ownership tail is preserved verbatim. The patch is a selective hunk, not a replacement of the entire old file. No global fault hook or additional format parser is introduced.

## Preregistered functional comparison

Artifacts are `.artifacts/experiments/main-fallback-pixel-transfer-v1/`:

- `source-binary-before-freeze.json` captured current source, exact actual built Imaging/Core/Contracts binaries and isolated candidate source before new outputs. Original Imaging SHA-256 is `d6a60b0580391e0359172ce4e509acd3985621a51e5128ddb598dddb7c4163b8`.
- `before-proof-freeze-v2.json` pins the15 new public tests and21 existing crop/ownership controls. Version1 is retained; version2 removed an unnecessary nongeneric Image alias before any compile or output, with no product-source change.
- `execution-registration.json` fixes the actual-before-first sequence, selected classes,120-second per-command cap and no-fitting exclusions. Only these tiny test sources are compiled; no training project or provider is invoked.
- `execution-results.json` and TRX files record actual before36/36 and candidate36/36, no skips or failed cases. The before tests load the exact preserved production DLL, not an extraction baseline. Three isolated builds succeeded with zero warnings/errors.
- All14 accepted format cases have byte-identical generated input, canonical pixel hashes and public metadata receipts across before/candidate. The fifteenth case checks invalid-input typed refusal/source preservation. `closure.json` confirms registered inputs remained unchanged.

The new cases cover every8-bit alpha value across all four TGA origins in raw and legal RLE packets; QOI alpha; TIFF RGBA and a two-frame TIFF;16-bit PGM/PPM and binary-whitespace PPM legacy behavior; and invalid bytes. Each accepted case asserts native SKCodec does not handle the fixture on this platform, proving fallback execution, then compares actual public Decode, in-budget DecodeBounded and cropped DecodeBand pixels against the existing parser plus legacy SetPixel oracle. Dimensions, original frame sizes/scales, source checksums/bytes and untouched input files are checked. These tests preserve old PNM bugs rather than asserting a new source-spec interpretation.

The21 existing derivative controls pass on both packages, guarding the already integrated root fixes. No measured native leak, exceptional-path allocation failure, alternate-platform representation, universal performance improvement or cross-platform pixel guarantee is claimed. No resource benchmark has run for this main backport at this point.

## Handoff

`pixel-transfer.proposed.patch` and `tests/FallbackPixelTransferTests.cs` are the proposed selective source/test changes. `matched-package-freeze.json` describes paired `original-main-binary/` and `candidate-main-binary/` runtimes: only Imaging DLL/PDB differ; Core/Contracts and the original dependency manifest are byte-identical. Dependencies were not rebuilt for the candidate. Main integration, full no-fitting engineering checks and any actual decoder resource comparison require parent review/coordination. The broad managed-TIFF fork remains a separate blocked replacement candidate.

## Actual-main resource preparation (no timing result)

Parent approved preparation of a separate actual-main comparison, independent of the fork's A17 measurements. The existing ResourceProbeProgram and functional Decode/hash helper are reused byte-for-byte, built against this proposal's exact current-main binaries. Complete baseline/candidate runtime pairs differ only in Imaging DLL/PDB; the ImageSharp, Skia and native dependency bytes are identical. The package freeze is `resource-proof/packages-before-functional-freeze.json`, SHA-256 `5682354f324bd73553e44907a564a19efb12a67c7c61b1e95dd1d8800749d3cc`.

The eight preexisting frozen inputs are P6, QOI,24-bit TGA and tiled RGB deflate TIFF at1024×768 and3001×2000. The actual current-main Decode outcomes were captured and sealed **before** evaluating the candidate, without assuming they equal the permissive fork's source oracles. All eight candidate outcomes exactly match those own-main pixel/dimension/source-identity records;16 owned functional processes exited0 and were reaped, with source/runtime closure intact. `actual-main-legacy-oracle-freeze.json` SHA-256 is `0d4d66a732008855e960f947692d8508a3c71794510faf5abe5db0706b0c820f`.

The proposed timed protocol is32 serial visits, ABBA per input, reusing the existing8-call program: one cold call, one warmup and six measured calls, no forced collection between calls. Canonical pixel/source checks run outside the timer and validate every sample. Managed allocations are measured around Decode; sampled process RSS also includes the helper's outside-timer hashing and its allocation/GC effects, so it is not a decoder-only native-memory figure. The registered independent limits are60-second wall/45-second CPU/1GiB sampled RSS per process,16KiB stdout/64KiB stderr/128KiB result,1200-second global batch cap, no concurrent workloads or adaptive visits, and abort on any mismatch/limit.

`resource-proof/timed/registration.json` is the preserved unexecuted first seal. `timed-v2/registration.json`, SHA-256 `c861ac418659529bf14f371d58c6e0bc52eeaf1d76461b5c1236baeebc693f63`, additionally anchors and rechecks the frozen source/package records; workload, program and limits are unchanged. No timing has run at this handoff. Parent review and a coordinated quiet window are required before execution. A favorable result would support this selective transfer change, not remove ImageSharp advisories or qualify the separate managed-TIFF replacement.

## Approved actual-main Decode measurements

The parent-approved exact `c861ac…` timed-v2 registration completed all32 serial ABBA visits in210.788 seconds. All256 decoded pixel/dimension/source-identity samples matched the frozen own-main legacy oracles. Every owned process exited0 and was reaped; no wall/CPU/RSS/log/result/global cap fired. Input, frozen source and complete runtime hashes remained unchanged. The controller exited0. No adaptive visits, retries, cap changes or main-source changes occurred during the batch.

The table reports the median of12 measured calls per variant/input (six calls in each of two fresh process visits); cold first calls and warmup calls are retained separately in the raw results and excluded from these medians. “Peak RSS” is the maximum sampled process RSS across those visits, including the outside-timer checking/final-GC work discussed above.

| Generated input | Decode ms, before → candidate | Managed allocation MiB, before → candidate | Sampled peak RSS MiB, before → candidate |
| --- | ---: | ---: | ---: |
|1024×768 P6 |339.655 →6.395 |146.256 →2.255 |90.7 →78.4 |
|1024×768 QOI |332.408 →2.919 |144.568 →0.568 |85.4 →70.6 |
|1024×768 TGA |343.431 →3.597 |146.255 →2.255 |104.1 →89.6 |
|1024×768 tiled deflate TIFF |343.474 →7.268 |144.038 →0.039 |86.6 →69.2 |
|3001×2000 P6 |2653.874 →23.373 |1116.180 →17.178 |207.3 →193.4 |
|3001×2000 QOI |2738.902 →21.028 |1103.309 →4.307 |142.7 →133.2 |
|3001×2000 TGA |2710.880 →27.904 |1116.177 →17.178 |294.0 →279.7 |
|3001×2000 tiled deflate TIFF |2692.906 →20.262 |1099.220 →0.221 |126.2 →114.9 |

These are descriptive matched results on this host and these fixed generated inputs, not confidence bounds or a universal speed/memory guarantee. “Cold” means the first call in a new process; operating-system caches were not flushed. Timers cover actual public Decode, not only a synthetic pixel-copy loop. The candidate preserves the existing parser and decoded values; the result is independent of the separate fork's A17 measurements. Full ImageSharp and Skia image buffers still exist, and the encoded/source-dimension limits do not constitute a hard total-memory bound.

All per-visit medians, cold/warm samples, CPU counters, final-GC records, logs, native process observations and exact order are retained in `resource-proof/timed-v2/`. `analysis.json` records all eight groups, while `verification.json` and `processes.json` establish closure and all256 fixed-output checks. Main integration and its no-fitting engineering gate are separate parent-owned actions; this report itself applies no production patch and does not clear licensing/advisory gates.


## Parent integration checkpoint

Root applied only the reviewed transfer hunk and its15 public regressions after the fixed resource comparison closed. The current derivative fixes remain intact. Fresh main engineering-v6 passes2050 cases with18 intentional skips, zero failures and zero build warnings/errors. Full frontend371 and production Angular build also pass after the separate principal namespace seam. PERF-03 is archived; SEC-01 remains open. Earlier statements about preparation/unmodified production describe their frozen handoff checkpoints, not the current integration state. No dependency version, model or training changed.
