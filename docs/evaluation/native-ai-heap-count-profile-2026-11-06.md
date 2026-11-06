# Native AI heap-count profile: eight-visit result

The eight registered visits finished with **seven passes and one default-profile index RSS-cap failure**. All four requested-two-heap visits completed 100 requests. Main index and production GC settings remain unchanged; this short profile does not resolve the original full-24/Phase-A failures or establish a production default.

Evidence is under `.artifacts/experiments/perf02-ledger-index-v1/`. Actual execution `gc-heapcount-execution-v1.json` SHA256 `28ecfb4ff68bc3eaa23dda217f77e2d42fafce1ac7845eb188d6c8689e1f8f32` and separate root binding `d7c6b68f21ad1303bacdf05a10f038333483c52110c4e9ab3eec4752902ebc7a` bind the reviewed mirrored order, 565 inputs and fresh 05:34:52.876550–06:04:52.876550 UTC window. The execution-disabled template and earlier evidence remain unchanged.

Every fresh API used the unchanged frozen baseline/index-v2 pair, copied generated F1000 store, 100 fixed replays at one-second cadence, 1 GiB sampled-RSS abort, 180-second API/60-second verifier caps, five-second TERM grace and 330-second next-visit reserve. DEFAULT removed heap-count aliases in that child only; LIMIT2 set exactly `DOTNET_GCHeapCount=0x2`. No hard limit, forced collection, GC-mode setting, image/model, natural corpus/label or provider work was introduced. All original-service pre/post checks passed; native JSON/version stayed exact. Configuration equality, runtime/host identity and hard-limit checks passed across matched profiles.

| Visit | Software / profile | Completed | Median after 50, ms | Sampled peak RSS, MiB | Sampled cumulative process CPU delta, s |
| --- | --- | ---: | ---: | ---: | ---: |
| 1 | Baseline / DEFAULT | 100 | 58.777 | 806.234 | 10.662 |
| 2 | Index / DEFAULT | 100 | 41.547 | 937.281 | 8.199 |
| 3 | Index / LIMIT2 | 100 | 41.730 | 678.047 | 6.822 |
| 4 | Baseline / LIMIT2 | 100 | 58.109 | 666.875 | 8.892 |
| 5 | Baseline / LIMIT2 | 100 | 58.877 | 612.984 | 8.811 |
| 6 | Index / LIMIT2 | 100 | 39.178 | 540.609 | 6.687 |
| 7 | Index / DEFAULT | 45, cap failure | No timed population | 1,033.531 | 4.170, partial |
| 8 | Baseline / DEFAULT | 100 | 57.889 | 810.438 | 10.338 |

At **05:45:58.432573 UTC**, visit 7 recorded 1,058,336 KiB RSS, triggering `rss_cap`; the watchdog sent TERM to owned API PID 58934, which exited 0. The declared continuation policy permitted visit 8 only after complete closure and original-service post-verification. No visit was retried, replaced or dropped. Failed visit 7's 45 requests are all warmup and remain in resource/state budgets; its missing post-warmup latency is not pooled with survivors.

Reported runtime configuration was `HeapCount=18` in every DEFAULT visit and 2 in every LIMIT2 visit, with server GC, 18 logical processors and .NET 10.0.9/Arm64. This is captured configuration, not an inference from processor count or a continuous topology trace. Configured dynamic-adaptation mode remained 1; [versioned Apple/region conditionals](https://github.com/dotnet/runtime/blob/v10.0.9/src/coreclr/gc/gcpriv.h#L135) prevent equating that value with active DATAS. Supported setting/hex syntax, platform qualifications and preserved initial allowlist mistake are documented in [the proposal](../tasks/native-ai-heap-count-profile-proposal-2026-11-06.md).

Within this matched short workload, LIMIT2 had lower observed RSS peaks and cumulative CPU deltas; the index retained lower sampled allocation/work than baseline, but DEFAULT index still failed the cap. Only two fresh processes per cell ran. Cumulative CPU covers first/last one-second samples including startup, observer and idle, not per-request CPU. Current estimates, last-GC metadata and sampled RSS remain distinct; no native ownership or root absence is inferred. Observer v5 adds configuration fields and has functional smoke, not a fresh overhead-acceptance proof. No universal speed, capacity, Greek accuracy or release-memory claim follows.

`gc-heapcount-analysis-v1.json` preserves every visit and matched first-45 operation rows with absolute UTC/monotonic records. All 24 API/verifier records exited 0, all watchdogs joined, all samplers exited, provider attempts were zero and its thread closed. All 565 inputs remained unchanged. The driver exited 1 to retain the failed outcome; no owned process remains. Any longer profile validation or code/default adoption requires a separately reviewed plan. Historical failed campaigns remain failed.
