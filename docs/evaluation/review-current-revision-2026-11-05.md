# Review summaries follow the current revision

Date: 2026-11-05. Generated in-memory fixtures only; no Greek specialist decisions were supplied or inferred.

`ReviewService.Summarize` previously collected every decision on a line across all revisions. Editing an accepted or disputed line could therefore leave its replacement text accepted, double-reviewed, or awaiting adjudication based on obsolete text. Adjudication CSV mixed those historical notes with the current text.

Review decision counts, distinct reviewers, outcomes, latest outcome, acceptance, dispute, double review, and adjudication now use only decisions anchored to the line's current `DiplomaticRevisionId`. A new revision starts unreviewed even if its text is byte-for-byte identical. The CSV uses the same revision filter and deterministic decision ordering. The append-only surface ledger and revision history remain intact.

`CorrectedLines` separately describes current-text provenance: a `Correct` decision must name the current revision as its `CorrectedRevisionId`. Creating that revision does not count as reviewing its new text. A subsequent acceptance can make the same line both corrected and accepted; these counts overlap intentionally. Editing to a later revision resets that correction provenance count unless the later revision itself is the recorded correction target. Historical corrections remain in the ledger.

Evidence:

- Before the fix, four new regression cases failed: stale acceptance, stale disagreement, mixed historical adjudication CSV, and correction counted as review of new text. Six existing/control cases passed.
- After the fix, all **10 ReviewService tests passed**. They include multiple decisions by one reviewer counting as two decisions and one reviewer, without double-review status.
- The related queue, packet, import, and service selection passed **18 tests**.
- `git diff --check` passed.

```sh
dotnet test tests/LeviathanText.Application.Tests/LeviathanText.Application.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:ArtifactsPath=/private/tmp/leviathan-streams-artifacts --filter FullyQualifiedName~ReviewServiceTests
dotnet test tests/LeviathanText.Application.Tests/LeviathanText.Application.Tests.csproj --no-restore --no-build -p:ArtifactsPath=/private/tmp/leviathan-streams-artifacts --filter FullyQualifiedName~Review
```

This change does not implement adjudication resolution, reviewer supersession, edit locking, or annotation concurrency. Different decisions on the same current revision keep the existing disagreement semantics; the fix prevents decisions from a different revision from influencing those semantics.
