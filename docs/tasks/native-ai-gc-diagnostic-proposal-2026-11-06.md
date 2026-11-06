# Native AI F1000 GC/resource diagnostic proposal

**Proposal only; execution is not authorized.** Main service and the failed full-24 campaign remain unchanged. This is a separate diagnostic of unchanged baseline/index-v2 assemblies, not a retry, continuation, performance acceptance or repair claim. Root must approve the actual harness, telemetry source, runtime closure, registration, deadline and coordinated quiet window before any API/diagnostic workload starts.

The preserved result and interpretation are in [the incomplete campaign report](../evaluation/native-ai-ledger-index-2026-11-06.md). Candidate v2 crossed the 1 GiB sampled-RSS cap after 25 F1000 warmup requests; baseline F1000 completed 1,000 requests. This proposal must not erase that failure or reinterpret its limits.

## Question and competing explanations

Determine whether repeated request-local allocation and differing GC pacing explain the observed accumulation, or whether references survive request completion/collections. Source inspection finds only request-local index dictionaries and wrappers, not an established persistent root. It does not prove all transient references are immediately collectible. Last-GC heap metadata and OS RSS alone cannot distinguish those explanations.

No new implementation is justified yet. In particular, changing GC mode/heap limits, adding forced GC to product behavior, shrinking history or raising the cap would answer a different question and cannot fix the original result retroactively.

## Phase A: unchanged-pair observational diagnostic

- Freeze the existing `api-runtime-v2` 112-file baseline/index pair; only Application DLL/PDB differ. No dependency refresh, product source change, schema/cache/history deletion, model/provider call or natural dataset access. The frozen dependency point predates the separate main Imaging fallback correction; it is not a distributable security-cleared package.
- Use the same registered generated F1000 prefix, fresh owned SQLite copy per visit: 4,180,973 native-JSON UTF-8 bytes, row version 2, source `A`, accepted text `B`. Preserve exact raw native JSON/version and original attachment checksums.
- Fixed order **baseline, index, index, baseline**, 100 fixed replay requests per API, one-second start cadence. First 50 remain part of memory/state/HTTP budgets; exclude them only if showing descriptive latency. Zero provider calls and zero native writes are required. No adaptive visit selection or request retries.
- Preserve the **1 GiB sampled-RSS cap**, 250 ms OS sampling and five-second TERM grace. This new short diagnostic has its own **180-second API wall cap and 900-second global budget**, not a change to the original 1,800-second visit cap. Require 190 seconds remaining before starting another API. Seal the actual global deadline before dispatch.
- Resource cap/HTTP/API failure ends that visit and preserves partial outputs. After all its owned processes/threads close and hashes remain valid, continue to the next predeclared visit. This intentional **failure-continuation policy differs from the original first-failure stop** and is selected before results. Never replace or rerun a failed visit. Integrity/mutation/provider/source drift, unexpected PID/cleanup failure or harness error aborts the whole diagnostic; deadline leaves remaining visits unrun.
- Independently verify the copied native payload through the original service **before API launch**, then stop that verifier before launching the API. After API closure, snapshot/verify the retained payload, including a failed visit's partial state. No intermediate verifier interrupts the 100-request sequence. Verifier children need their own 60-second watchdog inside the global deadline, factory-owned signalling and bounded cleanup; no untracked child may outlive the run.
- Retain raw responses, per-operation success/error and scheduling lateness, exact stores/snapshots, telemetry, stdout, watchdog first-cause events, process exit/cleanup and registered input hashes. Failure is a result; no full-matrix/three-replicate claim follows from these two visits per variant.

## Telemetry separation and observer qualification

Use a **new, separately frozen startup hook** for both variants; preserve old hook v2 source/DLLs and old observer-control results. Add one-second samples, source-review and a short harmless generated hook smoke before approving this diagnostic. The added counters are not automatically covered by previous overhead controls. This phase is diagnostic, not new performance acceptance.

Record approximate cumulative allocated bytes, current collection counts by generation, `GC.GetTotalMemory(false)`, cumulative total GC pause duration and process CPU ticks. Label `GetTotalMemory(false)` as the runtime's current managed-heap estimate excluding fragmentation, **not verified live/reachable bytes**. Never pass `true` in the observational phase.

Separately label `GCMemoryInfo` index/heap/generation/promotion/fragmentation/load/available-memory fields as information associated with the reported GC, rather than contemporaneous live heap. Capture sample start/end monotonic/UTC times and collection counts before/after to flag a crossing collection. Even unchanged counts do not make the multi-call sample atomic. Sampled last-GC pause arrays can miss indices; cumulative pause duration is a different counter, not an allocation-root trace.

Capture actual server/workstation GC, latency mode, logical processor count, runtime/architecture and the runtime's GC configuration dictionary. Read only an explicitly reviewed runtime GC-setting allowlist if needed; never dump general environment variables, provider keys or unrelated process state. No setting is changed. Keep OS RSS separate: subtracting either managed estimate from RSS does not establish native allocation or its owner.

Possible diagnostic patterns must remain qualified:

- Rising allocated-byte count with little collection activity suggests accumulation between collections, not an established root.
- A lower last-GC heap plus growing `GetTotalMemory(false)` suggests those counters describe different times/states; it is not proof of a native leak.
- Sustained post-collection managed estimates may justify a root inspection. Stable estimates do not explain all RSS or rule out every retention issue.
- A failed cap with graceful API exit is still a failed cap. Do not classify it as successful because native data stayed valid.

## Optional Phase B: sacrificial collection intervention

**Not selected for execution by the Phase-A proposal.** This needs a separate root binding and source review before being scheduled. Use fresh baseline/index F1000 copies, 16 fixed requests at one-second cadence, then cease HTTP and establish an explicit no-request-in-flight boundary. The small fixed count is chosen before results to create a bounded intervention, not to pass the original 1,000-request test.

A reviewed owned control may request a full blocking noncompacting collection, wait for pending finalizers and request another full blocking noncompacting collection. Put a 30-second intervention bound inside an independent owned-process watchdog; keep the same RSS abort active. Mark all boundaries, counters and post-intervention observations. No root inspection, collection or intervention may be triggered opportunistically by a sample threshold in Phase A.

This changes runtime behavior and therefore is **not a performance/cap acceptance run**. A large fall in post-collection estimates can support collectible accumulation, but cannot itself prove root absence, explain RSS or establish a product fix. Finalizers, JIT local lifetimes and runtime/native pools remain confounders. No such intervention belongs in product request handling.

## Available tooling and remaining gate

Read-only executable discovery and `dotnet tool list --global` found no installed `dotnet-counters`, `dotnet-dump`, `dotnet-gcdump` or `dotnet-trace`. No tool was installed or attached. `lldb`, `vmmap` and `sample` exist, but availability alone does not make them managed object-root tools or authorize attachment. Root analysis requiring a dump/GC-root graph remains open; no new tool installation is part of this proposal.

The installed .NET 10.0.9 reference-pack XML is the primary local API source for the distinctions above; its path/hash and reviewed members are recorded in `gc-diagnostic-proposal-v1/tool-availability.json`. Companion `proposal.json` and `proposal-seal.json` under `.artifacts/experiments/perf02-ledger-index-v1/` bind the proposal and unchanged baseline/runtime evidence. There is no execution-authorized registration, new hook build, API dispatch, profiling or model/training work in this preparation.
