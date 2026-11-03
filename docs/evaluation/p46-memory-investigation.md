# P46 resource-only memory discrimination — 2026-11-02 evening

The sustained lifecycle test preserves its disposal/parity result, but **does not demonstrate stable resident memory**. A separately preregistered five-route probe narrows the behavior: repeated successful or failed package loading shows more continued RSS growth than inference using one retained model. The cause remains unresolved. No production fix, garbage-collection policy, recognition score or model promotion follows from this probe.

## Sustained-run evidence

Read-only inspection of `.artifacts/experiments/p46-evening-sustained-lifecycle/` confirms 25,000 serial cycles followed by four independent synchronous workers with 5,000 cycles each: **45,000 valid loads, 90,000 expected corrupt-load failures and 315,000 saved-string comparisons**. Its wrapper/parameter lifetime and parity checks pass. It performs no fitting, CER calculation or checkpoint creation.

Its 226 approximately one-second OS RSS observations begin at **19:42:17 CST**, about20seconds after serial work begins, so they are not a complete initialization trace. Recorded serial observations rise **198.30→321.00 MiB**. Concurrent observations begin at356.36MiB and end438.70MiB; the sampled maximum is **478.45 MiB**. Darwin `/usr/bin/time -l` records a separate whole-process high-water value of **520,110,080bytes =496.015625MiB**, which can exceed the sampled maximum. Phase boundaries come from preserved stdout, not an inferred allocation timeline. Balanced wrapper counters and parameter handles alone cannot close the RSS question.

## Prospective discrimination protocol

Artifact root: `.artifacts/experiments/p46-evening-memory-discrimination/`.

`registration.json` was frozen before any route execution, SHA-256 **`bd7d4afd637ea03e900f8b63ce1e675335194828d2fac533b92fa07274f2b99a`**. It pins40 files covering the exact current managed DLLs, C# source/project, seven existing prepared inputs and source crops, valid secondary package and empty/truncated fixtures. The same seven saved predictions are used only for equality checks. The artifact-only C# project builds against frozen DLL copies; cached serial build passes with zero warnings/errors.

Execute one fresh CPU process per route, in this fixed order: construction/disposal only, successful package load/disposal without inference, empty corrupt load, half-truncated corrupt load, and inference using one retained model. Each performs exactly **3,000 cycles**, with one Torch thread and a60second process cap; hard cutoff is19:58CST. There is no warmup, repeat, tuning or retry. Construction uses the package's26 classes/hidden64/downsample4/height16/dropout0 architecture. All inputs are verified in each process, but only the inference route forwards them. Its seven-line batches have maximum size4.

Before work, validate the installed-runtime raw-tensor-to-Parameter handle-transfer control and balanced thread-local disposal statistics. Every cycle checks its live-statistic baseline; construction/load routes also check explicitly disposed parameter handles where a model is returned. This follows the [corrected lifetime oracle](model-load-lifetime.md), not the superseded zero-`Tensor.TotalCount` criterion. Counter equality does not count unique native allocations or bytes.

At cycles **0/100/500/1000/2000/3000**, record managed `GC.GetTotalMemory(false)`, generation0/1/2 collection counts, OS `/bin/ps` RSS and lifetime statistics. Managed statistics are sampled before spawning `ps`; process/JSON sampling contributes disclosed overhead. There is no forced GC during fixed work. Only after3,000 cycles, record a before-GC sample, perform collect/wait-for-finalizers twice, then sample immediately and2seconds later. The inference model remains alive through those samples and is disposed afterward. Other routes include first construction/load within work; retained-inference's baseline follows its single retained model load. Their absolute baselines therefore differ.

The separate pre-execution receipt SHA-256 is **`90789c397da5519c606043af30174f431c5a3cf0b07a3ac9bda38caadc09dea0`**. It pins the compiled executable/dependencies, harness and launcher before the first process. All five processes finish with exit0 by **19:50:30 CST**. No code/recipe/source changes or retries occur between routes.

## Results

All five routes complete3,000 cycles with balanced final live statistics. Empty/truncated routes each produce exactly3,000 expected `EndOfStreamException` failures. Retained-model inference reproduces **21,000 saved strings**; this is repeated compatibility evidence, not21,000 new scholarly observations.

| Route | RSS before / MiB | Cycle500 | Cycle3000 | After GC immediately | After GC +2s | Cycle500→3000 change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Construction/disposal only |141.42|156.11|160.78|160.38|162.73|+4.67|
| Successful load/disposal, no inference |142.20|160.92|169.16|169.16|169.17|+8.23|
| Empty corrupt load |142.36|160.75|173.50|172.09|172.48|+12.75|
| Truncated corrupt load |142.17|158.75|169.73|168.33|168.70|+10.98|
| One retained model, inference |147.42|174.53|174.66|174.69|174.92|+0.13|

| Route | Managed estimate before forced GC / MiB | Immediately after | After +2s | Generation2 collections recorded before forced GC |
| --- | ---: | ---: | ---: | ---: |
| Construction/disposal only |3.32|1.19|1.23|2|
| Successful load/disposal |7.23|3.12|3.17|505|
| Empty corrupt load |10.00|2.90|2.95|508|
| Truncated corrupt load |9.25|2.87|2.91|504|
| One retained model, inference |3.70|3.07|3.13|202|

Those collection counts are cumulative process counters, including initialization; they are not byte counts. Many automatic collections already happen during the workload. The after-work experiment lowers managed estimates, while RSS falls little or rises slightly. This does not support adopting forced GC as a production remedy.

Retained inference is nearly flat over the disclosed500–3000 interval, whereas repeated construction/load/error routes continue to rise. This points to repeated model/package lifecycle work as a useful next investigation scope. It does not prove that inference remains flat indefinitely, that every growth component is a leak, or that allocator caching is the explanation. Route work differs, one process is observed per route, early native/JIT initialization is included, and no combined or concurrent route is isolated by these five processes.

Whole-process high-water values for construction/success/empty/truncated/inference are **173.55/179.89/183.27/179.44/185.28MiB**. They include startup, hashing, work, sampling and post-work observations and are not comparable steady-state or optimizer-throughput measurements. No CPU-platform, server, full-page, worker-store or GPU conclusion follows.

## Verification and next boundary

`summary.json` SHA-256 **`b8a1143504df170e35f43e6b41bbe405393dd7d503d447b3bc6e88e8dd5b3ee7`** independently reconciles all45 raw samples with result files, counts/handles, preserved process exits, fixed input/source/DLL hashes, per-route OS high-water units and the sustained phase series. All registered40 file hashes and explicit execution-file hashes remain unchanged. Only explicitly registered runtime binaries under `bin` are checked; this is not a general `bin/obj` inventory. Source and model artifacts from earlier sessions, the sustained run and the maintainer packet remain untouched.

The next separately authorized resource task should use longer replicated fresh-process routes and native-allocation/retained-object tracing, separating model construction, package metadata/digest processing, successful weight loading and failure handling under a new fixed protocol. Preserve exact inputs, hardware/runtime and OS/managed observations, with isolated reproduction controls. Do not infer native ownership from `TotalCount`, change model disposal speculatively, add production forced GC, retrain, or reopen the failed accuracy screen. NT-20 remains partial pending attribution and the broader sustained/concurrent/platform checks.
