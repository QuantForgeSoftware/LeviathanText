# Authenticated direct-mutation attribution — 2026-11-05

## Reproduced impersonation

A generated HTTP regression created one actual Reviewer account and submitted two
accept decisions using the caller labels `specialist-A` and `specialist-B` against
one generated L1 revision. Both requests succeeded, and the review summary reported
one double-reviewed line. The baseline regression failed with expected zero versus
actual one. The account's authorization was valid; the attribution was forged.

Direct mutations now take identity from the authenticated `UserAccount.Id` placed
in `HttpContext.Items["user"]` by the existing middleware. The supplied display
label cannot select another author or reviewer. Local unauthenticated mode keeps
its existing caller-label behavior. Bootstrap authentication uses its existing
stable bootstrap identity; independent reviewers need independent accounts, not
different labels submitted with one shared credential.

## Bounded route coverage

- Normal and three-way-merged line revisions record the stable account as human author.
- Review decisions and their correction revisions record that account as reviewer/author.
- Direct annotation creation records the authenticated author; retraction records the
  authenticated reviewer. Local annotation author remains null as before.
- All ten direct geometry/audit paths bind the same actor: geometry, line/region
  orientation, add, split, merge, delete, restore, region order and line order.
- The existing AI bulk-accept controller binds the account as the editorial acceptor.
  Proposal/model provenance remains on the original proposal.

Source-document and CSV-import attribution, imported original markup, model/provider
authors, role policy and global-job ownership were not changed. Existing historical
caller-attributed records cannot be safely mapped back to accounts automatically;
this correction prevents new direct attribution forgery and does not certify earlier
double-review counts or scholarly independence.

## Verification and limits

The 31 generated attribution cases pass. One account submitting two names produces
one distinct reviewer, while two actual Reviewer accounts submitting the same label
remain distinct and produce a double-reviewed line. All route groups above have
authenticated and local controls, and the original `source-catalogue` revision author
remains intact. The AI controls explicitly seed the in-memory revision chain and make
no provider request; they test attribution rather than native-workspace AI persistence.

Combined attribution/auth/order/structural HTTP verification passed **51/51** with
zero source warnings, using isolated artifacts at
`/Users/johnbaima/Code/LeviathanText/.artifacts/midday-attribution`. `git diff --check`
passed. No training, live inference, source-data acquisition, rights change or Greek
specialist acceptance occurred.

## AI workspace gaps identified at the initial attribution-only snapshot

The browser's `ApiClient.acceptProposal` sends
`POST /api/v1/proposals/{id}/accept`, and `ProposalPanel.accept` calls that method.
The backend then exposed `proposals/bulk-accept` without the corresponding
single-accept route. This was an existing route mismatch, separate from the
attribution correction.

Code inspection also showed a singleton `RevisionChain` used by the proposal
controller, whereas normal transcription mutations use persisted native projects.
No native-project-to-chain hydration or durable acceptance adapter was present in
the API paths inspected. `AiProposalService` required `chain.Get(targetRevisionId)`
before calling the provider. A route-only addition would therefore not establish
working native-workspace proposal/acceptance persistence. This broader integration
needs its own generated entry-point tests, project authorization and persistence
contract before implementation; it is not established by the attribution tests.

Subsequent correction: the [native HTTP workflow](native-ai-http-2026-11-05.md)
now resolves and persists native issued proposals/L2 receipts, serves the browser's
single-accept route and returns JSON objects. The two bulk attribution controls were
updated to authoritative native issued records; the earlier in-memory-chain evidence
above remains scoped to the original attribution-only increment.
