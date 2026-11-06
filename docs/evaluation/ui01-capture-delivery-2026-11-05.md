# UI-01 completion — authorized browser capture delivery

Local PNG/JPEG captures now display in the Angular workspace through authorized
capture-ID endpoints. Capture-only surfaces need no layout/recognition first.
Native source bytes, paths, checksums and coordinates are not rewritten by display.
This is an engineering correction, not acceptance of Greek readings or OCR quality.
No training or corpus accuracy experiment ran. The UI/HTTP proof used generated
fixtures, and the browser AI check used one owned local scripted response; no
LM Studio or external AI call occurred.

## Delivery, authorization and coordinates

The API returns a versioned display descriptor and a bounded normalized PNG.
The descriptor distinguishes recorded checksum availability from a separately
observed source checksum, and source dimensions from preview dimensions. Image
requests use the descriptor's derivative ID in If-Match; source/descriptor changes
fail safely rather than pairing old geometry with different pixels.

Reader/Annotator/Reviewer/Admin access follows current account and project policy.
The endpoint rechecks credentials, account enablement, membership, local-mode
activation and capture metadata after buffering and before handing off output.
It never exposes a filesystem static root or accepts a source path/URI parameter.
New preview requests use HttpClient authentication headers; object URLs are revoked
on replacement, error, credential changes and destruction. Already delivered bytes
cannot be recalled, and this is not server-pushed revocation of downloaded images.
The separate existing line-crop/SSE query-token policy is unchanged.

Skia produces an 8-bit raw-frame PNG without applying EXIF orientation. Overlays,
hit testing and saved geometry use original source units, not preview pixels.
Independent rounded X/Y scales and an SVG source viewBox with no aspect-ratio
letterboxing keep resized previews aligned. Conflicting frame aliases, including
known dimensions added while rendering, are rejected before output. Unknown
recorded checksums remain explicitly unavailable; observed current pixels are
bound across requests without pretending historical identity was verified.

The client filters incompatible capture geometry, preserves same-frame drafts,
blocks unsafe capture/surface navigation and discards stale frame reads. Keyed
surface views load fresh sessions/data on navigation. Explicit reset can discard
a draft even after image failure. Production builds now use base href /ui/ to
match default API hosting; development ng serve keeps its root path. A custom
server WebPath requires a matching production --base-href build override.

## Regression evidence

- Final audited no-fitting C# selection: **1,756 passed**, one existing live-Ollama
  skip, zero failures. Release compilation: zero warnings/errors. Application 393,
  API 258; none of the six excluded fitting classes occur in the TRX results.
  Receipts: `.artifacts/ui01-final-engineering/`.
- Frontend: **169/169 passed** on actual Node 22.22.2/ChromeHeadless, production
  build with zero warnings. This adds 18 cases over the earlier 151; existing
  geometry assertions remain, with explicit generated frame fixtures.
- Independent HTTP: **18/18 passed**, including eight EXIF orientations with
  nonsquare raw-pixel fiducials, changed encoded bytes with identical decoded
  pixels, deterministic authorization races and both alias-dimension defects.
- Actual Chromium through API default /ui/: **16/16 passed**, all UI assets 200
  under /ui/, zero runtime exceptions. Capture-only viewing/drawing, mixed-frame
  refusal, raw 3001×2000 versus preview 2048×1364 alignment, physical pointer edit
  and save/reload, capture switching and revoked-access retry were observed.
- Real native AI HTTP acceptance through the browser retained durable L2 and
  unchanged saved L1 while an actual typed, concurrent L1 draft survived the
  delayed response. The provider was an owned local double, not an LLM.
- Root inspected the frame-overlay and accepted-L2 screenshots. Owned API/Chrome
  exited 0; temporary profile/store were removed. No user browser profile or LM
  Studio model/settings were touched.

Meaningful before failures are retained: the original local-path HTTP 404; two
frame-alias cases; a cached frame-adoption component failure; and actual Chromium
failing to boot under default /ui/ because the old build requested assets at root.
The earlier successful root-static application run is distinguished from the final
API-hosted run. Harness readiness/pointer mistakes and sandbox-blocked build
attempts are retained separately rather than reported as product failures.

## Explicit limits and follow-ups

- Local static PNG/JPEG only: 16 MiB encoded input, 12000 pixels per edge and 16M source
  pixels; preview at most 2048 per edge/4M pixels; PNG output at most 16 MiB.
- One admission lease covers source buffering, decoding and completed/failed
  response delivery. Output has a 30-second deadline and cancellation cleanup.
  Native decoder scratch/CPU, total process RSS and filesystem-operation duration
  are not hard bounded by that deadline.
- TIFF/other formats, animations, relative paths and direct file symlinks are
  refused explicitly. No unsafe ImageSharp fallback or unrestricted remote proxy.
  Existing remote viewing remains direct after fresh authorization, with unverified
  geometry read-only. Ancestor canonicalization retains normal macOS aliases;
  hostile filesystem replacement races are not completely eliminated.
- Compatible existing secondary-frame lines can be edited. Brand-new geometry
  uses only the primary capture with an explicitly compatible region, or the
  safe zero-region primary-frame case. Mixed-frame global reordering is disabled.
  A frame-selecting AddLine/region API is separate GEO-01 work.
- The preview is not an archival bit-depth/color-profile-preserving original.
  Full-preview links are labelled accordingly; originals remain available through
  existing storage/export workflows. Other platforms/browsers are not certified.

The older 451-file package is unchanged historical evidence. This assignment
verifies the current built API and production Angular assets; it publishes no
release and clears no ImageSharp, native-SBOM, Greek-review or weight-rights gate.

## Detailed handoffs

[Backend contract/tests](local-capture-display-service-2026-11-05.md),
[client lifecycle/frame tests](local-capture-client-2026-11-05.md), and
[independent HTTP/browser review](capture-display-independent-review-2026-11-05.md).
Final actual browser evidence/screenshots:
`.artifacts/experiments/ui01-independent-v1/browser-v1/attempt-5/`.
