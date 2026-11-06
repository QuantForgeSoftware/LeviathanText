# JOB-01 independent boundary review

Date: 2026-11-05. Source review of JobEventBus, JobEventStream,
BoundedJobEventQueue, JobStreamAccessPolicy and the Program job-event route, against
[the bounded advisory contract](../tasks/bounded-job-events-2026-11-05.md).
No production source was edited by this reviewer. Generated tests only: no fitting,
model, corpus, provider, worker dispatch or API host was run by these regressions.

## Findings and owner corrections

Two backend boundary failures were reported to root and corrected by the owner:

1. When all stream slots were pinned, an uncached Publish returned an event but
   ReadReplay for that job with cursor zero claimed complete empty history. The
   SSE capacity fallback already resynchronized, limiting the immediate impact;
   the public replay-status contract nevertheless hid known loss. A constant-size
   global missing-history flag now records the loss and sequence atomically with
   registry admission. The fallback rechecks whether that same job was admitted
   before dropping it; subsequently created streams inherit an unknown-history
   cutoff. No unbounded per-job tombstone dictionary was introduced.
2. Cancellation was ignored during initial replay and before the first snapshot.
   A precancelled iterator could yield records, and cancellation between replay
   yields did not prevent the next event. Explicit cancellation checks now precede
   admission/disclosures and interrupt replay; subscription cleanup occurs when the
   cancelled MoveNext exits.

New disjoint test source:
`tests/LeviathanText.Api.Tests/JobEventIndependentBoundaryTests.cs`.
`.artifacts/experiments/job01-independent-v1/registration.json` and `before/` source
copies pin the reviewed initial boundary. The exact selected class produced four
expected failures in `before/before.trx`: missing-cache zero-cursor loss, precancelled
replay, between-replay cancellation, and precancelled empty-history snapshot.
The same unchanged four tests passed after owner fixes in `after/after.trx`.
No unfiltered API suite was executed. Isolated compilation succeeded; offline NU1900
advisory-metadata warnings remain recorded and are not a security-audit pass.

## Locking, bounds and replay review

Registry admission does not enter a stream delivery lock while holding the registry
lock. Stream publication may enter registry/retention operations, while eviction of
an unpinned stream needs no stream lock; retention locking does not acquire a stream
or registry lock. Pins protect subscribed/publishing/replaying streams from eviction.
Global count/character reservations are serialized. Subscriber admission double-checks
capacity and releases failed reservations; disposal is idempotent.

Pending nested deliveries use publication-time subscriber snapshots and FIFO drain.
Overflow coalesces behind the already queued batch into an explicit gap; payload and
callback-failure retention are bounded. Live consumers retain a bounded queue and one
coalesced wake signal; queue checks avoid a lost-wake window. The live drain yields to
SQLite/auth observation within 64 examined entries. Gap markers clear SSE cursor state
and lead to authorized authoritative snapshots rather than partial replay presented
as complete. Current key/bootstrap/account/role/membership refresh is explicit.

These are managed cache/queue bounds. UTF-16 character counts are not byte/RSS caps.
A replay snapshot may contain the retained window (up to 8192 references), and an
authoritative serialized SQLite job snapshot is separate from the live queue budget.
Externally retained callback payloads and synchronous callback CPU/blocking are outside
the bus retention guarantee. Pre-existing cross-stream callbacks publishing into one
another can mutually wait on delivery locks; production SSE queue callbacks do not
cross-publish. This review does not claim that arbitrary user callbacks cannot block.

## Client followup reported while owner's source was in progress

JobsPanel source inspection identified a late-snapshot race: a resync can start a
Pending snapshot request; a later terminal event stops the stream and refreshes the
job list; the earlier snapshot can then replace the completed row because its guard
tracks watch/credentials but not a later list refresh. Reported to root before any
reviewer production edit. At that review checkpoint the client correction was still
pending; the dated owner evidence below now closes that race. It was not declared
resolved merely because the four backend regressions passed. Actual HTTP empty-ID/
revocation checks, client tests and central verification remain owner evidence.

### Owner client correction evidence

Root confirmed and corrected the client race with an independent snapshot generation
and cancellation on general refresh/watch/auth/destruction. A capacity-resync snapshot
still works after its event source closes. Owner evidence:
`.artifacts/experiments/job01-client-controls-v1/pre-snapshot-epoch.log` reproduced
one targeted late Pending→succeeded race failure with nine controls passing;
`.artifacts/overnight-2026-11-05/job01-client-focused.log` reports all ten cases passing.
Historical original-source controls (two pass/eight new boundary failures) remain in
`original.log`. Root's selected backend group reports 68 passing in
`job-focused-4.trx`. These owner receipts close the reported client race; the four
reviewer backend regressions were independently executed as described above. Final
central integration/operational limits remain root's handoff responsibility.
