# Review corrections — 2026-11-04

The owner authorized fixing all findings from the latest November 3 development batch,
`a1e5a90..6c9265f`. The working tree was clean when the review began. The seven original
findings are recorded in the [review report](../../.artifacts/review-2026-11-04/review.md).
Cross-review found two additional pre-existing editor defects in the touched workflow;
this correction includes those as well. Candidate changes are uncommitted over
`6c9265f6fb82f3472c9d7faa013e79c35fe6e155`.

## Dispositions

| Finding | Correction and regression coverage |
| --- | --- |
| 1. Raw server files could escape through image bundles | Shared-server imports from host paths and `import.dir` jobs now require an administrator. Bundle copying validates the exact byte-bounded snapshot as an image before writing the original bytes. Non-images and truncated images are omitted with a ledger entry. API tests cover all seven path-import routes, case/trailing-slash variations, annotator/reviewer denial, ordinary jobs, admin ingestion and a project user's valid-image export. Existing membership boundaries remain enforced. |
| 2. Advisory saves silently changed revision/range | The proposal panel retains the requested line, revision and scalar range, verifies the response source, and submits an explicit anchor with the original range. Changed inputs and late responses invalidate the proposal; 409 requires a fresh proposal. Component tests cover stale/current sources, wrong ranges, unchanged payload text and duplicate-save protection. |
| 3. Annotations rendered against another revision/layer | The editor paints only annotations anchored to its displayed saved L1 revision and suppresses marks on changed drafts. DualPane filters word boundaries separately for current L1 and L2; navigation clears/cancels old loads. Regression cases include supplied text, historical marks and distinct L1/L2 coordinates. |
| 4. Worker profiling could mutate a different API | Readiness comes from the owned child process's fresh listener announcement and liveness, before any HTTP request. Fixed ports and `PORT=0` are supported. Disposable-process tests cover occupied ports, stale logs, wrong announcements, healthy fixed/dynamic listeners and cleanup. A real one-page Debug API/worker control also succeeded. |
| 5. Edition HTML dropped advisory payloads | Translation, structured extraction, lexical and morphological entries render their saved `AlternativeText`, with HTML escaping. Four payload cases assert the actual content survives and active markup does not. |
| 6. Standalone editions used broken filesystem image URLs | Local captures become embedded PNG previews from bounded snapshots; missing/corrupt/oversize sources produce loss entries. API transport tests decode the returned image. Local PNG/TIFF, downsampling, project-wide budgets, remote references and relative bundle links have controls. Bundle HTML explicitly omits images when they were not included. |
| 7. Project HTML export unexpectedly required `--all` | CLI routing accepts `export edition-html <project> --out <file>` as documented; both optional and explicit `--all` forms are exercised through the command entry point. |
| Additional: reused editor retained another line's state | The editor resets/reloads when its line input changes, cancels outstanding subscriptions and guards callbacks by line and generation, including navigation away and back. Tests cover late loads/mutations, chained writes, destruction and draft isolation. |
| Additional: accepting an alternative used an L1 parent for L2 | Alternative acceptance uses the current L2 chain's head, or no parent when L2 does not yet exist. Tests cover both states and preserve the L1 annotation anchor. |

## Evidence before and after

New regressions were executed before the corresponding production fixes; failures were
behavioral assertions, not compilation failures. Later controls and broader coverage were
added separately, so focused totals need not match the initial baseline totals.

| Scope | Baseline failure evidence | Candidate evidence |
| --- | --- | --- |
| Bundle | 2 failed / 3 controls passed | 5 focused cases passed; included in full Application suite |
| Filesystem API | 9 failed / 1 control passed | 11 new cases passed, including the later route variation and portable-HTML control; existing role/membership suites also pass |
| Edition | 12 failed / 3 controls passed | 15 passed; [receipt](../../.artifacts/review-fixes-2026-11-04/edition/receipt.json) |
| Original UI findings | 11 failed / 13 controls passed | 24 focused cases and initial full suite 67/67 passed; [handoff](../../.artifacts/review-fixes-2026-11-04/ui/handoff.md) |
| CLI | Optional-form regression failed; explicit `--all` control passed | 5 focused TEI command cases passed, then full CLI suite 37/37 |
| Worker profiler | Occupied-port regression recorded seven requests to the other fixture service, including two POSTs | All four script cases passed; real `PORT=0` control imported 1/1 page with job status `succeeded` |
| Editor follow-up | 12 failed / 14 controls passed on the pre-follow-up candidate | 26 focused cases and full suite 78/78 passed; [handoff](../../.artifacts/review-fixes-2026-11-04/editor-followup/handoff.md) |

