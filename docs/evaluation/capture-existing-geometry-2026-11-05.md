# GEO-02: capture-bound existing geometry edits

Implemented November 5, 2026, against the authorized [task contract](../tasks/capture-existing-geometry-2026-11-05.md). This is generated engineering evidence, not Greek manuscript acceptance, natural-text accuracy, fitting or a resource benchmark. Root independent review and the integrated engineering gate remain separate requirements.

## Result and boundary

The editor now saves existing-line moves, resizes and unplaced-line geometry through `PUT /api/v1/projects/{project}/surfaces/{surface}/captures/{capture}/lines/{line}/geometry`. Its quoted display `If-Match` binds the request to the observed source derivative; required expected polygon/baseline protects against stale geometry. The existing capture mutation boundary supplies current authentication/membership/role checks, buffered rendering, source identity recheck, cancellation, admission release and optimistic persistence. No original image is rewritten.

The application method additionally requires exactly one target line across the passed project, the selected surface and matching raw frame, exactly one corresponding region in that surface with the same frame, finite geometry and raw-source bounds. A conflicting legacy region/line registration is refused with a repair-needed error rather than guessed. A successful edit preserves transcription, review, annotations, capture metadata and unrelated native fields and adds separate observed-display audit evidence. An unavailable historical checksum remains unavailable. The legacy line-only API/service signatures remain available and **do not acquire these capture/source-binding guarantees**.

The working client snapshot includes project, surface, capture, frame, raw dimensions and derivative. A same-identity blob/authentication refresh retains drafts. A changed source with pending work retains the old snapshot and displayed dimensions, withholds the replacement photo/overlay and blocks navigation and edits. It never silently rebinds the draft to the new pixels. Explicit discard after a source change or an unconfirmed write uses the authoritative `loadProjectFresh` path; failed or superseded reads retain work. A successful read precedes clearing and adoption. An unknown write outcome is retained per line, including a local reversal to the old coordinates; a later successful acknowledgement updates only its submitted baseline and leaves newer local edits dirty.

An image/authentication refresh closes active pointer gestures. A partial move becomes retained draft/undo state. A usable partially drawn box becomes an **unsent** draft in its original frame; no request is issued while blocked. Pointer anchors are cleared so later hover cannot continue the old drag. Existing dirty lines can be resolved while an unsent new box is retained, followed by an explicit box retry.

## Generated regression evidence

Evidence lives under `.artifacts/experiments/geo02-capture-edits-v1/` (local, not a shipped dataset). `source-freeze-v1.json` binds ten source/test files, including the independently owned capture helper's frame-kind guard; `candidate-v1/` retains their exact contents.

| Check | Result | Receipt |
| --- | --- | --- |
| New HTTP route before implementation | 2 expected failures, both missing-route 404 | `before-api/before.trx` |
| Initial source/draft client contract before implementation | 4 failures | `before-frontend.log` |
| Active gesture refresh before correction | 2 failures, 10 controls pass | `gesture-failbefore.log` |
| New application update/invalid-input/no-op tests | 19 pass | `application/application.trx` |
| Expanded generated scoped HTTP checks | 22 pass | `expanded-api/expanded.trx` |
| Final display/application geometry groups, including current helper | 53 pass | `app-final-2/app-final.trx` |
| Final capture HTTP group, including existing routes and new edits | 65 pass | `capture-all/capture-all.trx` |
| Selected existing/new geometry browser unit tests | 48 pass | `gesture-and-geometry-after.log` |
| Full frontend suite under Node v22.22.2 / Chrome Headless 156 | 198 pass | `frontend-full.log` |
| Production Angular build | success, no Angular warnings | `frontend-build.log` |

The application fixtures verify complete unrelated-field preservation and that invalid scope, identity, region, duplicate IDs, bounds, expected geometry and nonfinite inputs never reach a store write. The HTTP fixtures cover known and unavailable source checksums, source replacement, stale/missing tokens, cancellation, post-render membership/account/key/read-only changes, concurrent geometry rejection and preservation of unrelated concurrent project changes. All image inputs are generated PNG/JPEG. The frontend cases use fake observables and include no-op uncertainty, newer local edits after acknowledgements, independent per-line confirmation, failed fresh reads, stale auth observation, gesture interruption and explicit source adoption.

