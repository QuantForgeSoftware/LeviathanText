# Authorized capture previews: Angular client evidence

November 5, 2026. Client implementation and independent generated browser checks
for UI-01. This report supplements the [original published404 reproduction](local-capture-browser-delivery-gap-2026-11-05.md)
and [two-instance brief](../tasks/local-capture-browser-delivery-2026-11-05.md).
No recognition model, real provider, LM Studio, training or corpus label was used
in these client checks. The earlier development-period fitting incident is a
separate disclosed record; this is not a blanket training-free session claim.

## Result and contract

Local capture previews now use the authorized capture-ID descriptor and PNG routes,
not a filesystem `imageLocation` as an HTTP URL. `ApiClient` retrieves metadata
without offline fallback, then obtains a Blob with the normal API-key header and
`If-Match: "<derivativeId>"`. The descriptor has no arbitrary image URL to trust.
The client validates route identity, raw orientation, positive dimensions, digest
shape and both independent scale ratios before requesting/displaying the raster.
A changed descriptor/image identity receives412 and leaves the old preview absent.
No static filesystem alias or image proxy was added by this client work.

Only an owned Blob URL reaches the new capture-preview image/link. Its subscription
is cancelled and URL revoked on capture selection, API-key change, storage event,
failure or component destruction. Old replies cannot replace a new selection.
Previous photos/overlays are cleared while a new request is pending or denied.
Same-frame image/auth retries preserve geometry drafts. A user can explicitly
discard geometry/order drafts while a preview is unavailable, provided no write is
still pending. Existing L1 draft/recovery and pending-write behavior is retained.

The recorded-checksum status remains distinct from the newly observed image hash.
An ordinary unknown-checksum capture can display and support compatible raw-frame
geometry; the UI says the original checksum was not recorded. This does not verify
historic annotation/image correspondence or write a checksum back into native data.

Existing remote HTTP(S) images retain direct viewing after a fresh authorized
project read, never through a credential-bearing HttpClient image request to the
remote host. Their unverified frame has no overlays or editing. Remote proxying and
TIFF preview are not implemented; unsupported local input has an actionable refusal.
The remote authorization read deliberately does not use the offline project cache.

## Coordinates, editing and lifecycle boundaries

The displayed PNG may be resized/normalized. The SVG uses declared raw source
width/height, `preserveAspectRatio="none"` and inverse screen coordinates. Its
raster/SVG bounds therefore agree even when separately rounded preview axes have
slightly different scales. Browser natural dimensions validate the delivered
preview; they never replace source dimensions. A failed decode hides overlays
without inventing a1×1 source frame. Resized previews show both preview pixels and
source-coordinate dimensions. Full-capture links are labelled **Open capture
preview**, not original/full-resolution image. The independent line excerpt remains
separately labelled.

Only lines/regions whose declared `frameRef` matches the descriptor frame are
shown/edited. There is no guessed inter-capture registration. Existing compatible
secondary-capture lines can be edited or placed. Brand-new line drawing is limited
to the primary capture and passes an explicit compatible region ID. With no regions,
the API's existing primary-capture fallback is allowed only when capture ID equals
the source frame reference. A surface containing only secondary-frame regions
cannot silently create a line in that region from the primary image. Mixed-frame
views disable global region/line ordering rather than submit incomplete ID lists.
Frame-aware new-line creation beyond this boundary is a future extension.

Capture/frame changes invalidate older geometry reads and load current native
state. Unsaved geometry/order or pending operations block capture and collection/
surface navigation, including direct handler calls. `ProjectsPage` keys a surface
view by project/surface identity, giving a newly selected surface fresh metadata,
image requests and session state. Source drafts must be saved/discarded first;
no implicit geometry discard is introduced by the keyed view.

## Meaningful regressions and build

