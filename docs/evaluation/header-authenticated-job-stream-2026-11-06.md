# Header-authenticated job streams

The browser jobs panel now opens same-origin fetch streams with a captured `X-Api-Key` header. Job stream URLs contain only the encoded job ID; redirects, cookies and credential query parameters are not used. This changes the browser transport, not server authorization or the server's separate legacy query-credential compatibility policy. Other legacy URL-helper callers are unchanged.

The panel retains its bounded200-event/100-job presentation, authoritative snapshot refresh on gaps and terminal events, selected-job refresh outside the recent page, late-response guards and no automatic job creation/retry. A generic synchronous authority-boundary subscription closes streams and conceals prior jobs before credential/cache mutation; it grants no project admission. Current401/403 stream response headers invalidate shared cached authority immediately, before waiting for body cleanup. Delayed old-owner refusals cannot invalidate the new authority. A missing job404 remains local to the job panel.

## Explicit protocol and resource policy

The parser handles incremental UTF-8, split CRLF/LF/CR, one leading BOM, comments, literal field names, multiline/empty data, inherited/empty/NUL IDs and incomplete EOF. These framing rules follow the [WHATWG event-stream interpretation](https://html.spec.whatwg.org/multipage/server-sent-events.html#event-stream-interpretation). It intentionally refuses invalid UTF-8 rather than replacement-decoding, and imposes application limits; this is not a drop-in native EventSource implementation.

| Boundary | Current policy |
|---|---|
|Single read chunk|1MiB maximum|
|Line|64Ki UTF-16 units|
|Retained event fields/data|256Ki UTF-16 units|
|Connection input|32MiB maximum|
|Response headers|45-second admission timeout|
|Healthy quiet response body|Remains open; no inactivity timeout|
|Live credential scope|Checked before dispatch/read/event and polled each second; normal boundary notifications close synchronously|
|Reader/body cleanup|Cancellation initiated, independent1-second cleanup deadline, reader lock released; cleanup deadline survives close|
|Reconnect|At most three reconnects per watch; initial1s exponential delay unless validated server retry; clamp250ms–30s|
|Replay header|Last dispatched valid nonnegative Int64 event ID, max128 digits; empty ID clears; no credential in cursor|
|Permanent stop|Close, terminal, capacity,401/403/404/other permanent status, malformed framing/MIME or overflow; no reconnect|

A fetch/clock seam supports generated tests. The header fetch timeout is distinct from response-body lifetime. Body reads remain abortable even when quiet; a late response from a nonconforming injected fetch is cancelled after close. Retry, scope polling and cleanup timers are separately owned. Consumer callback errors surface through the settled promise after cleanup; operational failures expose fixed status messages, not credentials/private payloads.

The healthy-idle policy was corrected before acceptance. The current server emits only changed snapshots/events. The versioned [.NET10.0.9 result implementation](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.9/src/Http/Http.Results/src/ServerSentEventsResult.cs) and [formatter](https://raw.githubusercontent.com/dotnet/runtime/v10.0.9/src/libraries/System.Net.ServerSentEvents/src/System/Net/ServerSentEvents/SseFormatter.cs) do not add a periodic heartbeat. Removing only the client body inactivity timeout preserves long quiet watches without adding backend traffic. The10.0.9 primary-source tags were checked to match the installed runtime version; this is source review, not binary/source attestation. Header admission, cancellation, parsing/byte bounds and finite error retry remain.

## Actual before evidence and fixes

Independent original-code tests failed2/2: the real URL helper appended a generated long-lived credential, and the actual panel constructed native EventSource with that URL. No real secret or provider participated. Original source and executed tests are retained in `.artifacts/experiments/job-stream-url-boundary-v1/`.

A later independent deferred-cancel test failed1 of33: closing during cleanup cleared its only timeout and left the reader locked/settled promise pending. Cleanup timers now have separate ownership. The original32 controls passed in that before run. Evidence is preserved in `header-job-stream-independent-v1/cleanup-before.log` and its source freeze.

The healthy-idle/current-denial policy tests next failed3 of41: a quiet46-second body retried, and current401/403 notices waited for a deferred body cancellation. The stale-old403 control passed. The final module leaves healthy bodies open and signals current authority refusal on headers first. Independent fixed controls passed42/42. All earlier failures, test-fixture readiness adjustments and compile-only attempts remain retained; none is relabeled a missing-method product failure.

Panel integration separately captured a qualified shared-denial before run7pass/1fail: another valid active workspace remained active after current403. The new panel clears shared authority only for an admitted current401/403 notice; its old-owner control stays passing. The first attempt had an invalid generated principal missing its required name and is retained as a fixture error. Existing12 job presentation/order controls preserve their original assertions through an explicit TEST-only transport scheduler, alongside eight new global/header boundary cases.

## Engineering and browser gates

The first whole frontend gate passed600/600, with production build506.67kB initial output and the unchanged500kB warning budget exceeded by6.67kB. That is historical pre-idle-policy evidence; its immutable production copy is `header-job-stream-integration-v1/dist-v1/`. No budget increase was made. The newer policy gate and actual browser/API evidence are recorded below once complete.

The public-interface tests use generated byte streams, injected fetch/clock and actual HttpClient/panel boundaries. They are not a replacement for actual browser/server header, cursor, resync, cancellation and permission evidence. Independent browser proof owns a newly registered job01 harness copy; old native-EventSource browser results are historical, not relabeled as fetch transport coverage. No model, fitting, provider, real corpus or Greek accuracy study is part of this work.

### Final callback/resource correction and accepted current gate

Independent callback controls exposed four additional failures before the final fix: thrown `null` in event/open-state callbacks was mistaken for the sentinel meaning no callback failure; a closed-state observer could replace the original failure; and a throwing authorization observer skipped unread-body cancellation. Undefined-value controls already passed. The retained before result is44pass/4fail in `header-job-stream-independent-v1/callback-and-denied-body-before.log`.

The final implementation records a separate failure flag and retains the exact FIRST thrown value, owns an unread response body before invoking any current/status callback, transfers ownership only after acquiring a reader, and performs unconditional bounded body/reader cleanup. Operational cleanup/closed-notice faults cannot replace the first consumer value. Independent final controls passed48/48, including null/undefined identity, denied-body cancellation and all earlier framing/retry/quiet/authority cases.

The final whole frontend suite passed **624/624** on actual Chrome Headless147. Production build passed with **506,679 initial bytes**, exceeding the unchanged500,000-byte warning budget by6,679bytes. The warning remains; no budget increase or code-loading change is part of this feature. Actual production bytes and source hashes are frozen at `.artifacts/experiments/header-job-stream-integration-v1/dist-v3-final/` and `source-dist-freeze-v3-final.json` (SHA256 `bf506cccbdc772c770569a1c04e44ae9e8dd9d8f669e23c4dd24fd0b9cd76c58`). Earlier600/618 gates and v1/v2 production copies are historical.

### Actual browser/API result

The independently registered browser/API run passed **29 functional checks and1,118 source/runtime/input closure checks**, using this exact final production copy and generated Reader accounts/jobs. It observed real pinned HTTP headers with no credential URL, numeric replay cursor/empty-ID resync, server restart/reconnect, authoritative terminal status winning over a delayed pending snapshot, capacity refusal, key rotation and permission revocation. It recorded zero external-egress attempts, zero runtime errors and no surviving owned processes; the dedicated temporary fixture/profile was removed. This14.7-second functional campaign is not a performance benchmark.

Evidence: `.artifacts/experiments/header-job-stream-browser-v1/results-v1/proof.json` and `final-header-stream-boundaries.png`. The first deliberate server restart used the registered SIGKILL fallback after its2.5-second SIGTERM grace; Chrome and the final host exited zero. Thus the restart evidence includes a forced interruption and does not claim every shutdown was graceful. No real credential, corpus, model, fitting or provider call participated.

The initial bundle warning remains a separately tracked build-size issue. An isolated idle-panel proposal is independent of this security transport and has not been adopted. Removing job URL credentials also does not remove the backend's external legacy query-auth compatibility or remediate every other legacy helper/caller.