An initial `app-final.log` attempt produced no test receipt and is not counted; the fresh-output `app-final-2` invocation above is the actual final result. Isolated Release builds had NU1900 warnings because vulnerability metadata could not be fetched offline; they had zero compilation errors. This is not a fresh dependency-audit result or a claim that the separate Imaging security gate is closed.

## Actual browser/API proof

`browser-v1/registration-attempt-3.json` records the runner, generated seed, copied production `/ui/` assets, isolated Release API/seed runtime and frozen geometry source hashes before launch. The real installed headless Chrome used a fresh private profile, a fresh native SQLite store, generated dummy credentials and membership, generated PNG captures, the actual Angular application and actual API static `/ui/` hosting. The loopback proxy forwarded UI/API paths without rewriting their contents. Its provider endpoint rejected/counts any attempt; the observed provider count was zero. No models, real corpus or labels were loaded.

`browser-v1/attempt-3/proof.json` has **14 passing checks**, SHA-256 `1df11b3678defd0cb4a0996cc43919c493956409dc2ccf18c51b6b0d5a342ca0`:

- A real pointer moved an existing polygon from approximately `(80,80)` to `(110,110)` in raw coordinates and saved through the new capture-scoped PUT; a browser reload reconstructed the persisted polygon.
- Existing source transcription and capture metadata remained unchanged.
- A second unsaved move was followed by replacing only the owned generated PNG, from raw `3001×2000` to `3501×2100`. The legacy generated capture deliberately had unspecified recorded dimensions and unavailable checksum. The save returned actual HTTP 412; the exact visible draft and complete serialized native-project response were unchanged on refusal.
- Retrying the image obtained the changed source identity, but the editor retained the old `3001×2000` working dimensions, hid photo/overlay and blocked navigation. Only an explicit discard and fresh project read adopted `3501×2100` and restored the previously confirmed geometry. Native fields and historical unavailable checksum remained unchanged.
- JavaScript assets loaded successfully from the actual default `/ui/` prefix; no browser runtime exception occurred.

Screenshots `confirmed-move.png`, `source-refusal-retained-draft.png`, `new-source-locked-old-draft.png` and `discard-adopted-new-source.png` retain the rendered states. The locked-source screenshot was visually inspected. Native before/after snapshots are retained alongside the proof. Browser/native numeric coordinate comparisons use 0.01 source-pixel tolerance because SVGPoint exposes float32 values while native JSON stores doubles; native-state refusal comparisons remain exact serialized-response comparisons, **not SQLite-file byte comparisons**.

Two earlier attempts are retained. Attempt 1 prepared scrolling in the wrong order, so the pointer hit a section rather than the polygon. Attempt 2 successfully moved and saved (HTTP 200), but its exact SVGPoint/native-double comparison timed out. Only the harness was corrected between these attempts; production source stayed frozen. All owned processes exited. Final API PID 22354 and Chrome PID 22367 exited 0; the disposable fixture/profile tree was removed in `finally`. No user browser session was touched.

## Limits and handoff

This does not repair inconsistent historical frame/region registrations, provide durable geometry-draft recovery across reload/crash or remove the legacy API. The application normally preserves same-scope drafts, but an arbitrary embedding host forcing a project/surface replacement remains a lifecycle reset; the actual parent blocks user navigation while work/pending requests exist. Other pre-existing write types, including order-write uncertainty, are not newly guaranteed by this per-line geometry change. TIFF, remote-image and preview-resource policies remain those of the capture display service; no new decoder support is implied. The actual browser proof uses generated ASCII source text and is not a Greek scholarly review.

All ten frozen source hashes were rechecked unchanged after the browser proof. The optional JOB-01 real-client eviction proof was not run in this session. CPU-heavy work paused after this proof for the independently registered decoder/archive measurement window. Root should independently review the scoped application/controller/editor changes and run its current no-fitting engineering gate before integrating completion status.

## Central integration result

Root completed `.artifacts/overnight-2026-11-05/engineering-v3/`: 1935 passing cases, 18 intentional skips, zero failures; compilation reports zero warnings/errors. The first central attempt (`engineering-v2`) retained a Roslyn `csc` exit139. The unchanged source passed with other owned builds paused; the crash cause is not established. Existing dependency-advisory status is separate from compilation. No fitting capability was enabled. GEO-02 is archived after independent source review and its frozen owner frontend/browser proofs.
