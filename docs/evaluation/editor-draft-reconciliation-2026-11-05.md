# Editor acknowledgement and draft reconciliation — 2026-11-05

## Data-preservation behavior

The initial pending-save guard preserved later typing but could silently lose another
writer's merged edit on the next save. For example, a submitted `αγ` can merge with
a concurrent prefix to become `δαγ`, while the user continues typing `αγε`. Keeping
`αγε` unchanged and naming the new `δαγ` parent would treat deletion of `δ` as
intentional. The editor now merges the post-submission edit from `αγ` to `αγε` onto
`δαγ`, producing `δαγε` before permitting the next save.

Revision writes own an identity token through acknowledgement and the following read.
Earlier metadata reads are invalidated when a write starts; unrelated review or
annotation callbacks cannot release that write's lock or supersede its reconciliation.
Metadata refreshes rebase unsaved text against its recorded ancestor. Successful
parent/text changes discard unsafe undo snapshots and reset caret/annotation form
offsets rather than carrying them onto different letters.

Overlapping changes preserve the visible draft and its original ancestor, show the
latest saved reading and block saving, review and source-offset annotation actions.
Reloading latest attempts reconciliation without silently accepting a new parent.
The user can explicitly keep the compared draft as a replacement or use the latest
saved text. A successful write whose following read fails keeps its draft but requires
a successful latest read before either comparison choice or another save. A correction
review's HTTP409 follows the same gate. Initial read failure leaves the editor unloaded
with a Retry control; it no longer synthesizes a blank, writable line payload.

## Recovery format

The authoritative localStorage record at `leviathan:draft:<lineId>:recovery` is one
atomic JSON value: `{version:1,text,base}`. Its `base` is either the exact ancestor
text or null. The existing raw draft key is maintained only as a compatibility copy.
A mismatched raw copy forces unknown-ancestor comparison; the authoritative envelope
still retains its own text. A malformed envelope falls back to raw legacy text without
an ancestor. A separate legacy `:base` key is never trusted.

Known-ancestor recovery uses the same conservative merge before naming the current
parent. Unknown-ancestor and overlapping recovery stay visible but require comparison.
Storage failures leave an older consistent record or an explicit comparison case; they
cannot guarantee preservation of the newest keystrokes. Drafts are per browser and do
not constitute a server backup or cross-device editing protocol.

## Scalar merge contract and verification

`text-merge.ts` follows the owned C# `Core/Text/ThreeWayMerge.cs` semantics:
Unicode scalars without normalization, deletion-first LCS ties, an exact diff only
when `(base length + 1) × (variant length + 1) ≤ 4,000,000`, and a conservative
prefix/suffix single-hunk approximation above that cap. Non-overlapping and identical
edits combine. Other overlaps fail and return the unchanged base. Combining marks
remain separate scalars; astral characters remain intact. It is not a grapheme
normalizer or a scholarly choice of reading.

The helper's 17 scenarios include six fixtures with expected results shared with the
C# regression suite, the merged-save example, scalar/combining cases, boundary and
interior insertions, ambiguous repeated text and large conservative fallback. Editor
tests cover both timing orders of the prior-review/pending-save race, combined merge
and later typing, overlap reload, explicit choices, restored known/unknown ancestors,
source-offset resets, correction conflicts and initial-read failure. Injected
`Storage.setItem` failures exercise both envelope failure and compatibility-copy failure.

Final complete Angular verification: **135/135 passed** (including 51 editor and
17 scalar-merge cases), using `npm test -- --watch=false --browsers=ChromeHeadless`.
`git diff --check` passed. Earlier intermediate checks passed 128 tests before the
storage/error-path refinements, and 66 focused cases before the two final offset tests.
No model fitting, inference request, source-data acquisition or acceptance-label
change occurred. The two additional concurrency defects were identified by independent
code review; this note does not claim a separately executed historical build for them.
