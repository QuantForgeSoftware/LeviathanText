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

## 13. A cross-layer text map must respect the target layer's claimed spans

Matching repeated text by context is not sufficient when the target contains another
annotation on the same occurrence: an L1-only unclear span in
`<del>ιͅ</del><unclear>ιͅ</unclear><del>ιͅ</del>` could be mapped onto the second deleted
span, displacing it and duplicating text on re-import. Treat target-layer annotation ranges
as occupied and report `annotation-mapping` instead. Fuzz combining marks and zero-width
spans, not only plain Greek strings. Added 2026-11-03.

## 14. Project access does not authorize arbitrary host-file access

Server-path imports and directory jobs need an administrator boundary before touching the
filesystem. An image export must validate the exact bounded bytes it copies, not the path
extension or an earlier read. Test both denied imports and authorized valid-image exports
through the API. Added 2026-11-04 after the bundle disclosure correction.

## 15. UI coordinates and asynchronous results belong to a source revision

Capture the line, revision and scalar range when requesting AI output; preserve that source
when saving. Render marks only on their anchored layer/revision. Reused editor components
must reset on input changes and reject late responses, including return-to-the-same-line
races. Test real API payload shapes and L1/L2 parent rules. Added 2026-11-04.

## 16. A responding port does not establish ownership of a service

Before a diagnostic script sends mutations, establish readiness from its own spawned
process and fresh listener announcement. Exercise occupied-port, stale-log and healthy
controls against disposable stores; clean up only the processes the script owns. Added
2026-11-04 after the worker-profiler correction.

## 17. A VLM model name does not add image evidence to a text-only request

The owner's VLM route is LM Studio (2026-11-04). Plan explicit image transport, pin the
model and matching vision projector, and verify that the installed runtime consumes images
before comparing Greek recognition. Keep inference benchmarking separate from training
authorization and preserve pending specialist decisions; no text-only fallback may be
reported as visual recognition.

## 18. Audit fitting inside tests before claiming a no-training verification run

An existing CLI alphabet test performs one synthetic epoch even though its name emphasizes
split integrity. Two VLM-session regression invocations excluded the diagnostics training
test but missed this second fitting test (2026-11-04). Search every invoked test's actual
entry points, not a truncated filename/name listing. Explicitly exclude all fitting tests
when training is out of scope, retain any accidental executions, and distinguish synthetic
fixture fits from collection/VLM training in the handoff. Never report an unqualified
"no training" result after a fitting test has run.

## 19. Admission and publication checks belong to the operation they protect

A quota check on a paged listing can undercount and races with insertion.
Enforce the count and admission in one database write. Export to a staged
file before replacing an existing result, and measure the completed archive.
Test late editor replies across every mutation and acknowledge submitted
snapshots. Review findings added 2026-11-05.

### 2026-11-05 fitting audit refinement

`LearnedLineDetectorTests` fits a row-logistic model on generated pages; it must also
be excluded from a no-fitting verification selection. The exclusion set therefore
covers six classes, including this non-TorchSharp trainer, rather than only the
five previously named neural/CLI fitting classes. Audit actual entry points again
when tests are added.

### 2026-11-05 pure utility selection and source-snapshot refinement

The mixed ML project may be selected only through an explicit inspected utility
class allowlist (currently LabelCodecTests and LabelCodecInvariantTests), each
with its own nonempty receipt. Never turn that exception into a project-wide run.
Keep retained C# baseline snapshots outside default Compile globs (for example
`.cs.txt`), or use explicit Compile includes. A shadowed source type can make a
green build or before/after comparison exercise the wrong implementation; inspect
compiler warnings and verify the actual referenced assembly. Discard and repeat
ambiguous harness receipts rather than attributing them to product behavior.

NuGet's signature-independent content hash differs from the raw signed nupkg ZIP
hash. Preserve those definitions separately; do not invent package-integrity
failures by comparing them. Notice collection is evidence, not signature
verification or a complete native-component license attestation.

### 2026-11-05 delegation and fitting-filter incident

Every delegated verification assignment must repeat the non-fitting command and
the six excluded classes explicitly. Saying “full Application/API suite” is unsafe:
both contain synthetic fitting fixtures outside the ML project. Use the audited
engineering entry point, or copy its exact exclusion filter for focused work.
Before reporting no fitting, inspect actual invocation filters and discovered tests,
not only data paths or passed totals. Record accidental generated fitting separately
from authorized corpus training and correct all blanket claims. Investigate a
default opt-in guard so a missed command-line filter cannot repeat this failure.
