# NT-20 P46 retained-page resource route — 2026-11-05

This registered comparison exercises production surface recognition against the
three retained Michigan P46 review photographs. Its purpose is resource and process
lifecycle coverage. It does not score recognition accuracy, change source readings,
resolve specialist decisions, fit models or promote a checkpoint.

## Frozen material and geometry

The source is the existing `specialist-packet-v1`, not a new acquisition. Its
preserved APIS 3612/3613/3614 canvas `dlxs_ri` notices identify the photographs as
public domain with Public Domain Mark 1.0. University of Michigan attribution,
item notices and packet manifest are copied alongside the replay inputs. Separate
CSNTM photographs are not used. The local files match their packet manifest hashes.

| NTVMR page | Retained image pixels | Proposed bands | Image SHA-256 |
| --- | --- | ---: | --- |
| 1440 | 2200 × 2536 | 27 | `ed2b5873fcf76c410ed94fb0bc99d56ecd4c0af4b61ec23054ebcef1d338ca5d` |
| 1450 | 2200 × 2577 | 26 | `a13a5e4bfbdc3ee8299d47590efd84bd4479d41b7221e7c3367f9434df139608` |
| 1460 | 2200 × 2626 | 28 | `4079842d996f67a137c61b700b8e694f5c135ee2aecefdbe19e19e0a15a7d4a4` |

All 81 quadrilaterals come unchanged from `crop-map.json`, SHA-256
`a9a658cd5724e4effb641573c591d357c50bb647fcdf1ee0453bbca3e43f52c7`.
They are in-frame proposals with specialist alignment still pending. A reduced
geometry-only input retains coordinates, line/page IDs, frame sizes and pending
statuses; it omits source readings, flags and alternatives. No TEI or source-target
text is loaded into the replay store. The native project records imported geometry
and its provisional status. It starts with no text revisions.

The generic production cropper takes padded axis-aligned bounding crops from these
quadrilaterals. It does not apply the external column rectification used by some P46
diagnostics. These are complete retained review-page decodes, approximately 5.6–5.8
megapixels. They do not cover original-resolution photographs or the greater-than-
25-megapixel banded/tiled route. Including every proposed band tests the route;
it does not certify complete or correct page transcription geometry.

## Checkpoint and preprocessing

The replay uses the previously authorized P01 `model-p01-h16-aug` checkpoint, digest
`03952a43d94bf9e53d8f5de886d3470057a2db55222c01d14172111a47b06fda`.
Its legacy manifest and checkpoint metadata have no typed preprocessing profile.
The unchanged loader returns null, and the production page pipeline therefore
selects legacy ink-positive grayscale at height 16 with standard line cropping.
This establishes runtime route compatibility, not historical training-preprocessing
parity or suitability for accurate P46 recognition. Externally prepared geometry
checkpoints from the P46 diagnostic search are deliberately not substituted.

The frozen production source is worker-budget v2 plus the independently tested
failed-layout-loader correction. No optional learned layout package is loaded and
no layout is recomputed. Its complete source/model/input, harness and binary hashes
are retained before inference.

## Registered comparison

Each route runs ten fixed cycles of pages 1440, 1450 and 1460: 30 successful surface
attempts and 810 expected L0 revisions. The baseline is one default daemon; the
candidate is five sequential fresh workers with budget six. Two replicates run
ABBA, totaling 120 surface attempts and 3,240 persisted L0 revisions. These repeated
visits do not increase the number of distinct pages or geometric proposals.

The driver waits for the complete 8,000-job generated-input comparison and observes
every owned worker's exit before starting. Each natural route has a 600-second
cap. It records 250-ms OS RSS samples, elapsed time, job completion logs, per-job
expected line counts, all persisted revision IDs/scopes/text/mean and grapheme
confidence, absence of non-L0 writes, and each owned process's exit.

Acceptance requires all expected jobs and revisions, exact prediction/confidence
parity between all four routes, no duplicate/missing/non-L0 revisions, no pending,
running, failed or cancelled jobs, and every worker exiting zero. These are resource
and preservation criteria. No CER, new labels, VLM requests or fitting are involved.
Latency is observational on a shared development host and cannot establish release
performance acceptance.

## Results

All 120 surface attempts succeeded and produced exactly 3,240 L0 revisions. Every
visit retained its expected 27/26/28 rows; there were no non-L0 writes or pending,
running, failed or cancelled jobs. Exact text, mean-confidence and grapheme-confidence
parity held across all four routes, digest
`51f53526377d31886a0e7a768011d7e8890a2a4742dd1b35de987ac008fce010`.
All 12 worker processes exited zero and were awaited; the driver also exited zero.
The natural replay began only after the generated replay finished.

| Route | Surface attempts / L0 | Processes | Maximum attempts per process | Route seconds | Largest sampled RSS, MiB |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1-persistent | 30 / 810 | 1 | 30 | 34.87 | 284.03 |
| 1-recycled | 30 / 810 | 5 | 6 | 31.47 | 324.92 |
| 2-recycled | 30 / 810 | 5 | 6 | 31.71 | 337.22 |
| 2-persistent | 30 / 810 | 1 | 30 | 33.03 | 333.20 |

Sampled peak RSS is not consistently reduced. Recycling bounds completed attempts
per process at six on this route; it establishes no byte ceiling or native leak fix.
The route covers retained full-page decoding and production crop/preprocessing/OCR,
not original-resolution or greater-than-25-MP tiled decoding. It establishes no
accuracy or geometry acceptance. The later scalar-query, SQLite-command, NativeAI
and shared layout-ownership changes are absent from these frozen binaries.

Evidence is retained locally in `.artifacts/experiments/nt20-p46-page-resource-v1/`,
including registration, source/input/binary hashes, job/prediction snapshots, process
receipts, sampled RSS and `analysis.json`. The [worker-budget report](nt20-worker-budget-2026-11-05.md)
describes attempt semantics and the native-retention limits that remain open.
