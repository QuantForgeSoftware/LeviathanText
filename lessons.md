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

