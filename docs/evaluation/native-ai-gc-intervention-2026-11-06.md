# Native AI sacrificial GC diagnostic, Phase B

Both generated baseline/index visits completed the registered 16-request boundary, collection sequence, fixed idle observations, integrity checks and owned cleanup. **This does not resolve the earlier index memory-cap failures or permit index adoption.** It is an intervention, not performance/cap acceptance or product GC behavior.

One important qualification: source requested `blocking: true, compacting: false` for both full collections, but runtime metadata reported **`Compacted=true` after the first and false after the second in both variants**. Do not describe these as two observed noncompacting collections. Requested flags and actual metadata are both preserved; no rerun or GC-setting change followed.

## Execution and actual request boundary

Evidence is under `.artifacts/experiments/perf02-ledger-index-v1/`. Actual registration `gc-phaseB-execution-v1.json` SHA256 is `35e9876fa3eacb1ed027beded9249ad8863c252a368c5c827e1cde3ba123da4e`; separate root binding SHA256 is `e4fc039fcefdef709c81983def6ea26ef751554e48372a5272d4d303c63820fe`. The reviewed 557-input seal, execution-disabled template and earlier failed campaigns remain unchanged. Start was 05:11:43.893045 UTC, with a fixed 05:21:43.893045 UTC deadline.

The unchanged frozen API pair ran baseline then index, fresh generated F1000 stores, exactly 16 fixed accepted-proposal replays at one-second cadence. The registered limits remained 1 GiB sampled RSS, 180 seconds/API, 60 seconds/original verifier, five-second TERM grace and a separate 30-second intervention guard; next-visit reserve was 330 seconds. No cap fired. No provider request, image/model use, natural corpus, Greek label or training was involved.

The real ASP.NET DiagnosticListener, rather than the client body read, established these pre-command boundaries:

| Variant | Observed stop UTC | Starts | Stops | Active contexts | Invalid |
| --- | --- | ---: | ---: | ---: | --- |
| Baseline | 05:12:11.913139 | 16 | 16 | 0 | false |
| Index | 05:12:44.575729 | 16 | 16 | 0 | false |

No further HTTP requests were issued. The hook rechecked this boundary between actions. Both recorded collection 1 requested/completed, pending-finalizer wait requested/completed, collection 2 requested/completed and intervention complete. No 17th start, unmatched stop or invalidation was observed. This is the defined ASP.NET request-activity-stop boundary; it does not prove every framework scope/pool object was disposed or identify surviving roots.

The hook recorded fixed idle marks at 1/5/15 seconds after the sequence: baseline actual 1.00105/5.00129/15.00157 seconds; index 1.00105/5.00124/15.00150. Both independent intervention guards joined. Every native JSON snapshot and row version matched the registered prefix exactly; independent original-service verification passed before launch and after API closure. All six API/verifier records exited 0, all API samplers/watchdogs closed, provider attempts were zero, and the provider thread closed. The driver exited 0. `gc-phaseB-v1/results.json` retains the complete execution; `gc-phaseB-analysis-v1.json` extracts numeric observations and their qualification.

## Numeric observations

Values below are runtime `GC.GetTotalMemory(false)` estimates, **not verified live/reachable bytes or a heap-root graph**. Last-GC metadata is retained separately.

| Measurement | Baseline estimate, bytes | Index estimate, bytes |
| --- | ---: | ---: |
| Before collection | 608,347,712 | 637,581,704 |
| After collection 1 | 112,156,856 | 129,027,952 |
| After pending finalizers | 112,165,080 | 129,036,176 |
| After collection 2 | 112,160,920 | 129,030,712 |
| At +1 second | 112,218,488 | 129,088,280 |
| At +5 seconds | 112,308,440 | 129,170,344 |
| At +15 seconds | 112,439,392 | 129,309,760 |

After collection 2, last-GC heap/committed bytes were baseline 106,555,496 / 589,332,480 and index 123,379,792 / 469,270,528. Generation collection counts changed from 4/3/2 to 6/5/4 for baseline and 2/1/1 to 4/3/3 for index. These are different measurements/times, not quantities to subtract from RSS.

Sampled peak RSS was 723.219 MiB baseline and 666.750 MiB index in these short sacrificial visits. Nearest OS samples at the +1-second idle mark were 634.578 and 508.109 MiB, respectively, within 0.034 seconds of the numeric mark. OS sampling is 250 ms; immediate collection events share a preceding nearest sample and therefore are not exact post-collection RSS observations. Raw timestamps/offsets are retained. No native byte ownership is inferred.

## Interpretation and remaining work

The large fall in managed estimates supports the presence of collectible accumulated allocations under this intervention. It does not prove the absence of persistent roots, explain all RSS, establish a native leak or show that forced GC belongs in the application. The first collection's observed compaction is an additional causal qualification. Sixteen requests, an artificial collection boundary, observer work and fixed idle differ from both [the full-24 campaign](native-ai-ledger-index-2026-11-06.md) and [Phase A](native-ai-gc-observation-2026-11-06.md); comparisons across those protocols cannot establish why a particular visit crossed its cap.

The request-local index still has observed cap failures at 25 requests in the original campaign and 45 in Phase A. Main remains unchanged. No production GC mode, heap limit, forced collection, cache, schema or history deletion is introduced. Actual retained-root attribution still requires a separately approved heap/root diagnostic. A useful next implementation must arise from a concrete supported hypothesis, preserve full validation/error parity and undergo a newly registered resource comparison. These measurements are not Greek recognition or training evidence.
