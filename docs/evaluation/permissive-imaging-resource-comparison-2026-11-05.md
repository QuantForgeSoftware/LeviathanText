# SEC-01 generated resource comparison

Status: primary decoder and archive sets completed; no production replacement approval. This report separates matched resource evidence from tiny functional coverage and from model accuracy. No fitting, model inference, labels, natural corpus, providers, or LM Studio calls occur.

## Frozen workload

The baseline is the pre-replacement Imaging/Contracts/Core snapshot; candidate is the immutable v13 prototype. Source, candidate registration, binaries, package/native files, harness and generated input hashes are recorded before each batch. The decoder matrix uses seven formats at1024x768 and3001x2000: PNG, JPEG92, RGB TIFF none/256-row strips, RGB TIFF Deflate/256-pixel tiles, bilevel TIFF Group4, associated RGBA16 TIFF none/64-row strips, and RGBA QOI. Each uses Decode, DecodeBounded500000 and DecodeBand at the fixed source mid-row for32rows. Band receives its fixed top directly; no extra Dimensions call is included in its measured API work.

Actual baseline execution, rather than scale arithmetic, establishes that **all14bounded results are already within500000pixels**. JPEG returns768x576/751x500. Therefore this batch requires exact original pixels/dimensions on both sides; no changed-output speed comparison is allowed. The earlier prediction of JPEG overshoot in these sizes was incorrect and was corrected before candidate resource outputs. Five tiny true-overshoot examples remain a separate correctness study.

For each file/operation, four fresh processes run baseline,candidate,candidate,baseline. Each has1cold call,1warmup and6measured calls. Every returned bitmap is hashed in canonical Skia RGBA using one small row buffer outside API time/allocation measurements; source SHA, raw dimensions and API checksum/encoded count are checked. No forced GC occurs between calls; final disposal is followed by full GC with raw diagnostics retained. The known GetTotalMemory counter anomaly is not treated as verified live heap.

The initial design mentioned a reverse BAAB replicate after primary parity/budget success. An explicit1500-second trigger was recorded only after the primary set had started and its initial progress was visible. That late record is preserved candidly, **is not an untouched preregistration, and will not trigger execution**. Root directed completing only the primary decoder and separately registered archive sets, then releasing the quiet window. Any reverse-order follow-up requires a separate reviewed proposal after primary results, with post-observation scope disclosed and no favorable-metric selection.

Each owned process has60s wall,45s CPU,512MiB observed sampled RSS and64KiB logs, with finally cleanup/reaping. Sampling can miss transient peaks and is not an OS-enforced universal RSS bound. Other instances acknowledged a quiet window before execution; root and both other instances hold builds/tests/CPU profiles. Code/docs preparation can continue. Native synchronous decode is not cancelable by this harness; the owning process is terminated on its deadline.

## Separate archive comparison

A separate batch compares the exact old ProjectBundle validator (Identify then Load, MaxFrames1, SkipMetadata=true, TargetSize1x1) with candidate ValidateEncodedSnapshot on the **same caller-owned byte array**, capped25MiB and12000dimensions. It uses the two sizes across PNG/JPEG/GIF/WebP/TIFFDeflate/QOI/PNM, all14encoded sources below25MiB. Input materialization/hash occurs before API timing. The strategy/full-destination flag, original dimensions, acceptance, unchanged snapshot and resource counters remain visible. This is a decodability comparison, not a strict entropy-integrity proof; recoverable malformed JPEG controls remain accepted observations in the independent functional report.

Full native destination modes for PNG/GIF/WebP/TIFF must be compared to oldTarget1, not to old full-image decoding. A false destination flag does not bound native scratch or peak RSS. Metadata-only tiny compressed ICC/text observations and large sparse Dimensions tests are separate controls, not substitutes for these matched actual validation routes.

## Results and limits

All168decoder and56archive processes exited0 with exact required outputs, unchanged sources/binaries and no watchdog/resource/log failure. Decoder wall944.15s; archive wall16.42s. All owned processes were awaited; quiet release preceded other build/test work. ReverseBAAB remains unexecuted. Do not pool the best repetitions. No universal throughput, all-platform compatibility, complete TIFF coverage, natural accuracy, proportional Band memory, or total native memory correction follows from this shared-host generated study. Gray-plus-alpha two-channel formats refused by the old baseline are excluded from these14inputs; v13's incorrect new pixels are preserved separately and deferred behind typed refusal in a later prototype, never counted as validated new capability here.

Artifacts: `.artifacts/experiments/sec01-independent-v1/fixtures/representative-resource-*`, `resource-source-oracles.json`, `resource-oracle-freeze.json`, `resource-replicate-policy.json`, `harness/run-resource*.py`, and corresponding per-batch `runs/` registrations/processes/results/verification. Decoder and archive batches have separate60-minute limits. A later source change requires explicit source-delta review and fresh affected evidence before adoption.

## Primary results

