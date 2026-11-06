# Matched native recovery-journal resource comparison

**The completed campaign failed its prospective local latency gate. The candidate remains isolated; no main-store adoption is authorized.** All semantic, evidence, cap and cleanup checks passed. The512-scalar successful-load median improved9.50%, below the predeclared minimum10%; no rounding, threshold change, sample exclusion or rerun repairs that result.

## Frozen scope and results

The isolated candidate adds a per-load resolved-ancestor set to the original recovery-journal validation. It changes no schema, journal capacity, compaction, persistent cache, retained bytes or ownership policy. Independent functional evidence compared2890 graphs/5780 counted public-load observations, with additional save/mutation controls. The matched study used actual native localStorage in eight fresh private Chrome profiles, fixed64 original/candidate/candidate/original then512 original/candidate/candidate/original. No API, editor, model, provider, natural corpus, fitting or forced GC was involved.

Both arms received the same480-record frozen prefix per cohort. The64 prefix replays exact actual generated raw values; the512 prefix is honestly regenerated from the frozen original formula, not recovered from the earlier capped LevelDB archive. Each visit performed32 successful appends and nine explicit `journal-full` refusals; every attempt had a timed save and separate timed public load. Initial admission load and complete integrity/evidence checks were untimed but remained inside resource budgets. All328 attempts passed exact old/new raw strings, scope/base/draft/undo/handle/unique-tip and refusal-without-mutation checks.

The fixed percentiles use sorted rank `floor((n-1)*p)`. Each arm/cohort pools64 successful saves and64 successful loads from two profiles, while18 refused saves and their18 loads remain separate. No warmup or outlier sample was removed.

| Cohort | Successful save p50 original→candidate | Save p95 original→candidate | Successful load p50 original→candidate | Load ratio | Local criteria |
|---|---:|---:|---:|---:|---|
|64 scalars|58.2→49.6ms|62.3→52.6ms|20.2→16.1ms|0.797030|Pass|
|512 scalars|125.5→116.9ms|130.5→121.3ms|42.1→38.1ms|0.904988|**Fail load criterion**|

The prospective criteria required candidate save p50 ANDp95≤1.05×original and load p50≤0.90×original in BOTH cohorts, plus exact semantics/no caps/drift/cleanup failures. Saves pass both cohorts; the512 load criterion fails. Full per-visit distributions, minima/maxima and separate refusal timings remain in the frozen analyzer output. These are local engineering observations from two fresh profiles per arm/cohort, not a universal user SLO or proof of actual-editor responsiveness.

Refusal samples were kept separate from successful-call gates:

| Cohort | Refusals per arm | Refused-save p50 original→candidate | Following-load p50 original→candidate |
|---|---:|---:|---:|
|64 scalars|18|38.6→33.6ms|21.7→17.0ms|
|512 scalars|18|83.7→79.2ms|43.5→39.5ms|

Across all eight visits there were328 attempts:256 successful appends and72 explicit refusals, each followed by a public load. Eight initial selected-store loads were untimed. Internal `save` validation loads are part of save timing rather than additional independent latency samples.

## Resource and closure evidence

The run lasted10:08:11.679–10:08:55.467UTC. The largest recorded pre-cleanup visit elapsed interval was7.085s, largest recorded sampled cumulative owned-tree CPU7.67s and largest recorded sampled tree RSS807.86MiB, below the registered60s/45s/1GiB visit bounds. Observation covered the controller plus witnessed Chrome/descendant tree; shared RSS is counted per process, the launcher is excluded, and100ms sampling can miss short peaks. CPU observations are not a complete scheduler trace and RSS is not live managed-heap/native-retention attribution.

The descriptive lower-rank median of two per-visit RSS peaks was674.28→700.17MiB for64 and790.81→800.98MiB for512. Neither exceeded its predeclared original-plus-`max(16MiB,5%)` flag allowance. This does not establish zero memory regression: observed candidate peaks were higher, and the allowance is only a descriptive local comparison.

All90 registered inputs remained unchanged. Eight owned Chrome processes exited143 under deliberate cleanup and were reaped; the controller exited0. No remaining/uncertain descendants, launcher errors or cleanup errors were recorded. All fresh profiles and the owned temporary root were removed. Complete full before/after evidence is retained; charged evidence was43,812,310 bytes within the existing64MiB campaign budget.

## Transport versions and limitations

All prior failures remain preserved and distinct. First metric v2 completed zero visits after builtin compressed-CDP receive failure. The separately captured direct diagnostic showed `Max decompressed message size exceeded`/1006 before store execution; fixed wire controls did not reproduce that compressed-builtin negative. Cached uncompressed v3 completed the64 cohort then failed first512 admission on the unchanged8MiB wire bound. A subsequent fixed-prefix untimed control reached ONE save/load but failed returning the entire result through CDP. None is pooled into the completed campaign.

