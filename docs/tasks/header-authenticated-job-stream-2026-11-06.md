# AUTH-SSE-01: header-authenticated job streams

Root authorizes source preparation after the journal baseline source closure, with independent review before integration. No models, training, providers or LM Studio calls. Preserve JOB-01 replay, cursor, explicit gap, slow-consumer termination, bounded client history and authoritative job refresh semantics.

## Transport owner

Own one small browser transport module plus focused `ApiClient.jobEventsUrl` / JobsPanel integration hunks. Stop placing the long-lived API key in job stream URLs. Use same-origin fetch with a captured `X-Api-Key` header and AbortController; no cookies, new token service or server authorization relaxation. Capture the key when the stream is opened and terminate on credential/global authority changes and destroy. Do not repurpose the private project workspace facade for the global jobs panel. Preserve server-side authorization and existing endpoint contract.

Implement bounded incremental UTF-8 SSE parsing across arbitrary byte/chunk boundaries, CRLF/LF/CR, blank dispatch lines, event/data/id/retry fields, comments and multiline data. Define conservative finite line/event/read bounds, reject overflow explicitly, and cancel/release the reader on every exit. Cursor is the last dispatched valid event ID, supplied as Last-Event-ID on reconnect, never an API key. Any supported retry must be bounded and cancellable, honor a finite validated server retry value, avoid a tight error loop, and avoid reconnect after authorization denial, terminal event, explicit capacity refusal or close. No automatic duplicate job creation.

Keep a narrow injectable fetch/clock seam for generated tests. No browser credentials or private content in diagnostics. Event payload is advisory: terminal/gap handling still refreshes authoritative state. Errors must not leave background reads, retries or old-owner events active. Native EventSource semantics cannot be silently assumed for a custom transport; document exactly what is implemented.

## Independent reviewer/test owner

Write new adversarial parser/transport and actual browser/API checks. First capture the original jobEventsUrl putting a generated secret in a URL, and old JobsPanel's EventSource use, without real credentials. Test chunk splits including split UTF-8, CRLF splits, comments, multiline data, missing/empty/id-with-NUL, omitted event name, retry limits, BOM, incomplete EOF, line/event cap, invalid/denied responses, reconnect cursor and cancellation/destroy/credential changes. Verify the complete actual request URL contains no generated key and the expected header is present; stale events and delayed read failures must not reach the panel.

Preserve existing bounded JOB-01 tests, replay/resync behavior, server reauthorization/cancellation checks and full Angular suite. Register owned API/Chrome fixtures, caps, source/runtime/input hashes and exact cleanup before actual browser work. Existing query-credential compatibility on the server is a separate policy decision; removing browser URL credentials does not claim that all external callers or reverse-proxy logs are remediated.

## Root handoff

Record original failures, unchanged controls, candidate results, independent findings and cleanup in a new evaluation report. Root owns todo/completed/project-plan and operations guide. Freeze and review production source before current whole-suite/build/browser verification; retain all failed attempts. Do not close AUTH-SSE-01 merely from a URL helper unit test.


## Qualified healthy-idle and callback policy

Source/runtime review before browser acceptance found that the API emits no heartbeat for unchanged jobs. Preserve a healthy quiet body indefinitely, as native EventSource did; retain the45-second response-header deadline, one-second credential-scope polling, AbortController/read cancellation, independent cleanup deadlines, finite reconnect-on-error and parsing/byte bounds. Do not count inactivity as a transient network failure. Current401/403 headers fence shared authority before waiting for unread-body cancellation; old-owner responses cannot rotate the new owner's authority.

Consumer callbacks may throw any JavaScript value, including null/undefined. Preserve the first thrown value and reject the settled promise after bounded cleanup; do not use the value itself as a failure sentinel or turn callback faults into network retries. An accepted unread response body remains owned even if a status callback throws before a reader is obtained. These behaviors require independent before/after fault tests and a new frozen build before actual browser proof.
