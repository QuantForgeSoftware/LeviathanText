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
