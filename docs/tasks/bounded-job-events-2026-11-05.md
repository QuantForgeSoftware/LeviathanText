# Bounded advisory job events (JOB-01)

Root owns JobEventBus, JobEventStream, generated event/security tests and the job
endpoint integration. This is independent of capture geometry and Imaging owners.
No fitting, corpus/reference reads, real provider requests or LM Studio operations.

## Frozen behavior and resource contract

SQLite job state remains authoritative. The process-local event log is a bounded
advisory cache, never a durable transition history. Existing public Publish,
Subscribe and Replay entry points remain available. Add explicit replay status for
the SSE implementation; old Replay returns only the retained window.

Default bus limits: 128 streams, 8192 retained events globally and per stream,
2 Mi UTF-16 payload/kind characters globally, 16384 payload characters and 128 kind
characters per event, 32 subscribers per stream/128 globally. Bound nested pending
delivery to 256 entries/65536 characters per stream. Bounds cover bus-owned
retention, not payloads retained by external callbacks or process/native RSS.
Constructor-configured smaller limits support deterministic generated controls.

Use pinned stream lifetimes: registry admission/eviction never holds a registry
lock while entering a stream delivery lock. Evict least-recently-used unpinned
streams; subscribed/publishing/replaying streams remain pinned. Global event/char
reservations are atomic. If all streams are pinned, publication remains advisory
and the authoritative job write must not fail; a new subscription gets explicit
capacity/resynchronization behavior, rather than an unbounded allocation.

Retain FIFO publication-time subscriber snapshots and nonrecursive reentrant
draining. At bounded pending overflow, record an explicit resynchronization marker
instead of silently claiming complete delivery; retained original payloads remain
subject to the same limits. Cap collected callback failures while preserving the
existing first/small aggregate exception behavior. Document callback CPU/blocking
as outside this managed-retention guarantee.

Track lost-through sequence per retained stream. A cursor older than the retained
window, an unknown/future cursor after restart, evicted history or live queue loss
requires `job.resync-required`, an empty SSE ID to clear browser cursor state,
and a freshly authorized SQLite snapshot. Do not replay a stale partial history
as complete after a gap. Conservative resynchronization is allowed when bounded
state cannot distinguish an evicted stream from a new one.

Each live SSE consumer has at most 256 queued event references/65536 payload-kind
characters, plus one bounded wake signal. Queue overflow discards covered advisory
events only after a resync boundary is recorded. Observe SQLite and authorization
between at most 64 live deliveries; terminal SQLite state still closes the stream.
Observe current exact credentials/bootstrap configuration, enabled account/role
and membership before disclosure. Cancellation/disposal releases subscriptions,
queues and stream pins. Events never cross jobs.

## Verification and handoff

- Fail-before generated controls demonstrate existing unbounded history/idle job
  retention. Verify count/character/stream/subscriber/pending limits independently.
- Preserve current FIFO, nested publication, callback failure, replay/live race,
  standalone worker observation, terminal fairness and authorization regressions.
- Prove explicit gaps for trimmed/evicted/restarted/future cursors, oversized
  payloads, capacity pressure and slow consumers. Verify empty SSE ID on actual HTTP.
- Verify live key rotation/bootstrap change/account/membership revocation closes
  an admitted stream without leaking the queued payload or a snapshot.
- Freeze a generated fixed workload for before/after allocation/latency/retained
  state measurements, preserving negative results. No RSS correction inferred from
  count bounds alone. Independent review and the no-fitting engineering gate are
  required before marking JOB-01 complete.

## Registered resource follow-up

The first independent ABBA comparison retained exact100000 publication/callback
hashes and bounded-state plateaus, but exposed an idle-stream-churn regression:
loop513/545ms versus257/264ms, with106/107MiB allocations versus62MiB. Preserve
those failed performance results. Candidate v2 will replace linear oldest-stream
search with an O(1) LRU list and recycle only completely unpinned idle stream slots
(all callbacks drained, old log/counters cleared). It will bypass pending delivery
allocation when there are no subscribers and no active drain, and use allocation-
free kind validation. No limits, gap/authorization semantics or workload change.
Require the same generated bounds/FIFO/concurrency tests plus a new frozen matched
comparison before acceptance. Raw GC.GetTotalMemory is inconsistent with heap/
committed/allocated counters on this runtime; report it as a raw counter rather than
a verified live-byte measurement. No post-hoc rescaling.
