# PERF-01 independent evaluation registration

Follow-up: the original null-only candidate below was rejected after a real transient-input recovery counterexample. A separately registered retry-safe candidate retains the pause for Pending retries and passed all 12 new primary visits. See [the follow-up report](worker-retry-safe-followup-registration-2026-11-05.md) for the retained failed observation gate, complete results, and the recommendation limited to the revised condition.

Status: root-approved campaign completed, all24owned workers exited0, all input/source/runtime/evaluator hashes unchanged. No production adoption is implied by this report. Root owns the fresh baseline/candidate build and semantic cases; this evaluator owns reduced inputs, complete-state inspection, normalization, controls and independent execution.

## Actual frozen baseline

`.artifacts/experiments/perf01-backoff-v1/` contains226frozen source files and109files per worker package. Independent checks confirm the sole source difference is conditional idle delay, and the only package differences are WorkerDLL/PDB. The candidate was compiled against the exact baseline references. Independent TRX parsing confirms35/35passed on both variants, with zero skipped/failed cases. The POSIX active FIFO drains and generated lease/heartbeat tests belong to those receipts; they do not establish an automatic production heartbeat, which has no caller.

The seed/inspector is compiled from the same frozen source, then staged with the **exact109baseline runtime files plus its four helper files**. Its own source-project build outputs have differing hashes and remain preserved, not silently substituted for the worker package. It references only Infrastructure/Imaging; no Worker/Training code is needed for seed or inspection. No model was loaded during preparation. The complete model package is copied/hashed, including an optimizer sidecar that is never restored; the selected230005-byte weight file is pinned to`03952a43d94bf9e53d8f5de886d3470057a2db55222c01d14172111a47b06fda`.

Known limitations remain explicit: this worker retains ImageSharp3.1.11with six known advisories; the SEC prototype is absent. Later main fallback-dimension fixes are outside this immutable benchmark snapshot and do not affect its registered small PNG/JPEG inputs. Native allocation attribution is unavailable: sampled RSS measures the complete process, not native bytes or a leak repair. Historical P01 remains spent; this is no new accuracy confirmation.

## Inputs and complete state

Generated inputs are three240x96white/bar/diagonal PNGs with one fixed source polygon, no target text. Retained P46 photographs/rights notices and the exact reduced geometry file are copied unchanged. The latter admits only six named structural fields per row,81rows/27–26–28, with all specialist decisions pending. No transcription, review packet, PAGE/TEI, dataset loader or accepted/locked labels are input. No training, scoring, providers or LM Studio calls are authorized.

Model-free preflight successfully seeds0/3/3projects and101/201/31jobs for N00/G00/P00. Each primary route uses one lower-priority pending no-op sentinel. Initial databases and full JSON states are hashed before dispatch; fresh per-process copies preserve the exact initial bytes. All variants use the same absolute image/model/rights paths.

Inspection reads **every column** of projects/jobs and their entire raw native/jobJSON, retaining unknown fields. The normalizer checks all seeded native fields, exact project-version increments, whole-job success/output transitions, scope/layer/model provenance, exact prediction/confidence/grapheme values, IDs, metadata, geometry, array order and untouched sentinel. Only`createdAt`on the exact expected newly emitted L0 revision IDs is replaced; every removed clock remains in a separate receipt and the original state remains untouched. Seeded job clocks and nested/unknown clocks are not normalized.

Seventeen pure generated safety controls pass: full fixed-route graphs, clock-only equivalence, tiny confidence/text/provenance/unknown-field changes retained, wrong scope/nonL0/missing/duplicate outputs refused, seeded geometry/rights/version/job/sentinel changes refused, timestamped worker-log parsing, immediate owned memory abort and partial-constructor ownership/drain. No model, corpus or actual worker runs are used by these controls. Independent source review caught the timestamp prefix in actual Worker log lines before dispatch; the parser now retains the raw UTC stamp separately and compares the bare job ID. A compile-only File API typo was corrected with the failed build log preserved.

## Fixed primary matrix

| Route | Per process | Expected native result | Wall cap |
| --- | --- | --- | --- |
|N00|100attempts of101pending no-ops|100exact successes, sentinel pending|150s|
|G00|200fixed one-line recognition jobs plus sentinel|200L0, noL1/L2|360s|
|P00|10cycles of pages1440/1450/1460 plus sentinel|30surface jobs/810L0, noL1/L2|180s|

