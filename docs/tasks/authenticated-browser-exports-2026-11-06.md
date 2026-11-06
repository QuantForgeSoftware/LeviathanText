# Authenticated browser export correction

Source audit confirms two bare protected download links: ProjectsPage adjudication CSV and SurfaceView project bundle. Ordinary anchor navigation cannot run Angular's auth interceptor or obtain the API key stored in localStorage; the API middleware reads only Authorization, X-Api-Key or explicit api_key query credential. Shared authenticated deployments therefore cannot use those links as intended. This is source-derived; no HTTP/browser proof has run during the long quiet window.

Root owns ProjectsPage adjudication and ApiClient signatures. R owns only SurfaceView export handlers and new generated test sources. Preserve current originals before editing. Replace the bundle link with an authenticated HttpClient Blob download button. Root adds projectBundle and optional captured credential on tei/editionHtml/wordBoundaryCsv/searchablePdf/trainingExport. No key is added to export URLs. Existing explicit query-key EventSource/line-crop policy and blob/external preview links remain unchanged.

All six SurfaceView export handlers capture project, surface, key, local generation, shared authority revision and destruction state, snapshot filenames and discard stale success/error/download side effects. Reuse the existing destroyed guard. A minimal ngOnChanges increments only the export-scope epoch for project/surface inputs, fencing A→B→A without changing preview, geometry, draft, presence or session behavior. Actual Angular input lifecycle is exercised in controller tests. This does not establish whole-workspace ownership.

Generated controller tests use controlled responses without mounting children or reading a corpus. Check six current success paths and captured arguments/filenames, stale project/surface/credential/shared-ticket/destroy replies, no post-destroy dispatch, current versus stale errors, and blob URL cleanup. Stub advisory presence cleanup only; do not replace the export guard or auth/cache implementation. Root's composed ApiClient tests separately assert actual captured headers and legacy defaults.

Before/after proof must restore only the preserved SurfaceView while retaining candidate optional ApiClient signatures, whose omitted arguments preserve old behavior. Label old bundle handler absence as a missing authenticated UI path rather than a type-error proof. No build, browser, HTTP, decoder, provider, model or fitting execution is allowed until quiet releases. Later focused/full frontend and actual authenticated generated HTTP/browser controls remain integration gates.


R's source-only candidate is now frozen in `.artifacts/experiments/surface-export-auth-v1/candidate-source-freeze.json`. It includes55 controller cases across six exports and21 composed real ApiClient/interceptor cases across seven export routes. Controller setup preserves actual ngOnChanges/OnDestroy, stubs unrelated ngOnInit/presence cleanup and renders no child editors. They have not been compiled or executed. Earlier36 composed tests are unchanged.

## Browser navigation inventory

| Original UI path | Authentication mechanism | Finding/status |
|---|---|---|
| ProjectsPage adjudication CSV anchor | Bare protected API URL | Cannot obtain localStorage API key via Angular interceptor; root owns authenticated HTTP replacement. |
| SurfaceView bundle anchor | Bare protected API URL | Same source defect; R's guarded Blob button replaces it. |
| Other five SurfaceView export buttons | ApiClient HTTP | Already use interceptor; now require exact captured request credential and stale-download fences. |
| Project PDF/HTML buttons | ApiClient HTTP | Root's current intent correction owns these. |
| DualPane/LineEditor capture-preview links | Loaded Blob URL or explicit external preview | Not a bare protected export URL; do not attach API credentials to external URLs. |
| Line crop image | Explicit legacy `eventsUrlWithKey` query policy | Preserved; no global claim that credentials are absent from all existing URLs. |
| Jobs EventSource | Explicit existing query-token policy | Preserved; not an export download. |

The strengthened v2 source-only freeze keeps the initial freeze unchanged. Current-success controller tests compare actual Blob MIME and literal UTF-8 text/pretty JSON or exact binary PDF/ZIP fixture bytes, including NUL/high bytes. No test has run and no valid PDF/ZIP parser capability is claimed from these opaque download payloads. Rendered-button and actual authenticated browser/download acceptance remain later gates.
