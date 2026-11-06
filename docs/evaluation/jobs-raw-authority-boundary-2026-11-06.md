# Raw Jobs read authority boundary

Five component regressions pass after the bounded integration fix. Four had failed against the original current product: current job-list and selected-job snapshot responses with HTTP 401 or 403 cleared only the Jobs panel, leaving another freshly authorized workspace active under an unchanged shared authority ticket. A fifth control already passed: delayed A list 401 and snapshot 403 errors could not invalidate a newly admitted B workspace.

The tests use controlled RxJS Subjects, the actual JobsPanel, a real WorkspaceAccessCoordinator and actual shared cache-authority storage. They check synchronous suspension before the error notification returns, authority-ticket rotation, cleared job rows and no automatic pre-mutation read. The stale test deliberately changes raw key storage without its ordinary notification, retaining actual A subscriptions long enough to exercise each callback's current-scope check. It is not an HTTP/header transport proof; R's independent scheduling-interceptor tests establish that separate captured-request boundary.

`web/src/app/jobs-raw-authority-boundary.spec.ts` is unchanged between its executed before and after versions: SHA-256 `2fbc5ac294146e83eacf1b64549f3fd4f419f0364152dfd1f2df3393c835a392`. Evidence lives under `.artifacts/experiments/jobs-raw-authority-boundary-v1/`:

- `before-v2.log`: 5 executed, 4 intended behavioral failures and 1 unchanged stale-response control.
- `after.log`: 5/5 pass against the integration owner's current-read shared-invalidation fix.
- `executed-before.spec.ts`, before/after outcome JSON and source freezes preserve the exact comparison.

The first `before.log` is compile-only, not a product failure: a cleanup-array generic-variance declaration was corrected to a structural destroy-only type. No case, input or assertion changed. Source and the compile receipt remain preserved.

This closes the tested raw read-denial gap, complementing the header-stream 401/403 fence. Missing-job 404 remains scope-local. Root accepted the separate enqueue-role distinction: current POST 401 fences shared authority, while POST 403 stays local because a Reader can legitimately receive a write-role refusal and retain read access. R owns two additional controls for this policy and the actual captured-header scheduling proof. Whole-suite/build and any new browser closure are integration gates, not inferred from these five component tests. No real provider, model, corpus, fitting or training operation was used, and all owned Karma processes are closed.

## Integrated frontend gate

R's separate 10 transport/policy cases and these 5 read-denial cases pass. The whole current frontend suite passes 639/639. The initial whole invocation had 638 passes and one obsolete spy-arity failure: the old selected-job test expected only `job-old`, while the now-pinned call includes `job-old`, the exact captured empty key and a current-scope predicate. Its original source/log remain. The expectation now requires all three arguments and additionally checks the predicate is currently true; the existing job-capacity/state assertions remain unchanged.

Production build passes with the existing warning: initial 506,787 bytes (main 472,202 + polyfills 34,585; styles 0), 6,787 bytes over the unchanged 500,000-byte warning threshold. No lazy-stream or idle-panel proposal was adopted. Frozen copies comprise 114 source/configuration files and 5 dist files: `final-source-dist-freeze.json` SHA-256 `cd21630d5f6417d7ad332e215f55e16d8c7a484c4a3c41292c4a68934a079a7d`, with assets in `dist-final/`. The new actual browser replay remains a separately registered integration check; the older 624-source browser proof stays historical. All owned Karma/build processes exited before the next coordinated metric window.
