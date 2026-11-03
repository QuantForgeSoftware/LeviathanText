# Lessons

Corrections from the user, converted into rules for future sessions.

1. **Deadline = wall-clock, not subjective effort.** When the user gives a stop time
   (`until 11 AM CDT`), check the machine clock (`date`) at the start and periodically;
   do not estimate elapsed time from how much work was done. State the timezone explicitly
   when confirming the stop. (Correction: 2026-10-19 — reported stopping at 11 AM when the
   clock was 08:01 CDT.)
2. **Do not announce completion before verifying the clock against the goal.**
   Finish with a real `date` check, not an assumed elapsed duration.
3. **Shared worktree: stage explicit paths, never `git add -A`.** Two instances share one
   working tree; `-A` swept another instance's in-flight files into the wrong commit
   (2026-10-19, `51eb654`). Prefer separate worktrees; otherwise stage only owned paths.


4. **Cycle-end archiving (standing instruction, 2026-10-19).** At the end of every work
   cycle, move completed checklist items from `todo.md` to `completed.md` (verbatim, grouped
   under the original section headings); leave open/in-progress items in `todo.md` and plan
   narrative/reference records (tables, registers, decisions, estimates) in `project-plan.md`.
   Add a section note where a section's items are all archived, refresh `completed.md`'s build
   date/item count, and commit both files together. "Cycle" = a closed work session or
   assignment handoff.

## 5. Imported document ids are not globally unique
PAGE/ALTO/hOCR ids (`ln1`, `line_…`) repeat across documents. Namespace them per import
(`ln1-<capture token>`) or globally addressed endpoints can act on the wrong project. Added
2026-10-19 after a real cross-project mutation was reproduced in a test.

## 6. CPU training is not bit-reproducible across processes
Identical seed/config can yield materially different CER (control range 0.878–0.953 for the
same code). Report seed spreads for end-to-end claims, not a single favorable run.

## 7. Dev-selected rules must be validated on out-of-domain held-out pages
A merge rule that improved M02a dev collapsed P01 (precision 0). Keep domain-split reporting
so selection overfitting is visible.

## 8. Treat analyzer warnings as defects
`dotnet build` warnings (e.g. xUnit2031) count against the release gate; fix at the source
rather than suppressing, and confirm `0 Warning(s)` before closing a cycle.

## 9. Validate a resource counter before treating it as a lifetime oracle

TorchSharp0.107 `Tensor.TotalCount` can retain a counter increment when a raw tensor's
handle moves into `nn.Parameter`, even after both handles are empty. Model-construction
loops therefore need validated disposal-scope lifetime statistics and explicit handle/
ownership checks, not a zero-`TotalCount` requirement or a tolerated leak allowance.
Preserve the failed exploratory measurements and independently reproduce the counter
control. Fixed-model relative deltas and actual OS RSS are distinct evidence; neither
proves universal native-memory behavior. Added2026-11-02 after the failed-model-load audit.

## 10. Learnability tests must not assert near-perfect single-run accuracy
CPU scheduling/thread variance produced an intermittent >2% CER failure in a synthetic
training test that otherwise passed on repeated runs. Assert loss improvement plus a loose
learnability ceiling (or a multi-seed/repeated protocol), not a precision threshold that
turns normal training variance into a red build. Added 2026-11-02.

## 11. Reproduce migrations and trust boundaries through their actual entry points

Migration tests must construct the old table shape, not just lower a current database's
schema marker. Concurrency tests must include the initially empty revision state and bases
from other lines/layers. Registry security tests must exercise the API/CLI transport: an
initial host check does not cover redirects, and an archive-member check does not constrain
an unsafe package-directory name. Preserve whole-response timeouts when switching to
header-only HTTP completion. Added 2026-11-03 after the review corrections.

## 12. TEI text equality does not establish annotation-coordinate equality

Test nested/equal ranges, inline boundary spaces, zero-width markers, repeated substrings,
and explicit abbreviation/editorial spans even when complete L1/L2 strings are equal.
Retain source IDs as metadata while minting unique native IDs per import; remap every
revision/annotation reference together. Report ambiguous mappings instead of assigning
them to the first matching letters. Added 2026-11-03.
