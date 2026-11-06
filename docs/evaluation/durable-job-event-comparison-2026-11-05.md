# JOB-01 durable SQLite comparison

Root executed the [fixed generated comparison](../tasks/durable-job-event-comparison-2026-11-05.md) after all three other agents acknowledged a CPU quiet window. `.artifacts/experiments/job01-durable-v1/` retains the frozen49-source/project-file closure, harness, execution registration, eight fresh process/store visits, raw250ms OS samples and summary. The built variants differ only in Probe DLL/PDB; they compile the same actual JobEnqueuer and current SQLite store, with original unbounded versus final bounded v3 bus. Compilation reports zero warnings/errors. Audit was disabled for this cached engineering build; it is not a fresh security audit.

Every process exits0. All full returned job JSON, callback sequence/kind/job/payload checks and final authoritative SQLite records match exactly. Across variants and visits, complete transcript and final-state SHA-256 match. Frozen source and all runtime/native files remain unchanged. No image, model, training, HTTP host, real corpus, label or provider is loaded. SQLite initialization and seed writes are outside timing; actual progress/completion durable writes and their synchronous callbacks are inside method timing.

| Workload | Original/bounded method ms | Original/bounded loop ms | Original/bounded allocated MiB | Original/bounded post-GC heap MiB |
| --- | ---: | ---: | ---: | ---: |
|32 jobs/subscribers,10240 writes|2920.9 /2815.8|2955.9 /2848.8|72.07 /71.73|7.69 /4.80|
|1000 jobs,10000 writes,no subscribers|5679.9 /5804.8|5710.5 /5833.0|66.76 /65.78|7.92 /0.53|

Values are medians of two visits per variant, not confidence estimates. Churn method visits are original2868/8492ms and bounded3141/8468ms: both variants become much slower late in the fixed order. That variation dominates the small median difference; the measurement does not identify its cause or establish no performance regression. The subscriber median is3.6% lower and churn2.2% higher here, but neither is reported as a universal application throughput effect. There was no selective rerun or best-visit selection. Maximum sampled RSS and qualified managed counters remain in the summary/raw process receipts.

The separate [publication-only measurements](job-event-retention-resource-2026-11-05.md) still show a real narrow cost (roughly0.2microsecond/event in those visits); this durable-write comparison does not erase it. Bounded event/history retention and exact final records are demonstrated. HTTP/browser concurrency, all-platform resource limits, hard native memory bounds and release performance acceptance require their respective evidence. Raw GetTotalMemory is retained as an unverified runtime counter, without rescaling.

The execution registration adds a fixed300-second overall safety cap before any timed output, besides the180-second/120-CPU-second/512MiB/64KiB per-process limits. No cap was reached. Only recorded owned process groups could be signalled; all completed naturally. The earlier source-only plan's per-process caps were unchanged.