Root separately approved v5 after pure protocol controls and a fresh untimed ORIGINAL512 complete481-result roundtrip. Prefix evidence used30 fixed batches×16 rows; complete result UTF-8 used exactly32 binary batches, each≤256KiB and aggregate≤the existing8MiB. Strict nonce/label/index/count/byte bounds, fatal UTF-8 decoding and unchanged full controller-side oracles apply. CDP returns only a delivery acknowledgement; no evidence is omitted or replaced with digest comparison. Encoding/transport stays outside store timers and inside the resource budget for both arms/cohorts. The detailed failure/control chronology is in [the transport diagnostic](workspace-lineage-transport-diagnostic-2026-11-06.md).

This finite matched workload does not complete the earlier capped512 full-journal campaign, demonstrate actual editor input responsiveness or remove the need for a separately reviewed optimization. The failed prospective gate leaves the candidate isolated. No main source change, policy relaxation, history deletion, forced GC, dependency installation or cap increase follows.

## Exact receipts

All paths below are under `.artifacts/experiments/workspace-lineage-matched-v1/`:

- `registration-v5.json`:90-file seal SHA256 `3df965bc501ca49c437e97903b4ce7f5cd529d99e6a959833ba7ccc3f702954a`.
- `root-execution-v5.json`:separate exact root approval and absolute deadline, before dispatch.
- `results-v5/proof.json`:complete eight-visit proof SHA256 `45401e120f42372357879b7aff940806b6286cfb594da64503c3ca3a9007b61f`.
- `results-v5.launcher.json`:controller/observer/cleanup proof SHA256 `b35e08f721fffbbbff8631ad864dbcb6fe0b872da50f997a570f7bc3b285cd5a`.
- `results-v5/closure-audit.json`:90-input zero-drift and complete owned-process closure.
- `results-v5/analysis.json`:frozen prospective analyzer output SHA256 `2b65b1f73af9875198171368c5977c6d98f139c988b3827498292e9d4b98f5ee`.
- `results-v2/`, `results-v3/`, `close-diagnostic-v1/` through `-v4/`:failed versions, bounded closed LevelDB archives, controls and chronology, never overwritten.

## Source-only next hypothesis, not a new implementation

The source continues to enumerate and reread current native storage on each scan, bound UTF-8/depth/aggregate bytes, parse JSON, clone/validate every matching record, verify scope/key identity, and reread keys/raw strings before returning. `scan` calls `boundedRaw` and then `parse`, which calls that pure bound check again; `parse` also performs recursive `cloneJson` and schema checks. The lineage-only candidate leaves these costs intact. The smaller proportional512 benefit is consistent with a larger remaining parse/validation cost, but no phase profile establishes dominance or a causal allocation breakdown. No new prototype or workload is authorized by this hypothesis.

A future finite proposal should first separate current storage reads, UTF-8/bound checks, JSON parsing, schema/clone validation and lineage traversal while preserving exact results/error order, using generated records and a separately registered observer. Any optimization must retain every security budget, malformed/unknown-field refusal and final mutation reread. Persistent parsed-record caches risk accepting externally changed localStorage, missing cross-tab writes, reviving another owner or trusting forged/branched history; digest-only equality is insufficient. Sharing mutable parsed snapshots can also permit caller mutations to contaminate later reads. Do not infer that a cached result is authority, remove concurrency checks, skip validation, compact/delete history, or change journal capacity merely to improve a timing. Any later candidate needs independent semantic controls and a fresh prospective gate, without relabeling this failed gate.

## Retained diagnostic profile inventory

`retained-diagnostic-profiles-v1.json` records exactly two closed owned roots: `/private/tmp/lt-lineage-close-93h6ca` and `/private/tmp/lt-lineage-close-U51Xab`. At inventory time, each five-file closed LevelDB set matched its artifact archive's names, sizes and SHA256 values; no database engine opened them. Archive scope is the LevelDB evidence, not a full Chrome-profile backup. Their proofs record deliberate Chrome143 cleanup and no descendants. Root later explicitly approved cleanup of exactly these two roots. Before removal, canonical root/lstat/UID identity, unchanged inode across checks, exact five-file source/archive hashes and absent recorded PIDs/no process references were rechecked. Both roots are now absent; all archives/proofs and the original inventory remain unchanged. No foreign process was signaled. Successful controls and both campaign roots were also removed after confirmed cleanup. No user/default store, corpus, labels or original study archive was changed. Inventory SHA256 `351e03c25a8564136da80ebd82b4fe0ebc4eb931d9335d1b927ac34a40593b31`.

Final retained-root cleanup receipt: `retained-diagnostic-profiles-cleanup-v1.json`, SHA256 `900f23f6f936c63d341a11e89656638c426df58eab27f2b150c9a2c4f0e6859b`. It records root absence and unchanged five-file archive hashes for each root; no new browser/store workload ran.
