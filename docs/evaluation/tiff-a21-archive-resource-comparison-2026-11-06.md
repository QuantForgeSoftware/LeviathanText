# A21 archive validation: matched generated resource comparison

The fixed eight-visit campaign completed with exact validation outcomes and full cleanup. **The candidate has a measured tradeoff: lower managed allocation on both inputs, faster median on the small input, but a26% higher median on the6MP input.** This does not clear the archive performance/migration gate. Main still retains ImageSharp and its existing advisory, OJPEG/A18, fork-provenance and licensing gates.

## Exact scope and versions

This compares isolated **A16 + qualified fork5** with **A21 + the identical fork5**, not current production and not the historical ImageSharp TargetSize1 reference. Complete60-file runtime closures differ only in Imaging DLL/PDB. Candidate Imaging SHA-256 is `d01745644768dc8ab834176a3ec7793ff5c8efa1714374ddc858098deac3c904`.

Two frozen generated tiled RGB Deflate TIFF inputs were used, at1024×768 (21,140 encoded bytes) and3001×2000 (167,888 bytes). Only actual `ImageDecoder.ValidateEncodedSnapshot` was measured. Each fresh process made one cold, one warmup and six measured calls; every call's decision, dimensions, format, mode and full-raster flag was checked outside the operation timers. All64 calls matched the fixed functional oracles. Source array/on-disk hashes and full runtime closures remained unchanged. No explicit GC was requested before, between or after calls; passive last-GC counters are not verified live heap.

Fixed order: small A16/A21/A21/A16, then large A16/A21/A21/A16. Twelve measured calls per version/input are descriptive observations from two fresh processes, not twelve independent process replications. Cold/warmup outputs remain retained. No sample, visit or outlier was dropped and no adaptive retry occurred.

## Results

| Generated source | A16 median ms (range) | A21 median ms (range) | A21/A16 | A16 allocated MiB/call | A21 allocated MiB/call |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1024×768 RGB Deflate tiles |7.012 (6.914–7.268)|6.417 (6.308–6.740)|0.915|3.319|0.992|
| 3001×2000 RGB Deflate tiles |39.401 (37.980–55.561)|49.659 (34.759–51.256)|1.260|23.429|5.329|

Per-visit measured medians preserve the fixed order:

| Input | A16 first | A21 first | A21 second | A16 last |
| --- | ---: | ---: | ---: | ---: |
|1024×768|7.053|6.380|6.442|7.011|
|3001×2000|39.786|49.176|49.659|38.971|

Median measured process CPU per call was7.095→6.496ms on the small input and46.959→50.942ms on the large input. Sampled peak RSS per visit (MiB) was A16 `[63.05,62.64]` versus A21 `[65.45,63.72]` for the small source, and A16 `[66.47,63.47]` versus A21 `[66.86,67.13]` for the large source. These sampled process peaks can miss brief maxima and do not identify managed versus native retention or establish a total-memory bound.

The observed allocation reduction does not make the large-input latency regression disappear. There is no causal attribution to GC, native scratch, operating-system scheduling or a particular method from these samples alone. The original approximately45% versus-legacy slowdown remains separate historical evidence; this study has no matching legacy arm and cannot declare it repaired.

## Registration, preparation and closure

Proposal SHA-256 `9f0ef60e714504e9330a4e72082502067d50cb3354fb9a4033c6bde5007b74d0` and actual root binding `execution-v1.json` are retained under `.artifacts/experiments/a21-archive-resource-v1/`. The absolute deadline was07:59:34 UTC. Limits:60s wall/45s CPU/512MiB sampled RSS per process,64KiB stream/result caps,600s global budget and a joined whole-driver deadline guard covering result/integrity waits. All eight children exited0 and were reaped; all visit/global watchdogs joined and no limit triggered. No owned resource process remains active.

The new helper's initial raw-reference dependency graph omitted native runtimeTargets. Its first baseline-only non-timing check failed; no candidate result was produced in that failed preparation. Root approved a versioned graph derived from the exact qualified SnapshotProbe deps metadata, renaming only its helper project/library/runtime entry. Four subsequent non-timing checks passed baseline-first/frozen-oracle/candidate with exact outcomes and matching native asset hashes. Five harmless owned-child controls verified global deadline during posthash waits/blocked child, normal watchdog cancellation, exit/cap race and SIGTERM cleanup. They execute zero archive calls. All original failed preparation evidence remains preserved.

Evidence: `results-v1/{processes.json,verification.json,analysis.json}` and all eight raw per-process JSONs/stdout/stderr. Analysis SHA-256: `18e4413ad6e8555e382ae91c89b8b0c51df2b919819052eb9b15e2a7018e1f44`.

This is generated archive-validation engineering evidence, not universal TIFF validity, all-platform compatibility, linguistic accuracy, training, inference, a complete native SBOM or legal approval. No natural image corpus, Greek labels, models, providers or LM Studio were accessed.