Actual Node22.22.2 (`/Users/johnbaima/.hermes/node/bin/node`) and owned Chrome
Headless156 pass **169/169 frontend cases**; the production build has zero warnings.
The previous151 assertions remain, including the22 geometry cases, whose generated
fixtures now declare explicit frames instead of bypassing that boundary. The18 new
cases cover header/If-Match transport, invalid metadata, stale/cancelled replies,
object-URL cleanup, same-/other-tab key changes, remote viewing, fractional raw
coordinates, incompatible frames, safe line creation, explicit draft discard,
late old-frame reads and keyed/blocked navigation.

A generated before-control exposed a cached-computed-state bug: after a new frame
was initially blocked by a draft, explicitly discarding the draft adopted that
frame but left the SVG hidden until another descriptor arrived. The before run has
one failing adoption case and seven passing controls. Making the active frame a
reactive signal corrects that case; the final169-case run includes it. The failure
was closed rather than reported as a data-loss or native-frame corruption issue.

Two initial sandboxed builds aborted before diagnostics (Node26 and then Node22).
The permitted Node22 build succeeds. An early new frame test incorrectly searched
all text for a heading also mentioned in a hint; its selector was corrected. Those
attempts remain in the artifact directory and are not product-regression evidence.

## Actual browser/API and default hosting

The independent reviewer owns the browser/API/provider processes and generated
fixtures. Initial application integration passed16 checks through a root static
proxy, which did **not** prove default API hosting. A separate real-browser before
run served `/ui/` HTML but requested its root-based JavaScript URLs at `/`, receiving
404. Production `angular.json` now sets `baseHref` to `/ui/`; development serving
retains `/`. Other configured WebPath values require a matching build override.
The proxy did not rewrite index HTML or script paths to manufacture a pass.

The final independent run passes **16/16 checks through actual API `/ui` hosting**:
all UI assets200, no runtime exceptions, capture-only display/drawing, noninteger
preview coordinates, matching raster/SVG bounds, fixed-color fiducials, real
pointer edit/save/reload in source units, incompatible capture suppression,
primary/secondary-region safety, and a generated native-AI acceptance that refreshes
visible L2 while the concurrently edited L1 draft remains. The actual saved L1 is
unchanged; the receipt/L2 are durable. Revoked access clears the raster on explicit
retry and actual HTTP denies the revoked key. This does not retract bytes already
disclosed before revocation or claim continuous browser-side revocation detection.

The browser uses a generated loopback provider double once, no real model. Owned
processes exit0 and the owned browser profile is removed. Screenshots and exact
network/native state are retained by the reviewer; root inspected the screenshots.
The final proof is
`.artifacts/experiments/ui01-independent-v1/browser-v1/attempt-5/proof.json`.
The earlier static-proxy pass and default-hosting failure are retained separately.

## Frozen evidence and remaining limits

Client receipts are under `.artifacts/ui01-client-2026-11-05/`:
`full-final.log`, `build-ui-hosting-final.log`, `frame-adoption-before.log`, original
pre-change snapshots, source copies and `client-final-ui-hosting-freeze.json`.
That final manifest is SHA-256
`f70355b4f1bcacf4462fd0247c0db05b614919021a384971607910c2f0f4a399`.
Its13 source hashes and five production assets remain unchanged after the
independent run. The final index SHA-256 is
`44fa787d244516e0adf46358c9cb448d32f6ad69bff981982e0d26b5ab4db4c2`;
main asset is `main-CLX4ZAAY.js`. The prior five root-base assets were copied and
hash-verified before rebuilding; their earlier receipts were not overwritten.

This work does not migrate the existing line-crop/SSE query-token policy: the
separate `lineCropUrl` still uses the pre-existing API-key query parameter. The
new capture-preview header/Blob guarantee must not be generalized to every old
image/event URL. Browser reload/storage failure, all platforms/browsers, remote
frame verification, TIFF previews, automatic inter-frame registration and
secondary-frame new-line creation remain outside this demonstrated boundary.
No Greek reading/geometry acceptance, model accuracy, general resource guarantee,
license clearance or product release follows from generated UI checks. Original
image bytes and source text remain distinct from intentional user geometry/L2 edits.
