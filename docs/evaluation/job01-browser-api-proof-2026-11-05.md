# JOB-01 actual browser/API proof — 2026-11-05

The generated end-to-end visit passes **all16 recorded required assertions** against the real Angular `/ui/` and current API over Kestrel. The fixed optional300-event visit also produces an actual `slow_consumer` resynchronization marker with the unchanged production live-queue defaults. No product source was changed to obtain this proof.

Accepted evidence: `.artifacts/experiments/job01-browser-v1/attempt-4/`. Its sealed registration is `registration-attempt-4.json`, SHA256 `c9b5862e2f827d88c68c2e2e5252dabbb7213651c7d15c3be4e1e935a28715aa`. The proof SHA256 is `f45fec5ac8328a6b8d4d03645e4cd1c2dd53bb5fc215338844ff8e6a68f01710`. `final-evidence-manifest.json` inventories all attempts, registrations, immutable drivers and screenshots; SHA256 `3de9626cc623cc92d1bc9f85f7a8bfa9b28ac0752e8e645bf47bb39ca15dcf1c`.

## Actual scope

The host references the actual current `Program` and uses .NET10 `WebApplicationFactory.UseKestrel`. It serves the normal configured `/ui/` path and fresh Angular assets built with Node22.22.2. It uses a generated SQLite store, two real Reader accounts and disjoint project memberships, an empty model directory and no worker. Existing browser profiles and the real project store are not used.

Isolated host wiring configures only a small advisory bus (2 streams,4 retained events,2 per stream and fixed bounded characters/subscribers) plus loopback/token-protected fixture controls. Production endpoint, authorization, stream and live-queue implementations are unchanged. The queue is the real default256 references/65536 UTF-16 characters. The proxy forwards actual HTTP/SSE data and only controls network delay/disconnection; no job/snapshot/event is synthesized. A thin subclass observes native EventSource creation/close; real Chromium/CDP records events and cursor behavior.

Fixture controls write only the owned generated store. This is not evidence that production Readers can mutate jobs. No fitting, model execution, corpus, Greek reading decision, LM Studio or real provider request is involved. Page requests to other origins are blocked; no such requests occurred.

## Required observations

| Boundary | Actual browser/API result |
| --- | --- |
| Default UI and Reader membership | `/ui/` loads; identity is Reader A; only its project jobs appear. |
| Evicted advisory history | The bus reports history eviction while SQLite changes Pending→Running. A real Watch action receives `job.resync-required` with empty ID, fetches the selected actual job resource and shows Running. |
| Native cursor | A real ID-bearing publication is followed by disconnection/eviction. Native EventSource automatically supplies Last-Event-ID on reconnect. The real empty-ID message clears it on the following reconnect; host telemetry records the empty header. |
| Actual process restart | Only the owned API process is stopped/restarted with the same SQLite store. Native reconnect carries an old process cursor; the new bus resynchronizes and the row reflects the deliberately changed persisted state. |
| Delayed Pending snapshot | The proxy captures an actual200/Pending JSON response and holds its exact bytes. SQLite becomes Succeeded; a real terminal event intentionally carries a nonauthoritative Pending advisory payload. The UI refreshes to Succeeded and cancels the older request. Releasing it cannot restore Pending. The source body hash is retained. |
| Credential change | Entering Reader B's key through the actual AuthPanel closes every native EventSource, clears old rows/events and shows only B's job. The backend subscription is disposed; a later A-job event cannot repopulate the view. B's actual HTTP request for A's job returns403. |
| General integrity | No unhandled browser exception or external page request. Source/runtime files and inventories remain identical to the sealed inputs. |

The pending-response observation verifies real cancellation/authoritative refresh, not a fake observable forced to deliver after cancellation. Earlier client tests remain separate evidence for their callback-generation paths. Likewise, the existing backend70 cases (17 actual HTTP cases) and client12 cases are references, not relabeled as these browser assertions.

## Fixed optional queue observation

The actual proxy pauses SSE reads for500ms while the host publishes exactly300 progress events with2048-character markers. An actual `slow_consumer` marker is observed. The live queue's implementation and256/65536 defaults are unchanged; there was no larger adaptive burst or constructor seam. This is one bounded functional observation, not a universal slow-client/RSS/latency guarantee.

## Preserved failed attempts

1. **Attempt1:** readiness observed five SQLite jobs just before the seed file was written. No browser launched. The correction waited for this owned host's explicit READY marker and seed file.
2. **Attempt2:** five checks passed, then restart timed out because the proxy did not propagate an aborted upstream SSE response to its downstream socket. The browser remained attached to the proxy's orphaned connection. The next driver forwarded abort/error/incomplete-close without inventing a response.
3. **Attempt3:** ten checks and the optional actual slow-consumer observation passed. Credential UI switched correctly, but backend disposal timed out because downstream close was not propagated to the proxy's upstream request. The final driver adds that cancellation and records native closure before backend cleanup.
4. **Attempt4:** all16 required assertions and the fixed optional observation pass. Prior driver/registration/result files remain unchanged. These plumbing failures are not claimed as product bugs.

The initial isolated Angular build exited134 after its build banner without a diagnostic stack; its log/outcome remain. The same source passed in a fresh output directory with native subprocess permissions; no cause is inferred. A provisional host build predating root's fallback-dimension source change also remains historical. The accepted host was rebuilt from the new source freeze with zero build warnings/errors before sealing.

## Cleanup and evidence limits

Both owned API PIDs and the owned fresh-profile Chromium PID exited. The first API stop is the deliberate restart and required SIGKILL after the bounded grace period; this is recorded, not called a graceful shutdown. Final host and browser cleanup completed. The repository default-store existence/hash is unchanged (absent before and after). Generated fixture store copies, logs and screenshots remain outside the auto-cleaned temporary root. No broad process kill was used.

The final registered wrapper exits0 with zero before/after source/runtime hash or source-inventory mismatches. It includes compiled source/config, host/API dependencies, actual Angular files and exact tool binaries. This is an isolated configured current-source proof, not verification of a new published release package or a performance study.

Screenshots in the accepted attempt: `evicted-resync.png` (SHA256 `de1c678d8a89fc7a3d5b56d56ee19233cc8e2d64bd51628ecdc0274d61235ba3`), `terminal-wins.png` (`159202ed9c17f047121e52e887b4e5869a655dcb38796a0548406ed4212751f1`), and `credential-switch.png` (`48883a49df01e27627013eab46609ca182868ecf0c6eb8939167899fb63fa5d7`).

## Separate workspace credential issue

Root's independent screenshot review found that ProjectsPage still displays the old collection name after the JobsPanel credential transition. Source review additionally identified unscoped cache fallback on401/403 and late old-credential cache writes. These are separate workspace/auth defects, described in [the source-only credential review](workspace-credential-review-2026-11-05.md); the sixteen JOB checks establish JobsPanel disposal and authorization boundaries only. They do not establish whole-workspace reauthorization, draft ownership or cache isolation.
