# Native AI workflow independent review — 2026-11-05

The native persistence, HTTP resolution/authorization and final anchored-merge diffs
were reviewed independently of their implementation owners. Two substantive issues
and one early batch-bound gap were reproduced and corrected. No additional blocking finding remains in this
reviewed slice; centralized final verification remains the integrator's responsibility.

## P1: authorization after save could return 403 after publishing data

The original controller rechecked access after `Issue`/`Accept` had already saved.
Membership or disabled-state changes do not advance a project's version, so the
optimistic project write alone could not detect revoked access. An independent
generated controller/store-wrapper reproduction revoked membership immediately
before forwarding `SaveProject`:

| Operation | HTTP result | Project version | Published despite revocation |
| --- | ---: | --- | --- |
| Issue | 403 | 1 → 2 | One issued proposal attachment |
| Accept | 403 | 2 → 3 | One L2 revision and acceptance receipt |

The correction adds `LocalStore.SaveProjectAuthorized`. It reserves SQLite's write
transaction before reading the current account, checks enabled
Annotator/Reviewer/Admin status and configured membership, then performs the existing
optimistic project save within that same transaction. Current admins bypass
membership, not disabled/role checks. A null actor represents admitted local mode
and requires that no accounts exist, including disabled accounts. A literal
`usr-bootstrap` ID is not a storage bypass; the authenticated bootstrap route is an
explicit caller decision. Ordinary trusted `SaveProject` behavior is preserved.

The concrete request-scoped store retains this authorization binding even when
`IProjectStore` has an outer decorator. Storage denial becomes the native HTTP 403
without a save. Response-time access checks remain on proposal, acceptance and
preview branches. A revocation committed after an already authorized project commit
can still deny response disclosure; that is distinct from an unauthorized write.

The complete infrastructure suite passes 65/65, including missing/disabled/demoted
principals, current admin behavior, membership opt-out, first-account activation,
anonymous local mode, optimistic conflict, unchanged legacy save semantics and
command cleanup on allowed/denied publication. The HTTP owner reports 19/19 native
cases, including five before-save interception cases covering issue, single/bulk
acceptance, persisted-admin disable and local-mode account activation. Each requires
unchanged native JSON/version/issued/L2/receipt state after denial.

## P2: whole-string rediff could move a selected edit across repeated letters

For L1 `ααα`, a proposal replacing scalar range `[0,2)` with `α` produces `αα`.
Rediffing those whole strings moved the deletion to the final alpha instead of
retaining the supplied source span. The generated reproduction showed:

- Existing L2 `ααβ`, whose change is outside the selected range, falsely conflicted.
- Existing L2 `βαα`, whose change overlaps the selected range, was accepted as `βα`.

The native planner now passes the exact issued replacement hunk to a separate
anchored merge entry point. The existing editorial text is aligned with a bounded
unit-cost Levenshtein table only when that alignment is unique. Ambiguous repeated
insertions/deletions and oversized mappings refuse rather than choosing an anchor.
Equality with the composed proposal is accepted only after the same uniqueness
proof; prior durable acceptance replay remains a no-op. Generic full-text merge
semantics remain unchanged.

The service owner reports five new native regression cases failing before the fix,
then 41 native service cases and 105 complete Core cases passing, including 17 new
Core cases and two direct/base-equality controls. Final diff inspection covers the
DP bounds/path-counting, scalar offsets, anchored hunk composition and preservation
of the original generic merge path.

## Remaining reviewed boundaries

Issued record checksums, native ownership, immutable source fingerprints, supplied
graph identity, duplicate IDs, advisory-task refusal, one-proposal-per-line batches,
single-save atomic publication and receipt/revision/annotation fingerprints were
inspected. L1 and original source attachments remain separate from append-only L2
acceptance. Restoration is deliberately limited to a zero-width source insertion
whose editorial mapping is exact; unrelated editorial changes cannot be guessed.
Source supersession still blocks even a historical acceptance replay, as documented
by the service tests. These are engineering workflow guarantees, not Greek reading
adjudication or recognition accuracy claims.

The 128-proposal batch bound was initially checked only after HTTP target resolution.
A 129-ID batch therefore performed project resolution first (the generated unissued-ID
control returned 404) instead of rejecting its shape. Both controller and resolver
now enforce the same native-service constant before resolving records. Two HTTP
controls fail before the correction and pass afterward; the complete native HTTP
class passes 21/21 with unchanged project state and no additional provider dispatch.
This also avoids repeated whole-project parsing for an oversized batch of valid IDs.

The frozen before source, generated reproduction, final reviewed source hashes and
infrastructure receipts are retained under
`.artifacts/reviews/native-ai-midday-2026-11-05/`. No real provider/model call,
fitting, source acquisition or specialist decision was involved. See the separate
[application](native-ai-application-2026-11-05.md) and
[HTTP](native-ai-http-2026-11-05.md) handoffs for the owners' implementation evidence.
