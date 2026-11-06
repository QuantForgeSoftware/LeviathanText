# Local-capture browser delivery gap

November 5, 2026. Confirmed against the frozen final reviewed package using one
owned localhost API, a fresh generated store and a byte-exact copy of an existing
generated 64×24 PNG. No browser, corpus, model, inference, training or external
provider was used. The Mac's locked browser remains a separate UI-01 limitation.

## Actual published HTTP evidence

Inputs and limits were registered before API startup in
`.artifacts/local-capture-browser-delivery-gap-2026-11-05/registration.json`:
package manifest/API files, copied/original generated fixture, inspected source
and harness hashes. The original PNG and package were never edited. Bootstrap
authentication used a generated dummy key only. The fresh store is owned evidence.

At 18:47:38 UTC, the published API on `http://127.0.0.1:64260` returned:

| Operation | Result |
| --- | --- |
| Create generated project | 201 |
| Import absolute-path copied PNG | 200; capture 64×24, zero lines |
| Get project | 200; `capture.imageLocation` is the unchanged absolute server filesystem path |
| Get that path relative to the HTTP origin |**404**, zero-byte body, no image content type |
| Get `/ui/` | 200 HTML; actual packaged UI serving is enabled |

The capture is `cap-cap0001-05636e16` on
`surf-surf0001-e80b3748`, project `col-79b580ab9265`.
The stored location points to this probe's copied generated PNG under `.artifacts/`.
The harness completed in 0.53 seconds and stopped only its owned API in `finally`;
exit 0 and `ownedApiAlive=false` are recorded. Every registered input hash remained
unchanged. Raw response bodies/headers/statuses/hashes, exact process ownership,
registration, harness and final results are retained in the artifact directory.
There were no failed/retried setup attempts.

This proves the HTTP delivery gap for the recorded local capture. It does **not**
claim an observed broken image in a real browser, a browser decode result, or an
Angular proposal/acceptance success. No screenshot was available or manufactured.

## Source trace and scope

- `InterchangeService.ImportImage` stores `Path.GetFullPath(imagePath)`
  (`src/LeviathanText.Application/ProjectService.cs:231`); TEI/legacy paths also
  retain server-local locations.
- GET `projects/{id}` serializes native project data unchanged
  (`src/LeviathanText.Api/Program.cs:488–492`). `ApiClient.loadProject` performs
  a direct HTTP GET (`web/src/app/api-client.ts:239`). No delivery mapper rewrites
  these locations.
- `SurfaceView` assigns `capture.imageLocation` directly to `imageLink`
  (`web/src/app/surface-view.ts:408–411`, selection switch 242).
  `GeometryEditor` binds that value to `<img src>` and uses natural dimensions
  (`web/src/app/geometry-editor.ts:40/288–291`).
- Static middleware exposes only the configured packaged web root under `/ui`.
  There is no full-capture ID delivery endpoint. The authorized
  `lines/{lineId}/crop` route resolves server-local bytes, but it serves a line
  excerpt, not an entire capture or a newly imported capture with no lines.

Consequently an absolute Unix filesystem location becomes an origin-root HTTP
path, not an authorized server-file request. The actual404 above confirms that
specific path with the published server. HTTP/IIIF references may behave
otherwise; no claim about all captures or all browser formats follows.

The existing geometry component tests supply an empty image URL and simulate
image dimensions. They validate component behavior, not local capture delivery.
Line excerpts already have a separate crop route; the full-surface gap does not
mean every source excerpt fails.

## Required next work

The [two-instance brief](../tasks/local-capture-browser-delivery-2026-11-05.md)
defines authorized capture-ID delivery, explicit display-frame metadata and client
blob/URL lifetime handling. It rejects filesystem-root exposure/static aliases.
TIFF/browser compatibility, EXIF orientation and bounded derivatives must be
resolved against native geometry before any implementation claims preview parity.
Original bytes, native capture identity/geometry and source frames remain intact.
The final package is preserved as evidence; this report makes no runtime fix.
