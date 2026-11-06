<!-- Copyright (c) 2026 QuantForgeSoftware. All rights reserved. -->
# Job event bus reentrant ordering — 2026-11-05

## Reproduction and relevance

The per-job monitor prevented competing threads from overtaking a delivery, but
C# monitors are reentrant. If subscriber A published a nested event while
receiving event 1, subscriber B received event 2 before event 1. A consumer that
advanced its cursor to 2 could consequently discard 1. The generated regression
failed before the change with expected `[outer, nested]` and actual
`[nested, outer]`; see `/tmp/leviathan-event-reentrancy-before.log`.

The current production subscriber in `JobEventStream.Read` only writes into a
channel and does not publish recursively. No claim is made that this callback
currently triggers the reproduction. The public `JobEventBus.Subscribe` API
allows arbitrary callbacks, however, and the ordering guarantee should remain
valid when a callback publishes. This is a bounded correction to that contract,
not a replacement of the SSE/channel architecture.

## Change and explicit semantics

`JobEventBus` retains its per-job lock for allocation, replay and delivery. Each
publication appends to the replay log and queues a delivery containing the
subscriber snapshot at that publication time. One outer drain delivers those
snapshots in FIFO order. A nested publication records/enqueues its event and
returns before its callbacks run; the outermost publication drains every
admitted event before returning. Competing threads remain serialized as before.

A subscription removed during a callback still receives an already admitted
event whose snapshot contains it. New subscriptions receive only events whose
publication occurs after they are registered. Removing a subscriber after a
nested event was queued does not retroactively alter that queued snapshot.

Callback failures do not strand other snapshot recipients or nested events.
The drain records callback exceptions, completes admitted deliveries, then
reports one original exception with `ExceptionDispatchInfo` or multiple failures
with `AggregateException`. Failures are not silently swallowed. For nested
publications, callback failures necessarily reach the outer publishing caller,
since the nested call returns before delivery. A `finally` resets the drain flag;
subsequent publications remain usable after callback failures.

The replay log remains advisory and process-local. The change does not add
durability, retention limits, asynchronous scheduling, or support for callbacks
that block indefinitely. Callbacks still execute under their job's lock.

## Verification

The candidate command passed **16/16 tests**, with no compile warnings:

```sh
dotnet test tests/LeviathanText.Api.Tests/LeviathanText.Api.Tests.csproj \
  --artifacts-path .artifacts/job-event-reentrancy-after \
  --filter 'FullyQualifiedName~JobEventBusTests|FullyQualifiedName~JobEventStreamBoundaryTests|FullyQualifiedName~JobEventStreamFairnessTests'
```

The 12 bus tests include six new generated regressions for nested ordering,
return-before-delivery/replay consistency, subscription snapshot changes,
5,000 nested publications with a maximum callback depth of one, original
exception identity/stack preservation with remaining deliveries, and aggregated
failures followed by successful publication. Existing bus controls exercise
concurrent publishers, replay snapshots, subscription churn and per-job
isolation. Three SSE boundary tests preserve replay/live once-only delivery and
current-access checks; the fairness control preserves bounded observation of
persisted worker state. `git diff --check` passes.

Candidate log: `/tmp/leviathan-event-reentrancy-after.log`. No training, provider
calls, source-data changes or root SSE implementation changes were made.