Root evidence is under `.artifacts/review-fixes-2026-11-04/root/`; the other scopes use
`ui/`, `edition/`, `cli-script/` and `editor-followup/` beneath the same artifact directory.
These local ignored artifacts retain logs, TRX and the synthetic fixtures/results. The
[verification receipt](../../.artifacts/review-fixes-2026-11-04/root/receipt.json) records
candidate source hashes, environment, exact commands and result hashes.

The old bundle control called five arbitrary bytes an image; it now generates a real PNG
and still checks byte-for-byte export. Existing role tests now explicitly assert that
annotators cannot import server paths and use administrator ingestion before their original
membership/review assertions. The old editor alternative test expected the erroneous L1
parent; its corrected expectation failed before the fix. These test changes follow the
corrected contracts, rather than suppressing valid failures.

The ImageSharp fallback now loads only the first frame, which is the single bitmap this API
already returns. A multi-frame TIFF control checks unchanged first-frame pixels, full and
bounded dimensions, and whole-file source checksum. This is a compatibility control, not
a fail-first resource benchmark or a claim that total process memory is bounded by preview
size.

## Integration verification

Release solution build: **0 warnings, 0 errors**. Fourteen full or explicitly filtered .NET
runs passed **917 tests**, with zero failures and one opt-in live-Ollama test skipped:

| Suite | Passed | Scope |
| --- | ---: | --- |
| Application | 234 | Full |
| API | 83 | Full |
| CLI | 37 | Full |
| Imaging | 35 | Full |
| Formats | 101 | Full |
| Contracts | 42 | Full |
| Worker | 20 | Full |
| Core | 88 | Full |
| Infrastructure | 29 | Full |
| Evaluation | 39 | Full |
| AI | 88 | Full; one live-provider test skipped because opt-in was unset |
| Layout | 43 | Full |
| ML preprocessing/data transforms | 67 | Filter in `root/final/additional-commands.json` |
| Pilot intake | 11 | Existing P01, inscription, IIIF and manifest-audit cases; no training |

The final Angular suite passed **78/78**, and its production build completed without
warnings (390.26 kB initial bundle). All **four** worker-script regression cases passed.
Independent review of the bundle/API/edition corrections and the final editor follow-up
found no further defect. Root verified the API payload/parent contracts and final source
hashes; local documentation links and `git diff --check` also pass.

Reproduction commands (run from the repository root unless specified):

```bash
dotnet build LeviathanText.slnx --no-restore -m:1 -p:UseSharedCompilation=false -c Release
dotnet test tests/LeviathanText.Application.Tests/LeviathanText.Application.Tests.csproj -c Release --no-build --no-restore
python3 -B -m unittest discover -s scripts/tests -p 'test_profile_worker_import.py' -v
```

Run each remaining full .NET suite with the same `dotnet test` options; exact suite paths,
filters, logger and result-directory arguments are in the receipt's linked command files.
From `web/`, use `npm test -- --watch=false --browsers=ChromeHeadless` and `npm run build`.
Chrome and testhost needed the normal approved unsandboxed execution path on this host.
The initial sandboxed Angular production builds in both UI passes exited 134 without
diagnostics; each identical approved rerun passed. Those failed invocations remain in the
UI and editor-follow-up evidence.

## Operational changes and limits

- Shared-server operators must use an administrator for host-path imports and directory
  import jobs. Data-only line-text import and ordinary project work retain their permissions.
  Local-only/CLI operator access remains available. See [operations](../operations.md).
- Edition inputs and embedded PNG bytes each have independent 25 MiB per-image and 250 MiB
  project budgets. Previews are at most 4 million pixels and report downsampling/omissions;
  original images remain authoritative. Full temporary PNG/TIFF first-frame decoding can
  still occur within the 12,000-pixel dimension cap. Remote HTTP(S) references require access
  to their external server when the exported page is opened.
- Security probes used invented files and disposable stores. No private-file probe, existing
  user-store repair, training, locked acceptance scoring, model promotion, deployment,
  publication or commit was performed. Existing P46 specialist and resource gates stay open.
- Recognition algorithms, versioned preprocessing, models and scholarly labels were not
  changed. The shared decoder's first-frame compatibility was checked; costly retraining
  and full held-out corpus-quality studies were not rerun, and no recognition-quality or
  throughput improvement is claimed.
