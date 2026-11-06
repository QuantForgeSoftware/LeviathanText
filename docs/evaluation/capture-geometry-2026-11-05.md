# Capture-specific creation and ordering — November 5

GEO-01 is implemented and independently verified. New lines can be drawn on
secondary local captures, including sources whose frame identifier differs from
the capture identifier. The selected capture's raw image coordinates are retained.
An explicit region must match that frame; absent a compatible region, a manual
whole-frame region is created. Implicit legacy creation now selects the primary
frame rather than borrowing the first unrelated region. Cross-frame merge is refused.

New capture-scoped POST `lines`, PUT `reading-order` and PUT
`regions/{regionId}/line-order` live under
`/api/v1/projects/{projectId}/surfaces/{surfaceId}/captures/{captureId}`. All require
a current quoted display derivative in `If-Match`. Creation additionally requires
the complete loaded surface line/region ID sequences. Ordering requires the full
loaded global region order or complete loaded region line order; only the selected
frame's slots are replaced. Other frames retain their global slots and relative
order. Existing numeric reading-order gaps may be normalized.

The endpoint renders the bounded local preview, refreshes the exact current
credential/account/membership and complete capture metadata, validates current
observed dimensions including frame aliases, then saves through the authorized
optimistic store. Creation checks finite raw coordinates against observed source
bounds. The edit audit records observed checksum/derivative/dimensions separately;
historical unavailable checksums remain unavailable. No native source bytes or
frame registration are inferred or rewritten.

A refused creation retains its box and original structural/source expectations.
Retry preserves those values; changing preview identity blocks retry until explicit
discard. Drafts/pending writes guard surface and capture navigation. The browser
uses authenticated HttpClient headers, with no credentials in the new URLs.

## Evidence

- Two generated regressions failed before the fix: implicit cross-frame region
  choice and merging lines with unequal frame references. Application geometry
  selection then passed30 cases, including17 new capture tests.
- The full no-fitting engineering gate passed **1820 C# cases**, with18 intentional
  skips (17 harmless capability sentinels and the existing live Ollama test).
  Receipts: `.artifacts/overnight-2026-11-05/engineering-v1/` and `counts.json`.
- Angular174 cases passed and the production build had no warnings. The exact
  source freeze is `.artifacts/overnight-2026-11-05/geo01/source-freeze-v1.json`.
- Independent actual API-hosted Chromium verification passed **18/18 checks**,
  with zero runtime errors or provider calls. It covered secondary creation,
  noncapture frame IDs, raw pointer coordinates, both mixed-slot order types,
  actual source replacement yielding412, retained box, unchanged complete project
  JSON, guarded navigation and primary creation with only secondary regions.
  [Independent review](capture-geometry-independent-review-2026-11-05.md),
  `.artifacts/experiments/geo01-independent-v1/attempt-4/proof.json`.
  All owned API/Chrome processes exited0 and the temporary browser profile was
  removed. Earlier harness failures remain preserved; they were not product fixes.

## Limits and next work

These stronger source checks apply to the new creation/order routes. The existing
line-only geometry PUT retains expected-geometry concurrency but has no display
If-Match/source-bound requirement. GEO-02 tracks capture-scoped existing-line edits
and draft source binding; GEO-01 does not claim that broader protection. Legacy
API signatures remain available. TIFF/remote delivery remains outside the current
PNG/JPEG preview policy. Decode scratch/RSS and hostile concurrent filesystem
replacement are not hard-bounded by this endpoint. No fitting, corpus labels,
Greek adjudication, accuracy assessment, model promotion or publication occurred.