Each route uses sequential fresh processes baseline,candidate,candidate,baseline. Complete normalized outputs must match all four times, including both baseline replicates; no confidence rounding or selected-field inspection is allowed. The twelve primary processes total400no-op,800generated inference and120retained-page visits/3240L0. Repetition is not additional natural pages or geometry acceptance. A mismatch stops the campaign; no adaptive rerun or checkpoint selection occurs.

## Generated operational controls

Before primary inference, both variants run actual empty-store1.25sbackoff followed by a one-job admission/budget exit, separate idleSIGTERM/SIGINT, unsupported dispatch plus four missing-package attempts with budget5and an untouched sentinel, and two owned workers consuming60priority/time/ordinal-ordered no-ops. The missing directory is guaranteed absent, so retry controls cannot construct a model. A separately registered SQLite AFTERUPDATE audit is installed **only in generated multiworker control stores**; it preserves full claim/finalization records and actual lease owners. Finalization stdout alone is not misrepresented as claim order. Distribution is reported without an equal-sharing threshold. Active FIFO drain and managed lease controls reuse the exact35semantic receipts. Existing central generated HTTP tests provide quota/auth/SSE evidence; this campaign does not claim new browser/SSE coverage.

## Bounds, cleanup and approval

Root approved a90-minute overall campaign, at least nine minutes reserved for cleanup,2GiB observed RSS per worker and4GiB aggregate. Target sampling is250ms; actual timestamps include OS process-query latency and do not create an instantaneous memory/time bound. Complete within-cap logs are retained with64KiB limits per stream; snapshot/helper output has a32MiB limit. Resource/log/unknown-child breaches immediately kill only owned groups and preserve partial stores. Ordinary wall/user cancellation signals the owned worker, drains at most60s, then kills only its owned group. Partial constructor failures remain registered for cleanup. Every PID, command, attempt timestamp, sample, exit and source closure is retained; no failed state is erased or automatically requeued.

The final `registration.json` must pin sources,113-file helper runtime, worker/native packages, reduced inputs, model, initial stores/states, control SQL, normalizer/runner and thread/GC environment before outputs. Root approval is a separate file binding the exact registration SHA. A changed evaluator or workload requires a new registration. Source-only preparation and prior no-op findings are not execution authorization. Root will coordinate the actual quiet start after other approved resource work has cleaned up.

## Completed registered results

Root approved exact v2 registration SHA`db5788ebd85649f0382cbbe0bbe9114e01d51e6d69415842e15e3c7289d8eea6` before dispatch. Root's pre-dispatch review found that failed/requeued logs append a parenthesized error string; the first v1 seal and matching evaluator source snapshot were retained unexecuted under`registrations/v1-unexecuted`. The v2 parser retains bounded raw failure text and has actual Program-shaped Pending/Failed controls, rejecting unframed/oversized tails. No failed inference run was hidden or retried.

The approved campaign completes in719.80seconds, well inside90minutes. All12operational and12primary workers exit0, every registered closure remains exact, every pending sentinel is untouched and noL1/L2is written. All four full-state digests agree per route: N00`beb4db8f…e08d1`, G00`2fd35104…d8ac9`, P00`4cb3e908…7c544`. The raw full digests, complete native/job state, timestamps and all process receipts remain in`campaign/`. G00totals800generated L0; P00totals120visits/3240L0from the same three provisional pages. No source labels, fitting, scoring or optimizer restoration occur.

### All primary visits

| Route / visit | Variant | Wall seconds | Median observed completion gap, ms | Last sampled CPU seconds | Maximum sampled RSS MiB | RSS samples |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| N00 / 1 | baseline | 99.815 | 1004.996 | 0.81 | 81.1 | 340 |
| N00 / 2 | candidate | 0.283 | 0.702 | 0.00 (missed execution) | 3.0 (startup only) | 1 |
| N00 / 3 | candidate | 0.283 | 0.709 | 0.00 (missed execution) | 2.5 (startup only) | 1 |
| N00 / 4 | baseline | 99.746 | 1004.948 | 0.79 | 79.3 | 341 |
| G00 / 1 | baseline | 202.838 | 1010.251 | 3.29 | 194.9 | 691 |
| G00 / 2 | candidate | 2.784 | 4.108 | 1.14 | 180.3 | 10 |
| G00 / 3 | candidate | 1.112 | 3.897 | 0.86 | 179.2 | 4 |
| G00 / 4 | baseline | 201.548 | 1010.428 | 3.13 | 193.8 | 686 |
| P00 / 1 | baseline | 31.195 | 1061.900 | 3.13 | 346.4 | 107 |
| P00 / 2 | candidate | 1.668 | 40.867 | 1.72 | 339.1 | 6 |
| P00 / 3 | candidate | 1.686 | 40.560 | 1.74 | 337.2 | 6 |
| P00 / 4 | baseline | 31.136 | 1061.853 | 3.16 | 344.9 | 107 |

