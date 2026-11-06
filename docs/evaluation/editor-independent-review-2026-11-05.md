# Independent editor lifecycle review — 2026-11-05

Review compared HEAD `4138126` with the current editor/proposal-refresh changes and their generated-observable tests. No additional high-confidence defect introduced by the new draft acknowledgement/rebase or ProposalPanel → LineEditor → SurfaceView → DualPane lifecycle was found. One pre-existing same-line annotation-read race was reproduced and corrected within the authorized follow-up.

## Finding and bounded correction

**P2: an older annotation-list response can replace a newer same-line list.** `forCurrentLine` prevents cross-line or destroyed-view callbacks but does not order reads within the same line generation. A review-triggered metadata refresh can start annotation read A, followed by a successful retraction starting read B. If B returns the current empty list and A then returns its older list, the withdrawn alternative reappears with an enabled **Accept as L2** action. A late failure of A can instead clear the newer B list. These are stale presentation/action failures; the read itself does not delete or restore server records.

The annotation helper has the same unversioned behavior in HEAD `4138126`, so this is **pre-existing**, not a regression attributed to the new UI work. A separate review-only spec reproduced both failures against the current pre-fix component: **two tests failed**, observing the resurrected alternative/action and the erased newer list. No separate historical browser build of HEAD was executed; the historical classification comes from comparing the unchanged helper and its call paths.

`LineEditor` now assigns an independent monotonically increasing annotation-read version. Only the newest read may update the annotation list, on either success or error. Existing line-generation cancellation remains active. This token does not change write acknowledgements, text refresh versions, pending L1 locks, recovery storage, review attribution or proposal acceptance. The new two-case regression lives in `web/src/app/editor-independent-review.spec.ts`; all 149 existing tests retain their prior expectations.

## Reviewed behavior and evidence

Inspection and the existing adversarial fixtures cover navigation away/back to the same line, late initial reads and mutation responses, destruction, text typed after submission, edits during reconciliation, merged acknowledgements, stale review/metadata reads during a pending write, failed acknowledgement reads, correction conflicts, undo/recovery, failed storage writes and explicit conflict-comparison choices. Revision/refresh identity checks prevent unrelated callbacks from unlocking pending L1 work. Recovery retains its ancestor in one authoritative envelope and requires comparison when that ancestor is unknown or overlapping.

The accepted/advisory saved-event chain preserves unsaved diplomatic drafts, refreshes the current sibling reading view and rejects old navigation-generation callbacks. DualPane cancels prior read subscriptions and checks both its request version and line identity. Native API validation/attribution remains the authority; browser fake observables do not establish server permission or provenance behavior independently.

Final verification used actual **Node 22.22.2** at `/Users/johnbaima/.hermes/node/bin/node`, Angular CLI and Chrome Headless 156:

- Focused new review regressions: **2/2 passed**, after **2/2 failed before** the correction.
- Full browser suite: **151/151 passed**, comprising the unchanged 149 tests plus the two review regressions.
- Production Angular build passed with no warnings; `git diff --check` passed for owned files.

Logs: `.artifacts/editor-independent-review-before.log`, `editor-independent-review-after.log`, `editor-independent-review-full.log` and `editor-independent-review-build.log`. Final component/spec copies, their SHA-256 hashes and the HEAD component source are retained under `.artifacts/editor-independent-review/`. The fix was applied after the integrator released its package/source freeze; frozen experiment/package files were not rewritten.

This is a finite independent review and generated-observable regression check, not an exhaustive network/scheduler proof or browser persistence guarantee. No real provider/model call, source-text adjudication, recognition, training or specialist review decision occurred.
