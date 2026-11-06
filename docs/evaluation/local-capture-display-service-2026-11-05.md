# UI-01 bounded local capture delivery

## Implemented contract

The capture-display service and two GET endpoints implement the agreed
[UI-01 brief](../tasks/local-capture-browser-delivery-2026-11-05.md):

`/api/v1/projects/{projectId}/surfaces/{surfaceId}/captures/{captureId}/display`
returns the v1 descriptor; appending `/image` returns buffered normalized PNG.
Only exact nested project/surface/capture identities are resolved. There is no
filesystem-path/remote-URI request parameter, static source root or cross-project
first-match fallback. Native paths and project data are never rewritten.

The descriptor contains project/surface/capture IDs, `sourceFrameRef`, the exact
`recordedSourceChecksum`, `recordedChecksumStatus` (`verified` or `unavailable`),
separate `observedSourceChecksum`, observed raw `sourceWidth`/`sourceHeight`,
`displayWidth`/`displayHeight`, `scaleX`/`scaleY`, `orientation: "raw"`,
`mediaType: "image/png"`, `derivativeId` and `displayChecksum`. It has no source
path or image URL. Known recorded dimensions must match observed raw dimensions;
null legacy dimensions remain null in native storage. A newly computed digest
never changes an unavailable recorded checksum into a previously verified one.
The existing image importer deliberately records an unavailable checksum; HTTP
tests preserve that behavior rather than silently repairing it.

The source/display transform is exactly:

```
displayX = sourceX * scaleX     sourceX = displayX / scaleX
displayY = sourceY * scaleY     sourceY = displayY / scaleY
scaleX = displayWidth / sourceWidth
scaleY = displayHeight / sourceHeight
```

Rounded dimensions give independent invertible axis scales. There is no
translation, inferred registration or alteration of stored polygons/baselines.
Skia `GetPixels` decodes the raw stored frame; EXIF orientation is deliberately
not applied, and fresh PNG encoding removes orientation metadata. All eight
orthogonal EXIF orientations retain nonsquare raw dimensions and fixed corner
fiducials. This endpoint performs no TIFF decoding or ImageSharp fallback.
The display is normalized through an 8-bit RGBA bitmap, not an archival
bit-depth/color-profile-preserving derivative. Original encoded source bytes
remain unchanged. Native codec acceptance is not a complete independent
PNG/JPEG conformance or CRC validation claim.

The canonical JSON identity hash binds delivery policy v1, project/surface/
capture/frame identity, recorded and observed source checksums, source/display
dimensions and normalized PNG checksum. Thus changing JPEG metadata/encoding
while preserving decoded pixels still changes the derivative ID. Descriptor
and image responses carry `Cache-Control: no-store` and an ETag containing the
double-quoted 64-character lowercase derivative ID. Image GET requires that
exact `If-Match`; absent, malformed or stale values return 412 without raster
bytes. Source checksum/dimension changes after a descriptor also return 412 on
image delivery; descriptor requests report their controlled source/frame error.

## Resource and trust boundaries

| Boundary | Implemented limit/policy |
| --- | --- |
| Encoded snapshot | 16 MiB; bounded asynchronous read checks growth, not only initial length |
| Supported signature | Exact PNG eight-byte signature or JPEG `FF D8 FF` before native codec creation; codec format checked again |
| Decoded source | At most 12,000 per edge and 16,000,000 pixels, checked from codec metadata before bitmap allocation |
| Preview | At most 2,048 per edge and 4,000,000 pixels; rounded down |
| Output | PNG at most 16 MiB, checked before copying encoded output |
| Concurrency | One singleton admission permit, acquired before snapshot allocation and retained through completed/failed buffered response execution |
| Delivery | Request-abort cancellation plus a 30-second linked output deadline/connection abort; release in execution `finally` |
| Cache | None; descriptor/image recompute deterministically and bind with `If-Match` |

Cancellation is checked before/after synchronous native work. The output deadline
does **not** promise an interruptible native decoder, fixed total RSS/native
scratch allocation, or a global filesystem-operation deadline. The response
executor uses the built-in token-aware JSON/file writers; its output deadline is
distinct from decode admission and bounds on encoded/pixel/output sizes.

Only absolute local paths already present in an authorized capture are read.
Direct file symlinks are refused; ancestor paths are canonicalized, preserving
normal macOS `/var` aliases. No new unrestricted server-root policy is invented.
These checks do not atomically eliminate hostile ancestor-replacement filesystem
races. Recorded SHA-256 verifies the bounded byte snapshot when available;
unavailable legacy hashes remain explicitly unavailable. A snapshot and its
descriptor are consistent even if the external file later changes. Source file
locking/latest-filesystem-state attestation is not claimed.

