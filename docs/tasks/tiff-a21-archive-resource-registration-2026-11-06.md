# A21 archive validation resource comparison registration

Status: source/package preparation only. No timings or resource campaign authorized or dispatched. Main still uses ImageSharp and its six advisory, A18/OJPEG, fork provenance and migration gates remain open. This is not a model or natural-text evaluation.

## Exact compared versions

Compare qualified isolated **A16 + fork5** and **A21 + fork5**, not historical A16 + fork4 and not current production. The source of both packages is the 223/223 qualified cohort under `.artifacts/experiments/sec01-independent-v1/a21-qualified-functional-v1/{baseline,candidate}/snapshot`. Staged byte-identical reference copies are under `.artifacts/experiments/a21-archive-resource-v1/qualified-reference/`. Their 55-file closures differ only in `LeviathanText.Imaging.dll` and its PDB. Candidate Imaging SHA-256 is `d01745644768dc8ab834176a3ec7793ff5c8efa1714374ddc858098deac3c904`; both carry the exact qualified fork5 binary.

Reference/input freeze SHA-256: `1a418f12d3dae0b2d422cc3beb23b0bb5d0c5daa4e91627cda631303ef7b5f09` (`reference-freeze.json`). The prior TargetSize1-vs-A16 experiment is historical context only. No third arm is proposed; a legacy reference would need independent exact program/source/runtime qualification, a new prospective registration and separate approval.

## Fixed inputs and operation

Two copied generated tiled RGB Deflate TIFF sources, unchanged from the registered representative resource fixture set:

| Dimensions | Encoded bytes | SHA-256 |
| --- | ---: | --- |
| 1024 × 768 | 21,140 | `045179bd00783d6ea7cf2e9149ad86e977215fe87c0a3225856b556b6077ef86` |
| 3001 × 2000 | 167,888 | `4cd345109f6976fc22b9a4b5413042f9cdd7ea88c5c6196e67ad0e7fa1ce8123` |

Only actual `ImageDecoder.ValidateEncodedSnapshot` is measured, on the same caller-owned source array. Each fresh process performs one cold, one warmup and six measured calls. Every call's decision, width/height, TIFF format, validation mode and no-full-raster destination flag is checked outside the operation timers, without dropping unsuccessful samples. Source array and on-disk input hashes are verified before/after. The helper uses no forced collection, before, between or after calls. Passive last-GC values are not a post-collection live-heap claim.

The new helper deliberately removes BOTH initial collection/final forced compacting collection from the historical archive helper; no cross-study causal or timing comparison is justified. File reads/source hashing, assertion checks and result serialization remain outside operation timers. Allocation and CPU counters surround the validation call; they are observations from this generated shared-host study, not attribution to every runtime/native allocation.

## Prospective package and functional gate

Compile one new helper against only the frozen qualified references, without production project builds, package upgrades or dependency substitutions. Stage its identical helper/deps/runtimeconfig files into both complete package closures; only Imaging DLL/PDB may differ. Preserve unused upstream package files in the registered closure rather than silently replacing dependencies. Seal helper, references, complete actual runtimes, copied inputs and driver before dispatch.

Before any timing campaign, run exactly four non-timing functional checks: each version on each fixed input, using the helper's `verify` mode (one actual validation, no stopwatch samples). Both must match the already qualified fixed dimensions, accepted state, format, `tiff-first-frame-decodability-blocks` mode and `allocatedFullRasterDestination=false`, with immutable sources. Source/hash mismatch or changed outcome blocks timing. Compile and functional execution require root approval of this preparation before proceeding.

## Prospective resource campaign — separate root quiet approval required

Fixed eight visits, in this order: small A16/A21/A21/A16, then large A16/A21/A21/A16. Concurrency one. All eight processes and all eight samples per process are retained; the six measured calls are the declared latency population. Cold/warmup remain part of state, budgets and separately reported evidence. No adaptive order, retry, changed input, GC intervention or outlier exclusion.

Per process: 60 seconds wall, 45 seconds CPU, 512 MiB sampled RSS, 64 KiB each stdout/stderr and bounded result size. Global deadline: dispatch time + 600 seconds; require the full remaining visit allowance plus cleanup reserve before admitting each child. Independent owned-process wall monitoring must cover startup, monitoring and result collection; bounded TERM-to-KILL cleanup and reap completes before the next visit. Sampling failures, caps, mismatches and source/runtime drift stop the campaign at the first failure with partial evidence preserved. No foreign process signaling.

After complete integrity/closure, report each visit and each measured sample, paired ABBA order, median/range, allocated bytes, process CPU and sampled RSS. No p-value or natural-text claim. Missing short RSS peaks and shared-host variability remain explicit. A matched improvement against A16 does not close the original approximately45% versus-legacy timing finding; no migration or permissive-native legal/SBOM approval follows automatically.

Root must separately approve the exact sealed execution registration and coordinated CPU quiet start. No timing starts merely because source, compilation or four functional checks pass.

## Prepared package and functional evidence

The helper compiled with zero warnings/errors. The initial raw-assembly-reference helper graph omitted native runtimeTargets, despite copied native files. Its first baseline non-timing check aborted with exit -6/typed archive rejection; the controller stopped before any candidate call, reaped the child and preserved all receipts (`functional-v1`). This is a new harness packaging failure, not evidence of a changed A16 decision.

Root approved a versioned graph repair: derive the helper deps graph from the exact qualified SnapshotProbe graph and rename only the main project/library/runtime assembly entry to ArchiveProbe. All other dependency entries and native asset paths remain exact. Runtime-v2's two complete 60-file closures still differ only in Imaging DLL/PDB. The actual macOS native asset digests match on both sides, including Skia's `11a672e8df0701967aa041b449bb4db6f9af040a5e8b0ed9ea44a922ec0effaa`.

The repaired four **non-timing** checks passed: baseline first on both inputs, its exact oracle file frozen before the candidate, then the same candidate cases. All four children exited zero and were reaped, watchdogs joined, and source/runtime hashes remained unchanged. Timing calls: **zero**. Evidence: `.artifacts/experiments/a21-archive-resource-v1/functional-v2/`; registration SHA-256 `0f3988f8e7664a872138a1423b5d53c1a268331562df5ffdac3f7a3ae0790c25`.

The prospective resource driver and false proposal are staged, not dispatched: `resource-driver.py`, `resource-proposal.json` (SHA-256 `8563f5f1279832836e56399519ed174f4866a4fac1948c867599f31dfb144d31`). Its active child wall watchdog also sees the absolute global deadline; admission requires70 seconds remaining. A standalone whole-driver/global watchdog and small harmless cap/cleanup controls remain for review before any timing authorization, especially the post-visit integrity-hash phase. No completed resource comparison is claimed.
