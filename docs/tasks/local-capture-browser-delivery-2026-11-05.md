# UI-01: authorized capture delivery and browser frame contract

November 5, 2026. Proposed implementation briefs for two instances plus an
independent security/integration reviewer. No implementation is authorized by this
brief alone. The [published HTTP reproduction](../evaluation/local-capture-browser-delivery-gap-2026-11-05.md)
shows an imported local PNG's serialized filesystem location receives 404 when used
as an HTTP image path. Browser rendering remains unobserved because the Mac is
locked. Preserve the frozen package and all current evidence.

## Shared contract before either instance edits product code

A and B first agree a small versioned delivery descriptor and generated fixtures.
Use explicit project/surface/capture IDs, never a client-supplied filesystem path.
An illustrative URL is
`/api/v1/projects/{projectId}/surfaces/{surfaceId}/captures/{captureId}/display`;
its exact route/DTO is agreed before implementation. Native `imageLocation` remains
provenance/storage data, not the UI's fetch URL. Do not rewrite persisted paths or
add a static alias to a filesystem root. No new dependency is selected here.

Specify the descriptor's capture/source-frame identity, source pixel dimensions,
source checksum availability, displayed raster dimensions, media type, derivative
identity/checksum and an exact display-to-source coordinate mapping. Source
checksums marked unavailable must remain explicitly unavailable, rather than being
mistaken for a previously verified recorded digest. Declare any newly computed
byte digest separately. Display URL/metadata are derived delivery state, not an
accepted geometry or text revision.

Choose and test the raster policy before implementation:

- For directly browser-decodable PNG/JPEG, determine whether exact source bytes
  can be served while preserving the stored coordinate frame. Never assume that
  browser EXIF orientation and the importer/decoder use the same orientation.
- For EXIF rotations/reflections, preserve native raw-frame geometry and expose
  an explicit invertible transform to the display raster. Test all orthogonal
  orientation cases, non-square dimensions and boundary coordinates. Do not rotate
  existing polygons/baselines silently or infer transformations from Greek text.
- TIFF is not a universal browser `<img>` format. Decide a bounded PNG/JPEG preview
  policy or explicit actionable unsupported response. Multi-page selection,
  color/alpha and source-frame identity must be defined. The existing TIFF
  full-frame fallback/security gate is still open; a new preview endpoint must
  not route arbitrary large/adversarial TIFF through it and call that bounded.
- For resizing, the descriptor must expose exact scale/translation/orientation
  composition and source/display extents. Preview dimensions are not replacement
  native dimensions. Hit-testing and overlay edits convert through the registered
  mapping; reject unsupported/noninvertible transforms rather than guessing.
- Define maximum encoded input, dimensions/pixels, output bytes, decode allocation,
  concurrency and elapsed work/cancellation. Check trustworthy bounds before
  raster allocation where possible. A post-decode size check is insufficient.
  Preserve original file bytes/checksums and unchanged native geometry throughout.

Capture-only surfaces with zero lines are a mandatory first fixture. A line crop
or recognition/layout pass is not a prerequisite for seeing their source image.
Multiple captures must retain per-capture frame identity: overlay only compatible
geometry or apply an explicit validated inter-frame registration. Never display
another capture's coordinates as if they were pixel-aligned automatically.

## Instance A — API, delivery descriptor and resource/frame ownership

**Own:** new capture-ID delivery endpoints and DTOs, server-side capture lookup,
resource/format admission and derivative/frame contract; focused application/API
regressions. Coordinate any shared `Program.cs` section before edits. Do not edit
Angular components. Reuse existing storage/resolution policy without accepting
arbitrary path/URI input or exposing host roots.

**Dependencies:** shared descriptor/fixture agreement with B; current authentication,
project membership, capture/frame and safe decoder policies; SEC-01 constraints.
For remote/IIIF references, define a supported/deferred scope explicitly and retain
host/redirect/cache/rights restrictions. A local preview fix must not introduce an
unrestricted remote proxy or reinterpret upstream photograph permissions.

**Acceptance criteria, registered before implementation:**

1. Actual HTTP tests create/import generated PNG/JPEG capture-only surfaces, obtain
   the descriptor, retrieve image bytes by capture ID, and verify MIME/dimensions/
   hashes against the selected source/derivative policy. Zero-line captures work.
2. Project/surface/capture ownership is exact. Unknown IDs, cross-project captures,
   ambiguous IDs, traversal-like inputs and replaced/missing files fail safely.
   Symlink/remote resolution honors existing trust policy. No path query parameter
   or filesystem static alias exists.