Captures claiming the same native frame must have compatible verified checksum
identity and dimensions; every known dimension claim is checked against observed
raw codec dimensions before bitmap allocation, including when the selected
capture's own dimensions are null. That same check runs against the freshly
loaded project after buffering, so an alias inserted during rendering cannot
bypass it. Conflicting or unverified aliases fail with
`FrameUnregistered`, without reading another capture's file. A single legacy
capture with an unavailable recorded hash remains displayable without inventing
historical verification. Client overlay/edit policy is a separate exact-frame
contract, exercised by the browser work.

TIFF, other formats, animated PNG, relative paths and remote/IIIF sources receive
actionable controlled refusals. The endpoint never invokes the existing
remote-fetching/mutating `ImageResolver`. Existing remote direct-client behavior
is handled separately by the frontend and is not proxied through this route.
Errors disclose no stored private path.

## Read authorization and response ownership

The controller checks current read authorization before resolution, reloads the
project and rechecks it after buffering, compares the complete original selected
capture record, and rechecks immediately before handing off output. A separate
read policy includes Reader, Annotator, Reviewer and Admin. It re-resolves the
exact credential using the existing Bearer/X-Api-Key/query precedence, enabled
account identity, current role/membership and current bootstrap configuration.
An anonymous local-mode admission cannot upgrade after first-account/bootstrap
activation even if its old header now matches a newly valid credential.

Successful results transfer their idempotent disposable lease to an MVC execution
wrapper. Denials, stale requests and exceptions dispose before returning; output
failure, cancellation and timeout release in `finally`. A slow output cannot be
treated as completed while its admission has silently been released. Bytes
already delivered cannot be recalled after a later access revocation; the final
check is the documented buffered-response boundary, not an atomic permission
transaction spanning network delivery.

## Verification and handoff

Using the **exact six fitting exclusions**, the initial Application suite passed
**391/391** (32 new cases), and API passed **256/256** (20 owned and 16 independent
new capture cases), with zero final build warnings/errors. No fitting was run;
provider calls in these suites use generated/scripted doubles, with no real
external provider or corpus accuracy study. Command filters, final/source hashes and retained
logs are under `.artifacts/capture-display-verification/`.
The corresponding integrated audited engineering gate passed **1,752 tests**, one existing
live Ollama skip and zero build warnings/errors; no six excluded fitting classes
occur in its TRX results. Receipts are under
`.artifacts/ui01-integrated-engineering/`.

Independent review then demonstrated two further alias cases: a selected capture
with null dimensions allowed a same-checksum alias's wrong known dimensions, and
an incompatible alias inserted during a held render bypassed selected-record
equality. Both returned 200 before correction rather than the expected 422.
The shared observed-dimension helper above corrects both without alias-file I/O.
Two additional Application width/height controls pass; the current Application
suite is **393/393**, and the expanded independent HTTP selection is **18/18**,
with no build warnings/errors. Before receipts are
`capture-alias-unknown-dimensions-before.trx` and
`capture-alias-dimensions-after-await-before.trx` under
`.artifacts/experiments/ui01-independent-v1/test-results/`; its
`alias-observed-before/` snapshot preserves the exact pre-fix sources. The final
integrated audited gate passes **1,756 tests**, one existing live Ollama skip,
Application **393** and API **258**, with zero build warnings/errors and none of
the six excluded fitting classes in its TRX results. Its receipts are under
`.artifacts/ui01-final-engineering/`. The actual browser/integration results are
recorded separately in the [consolidated UI-01 report](ui01-capture-delivery-2026-11-05.md).

Generated controls cover actual HTTP PNG/JPEG import and capture-only surfaces,
descriptor/raster MIME/dimensions/digests, all EXIF orientations, alpha, preview
inverse scales, malformed/truncated signatures, encoded/dimension/pixel bounds,
known checksum/dimension mismatch and unavailable provenance, source changes,
missing/remote/relative/symlink/TIFF refusals, exact and duplicate identities,
role/membership access, disabled/rotated credentials, bootstrap change/clear,
first-account activation and record replacement during a held render. Controlled
response execution proves queued admission through success/throw/abort/timeout;
queued cancellation and double-disposal cannot release another response's
permit. Rejected responses leave native JSON/version unchanged and do not strand
the next admission.

Early test failures were fixture assumptions, retained rather than called product
regressions: malformed colon-form IDs were replaced with valid missing IDs, and
the image-import HTTP assertion was corrected to expect the importer's existing
unavailable recorded checksum. Independent review separately demonstrated the
actual conflicting-frame-alias defect (200 before, 422 after); that guard and
its receipts are part of the 16 independent cases. The first compile's nullable
warning was corrected before final verification.

The underlying product source is frozen for independent Angular/browser
integration. HTTP/component results alone do not close actual browser rendering,
fiducial/geometry-save or concurrent diplomatic-draft/native-AI acceptance gates.
Those results belong to the separate client/reviewer reports. Earlier 451-file
packages remain unchanged historical artifacts; no new release package,
commit/push or release clearance is implied by this backend change.
