# Native AI optional heap-count profile diagnostic

**Preparation only; no API profile execution authorized.** The failed full-24 campaign and Phase-A cap failures remain results; main index is unadopted. This proposes an experiment-only child-process GC setting, not a code fix, production default, cause determination or acceptance from 100 requests.

## Supported setting and platform qualifications

Microsoft documents `DOTNET_GCHeapCount` for server GC, with hexadecimal environment values; the profile uses exactly **`0x2`**. Settings are consumed during GC initialization, so each visit must be a fresh process. No existing process or machine environment is changed. [Microsoft GC configuration](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector#heap-count).

Logical processor count is not itself a heap census. DATAS can vary heaps; its configured mode alone does not prove the active platform algorithm. The .NET 10.0.9 source conditionally disables DATAS when explicit heap count is set, but its Apple build excludes regions and therefore dynamic heap count. This conditional behavior prevents a universal “18 dynamic heaps versus 2” explanation. These are versioned-source observations, not binary attestation. [DATAS documentation](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/datas), [v10.0.9 GC conditional](https://github.com/dotnet/runtime/blob/v10.0.9/src/coreclr/gc/gc.cpp#L13391), [Apple/region condition](https://github.com/dotnet/runtime/blob/v10.0.9/src/coreclr/gc/gcpriv.h#L135).

On this macOS Arm64/.NET 10.0.9 host, harmless server-GC console startup smokes report actual configuration **`HeapCount=18` for DEFAULT and `HeapCount=2` for LIMIT2**. Both report 18 logical processors, configured dynamic-adaptation mode 1 and hard-limit settings 0. The API profile must capture and validate its own configuration rather than copy those console results. Configuration reports may differ from user inputs; they are not a complete continuously observed heap-topology trace. [GC.GetConfigurationVariables](https://learn.microsoft.com/en-us/dotnet/api/system.gc.getconfigurationvariables?view=net-10.0).

An initial smoke allowlist incorrectly contained `GCHeapCount`, omitting the actual `HeapCount` dictionary key. That output is retained and cannot show the setting was ignored. Version-2 source/output correct the allowlist. The key mapping is supported by the versioned configuration enumerator and empirical smoke. [v10.0.9 configuration keys](https://github.com/dotnet/runtime/blob/v10.0.9/src/coreclr/gc/gcconfig.h#L77), [configuration enumeration](https://github.com/dotnet/runtime/blob/v10.0.9/src/coreclr/gc/gcconfig.cpp#L40).

## Fixed proposed experiment

Use the unchanged frozen 112-file baseline/index-v2 API pair, fresh generated F1000 SQLite copy per visit, original native payload 4,180,973 UTF-8 bytes and row version 2. Only Application DLL/PDB differ between software variants. No new model, provider, natural corpus/labels, images or training. This historical internal closure predates a later main Imaging guard; it is not a current release/security package.

Two factors are registered independently: baseline/index software and DEFAULT/LIMIT2 child profile. Eight fresh visits mirror the first half to reverse within-profile software order:

| Visit | Software | Profile | Replicate |
| --- | --- | --- | ---: |
| 1 | Baseline | DEFAULT | 1 |
| 2 | Index | DEFAULT | 1 |
| 3 | Index | LIMIT2 | 1 |
| 4 | Baseline | LIMIT2 | 1 |
| 5 | Baseline | LIMIT2 | 2 |
| 6 | Index | LIMIT2 | 2 |
| 7 | Index | DEFAULT | 2 |
| 8 | Baseline | DEFAULT | 2 |

Every visit schedules exactly **100 fixed accepted-proposal replays at one-second start cadence**. Keep the first 50 inside memory/state/HTTP budgets; exclude them only from descriptive post-warmup latency. Record all operations, including absolute UTC and monotonic start/end values. Failed visits retain every completed operation and missing/censored remainder. Zero provider calls and exact native JSON/row version are required.

DEFAULT removes both `DOTNET_GCHeapCount` and `COMPlus_GCHeapCount` aliases from the child environment only. LIMIT2 does the same then sets exactly `DOTNET_GCHeapCount=0x2`. No other GC setting is changed. Reject nonzero/malformed inherited hard-limit settings and frozen runtimeconfig heap-count/max-count overrides before API dispatch. Keep DATAS/configuration/affinity values visible through the numeric/boolean allowlisted runtime dictionary; do not claim their platform activity from a value alone.

The proposed fresh global budget is **1,800 seconds**, with **330 seconds reserved before each visit**; preserve 1 GiB sampled-RSS abort, 250 ms RSS samples, 180-second API wall cap, independent 60-second verifier caps and five-second TERM grace. A separately sealed actual deadline and root quiet approval are required before launch. Deadline/reserve failure leaves remaining visits unrun, never reorders or drops cells.

Resource/API/HTTP failures end that visit. Continue to the next predeclared visit **only after complete owned cleanup, exact unchanged native JSON/version, original-service post-verification and input/configuration checks**. Never retry/replace a failed visit. Integrity, provider use, settings mismatch, source/runtime drift, observer infrastructure or owned-cleanup failure aborts the whole diagnostic. No forced collection, finalizer intervention, heap hard limit, GC-mode change or product setting is allowed. Use hook v5 derived from the non-intervention observer, not the Phase-B collection hook.

Pre-verification completes before API launch; post-verification follows API/sampler/watchdog closure. There are no intermediate integrity verifiers. This differs from the original full-24 scheduling and cannot repair that result. No telemetry subprocess may outlive the next visit; all first-cause cap/HTTP errors, stores, snapshots, raw configurations/counters and exits remain evidence.

## Observer and comparison contract

New hook v5 adds the actual `HeapCount`/`MaxHeapCount` dictionary keys and OS metadata to the non-intervention hook. Separate harmless managed smokes capture two samples/profile with configured count 18/2 and clean closure. This is functional metadata proof, not an overhead or API-workload result. No general environment/credential dump is permitted.

During actual visits require server GC and matching reported heap-count configuration (DEFAULT 18, LIMIT2 2), no reported hard limit and no maximum-count override. Require full GC configuration equality within a visit and across visits of the same profile, rather than silently accepting unexpected settings. An actual mismatch is a failed registration condition, not permission to rename the arm.

Compare software within each profile and profile within each software, preserving replicate/order and all failures. Show matched operation numbers and telemetry aligned to recorded absolute times; do not pool censored failed visits into a surviving latency population. Keep throughput/cumulative CPU, GC/pause counters, current heap estimates, last-GC metadata and sampled RSS separate. Never subtract managed estimates from RSS or infer root ownership. Any observed CPU-versus-memory tradeoff is specific to this host, version, profile, observer and 100-request workload.

Even eight completed visits cannot promote the index, choose a production GC default, establish a release memory SLO or resolve the failed 1,000-request/growing-history gates. Any longer validation or main adoption requires a new reviewed plan. Profile controls and execution-disabled input/template seals are under `.artifacts/experiments/perf02-ledger-index-v1/gc-heapcount-preparation-v1/`; root must review them and authorize an actual execution registration separately.

## Executed preparation evidence

Twenty-three pure/mocked controls passed: 13 profile/runtimeconfig/telemetry controls and 10 whole-driver controls using mocked API/verifier/process boundaries and one generated SQLite row. They cover exact mirrored cells, correct child overlay, unchanged parent/GC settings, no hard limit, runtime/config mismatch refusal, failure continuation after integrity/cleanup, mutation/cleanup abort, reserve, verifier timeouts and actual self-SIGTERM/finally in the owned Python control child. No native F1000 API was used. The complete log and scenario receipts remain alongside the source.

Both corrected setting-smoke processes and both hook-v5 smoke processes exited zero with joined watchdogs. Hook smokes captured two samples per profile and the actual `HeapCount` key, without command/forced-GC/API/model/provider/corpus work. Builds have zero warnings/errors. Old incomplete-key smoke outputs are preserved as limited evidence, not silently replaced. Main service, production settings and all earlier frozen experiments remain unchanged.
