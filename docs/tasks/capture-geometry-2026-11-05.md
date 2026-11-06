# Capture-specific geometry (GEO-01)

Root owns Application geometry, API capture mutation endpoints, Angular integration
and generated regression tests. Imaging and training owners do not edit these files.
No fitting, reference corpus access or external provider calls are involved.

## Contract frozen before implementation

1. New-line creation targets an explicit project/surface/capture and uses that
   capture's `Frame.ImageRef`, which need not equal its capture ID. A requested
   region must belong to the surface and exact frame. Without one, use a compatible
   region or create a manual region in that frame. Never borrow another frame.
2. The browser sends the current quoted display derivative in `If-Match`, plus
   the complete surface line and region ID sequences it loaded. The endpoint
   re-renders the bounded local preview, rechecks current credentials/membership
   and capture metadata, and refuses stale source or structure before saving.
   The final store write retains optimistic project concurrency and atomic
   account/membership checks. Unsupported previews remain refused.
3. New geometry uses finite raw source coordinates within `[0,width]` and
   `[0,height]`; no rotation, rescaling or inferred frame conversion. A new manual
   region covers that raw frame. Capture identity, observed source checksum,
   derivative and dimensions are recorded as edit evidence, separately from
   historical native checksum metadata. Unknown historical checksums remain unknown.
4. Existing AddLine signatures remain available. With no requested region they
   choose the primary capture's frame, never the first unrelated region. Explicit
   region callers preserve their existing target. Merge refuses unequal frames.
5. Mixed-frame region reorder replaces only selected-frame slots in the full
   ordered surface sequence; other-frame relative ranks/slots remain unchanged.
   It requires the complete expected global order. Line reorder similarly replaces
   only compatible selected-frame slots within a region. Global indices may be
   normalized by the existing order implementation. No claim of unchanged numeric
   gaps. UI ordering remains restricted until these semantics have regression tests.

## Acceptance and handoff

- Generated fixtures demonstrate capture-ID/frame-ID differences, secondary-only
  regions, requested wrong-frame regions, empty surfaces, source bounds, stale
  derivative/structure, duplicate retry, cross-frame merge refusal and exact audit.
- Actual HTTP tests prove current key/account/role/membership revocation across an
  awaited render, anonymous first-account transition, readonly mode, no unauthorized
  native mutation, cancellation and admission cleanup.
- Angular tests prove authenticated headers, explicit capture/derivative targeting,
  secondary creation, raw-coordinate preservation and retained drafts after refusal.
- Run the filtered no-fitting engineering gate and frontend checks after owner
  coordination. Independent review and owned-browser runtime evidence follow before
  GEO-01 is marked complete. Record limits and remaining gaps in todo.md.
