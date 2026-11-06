# GEO-01 independent review and actual browser evidence

Date: 2026-11-05. Reviewed root's candidate registered in
`.artifacts/overnight-2026-11-05/geo01/source-freeze-v1.json`, against the
[capture-geometry contract](../tasks/capture-geometry-2026-11-05.md).
No new blocking defect was found in that bounded candidate. Eighteen independent
actual-browser checks passed on generated fixtures. Production source was not edited
by this review. GEO-02 below remains a concrete compatibility limitation.

## Source review

Reviewed GeometryService creation, structural refusal and selected-frame ordering;
CaptureGeometryController; CaptureAccessPolicy; shared capture display delivery;
Angular API methods, raw SVG frame handling, order snapshots and new-line drafts.
The review also read the owner's generated service/HTTP cases without running the
unfiltered API project. Root's concurrent JOB-01 tests/source are a separate track.

The new controller resolves the explicit project/surface/capture, requires a quoted
derivative token before rendering, reloads and reauthorizes after the awaited render,
checks capture identity and raw dimensions, and saves through the existing scoped
atomic account/membership guard and optimistic version check. The rendered lease is
disposed on success/refusal. The service checks complete loaded line/region sequences
before new creation and rejects requested foreign-frame regions. New manual regions
use full raw bounds; observed source evidence is an audit note, not backfilled
historical checksum metadata. Full-slot reorder composes only compatible selected
IDs into the complete order and retains other frames' positions.

The UI captures source identity and structural expectations with a pending box,
retains it after refusal, blocks navigation through the live child guard, and permits
explicit discard. Surface links themselves stay enabled; their handlers refuse
navigation while work is pending. Previous/Next controls also disable. The browser
proof tests both behaviors, rather than assuming every link has a disabled attribute.

## Registered browser workload

Evidence root: `.artifacts/experiments/geo01-independent-v1/`.
`registration.json` pins the 12 candidate geometry files before compilation and
records the previous UI-01 harness as provenance. Its old receipts were not changed.
The copied/adapted harness and generated seed are in this new evidence directory.
Each attempt has its own preregistered harness/runtime/fixture-build hashes and fresh
output directory; attempts never reuse a store, browser profile or result directory.

The workload uses a new local store, generated dummy account/key, generated Skia PNGs
and a private headless Chrome profile. API model directory is empty. Any unexpected
provider path returns refusal and increments a counter; the observed count was zero.
No original corpus images, XML, labels, real providers, training or LM Studio were used.
The seed includes deliberately different capture IDs and raw-frame IDs, two frame
families interleaved in global order, and one mixed-frame legacy line slot to preserve.
Its one original transcription is the generated ASCII string `GENERATED-UNCHANGED`.

Node 22.22.2 rebuilt the current production client with `/ui/` base; build completed
without warnings. Five built asset files were copied and hash-frozen under `dist/`.
A separately built current API hosted those assets at its actual default `/ui/` path.
The owned loopback proxy forwarded UI/API paths without index/path rewriting; it did
not replace C# static hosting. The isolated API/seed builds compiled successfully;
their initial offline restores logged NU1900 advisory-metadata warnings. These are
retained, not described as zero-warning builds or a dependency security resolution.

## Results

Final receipt: `attempt-4/proof.json`, SHA-256
`23e31e18475e419585b648047bf0f47d352c17a905d137a8534d4eb1ad178ebd`.
All **18 checks passed**, with zero browser runtime exceptions and zero provider calls.
These are browser assertions, not additional xUnit cases.

The actual Angular UI and API demonstrated:

- Capture-only drawing when the raw-frame ID differs from the capture ID, with a
  newly created region covering the complete 400×300 source frame.
- A 3001×2000 source displayed as 2048×1364, with raw SVG viewBox,
  preserveAspectRatio none, and identical displayed SVG/raster bounds.
- Selected-frame overlays only, selected-frame region reorder preserving foreign
  slots, and line reorder preserving both an incompatible-frame slot and other-region
  global slots. The complete saved permutations are in the receipt/native snapshot.
- Secondary-capture creation in its compatible region, retaining raw pointer
  coordinates within two source pixels; frame identity differs from capture identity.
- Deliberate replacement of only the owned generated secondary PNG after preview
  caused the actual mutation endpoint to return HTTP 412. The pending outline and
  actionable retention message remained visible. Surface selection was refused until
  explicit discard; discard plus refreshed preview released navigation.
- A primary capture with only secondary-frame regions created a separate compatible
  primary region, rather than writing through the unrelated existing region.
- Original text, existing line records, existing region polygons and all capture
  metadata remained unchanged. Unknown recorded checksums remained unknown.
- All JavaScript requests loaded successfully under the actual API `/ui/` prefix.

The failed-mutation equality assertion compares complete `JSON.stringify(GET project)`
payloads before/after, including version and audit collections. It does not measure
SQLite file-byte equality. `independent-analysis.json` additionally compares the final
native snapshot against the original seed by identity: one original revision remains,
all original revisions/line records/capture metadata are exactly equal, and existing
region geometry is unchanged. Expected order/audit/new-line changes remain explicit.

Screenshots were visually inspected: `secondary-created.png` shows the created box
in the selected 1000×800 frame; `stale-source-retained-box.png` shows the green pending
outline and the refusal/retention message with discard/retry controls.

Earlier attempts are preserved. Attempt 1 used the wrong line-ID prefix in a locator.
Attempt 2 reached secondary creation but did not dispatch its later pointer probe;
its timeout is not attributed to a product defect. The probe moved to clear visible
background and gained pointer-step diagnostics/render waits. Attempt 3 proved the
actual 412/no-mutation behavior but incorrectly assumed surface links disabled;
attempt 4 tests their real guarded handler. No production fix occurred between these
attempts; the same registered runtime/assets were used.

## Cleanup and limits

Final owned API PID 78677 and Chrome PID 78691 exited zero. The private profile/store
root was removed by the harness finally block. Earlier attempts likewise report their
owned processes exited zero. No other process or user browser session was touched.
All 12 candidate source hashes and all five copied asset hashes remain unchanged.
`independent-analysis.json` records the preservation and cleanup checks plus final
receipt/screenshot hashes. This is evidence for the registered generated workload,
not proof of arbitrary browser/platform support, natural-text quality, or every
possible pointer/cancellation interleaving.

### GEO-02: existing mutation source binding remains open

Derivative/raw-bound enforcement in this candidate applies to the new capture
creation/order routes. Existing line-only geometry PUT uses expectedGeometry; it
neither requires a capture display If-Match token nor verifies current source bytes
or raw source bounds. Legacy AddLine signatures/routes also remain callable under
the compatibility contract. They do not acquire the new route's source-binding
promise merely because the Angular UI now uses the capture route.

A followup should define an explicit capture-targeted existing-line edit contract,
with expected geometry, observed-source token, raw bounds, current write authorization
and atomic optimistic save, then migrate the UI and decide legacy compatibility
policy. Generated source-replacement/frame-mismatch/readonly/stale-geometry tests
should precede implementation. Do not silently claim existing moves are protected
by the new creation/order checks. No production expansion was made during this review.
