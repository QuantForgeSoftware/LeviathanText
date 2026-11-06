# Owned recovery journal: capped descriptive baseline

The registered baseline stopped at its first observed CPU cap. The64-scalar cohort completed; the512-scalar cohort is partial. The4096/16384-scalar cohorts and actual LineEditor128-event/rAF/recovery trial did **not run**. No retry, cap increase, compaction or production-store change was made.

Registration: `.artifacts/experiments/workspace-journal-study-v1/registration-v2.json`, SHA256 `9cb3ca1e87d919a75457aaee3356c68b139e0d619050d739df50fba88afd8e5a`; exact root approval and independent cleanup review preceded dispatch. The earlier v1 seal is preserved as unexecuted preparation. Results are in `results-v1/` beside that registration, with `results-v1.launcher.json` as the independent outer-process receipt.

## Observed store calls

Each cohort used its own empty native localStorage profile, the actual public WorkspaceDraftStore, generated Greek text, an unchanged true base and two fixed-shape undo entries. The per-save/load intervals exclude journal-byte verification, storage enumeration and progress transport. Those operations and the registered resource observer still contribute to whole-visit CPU/wall cost. These are single descriptive observations, not controlled before/after comparisons or actual editor responsiveness measurements.

| Generated base | Reported attempts | Successful records | First refusal | Save p50 / p95 / max | Load p50 / max |
|---|---:|---:|---|---|---|
|64 scalars|521, completed|512|edit512, `journal-full`|25.5 /56.2 /62.5ms|9.1 /22.7ms|
|512 scalars|513, partial|512|edit512, `journal-full`|58.8 /122.1 /131.6ms|20.3 /46.7ms|

Edit indices start at zero. The completed small cohort includes nine refused calls: the first refusal and eight fixed followups. In the partial512-scalar cohort only its first refusal was reported before the observed owned-tree CPU threshold stopped the visit. The last two successful saves took130.0 and129.4ms; they held511 and512 records respectively. Do not infer results for missing followups or larger cohorts.

Among reported64-scalar calls,341 exceeded the preregistered16.7ms descriptive marker and66 exceeded50ms; in the partial512-scalar calls the counts were434 and293. Percentiles use the fixed observed row arrays (p50 upper middle, p95 rank floor((n−1)×0.95)); they mix successful calls and explicit refusals and must not be treated as universal user latency thresholds. The small cohort's maximum sampled aggregate browser-tree RSS was683.9MiB, with21.44 observed CPU seconds; this includes Chromium processes and is not solely journal memory. The partial cohort has no accepted final peak/cost receipt, so no numerical RSS claim is made for it.

All reported rows passed exact prior-raw-byte immutability and last unique durable-tip/text checks. The small cohort ended with512 records and1,359,025 encoded key/value bytes; the partial cohort's last reported state held512 records and4,111,537 encoded key/value bytes. Key bytes are included in these observations; the store's own4MiB aggregate admission policy counts record values. No explicit refusal was relabeled a successful recovery or automatically assumed native quota exhaustion.

## Stop, closure and preparation evidence

The controller recorded `observed-tree-cpu` at the registered45-CPU-second per-visit threshold. The CPU limit includes more than timed save/load calls, so it is not a pure journal-CPU measurement. The returned stream was closed during the abort; that secondary CDP error is retained. The outer launcher exited1, with no own timeout or surviving descendants. The single capped run is a failed/incomplete study, not an all-cohort pass.

Closure verification checked all518 registered source/runtime/input files: zero drift. Every controller child exited and was reaped, no outer descendant remained, and cleanup reported zero errors. Both created private profiles were removed only after their generated partial native localStorage LevelDB evidence was archived; later profiles were absent. The completed small cohort also has a complete JSON result and raw nativeStorage snapshot. All partial progress rows, logs, controller proof, outer proof and native storage evidence remain preserved. Evidence: `results-v1/closure-verification.json`, `proof.json`, `A-64-result.json`, `A-64-nativeStorage.json`, both `*-visits.jsonl` files and partial LevelDB directories.

Preparation separately passed strict TypeScript/esbuild, isolated API and fixture builds, byte-exact native Save/Load/read-only reload, two admission controls, two native owned-process cleanup controls and four actual host-readiness controls. No browser or timing loop ran during those controls. The first generated fixture's optional default region/frame IDs serialized as invalid `col-` and failed NativeJson reload; its source, failed database and exception log are retained as a separate product finding. The corrected frozen fixture used explicit valid IDs and a metadata-only unavailable capture whose actual preview returns422. No real image, model, fitting, provider or corpus-accuracy workload participated.

## Remaining work

The observed journal costs justify a separately designed optimization investigation. The current append-only recovery policy and all old bytes must remain intact; no automatic deletion or capacity relaxation follows from this baseline. A candidate needs unchanged ownership/base/lineage/branch/error oracles, explicit concurrency and persistence-failure behavior, independent review and a newly approved matched protocol. The actual component input/rAF trial and unexecuted cohorts remain open. This run establishes neither their behavior nor a speedup, cross-platform result or production security/dependency closure.
