# JOB-01 independent managed retention/resource comparison

The frozen bounded-bus candidate plateaus in retained event count and post-GC heap size across100k publications. Its1000-job churn path is substantially slower and allocates more than the unbounded reference; this evidence supports a further optimization, not a general throughput acceptance or a claim that total API/native RSS is fixed.

## Registered experiment and parity

Evidence root: `.artifacts/experiments/job01-resource-v1/`. Before outputs, `source-freeze.json` pins the exact prior bus, current candidate (SHA256 `c9b54d094508d49c34027c7ff575f4936139723cd3c784f5487ee1961412bdf4`) and Contracts/build source. `registration-design.json` fixes100k unique256-character generated payloads,32 fixed jobs with one synchronous callback each, and separately1000 cyclic job IDs without subscribers. The source-only net10 harness compiles only bus+Contracts: no API host, training, image/native package, model, corpus, label or provider dependency.

Payload prefix is the index's eight lowercase hex digits; remaining characters follow the registered formula. Every returned event and callback checks full payload/sequence/job/kind, rather than relying only on a checksum. It retains no events outside the bus. All retained replay events are then independently checked for payload and per-job order. Expected/returned hashes match in all visits: fixed32 `e462e7d27d84864a`, churn1000 `c907fbace2003e76`; fixed callbacks receive all100k events. Subscription disposal leaves zero subscribers and pending deliveries/characters.

Each workload uses fresh processes in ABBA order, with20k-step snapshots after two blocking compacting GCs. Loop/publish timers exclude snapshot/GC work. Limits are30s wall,20s CPU,512MiB sampled RSS and64KiB logs. `execution-registration.json` pins exact source/harness/binary identities and order. All8 owned processes exited0, with empty stderr and no watchdog termination; source/binary closure remained exact. No source was edited by this evaluator.

## Results

Medians of the two visits per variant; heap size and committed bytes are post-forced-GC GCMemoryInfo counters, while RSS includes runtime/managed/OS overhead.

| Workload | Variant | Retained events / characters at every checkpoint | Heap size at100k(MiB) | Committed(MiB) | RSS(MiB) | Loop(ms) | Publish(ms) | Allocated(MiB) |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 32 live callbacks | baseline | 20k→100k / 5.32M→26.6M | 57.538 | 59.094 | 111.133 | 421.339 | 199.633 | 64.634 |
| 32 live callbacks | candidate | 7,884 / 2,097,144 | 4.593 | 14.812 | 66.039 | 438.346 | 213.074 | 65.359 |
| 1000-ID churn | baseline | 20k→100k / 5.32M→26.6M | 57.919 | 58.844 | 110.719 | 260.504 | 21.927 | 62.350 |
| 1000-ID churn | candidate | 128 / 34,048 | 0.252 | 8.594 | 60.242 | 528.699 | 328.581 | 106.633 |

The fixed candidate retains32 streams,7884 events and2097144 logical characters at every checkpoint. The churn candidate retains128 streams/events and34048 characters. The baseline retains all100k events; its heap grows from approximately12MiB to58MiB. Candidate heap remains approximately4.6MiB (callbacks) or0.25MiB (churn). Those counts/heap plateaus support bounded retention in this workload.

The fixed workload's loop is about4% slower and Publish about7% slower in these visits. Churn roughly doubles loop time, increases Publish time about15-fold, and adds about70% allocations. Callback validation and payload generation are common harness costs, so loop time is not bus-only throughput. Only two visits per variant on a shared host were measured; no universal significance/throughput assertion is made. Root will optimize the scan-based LRU and unnecessary no-subscriber delivery overhead, then freeze a new candidate for a new matched comparison. Original v1 evidence remains unchanged.

## Counter caveat and next diagnostic

The raw registered field named `liveManagedBytes` is GC.GetTotalMemory(false). On this runtime it is consistently approximately twice the last-GC heap size and can exceed both committed bytes and total allocation during the workload (for example120663616 versus60331848 heap and61964288 committed). It is **not reported as verified live bytes**, nor silently divided by two. Raw counters remain intact; `summary.json` corrects the analysis label to reported GetTotalMemory, and the first unqualified analysis summary is retained separately. No runtime cause is asserted.

