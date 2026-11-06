# Project job revocation and event-stream reauthorization

Date: 2026-11-05. Scope: membership-enforced jobs and streams, using disposable stores and generated layout images. No model fitting, benchmark model loading, specialist decisions, or live stores are involved.

## Behavior

- New non-admin project and surface jobs admitted with project membership enabled persist `BoundProjectId`. This captures the exact project and the admission requirement. Existing bound surface jobs use the same guard.
- Before standalone-worker dispatch, a bound job requires an existing project and an enabled owner whose current role permits mutations. A current admin may execute without membership; other permitted roles must still belong to the project. Rejected work becomes terminal `Cancelled`, carries a cancellation reason, releases the claim, and consumes no retry budget.
- Project layout and recognition recheck authorization between surfaces. Revocation stops before the next surface. Work already committed on previous surfaces remains intact. A surface already executing may finish; this change does not interrupt native inference or roll back committed writes.
- With membership enabled, open job event streams recheck the live account and project access before each replay/live event and on an idle interval of one second. Revocation ends the stream and disposes the subscription. An event already delivered cannot be recalled; authorization and network delivery do not form one atomic transaction.
- Local mode, bootstrap admin, membership opt-out, and unbound legacy/admin/global jobs retain their admission behavior. Historical unbound project jobs cannot distinguish membership admission from local/admin/opt-out admission; retroactive migration and ownership policy remain unresolved. This is not a claim that every historical or in-flight operation is revocable.

## Regression evidence

- `ProjectJobAuthorizationTests`: current member, revoked member, disabled owner, reader owner, current admin without membership, and unbound compatibility; a standalone worker process also rejects a revoked project job.
- `LayoutProjectHandlerTests`: generated two-surface work revokes membership during the first surface, retains its completed layout, leaves the next surface untouched, and terminates without retry. The existing layout detection test remains green.
- `ApiMembershipTests.Open_job_event_stream_reauthorizes_during_idle_and_stops_before_leaking_events`: idle revocation, a new event immediately after revocation, and membership opt-out compatibility.

Result: **9 worker tests and 3 event-stream tests passed** in the focused commands below. `git diff --check` passed.

Focused commands (serial builds, disposable isolated outputs):

```sh
dotnet test tests/LeviathanText.Worker.Tests/LeviathanText.Worker.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:ArtifactsPath=/private/tmp/leviathan-streams-artifacts --filter 'FullyQualifiedName~ProjectJobAuthorizationTests|FullyQualifiedName~LayoutProjectHandlerTests'
dotnet test tests/LeviathanText.Api.Tests/LeviathanText.Api.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:ArtifactsPath=/private/tmp/leviathan-streams-artifacts --filter FullyQualifiedName~Open_job_event_stream
```

The isolated full membership test class also contains fixture-path assumptions based on its usual build output location. Its PAGE import cases require normal repository build outputs; their isolated-output HTTP 400 results do not test the authorization change. Full normal-output verification is tracked separately by the coordinating session.
