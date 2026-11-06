# Authenticated export: actual browser and API proof

The approved fixed proof passed **16/16 checks** on 2026-11-06 at 04:19 UTC against the actual default `/ui` Angular build and actual Kestrel API. Native Chrome downloaded the adjudication CSV and project bundle through the rendered buttons. This is generated-fixture functional evidence, not a performance measurement, corpus-quality result, or proof of whole-workspace privacy.

The source scaffold and all attempts remain under `.artifacts/experiments/authenticated-export-browser-v1/`. Final registration `execution-registration-v3.json` SHA-256 is `8a08f4ae3195150397aea8550ab54f576e7f1fb6003aa31473dad1da87b244e3`. It pins the compiled host/runtime, current source graph, complete built UI, driver, exact Node22 executable and cached Chrome executable before launch. `attempt-3/source-runtime-closure.json` has no before/after mismatches and driver exit0. The isolated host build had zero warnings and errors.

| Actual action | Observed result |
| --- | --- |
| Click rendered “Download adjudication CSV” | Native download `col-export-browser-a-adjudication.csv`, 295 bytes, SHA-256 `93e54e2ff51209ca65c3e9d6ecda46802a456b8787e5d5d92731671063567cad`. Contains the explicitly generated line text. |
| Click rendered “Download bundle (.zip)” | Native download `col-export-browser-a.zip`, 10,083 bytes, SHA-256 `4f0fe710462229531245b64e04e3679f1a5f4b3eeee1fe204812cbca2dc915b0`. Its native JSON identifies the generated collection. |
| Revoke ReaderA membership, click both buttons | Both actual API responses403; no additional objectURL, anchor invocation or native download. |
| Hold a fresh actual ReaderA bundle200; save ReaderB key in actual AuthPanel; wait for ReaderB identity; release old200 | The browser connection was still open at release (`closedBeforeRelease=false`); the delayed response produced no new objectURL or native download. |
| Inspect all five actual export HTTP requests | Each used `X-Api-Key` for ReaderA, no credential query parameter, and no ReaderB credential substituted into the admitted request. |
| Compare persisted source project before/after | Exact native JSON hash and store version unchanged. |

Each downloaded file was compared with the **same actual API response** buffered and forwarded by the loopback proxy. A separate fresh bundle response was not used as its oracle: ZIP timestamps can vary, and the held bundle did in fact have a different response hash. CDP `Browser.downloadWillBegin`/`downloadProgress` records, actual files, and captured response bytes are preserved. Final instrumentation records exactly two objectURLs, two anchor invocations and two completed native downloads. ObjectURL/anchor/XHR wrappers only observe and call through to native behavior.

The generated fixture contains one line and two clearly labeled synthetic review decisions, used to make the adjudication CSV nonempty. These are not scholarly adjudications or accepted corpus labels. Source image location is empty; image preview refusal is expected and no actual image, model, external provider or fitting workflow was invoked. Bundle export creates its normal derivative/export entries, including `training-export.json`; this is serialization, not training. The two fixture Reader accounts have disjoint project memberships. Loopback-only fixture control routes use a generated control token and change only fixture membership.

Two harness-only attempts are preserved without being presented as product failures:

1. Attempt1 refused before host/browser launch because Python component-wise path sorting differed from JavaScript full-path lexical sorting in the closure inventory. All439 runtime files were identical; the new versioned seal helper uses matching string ordering.
2. Attempt2's host failed before Chrome launch because WebApplicationFactory could not discover the solution root from the isolated `/private/tmp` output. Attempt3 copied the exact same compiled runtime under the owned repository artifact directory, then sealed the new paths. Host/driver/product behavior was unchanged.

Attempt3 used a240-second overall watchdog, five-second control deadlines and a4MiB cap on the tiny generated export-response buffers. These are harness bounds, not new production response-size limits. Both owned API and Chrome processes exited0; their private profile/root was removed after preserving fixture DB, response bytes, downloads and logs. The repository default store's checksum/existence was unchanged. No external browser requests or unhandled browser exceptions occurred. `final-evidence-manifest.json` hashes the final receipts, screenshots and downloaded files.

The screenshot `attempt-3/authorized-downloads.png` visibly shows the generated diplomatic line and successful bundle status. The final credential-switch screenshot is retained separately. This proof deliberately does **not** establish that already-loaded projects/drafts are hidden on credential changes, that ownership can be inferred for legacy drafts, or that every export/cache response has a complete no-store policy. Those remain independent authorization/workspace/cache concerns. It also does not establish cross-platform browser behavior or arbitrary-image bundle acceptance.