Median wall time across both visits: N00baseline99.781s/candidate0.283s; G00baseline202.193s/candidate1.948s (individual candidate visits2.784s and1.112s); P00baseline31.166s/candidate1.677s. No fastest visit is selected. The large gains are dominated by removing the intentional one-second inter-job wait, not a faster recognizer or improved Greek accuracy. All per-visit cold startup, first/last completion and raw clock values remain available. Only two fresh processes per variant are observed here; no universal throughput/SLA confidence interval is claimed.

The fixed250ms sampler misses execution entirely on the sub-second no-op candidates: their2.5–3MiB/zero-CPU startup readings are **not** low-memory or zero-CPU results. Sampled CPU is cumulative at the last available observation, not exact process-final CPU; confidence/rate comparisons cannot treat it as a complete native cost counter. G00/P00sampled RSS is broadly similar/slightly lower in this set, but fast visits have fewer samples and can hide transient peaks. No native allocation attribution exists, no leak is repaired and no arbitrary byte ceiling follows from staying below the2GiB abort. Removing waits can increase allocation rate per wall second; keep process job-budget/recycling and independent resource gates.

### Operational findings and scope

Both variants preserve exact static claim priority/time/ordinal order. Baseline workers each claim30of60; candidate distribution is60/0. This is retained as startup/load-sharing evidence, not concealed behind an equal-sharing claim or called starvation on a finite batch. Continuous mixed-priority fairness and multiworker representative inference remain outside this proof. Retries retain the same five total attempts, error records and untouched sentinel, but candidate retries occur sooner because completed failed/requeued attempts also skip the old incidental delay. This change does not create a remote-service retry rate policy. Idle waits, SIGTERM/SIGINT and budget retirement remain intact.

Managed explicit heartbeat/lease controls pass; production automatic heartbeat renewal is still absent. Existing generated HTTP quota/auth/SSE receipts support unchanged policy, but this primary campaign creates no new browser/SSE responsiveness or API-admission throughput evidence. All timed jobs are independently owned, unbound local-mode records in fresh stores. No reviewer/model/rights gate is relaxed.

### Independent adoption recommendation

The minimal idle-only delay change is justified for production integration **within this reviewed scope**, followed by root's fresh current-source package and required engineering gates. It removes an artificial queue throttle while preserving complete outputs, retry bounds, static ordering, idle behavior and shutdown across generated and authorized retained-page inference. Do not widen the patch to heartbeat, scheduler, model, preprocessing, quotas or retry policy. Document the faster retry cadence and observed unequal tiny-job sharing; any required remote-rate backoff belongs to an explicit separate policy. Keep native-resource acceptance open and do not market the result as a memory fix, all-platform qualification or Greek accuracy improvement. Root owns actual adoption and release.

Full artifacts: `.artifacts/experiments/perf01-independent-v1/{registration,root-approval}.json`, `campaign/{controls,verification}.json`, all24worker receipts and complete states, and `analysis/full-analysis.json`. Analysis runs only after quiet release; immutable evaluator/package/source/input files are not edited by analysis.

## Adoption hold after transient-recovery review

The production recommendation above is **on hold**, not an adoption approval. Root identified a real missing axis before integration: skipping the wait after Pending retries can exhaust the same four attempts before a transient input becomes available. Exact static-missing-input counts and identical final successful-work graphs do not prove retry recoverability. A new separately registered delayed-package control fixes publication at1.25seconds and observation at2.75seconds on one generated line; no primary evidence is overwritten. If confirmed, a new candidate must retain delay for null **or Pending** results, with new recovery/preservation validation. See [the fixed recovery proposal](worker-retry-recovery-registration-2026-11-05.md). No adaptive source change or rerun is attributed to the completed db5788 campaign.

The separately approved fixed recovery experiment subsequently **confirmed** the regression: baseline waits twice then succeeds after package publication, producing oneL0; the null-only candidate fails after four fast attempts and produces noL0. Both snapshots/closures and owned cleanup are preserved. Thus the original null-only candidate is rejected for production despite the primary successful-work speed/preservation results. Root is preparing a distinct null-or-Pending candidate; it receives no inherited actual recovery proof until tested.
