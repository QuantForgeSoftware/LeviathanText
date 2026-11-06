# Ordered-layout snapshot guards — 2026-11-05

Project optimistic versions protect overlapping saves, but an API handler can load
a fresh project for a stale browser order. Without the order the browser originally
observed, the handler could overwrite a previously committed remote reorder.

`SetReadingOrder` and `SetLineOrder` now optionally compare the exact original ID
sequence before changing order, appending audit or saving. The API request fields
are `expectedRegionIds` and `expectedLineIds`. A mismatch returns the existing
`RevisionConflict`/HTTP409 with a reconciliation instruction. Omission preserves
legacy behavior; the project-version save guard remains in both cases.

Region order follows stable native `OrderBy(ReadingOrderIndex)`: null indices first,
then numeric ascending, preserving native array position for ties. The Angular
editor now uses the same rule instead of treating null as zero. Line order is the
native surface line sequence filtered to the selected region; other regions' line
slots remain in place.

The browser sends copies of the original loaded snapshots. Only an actual successful
acknowledgement advances the original snapshot to the submitted order. Changes made
while saving remain a dirty draft. HTTP409 preserves the current view's draft and
its original snapshot and explicitly directs the user to reload/reconcile; it does
not silently accept the remote order as a parent. These drafts are not persisted
across browser navigation and are not automatically merged.

A separate data-preservation defect was fixed in the same boundary. Hash-set-only
validation accepted desired `[a,a,b]` for existing `[a,b]`. Region ordering then
threw a duplicate-key exception; line reconstruction could silently duplicate `a`
and drop `b`, including its geometry and revision linkage. Desired orders now must
contain every existing ID exactly once, including the original sequence length.
Invalid or duplicate requests return `StageContractViolation`/HTTP422 without a
project save or audit entry.

## Verification

Two duplicate-order tests failed before correction: one expected domain error was
an ordinary duplicate-key exception, and one operation succeeded while duplicating
the line. Additional generated tests cover a duplicate third entry, omitted IDs
hidden by duplicates, text/geometry/ID preservation, no version/audit change, stable
null/tie ordering, other-region slots, stale UI snapshots after handler reload,
and the unchanged optimistic store-version guard.

Focused results:

- Application order and existing geometry tests: **21/21 passed**.
- Generated HTTP stale-order/duplicate-order tests: **4/4 passed**, including legacy
  requests without snapshots.
- Geometry-editor browser tests: **22/22 passed**, including in-flight edits,
  acknowledgement-only snapshot advancement and HTTP409 draft retention.
- Complete Angular integration after this slice: **138/138 passed**. Earlier
  editor-only integration totals remain dated evidence for their own snapshots.

C# verification used isolated output at
`/Users/johnbaima/Code/LeviathanText/.artifacts/midday-order-snapshots`, one MSBuild
process and `UseSharedCompilation=false`. Source builds reported no warnings.
No fitting, inference request, data acquisition, source-label change or specialist
acceptance was involved. This is a value-based order guard, not edit locking,
automatic structural merge, or proof of complete container/UI parity.
