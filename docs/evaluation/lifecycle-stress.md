# Bounded lifecycle stress (2026-11-02)

Passed on macOS ARM64 with TorchSharp 0.107.0 and libtorch CPU 2.10.0. This is resource and ownership evidence for current package loading, prepared-line recognition and synthetic trainer disposal. It does not measure Greek transcription quality.

`registration.json` predates execution; the harness, seven input floats, original crops, model/package manifest and seven managed DLLs are hash-bound. The expanded-1729 **secondary diagnostic** package supplied seven already exposed development predictions, compared as exact saved strings. No reference targets, CER, selection, new checkpoints or freshly decoded images were used. Provisional source/model permissions are unchanged.

| Phase | Completed workload | Seconds |
| --- | --- | ---: |
| Serial | 200 valid loads; 400 expected corrupt-load exceptions; 1,400 strings | 2.5256977 |
| Four independent synchronous threads | 100 cycles each: 400 valid loads; 800 expected exceptions; 2,800 strings | 1.2899309 |
| Synthetic trainers | 100 disposed trainers; two finite-loss updates each | 0.1303156 |

All 600 valid loads, 1,200 empty/truncated SHA-matching failures and 4,200 string comparisons passed. Each disposed iteration and worker returned to its validated thread-local lifetime baseline; captured model parameter handles were zero after disposal. Failed loads preserved `EndOfStreamException` while the valid model remained alive. CPU thread count was configured once to one before workers started; workers shared no model.

The 200 tiny synthetic updates are separate from all P46 accuracy fits. Caller-owned model forward calls and an independent tensor remained usable after each trainer's disposal. There were zero evaluation/CER calls and checkpoint files. A raw-tensor-to-Parameter control validated `ThreadTotalLiveCount` and real handle disposal first. `Tensor.TotalCount` was not used because Parameter move-handle accounting can retain counter drift after real handles are disposed (see `model-load-lifetime.md`). Live-wrapper balance does not measure unique native bytes.

One process completed in 6.19 seconds with `/usr/bin/time -l` maximum RSS 256,589,824 bytes (244.70 MiB). These are descriptive observations of one current-build workload. No matched baseline was run; this does not establish speed improvement or absence of throughput regression. Its short duration does not establish sustained memory stability, full-page/API/store-worker/GPU behavior, cross-platform behavior or general thread safety.

Completed at 19:35:23 CST under 120-second phase caps and a 20:03 CST hard cutoff, with no retries or tuning. Build: zero warnings/errors. Post-run `verification.json` verifies unchanged original hashes. Original registration, results, stdout, OS time, code and DLL snapshots are retained under `.artifacts/experiments/p46-evening-lifecycle-stress/`; `evidence-index.json` inventories them. Production files and training artifacts were unchanged.

A separate fixed-count [sustained workload](sustained-lifecycle.md) extends repetition evidence and records unresolved RSS growth. The post-run source receipt confirms all 339 source/test C# hashes match the 19:32 CST baseline.
