# Generated native AI process stability run — 2026-11-05

**Outcome: 1,000/1,000 cycles pass; both owned API processes exit 0 and the provider thread stops.**

The bounded engineering workload uses a frozen macOS arm64 package and an owned
loopback provider double. It does not call LM Studio or a real model, fit a model,
read a witness transcription, or evaluate Greek accuracy.

## Registered workload and source boundary

Evidence: `.artifacts/native-ai-process-soak-v1/`. Registration preceded launch:
1,000 issue/explicit-accept cycles at a nine-second start cadence, restart after 500,
and a 9,600-second wall limit. Package:
`.artifacts/releases/midday-runtime/`; registered release-manifest SHA-256:
`87e223a8fd7536028e7174d78bc3d7c56410d39e79b64f7caf95037d3e341d00`.

Each generated request supplies forged client source text. The native API must
instead issue from authoritative L1 `αβγδε`, selecting `β`, and preserve the
original provider request and issued/acceptance receipts. The double proposes `Ω`;
explicit acceptance appends L2 `αΩγδε` with the authenticated account ID. Every 100
cycles checks exact source L1/attachment preservation, count equality and optimistic
project-version increments. Restart checks exact persisted native JSON/version.
Final idempotent acceptance must make no write; diplomatic export retains only L1,
and original generated PNG/XML hashes must stay unchanged.

The registration's wider workload list also names revocation, tampering and caps.
Those negative boundaries belong to the separate [20-case process check](native-ai-packaged-process-2026-11-05.md).
This 1,000-cycle run uses unlimited logical requests and does not repeat those
negative cases. It measures sustained successful persistence/restart behavior.

The frozen backend predates later annotation UI, strict-PNG, TEI physical/language
and unused proposal-event retention corrections. This is evidence for its recorded runtime snapshot, not a final-source
whole-product soak. Its initial source/harness hashes and later source-observation
receipt retain their actual timestamps; later observations are not presented as
preregistered identities. A syntax error caught before any process launch is kept
in `syntax-before.log`, rather than counted as a product failure.

## Final results

The run completes in 8,992.40 seconds (about 2 hours 29 minutes 52 seconds), with 2,006 API requests and
1,000 loopback-double requests. Every registered successful cycle is retained.
The second API process resumes the exact project/version after cycle 500. Final
idempotent replay leaves native state/version unchanged; diplomatic output and
generated source hashes pass their preservation checks.

Root independently opens only the owned final store read-only: one project,
one L1, 1,000 L2 revisions, 1,000 annotations, 1,000 issued attachments and 1,000
acceptance receipts, native version 2,002. The normalized final checkpoint JSON
is 4,082,138 bytes; the actual persisted wrapper JSON is 4,936,362 UTF-8 bytes. These
are different representations, not interchangeable size measurements.

All 1,000 operation latencies: median 63.05 ms, nearest-rank p95 131.85 ms,
p99 175.16 ms, maximum 356.10 ms. These times include issue and acceptance requests;
the intentionally sparse nine-second start cadence is not maximum throughput.

| Owned API segment | RSS samples | First → last sampled MiB | Minimum / maximum MiB |
| --- | ---: | ---: | ---: |
| Cycles 1–500 | 50 (cycles 1–491) |152.20→437.28 |108.34 /479.02 |
| Cycles 501–1000 | 50 (cycles 501–991) |255.47→677.31 |208.33 /708.42 |

RSS grows materially while append-only history grows. There is no managed-heap,
GC-committed-memory or allocation-rate trace here to distinguish live retention,
GC capacity, serialization churn and native work. No memory acceptance or leak
correction is claimed. A fixed-size-versus-growing-project diagnostic is tracked
separately; history must not be deleted to manufacture a better memory result.
The [source follow-up](native-ai-growth-resource-followup-2026-11-05.md) identifies
an unused proposal-event log root and removes that publication path with HTTP
regressions. This completed run is before that correction; it measures no after-fix
RSS reduction and does not establish the cause of the full observed increase.

`results.json`, `cleanup.json`, `cycles.jsonl`, descriptive `analysis.json` and
`root-final-native-check.json` retain the actual outcomes. The driver exits 0;
both API exits are 0; providerThreadAlive/apiProcessAlive are false. Subsequent
read-only process inspection finds none of the three recorded owned PIDs. An
initial sandbox-denied ps inspection is not counted as product failure; the
permitted read-only check confirms absence. No unrelated process was stopped.

## Interpretation and limits

The project intentionally grows on each cycle with an issued record, acceptance
receipt, L2 revision and annotation. Per-process RSS is sampled every 10 cycles.
Latency and RSS summaries are descriptive, on a shared host; they are not a
preregistered performance comparison, native leak correction or peak-memory bound.
The analysis uses median and nearest-rank 95th/99th percentiles of all operation
latencies, preserving every cycle rather than selecting favorable windows.

No original corpus, Greek decision, geometry, label, model or checkpoint changes.
The package retains the unresolved ImageSharp Imaging dependency; this run does
not approve release distribution, public hosting or broader platform support.
Cleanup is restricted to the two owned API children and owned provider server;
LM Studio settings/models and unrelated processes are untouched.
