# Native AI sacrificial GC intervention: Phase B proposal

**Preparation complete; execution not authorized.** Main index remains unadopted. The original full-24 failure and [Phase-A cap failure](../evaluation/native-ai-gc-observation-2026-11-06.md) remain outcomes. This intervention deliberately changes GC behavior and is neither a performance/cap acceptance run nor product request behavior.

## Fixed contract for root review

Use the unchanged 112-file baseline/index-v2 runtime pair and fresh copies of the same generated F1000 prefix. Run **baseline then index**, exactly **16** fixed replay requests at one-second cadence, with absolute request UTC and monotonic start/end records. No natural corpus, labels, image/model loading or provider dispatch. Require exact native JSON/version and independent original-service verification before API launch and after complete API closure.

The proposed fresh global budget is **600 seconds**, with 330 seconds reserved before another visit, 180-second API and independent 60-second verifier caps, the same **1 GiB RSS abort**, 250 ms RSS samples and five-second TERM grace. Stop at the first failed visit after cleanup/integrity; no continuation, retry, reordering or adaptive threshold. An actual deadline/root binding/registration and coordinated quiet window are still required.

After the last client response, wait for actual registered ASP.NET diagnostic events: exactly **16 starts, 16 stops and zero active request contexts**. The hook subscribes to `Microsoft.AspNetCore` / `Microsoft.AspNetCore.Hosting.HttpRequestIn.Start` and `.Stop`. Those names and the same-HttpContext payload are verified against the installed .NET 10.0.9 Hosting assembly; local primary binary/decompilation hashes are retained. This is a defined request-activity-stop boundary, not a proof that every framework object/pool has been disposed or that no managed roots remain.

Do not guess idle from the HTTP client read alone. Missing/duplicate contexts, count mismatch, unknown stop, another matching listener, 17th start or overlap invalidates the boundary. The driver issues no further HTTP requests. It writes one owned, nonce-bound file command only after observing that boundary; the hook checks the boundary again immediately before collection and between each action. A request arriving during the intervention/idle window invalidates the diagnostic rather than producing a root-absence claim.

The hook requests two **full, forced, blocking, noncompacting** collections (`compacting: false`), with `WaitForPendingFinalizers` between them. A separate 30-second intervention timer triggers the existing owned API watchdog, including TERM/KILL cleanup, even if collection/finalizers block. Record requested/completed action events and numeric measurements before collection, after collection 1, after finalizers and after collection 2. Record runtime-reported `Compacted`/`Concurrent` metadata rather than assuming it from the request flag.

The hook then records fixed idle observations at **1, 5 and 15 seconds** from the end of the collection sequence, including actual elapsed time. No adaptive extra collection or sample threshold is permitted. The driver joins the intervention watchdog and requires those exact marks and unchanged request counts before accepting the diagnostic as complete. The ordinary API/global/RSS guards stay active during idle observations.

## Source ownership and retention audit

All candidate sources/evidence are isolated under `.artifacts/experiments/perf02-ledger-index-v1/gc-intervention-preparation-v1/`; production source and old experiment inputs are unchanged. `StartupHook.cs`, `Intervention.cs` and `run.py` are the review entry points.

The listener keeps reference-identity active contexts only until their matching Stop. The set is capped at 16; rejected extra contexts are not retained. It writes counts/events and primitive measurements, never contexts, request headers, URLs or body payloads. One all-listeners subscription and at most one matching ASP.NET subscription are retained; duplicate matching listeners invalidate instead of accumulating subscriptions. Process-exit cleanup disposes timers/subscriptions/writers. This source audit establishes the intended bounded observer lifetime, not the absence of roots elsewhere in ASP.NET or the application.

Hook v4 adds explicit requested/completed collection events and fixed idle marks. It retains numeric/boolean allowlisted GC configuration only. The owned command has fixed schema/operation/count/nonce, bounded file size and no executable payload; the driver writes it atomically inside its fresh output directory. No production API endpoint or public forced-GC switch is added.

## Executed preparation controls and limits

- **15 managed state-machine controls passed**, injecting no-op collection/finalizer delegates. They cover exact 16 and two collector calls/one finalizer call, missing/extra/duplicate contexts, reference identity, errors, replay, changed boundaries between actions, bounded context retention and Stop removal. No forced GC was executed by these controls.
- **12 driver helper controls passed** against the actual helper AST and mocked events/clock/guard. They cover delayed actual-stop observation, missing/17th/invalid context refusal, intervention timeout first cause/join, exact idle marks, late requests, unjoined guard, command no-overwrite and partial event records. They do not prove native collection behavior.
- **10 whole-driver controls passed** with mocked API/verifier/process boundaries and one generated SQLite row. They cover two successful 16-request flows, abort-first HTTP/RSS/integrity errors, original verifier cleanup, reserve/deadline, unclosed ownership and actual self-SIGTERM/finally in the owned Python control child. No native F1000 API or collection was used.
- **Four environment controls passed**: inherited intervention paths/nonces and unowned provider overrides are removed, OFF removes old metrics/hooks, and GC-setting values are preserved rather than changed. The combined Python gate is 26 passed; combined with the managed engine gate, 41 preparation controls passed.
- The final hook compiled with zero warnings/errors. A harmless managed console emitted 16 generated DiagnosticListener Start/Stop payload objects through the actual hook, ending at zero in-flight contexts; no command file existed, no collection/intervention ran, and the owned process/watchdog closed. This is listener-wiring smoke, **not actual ASP.NET request-boundary or full-GC proof**. Those remain gates for an approved actual run.

The source/input seal and execution-disabled template are under the preparation directory. An actual run must preserve those files and create separate approved execution files with the reviewed input seal and fresh deadline. Nothing in this preparation authorizes API dispatch or forced collection before root's explicit go.

## Interpretation

A large post-collection fall can support collectible accumulated allocation, but is not a root graph, complete live-object census or explanation of RSS. Stable estimates do not establish a native leak. Finalizers, JIT lifetimes, framework pools, observer work and last-GC versus current estimates remain distinct. Never subtract managed estimates from RSS to name native bytes. Actual GC-root attribution still needs a separately approved diagnostic tool/graph; none is installed or attached here. No product GC setting, heap limit, validation cache, history deletion or main index adoption follows automatically.