3. Anonymous local mode and explicitly authenticated Reader/Annotator/Admin modes
   retain declared policy. Outsider membership, disabled accounts, revoked
   membership and local-mode activation are denied consistently. Recheck before
   disclosing completed buffered output after slow resolution/decode. Define
   in-flight streaming limits honestly; bytes already sent cannot be recalled.
4. Test encoded/dimension/output limits, malformed payloads, large headers,
   cancellation and concurrent decode admission with generated fixtures. Record
   actual peak/allocations where a resource guarantee is claimed, including failed
   paths. Do not fit/load recognition models or consume corpus labels.
5. Generated EXIF/TIFF/resize cases establish the frame contract or explicit
   refusal; no silent image/frame fallback. Original bytes, native JSON/frame and
   polygons/baselines remain unchanged. Any cache is bounded, ownership-safe and
   keyed to source identity/policy, with exact cleanup/failure semantics.

**Handoff:** freeze route/DTO examples, source hashes, generated fixture generator/
hashes, before/after HTTP receipts, transform formulas and limits; identify every
unsupported format/frame. Send B a runnable generated API double/sample descriptor,
not an actual photograph. Send the independent reviewer the auth/resource delta.

## Instance B — client/auth URL lifecycle and independent visual evidence

**Own:** ApiClient descriptor/image retrieval, SurfaceView capture selection,
GeometryEditor display-frame use and affected full-source links; generated
frontend regressions and actual isolated browser integration evidence when a
usable browser exists. Do not edit A's endpoint/storage/decoder implementation.
Coordinate shared LineEditor/SurfaceView files with their current owner first.

**Dependencies:** A's frozen descriptor/route and transforms; generated fixtures;
a usable browser; existing API-key/auth behavior. An ordinary `<img>` request does
not pass through Angular HttpClient's interceptor. Agree authenticated blob loading
through ApiClient or a separately reviewed short-lived delivery-token approach;
do not append long-lived credentials to arbitrary image URLs or remote hosts.

**Acceptance criteria, registered before implementation:**

1. Use the descriptor/authorized image endpoint rather than `imageLocation` as a
   fetch URL. Source filesystem metadata remains provenance. Blob/object URLs,
   subscriptions and responses are released on replacement, failure and destroy;
   cancellation/version guards prevent an old capture response replacing the new.
2. Generated component tests cover no-line captures, capture/surface navigation,
   loading/refusal/404/auth errors, empty/invalid raster dimensions, late responses
   and token/account changes. Do not treat default 1×1 dimensions or mocked load
   events as evidence that an image was fetched or rendered.
3. Apply A's frame mapping to display, selection/hit-testing and geometry edits.
   Verify raw versus EXIF/display coordinates and resized previews with fixed
   fiducials, non-square images and exact inverse-map checks. Multiple captures
   with incompatible frames must refuse/segregate overlays without altering native
   geometry. Preserve source-line crops and diplomatic/editorial draft behavior.
4. With actual browser availability, use one owned generated API/store/provider
   double. Visibly render the capture and fiducial overlays, switch captures,
   verify authenticated retrieval and revoked-access refusal, and prove correct
   save/reload coordinates. Retain screenshots, network/status evidence and final
   native data. Stop owned tabs/processes in `finally`; no real user sessions.
5. Complete the original UI-01 actual Angular→API proposal request/accept and visible
   L2 refresh while a concurrent L1 draft survives. Verify durable issued record,
   acceptance receipt and unchanged L1/source. Screenshots and native/HTTP receipts
   complement unit tests; HTTP-only success cannot close the browser gate.

**Handoff:** exact source/build hashes; component/full frontend results; browser/
platform and screenshots; generated API/native-state receipts; object-URL/cancel
cleanup results; unchanged original/frame/geometry evidence. Explicitly list any
browser/platform/EXIF/TIFF case not exercised. No Greek quality/label acceptance.

## Independent reviewer/integrator — R or a separate instance

Review A's ownership/authentication, membership/revocation, resource admission,
remote/path trust and cache rules before B's real integration. Independently check
transform direction/composition with generated controls, unchanged original/native
geometry and client credential/object-URL lifetimes. Require meaningful fail-before
regressions, real HTTP capture-only proof and actual browser evidence separately.
Merge only after the agreed format/frame/auth matrix passes, with explicit
refusals and retained limits. A green UI build alone is not delivery verification.
The current 451-file package remains unchanged historical evidence; publish a new
package only after a separately authorized, reviewed implementation and full gates.
