# Job event concurrency

Date: 2026-11-05. In-memory generated events only.

The event bus previously appended under one lock but replayed without it, mutated subscriber lists under a different lock, and delivered concurrent publications without ordering. A later event could reach an SSE subscriber before an earlier one; advancing the stream cursor to the later sequence could then suppress the earlier event.

Each job now has one synchronization boundary for sequence allocation, append, delivery, subscription changes, and replay snapshots. Different jobs use independent locks. Replay returns a stable snapshot. Subscribe-before-replay and the SSE cursor's duplicate suppression remain intact. Subscriber callbacks are synchronous and must stay short; the production subscriber only writes to a channel. Log retention and backpressure policy are unchanged.

Two new regressions failed before the fix:

- A controlled first delivery blocks while a second publisher attempts delivery. The baseline allowed the second event to overtake the first.
- Four concurrent publishers, replay snapshots, and subscription churn exposed an unsorted snapshot on the baseline. Final checks require 1,000 unique events, ordered replay and live delivery, and exact replay after a cursor.

After the fix, all **6 event-bus tests plus 3 stream-revocation tests passed**. `git diff --check` passed.

```sh
dotnet test tests/LeviathanText.Api.Tests/LeviathanText.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:ArtifactsPath=/private/tmp/leviathan-streams-artifacts --filter 'FullyQualifiedName~JobEventBusTests|FullyQualifiedName~Open_job_event_stream'
```