The table reports each variant's mean of the two fresh-process means (six measured API calls each). Canonical pixel hashing occurs outside API timing/allocation. With only two processes per variant and one shared host, small differences are observational; no confidence/throughput acceptance threshold is invented afterward. Raw cold/warmup/call values, per-process means, GC diagnostics and sampled peaks remain in `analysis.json` and every process JSON. Cumulative allocated bytes are allocation churn, not simultaneous/live heap.

### Six-megapixel full decoding

| Source | Old/new API ms | Old/new allocated MiB per call | Old/new maximum sampled RSS MiB |
| --- | ---: | ---: | ---: |
| 3001x2000.png | 26.84 / 26.86 | 0.251 / 0.255 | 82.5 / 82.4 |
| 3001x2000.jpg | 24.48 / 24.34 | 4.128 / 4.132 | 92.9 / 93.2 |
| 3001x2000-rgb-strips256.tif | 2697.91 / 39.86 | 1116.186 / 44.603 | 210.7 / 277.4 |
| 3001x2000-rgb-deflate-tiles256.tif | 2696.07 / 67.60 | 1099.212 / 28.324 | 126.6 / 206.8 |
| 3001x2000-bilevel-g4.tif | 2690.91 / 37.80 | 1099.165 / 23.349 | 123.9 / 106.1 |
| 3001x2000-rgba16-associated-strips64.tif | 2689.54 / 74.21 | 1144.812 / 47.421 | 353.5 / 319.7 |
| 3001x2000.qoi | 2678.13 / 2693.81 | 1103.310 / 1103.310 | 142.7 / 117.1 |

TIFF full decoding has a large measured CPU/allocation benefit, but **RSS does not consistently improve**: RGBnone and Deflate tile peaks increase. PNG/JPEG are approximately unchanged; QOI full decoding remains expensive with roughly1.10GiB allocation churn per6MPcall on both sides. This is not a total native/RSS correction. A separate differential investigation is required before attributing QOI cost to an individual method.

### Six-megapixel TIFF32-row bands

| Source | Old/new API ms | Old/new allocated MiB per call | Old/new maximum sampled RSS MiB |
| --- | ---: | ---: | ---: |
| 3001x2000-rgb-strips256.tif | 2714.30 / 2.18 | 1116.194 / 9.691 | 211.9 / 71.7 |
| 3001x2000-rgb-deflate-tiles256.tif | 2710.04 / 14.95 | 1099.227 / 6.227 | 126.9 / 63.7 |
| 3001x2000-bilevel-g4.tif | 2722.21 / 3.24 | 1099.175 / 2.303 | 127.9 / 66.0 |
| 3001x2000-rgba16-associated-strips64.tif | 2694.95 / 3.19 | 1144.821 / 1.636 | 355.5 / 68.1 |

The named ROI paths preserve exact source-coordinate pixels while reducing measured work. This does not prove proportional memory for arbitrary one-strip/one-tile files: native decompression may still need the largest segment, including a whole-frame segment. All14bounded500000outputs also match exact legacy oracles; TIFF bounded paths still materialize a temporary full raster.

### Six-megapixel archive validation versus actual oldTarget1

| Source | Old/new API ms | Old/new allocated MiB per call | Old/new maximum sampled RSS MiB |
| --- | ---: | ---: | ---: |
| 3001x2000.png | 59.51 / 26.32 | 0.051 / 0.002 | 73.4 / 77.1 |
| 3001x2000.jpg | 34.01 / 22.84 | 0.035 / 0.002 | 63.9 / 59.0 |
| 3001x2000.gif | 62.26 / 6.56 | 0.061 / 0.002 | 81.0 / 80.0 |
| 3001x2000.webp | 43.12 / 4.04 | 0.241 / 0.002 | 127.6 / 98.3 |
| 3001x2000-rgb-deflate-tiles256.tif | 32.52 / 70.96 | 0.103 / 28.178 | 81.6 / 202.7 |
| 3001x2000.qoi | 35.86 / 6.19 | 0.047 / 0.001 | 81.0 / 58.7 |
| 3001x2000.ppm | 26.34 / 13.21 | 0.047 / 0.001 | 90.6 / 71.1 |

**TIFF archive migration remains blocked pending root review/correction.** At6MP, the candidate full-frame TIFF validator is2.18times slower, allocates28.18MiB versus0.103MiB, and reaches202.7MiB sampled RSS versus81.6MiB. The1MP TIFF also raises allocation/peak RSS despite lower time. A future separately registered all-firstIFD segment walk may remove the full destination; it must retain admitted format/pixel-decoding checks and disclose largest-segment/native scratch. The observed improvement of full TIFF Decode cannot justify this archive regression.

Other measured archive formats are faster in this small generated study; PNG/GIF/WebP still report full native destinations, so they do not gain a hard arbitrary-size memory guarantee. The reused expanded iCCP probes carry an ICC-shaped header with zero tags/illuminant, not a complete color profile; fully valid large-transform ICC remains a separate metadata qualification gap. No prototype is promoted from these results, and no natural accuracy statement follows.
