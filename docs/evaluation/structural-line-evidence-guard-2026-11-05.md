# Structural line edits preserve all-layer evidence — 2026-11-05

Delete, split and merge previously checked only `DiplomaticRevisionId`. Lines with
L0 recognition or L2 reference/editorial revisions commonly have no L1 pointer.
Deleting or splitting such a line could orphan its revisions and annotations;
merging could detach evidence from the geometry it originally described.

All three operations now refuse a line with any scoped revision, an L1 pointer,
or a review ledger entry. The guard is independent of the layer enum; current
L0/L1/L2 revisions are covered, including L1 history without an active pointer.
Merge independently checks both input lines, even though the first identity is
retained, because its geometry is also repartitioned. Annotations remain anchored
to their original revision; no evidence is reassigned, flattened or deleted.
Refusal returns `RevisionConflict`/HTTP409 before project, version or audit mutation.

Split and merge additionally require finite placed polygons with at least three
points and positive bounding width/height. Missing placement returns an explicit
`StageContractViolation`/HTTP422 instead of an empty-sequence exception. Split
rejects NaN and infinite positions. Evidence-free deliberate deletion remains
available for both placed and unplaced lines. Existing evidence-free split/merge
behavior and append-only geometric audit remain supported.

## Regression evidence

The corrected placed-fixture baseline ran 29 scenarios before implementation:
**20 failed and nine controls passed**. Failures covered all three scoped layers
on deletion, split and both merge positions, review-only evidence, unplaced geometry,
and NaN. The prior exploratory fixture inherited the sample's empty polygon and
produced additional fixture-side failures; its log remains separate and is not the
evidence-loss reproduction claimed here.

Thirty final structural service cases cover those refusals, unresolved L1 pointers
and review references, no document/audit/version changes, `ProjectIntegrity` without
new dangling references, and successful evidence-free operations. Healthy scoped
revision/annotation fixtures remain integrity-clean. Unresolved pointer/review
fixtures intentionally begin with integrity findings and preserve those exact
findings rather than claiming to repair them.

Final combined service verification passed **51/51** cases (30 structural,
eight order-snapshot, 13 existing geometry tests). Generated HTTP tests exercise
the same structural routes against each layer and both merge inputs, preserving
the entire returned project and an integrity-clean stored record. All **16/16 HTTP
cases passed** (12 structural and four paired order regressions). Source builds
reported no warnings; `git diff --check` passed.

No fitting, model load, inference call, natural-text label decision or data acquisition
occurred. This refusal policy does not supply a scholarly text-repartition workflow,
delete historical evidence, infer new reading order, or implement full geometry
entity revisioning. Existing project optimistic concurrency still protects the save.
