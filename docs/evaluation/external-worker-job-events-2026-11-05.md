# External-worker progress over SSE — 2026-11-05

The API-local `JobEventBus` does not receive writes from a standalone worker.
Previously an open browser stream could replay job creation and then never see
completion, while GET on the job resource already showed the terminal state.
Already-completed persisted jobs also had no event to replay after API restart.

`JobEventStream.Read` now combines process-local replay/live events with periodic
observation of the authoritative persisted job. It checks for changes at idle
intervals of one second and after each live batch of at most64 examined events.
Snapshot payloads are complete current JobRecords; terminal snapshots use
`job.completed`, `job.failed` or `job.cancelled`, then close and dispose the
subscription. This observes current state, not every intermediate transition.
Client backpressure, scheduling and database latency prevent a hard wall-clock
one-second delivery guarantee.

Snapshots have no invented event ID. Actual process-local bus events retain their
sequence IDs; Last-Event-ID now accepts an invariant nonnegative integer and resumes
strictly after that cursor. Invalid/negative/overflow/multiple values fail before
opening a stream. Process restarts discard bus history; a persisted snapshot still
communicates the current state, even if an old cursor exceeds the new bus sequence.
This is not a durable event log or distributed message broker.

The stream checks current authorization before replay and live delivery, and both
before and after a persisted observation. Disabled persisted accounts close even
when project membership is disabled; opting out of project membership does not
reactivate a disabled account. An anonymous local stream now closes when the first
account is created, enabling authentication; a configured bootstrap principal retains
its existing behavior.
Revocation during an observation suppresses its data. No project mutation or model
execution is performed by observation.

## Verification

An isolated source copy retained the current store-opening test helpers but restored
the original committed API Program/event-stream implementation. With corrected real
store initialization, all8 new external-state/cursor tests failed: independent
running/completion updates, three terminal restart states, three invalid cursors and
positive resume. The earlier exploratory log includes3 fixture-initialization errors
and is retained without counting those as product failures.

Candidate focused verification passed14 cases:8 external-state/cursor,3 existing
membership stream cases,2 account-disable cases and1 busy-live-queue fairness case.
An independent reviewer added3 passing direct iterator controls for revocation
inside observation, publish-between-subscribe-and-replay exactly once, and missing
persisted job closure. The source was independently reviewed before the bounded
live-batch follow-up. Final normal-output integration is recorded in the session
handoff; initial broad verification preceded that final fairness test.

Receipts: `/tmp/lt-midday-events-baseline-fixed.log`,
`/tmp/lt-midday-events-after.log`; isolated source under
`.artifacts/midday-events/baseline-source/`. Tests use generated records and separate
SQLite connections, without inference, fitting, external services or Greek labels.

## Authentication activation follow-up

A generated regression opens an anonymous local stream, creates the first account
through an independent store connection, and publishes a later probe. Before the
fix the stream remained open until the six-second test deadline; after the fix it
closes without disclosing the probe. The authorization closure now rechecks
`HasUsers()` for anonymous sessions. The focused final stream/boundary/fairness and
review-role batch passes19/19; receipts are
`.artifacts/midday-verification/{anonymous-before-real,streams-role-final}.trx`.
The earlier sandbox test attempt aborted before execution because the test runner
could not bind its local socket and is not counted as regression evidence.
