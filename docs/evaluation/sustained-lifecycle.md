# Sustained lifecycle resource check (2026-11-02)

The separately preregistered fixed-count workload passed with **45,000 valid package loads, 90,000 expected corrupt-package failures and 315,000 exact saved-string comparisons**. It added no accuracy fits, evaluation/CER calls, synthetic training updates or checkpoints. Production source, frozen training snapshots and the maintainer packet were unchanged.

Registered at 2026-11-03T01:41:35.659527+00:00 and launched at 2026-11-03T01:41:54.809064+00:00; completed at 2026-11-03T01:46:03.972123+00:00. This is a count/path-only fork of `p46-evening-lifecycle-stress`, with 25,000 serial cycles and four independent synchronous threads doing 5,000 cycles each. Synthetic training was omitted. Serial/concurrent caps were 600/240 seconds, with a 20:00 CST hard cutoff; there was one execution, no tuning and no retry. Build: zero warnings/errors.

| Phase | Seconds | Main-thread live wrappers before → after |
| --- | ---: | --- |
| serial25000 | 195.749 | 0 → 0 |
| four-independent-synchronous-threads | 51.209 | 0 → 0 |

Each cycle loaded the expanded-1729 **secondary diagnostic** package, recognized the same seven already exposed prepared development lines (chunks four/three), compared their previously saved strings, attempted SHA-matching empty and truncated weights, then disposed the successful model. Each iteration, failed load and worker returned to its validated per-thread lifetime baseline. All captured valid-model parameter handles were zero after disposal, and all corrupt loads preserved `EndOfStreamException`. Workers shared no model and CPU threads were set to one before launch. The valid model remained alive during failed loads; the same-cycle parity check preceded them. Separate model-load regression tests cover caller usability after failure.

The raw-to-Parameter control validates `ThreadTotalLiveCount` and real handle checks; `Tensor.TotalCount` is excluded because handle transfer can drift that managed counter. This counts tracked live wrappers, not unique native allocations. Inputs were frozen float tensors; no image decoding or new Greek reference scoring occurred. Source/model permission and specialist-review restrictions are unchanged. Repeated parity on seven already exposed lines supplies lifecycle determinism evidence, not quality or corpus-generalization evidence.

The whole process took 249.21 seconds according to `/usr/bin/time -l`; maximum RSS was 520,110,080 bytes (496.02 MiB). The separate peak-memory-footprint metric was 476,480,760 bytes. The read-only 1-Hz `ps` monitor retained all 226 samples, from 2026-11-03T01:42:17.245003+00:00 through 2026-11-03T01:46:03.781232+00:00; sampled RSS ranged 198.30–478.45 MiB. Its window begins after PID delivery and excludes subsecond peaks. Chronological quarters describe the observed shape without a fitted trend or steady-state inference:

| Chronological sample quarter | Samples | Minimum MiB | Median MiB | Maximum MiB |
| --- | ---: | ---: | ---: | ---: |
| 1 | 56 | 198.30 | 217.38 | 236.64 |
| 2 | 57 | 238.55 | 258.34 | 278.25 |
| 3 | 56 | 278.36 | 298.32 | 317.91 |
| 4 | 57 | 317.45 | 430.11 | 478.45 |

The final sampled RSS was 240.41 MiB above the first sample. The cause of this growth is unresolved; scope/handle balance does not explain OS memory behavior. Backlog: profile managed retention and native allocations in a separately registered follow-up before claiming memory stability. No allocator-cache explanation was established.

There is no matched baseline or throughput-regression claim. A finite successful workload does not prove sustained stability at other durations or workloads, unrestricted thread safety, full-page/API/store-worker/GPU/cross-platform behavior, or absence of all native leaks. This extends duration and repetition evidence for these specific routes only.

Evidence is under `.artifacts/experiments/p46-evening-sustained-lifecycle/`: immutable `registration.json`, exact `Program.cs` and DLL snapshots, input allowlist, corrupt diagnostic derivatives, `results.json`, `stdout.txt`, `time.txt`, all `os-rss-samples.jsonl` records and monitor receipt. `source-postrun-verification.json` confirms all 339 current production/test C# files match the 19:32 CST baseline; `verification-resource-summary.json` checks executed artifacts and input hashes after the run and separates descriptive OS metrics; `evidence-index.json` inventories these receipts. Model artifacts retain their provisional diagnostic status.