The next instrumentation version will record FragmentedBytes, explicit requested GC kind, GC index/generation/compaction/concurrency and runtime version, alongside a tiny generated no-bus control. That diagnostic is separate from source optimization and will preserve both raw metrics. No image/model inference, fitting, reserved labels, quality acceptance, or general native-memory correction follows from this managed bus experiment.

## Matched optimized v2 follow-up

The source-only v2 harness freezes JobEventBus SHA`2cef7469e84c0b280c3df66705aac41452113512f031c6feaa637eaf5a3f049c`. Baseline remains the original pre-limit bus; v1 evidence is unchanged. The new registration preserves generated payloads, all-event/callback verification,100k publications, ABBA order, forced-GC checkpoints and stopped timers. It adds qualified raw memory/GC diagnostics outside timed work: FragmentedBytes, requested FullBlocking kind, GC index/generation/compacted/concurrent and exact runtime. All8 fresh processes exit0 with no logs/limits, unchanged frozen binaries, identical returned hashes and expected100k/zero callbacks.

| Workload | Original loop ms | v2 loop ms | Original publish ms | v2 publish ms | Original allocated MiB | v2 allocated MiB |
| --- | --- | --- | --- | --- | --- | --- |
|32live subscribers|410.8/415.3|422.2/428.3|195.1/195.1|206.6/210.4|64.63/64.63|62.73/62.73|
|1000IDs,no subscribers|229.4/226.0|253.2/260.4|18.3/16.9|36.1/36.9|62.41/62.39|56.56/56.56|

The v1 churn publication cost of324/333ms is reduced to36/37ms in this separately registered follow-up, while still roughly twice original publication latency. Subscriber publication remains approximately6–8% slower. This is a narrow generated workload finding, not universal throughput acceptance. HeapSize plateaus at4.594MiB (32streams/7884events/2097144characters) and0.251MiB (128streams/events/34048characters), versus original57.5/57.9MiB at100k. Every20k checkpoint retains the same bounded candidate counts. Callback/replay semantics and disposal checks pass.

The independent no-bus control also reproduces inconsistent `GC.GetTotalMemory(false)` accounting:100k retained256-char strings report108347800bytes, compared with HeapSize55615944, Fragmented917752, Committed55803904 and totalallocated55699744. It contains no JobEventBus or API dependencies. Thus the anomalous raw counter is not evidence of retained bus bytes and is neither rescaled nor used as verified live memory. The runtime is recorded in both actual result files. The control is separately registered and excluded from workload timing.

Receipts: `.artifacts/experiments/job01-resource-v2/{registration-design,source-freeze,execution-registration,summary,verification}.json`, `runs/`, and `no-bus-{registration,results}.json`. No imaging/model dependencies, corpus, fitting, or provider calls occurred.

## Final v3 bounded follow-up

A single new matched ABBA set freezes final bus SHA`b1862445b8446037df73ea52e8496908d193da4ac96e34e51ff7da77674323ab`; no further optimization is selected from this set. All8fresh processes exit0, exact100k returned/callback payload hashes and replay checks remain correct, source/binary closure passes, and retention caps matchv1/v2.

| Workload | Original loop ms | v3 loop ms | Original publish ms | v3 publish ms | Original allocated MiB | v3 allocated MiB |
| --- | --- | --- | --- | --- | --- | --- |
|32live subscribers|394.4/405.5|427.9/425.8|187.9/189.6|208.0/206.4|64.63/64.63|62.68/62.59|
|1000IDs,no subscribers|230.3/232.7|251.0/259.2|16.7/15.3|35.6/36.0|62.40/62.29|56.56/56.56|

The final small optimization does not remove the remaining measured cost: subscriber publication is roughly9–11%slower than this set's original unbounded baseline; no-subscriber publication roughly2.1–2.3times. The full loop includes generated payload hashing/verification, so publication-only and loop costs remain separate. V3 is broadly similar to separately measuredv2; selecting best timings across versions would overstate its effect. The resource plateau remains4.592MiB/0.251MiB HeapSize at the same bounded retained counts, compared with original57.5–57.9MiB. Shared-host timing and short process samples do not establish application throughput or total native/RSS repair. Final receipts are `.artifacts/experiments/job01-resource-v3/{source-freeze,execution-registration,summary,verification}.json` and`runs/`.
