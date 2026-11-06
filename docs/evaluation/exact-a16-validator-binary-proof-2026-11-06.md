# Exact A16 validator binary proof

The intended published A16 Imaging DLL (`e8958e662674c28b6d0b49b697de053465de42e6e164bed43ffc732b1ba9b7d0`) passed the frozen generated functional checks. This closes the earlier **binary identity** gap; the earlier independently rebuilt, source-equivalent result remains a separate historical receipt.

The harness was not recompiled. Its existing runtimes received exact published Imaging, Core, Contracts and fork assemblies and matching symbols, with all other managed/native bytes preserved and hashed. Registration preceded execution: `.artifacts/experiments/sec01-independent-v1/exact-a-v16-binary-proof/registration.json` (SHA-256 `17bdcf69267adde326578d34a813c62862469e46a27a4a29a4f89cd1e2b399a4`).

All 171 sequential owned child processes exited zero and were reaped. The 154 snapshot cases preserved frozen A14 accept/refuse decisions, error codes, dimensions and source checksums. TIFF strategy names and allocation flags were the declared changes; diagnostic message wording was excluded from semantic equality. All five explicitly registered whole-image/small-segment flags matched. Seventeen variant controls produced 40 typed grayscale-alpha refusals, 45 accepted route controls and 17 readable dimension results. All fixture and staged runtime hashes remained unchanged. Full details: `exact-a-v16-binary-proof/analysis.json`; per-case logs and process receipts are in the four `runs/exact-a-v16-*` directories.

These are generated functional checks, not a memory or speed acceptance. Recoverable JPEG compatibility remains a decodability policy rather than an entropy-validity guarantee. Whole-image TIFF segments may still require frame-sized codec state; `allocatedFullRasterDestination=false` does not bound native scratch, process RSS or concurrent requests. OJPEG and the later proposed resource-cap policy remain separate qualification gates. No production decoder or dependency migration follows from this result.

## Matched archive-validation resource follow-up

A separate approved ABBA comparison used the same immutable 1024×768 and 3001×2000 RGB Deflate TIFFs with256-pixel tiles. The old validator was the actual `Identify` plus `Load` with `MaxFrames=1`, `SkipMetadata=true`, and `TargetSize=1×1`; the candidate used the exact A16 binary. Each fresh process made one cold, one warmup and six measured calls, preserving its caller-owned encoded snapshot. Every admission, dimension, unchanged-source and declared block-mode/false-full-raster flag check passed.

| Input | Arm | Visit1 / visit2 median ms | Managed allocated MiB/call | Sampled process peak MiB, visit1 / visit2 |
|---|---|---:|---:|---:|
| 1024×768 | Old TargetSize1 |25.706 /26.139 |0.0357 |57.44 /61.00 |
| 1024×768 | Exact A16 |7.549 /7.020 |3.3190 |60.47 /60.33 |
| 3001×2000 | Old TargetSize1 |26.202 /26.332 |0.1025 |81.52 /80.55 |
| 3001×2000 | Exact A16 |37.975 /38.445 |23.4297 |61.48 /61.41 |

For these tiled inputs the candidate avoids a page-sized destination, but cumulative managed block allocations remain substantially greater than the old validator. The larger input is about45% slower despite lower sampled RSS. These two process replicates per arm/input establish neither an all-format benefit nor a total-memory bound. Large strips, codec state, metadata, other formats and concurrency remain separate gates. No production replacement is approved by these results.

The completed registration is `.artifacts/experiments/sec01-independent-v1/exact-a-v16-archive-resource-native-v3/registration.json` (SHA-256 `90de78404d8444f8cd762a8ba431b227dd779990c1a833c2c42363b240bb44af`). All eight processes exited zero and were reaped in3.44seconds; every registered source and binary hash remained unchanged. Per-visit data and raw heap/counter values are retained in `analysis.json`, individual JSONs and `processes.json`; sampled RSS can miss brief peaks.

Two failed controller attempts are preserved separately under `exact-a-v16-archive-resource/`. The first stopped before dispatch because Python tuple ordering differed from its JSON list representation. The second launched one baseline child but sandbox denial of `/bin/ps` stopped its first monitor sample; the controller's `finally` killed and reaped that child. Neither attempt completed a resource visit or contributes metrics. The approved final attempt used native execution permission for owned-process monitoring, with unchanged inputs, order, call counts and limits.
