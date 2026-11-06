# Native AI HTTP and browser workflow — 2026-11-05

## Reproduced boundary and resulting workflow

A generated test created a project and imported invented TEI text plus a generated
PNG through the actual HTTP endpoints. Requesting a proposal for its persisted L1
revision returned HTTP409 instead of 200 before the correction. The old controller
resolved only a separate demonstration `RevisionChain`. Its serialized-string
response and absent browser single-accept URL were additional inspected contract
gaps; the end-to-end regression now exercises all three corrected boundaries.

Production proposal routes use the native persisted project service. The controller
prepares the scalar source on the server, calls the configured provider with that
prepared request, reloads the current authorized project after the await, and issues
the returned proposal as a checksummed native attachment. Incoming `SourceText` is
not evidence. A source changed during provider work prevents issuance. Provider
configuration errors retain their existing error; provider failures remain a
`{failure:...}` result rather than a partial proposal.

`POST /api/v1/proposals` returns an actual proposal JSON object. The browser's
`POST /api/v1/proposals/{id}/accept` now exists and checks route/body identity,
authoritative issued graph and current source before native acceptance. Bulk
preview/accept use the same native records. Batches belong to one project, reject
duplicate IDs, and commit accepted L2/annotation/receipt records in one optimistic
save. Repeating an accepted request returns its existing receipt/revision without
another save. No production route falls back to demonstration-chain authority.

The service's exact acceptance/layer policy is independently tested in
`NativeAiProposalServiceTests`: nonempty supported reading edits carry a reversible
L1 choice/orig-reg link, and bounded zero-width restoration carries supplied text
on L2. Task/provider/source/acceptor provenance lives in the receipt. These are
explicit human acceptance operations, not Greek-specialist approval or model claims.

## Resolution and authorization contract

Revision and issued-proposal IDs are resolved in persisted native projects. Multiple
matching projects are refused instead of selecting the first. The optional
`projectId` query on proposal creation and `projectId` body member on single/bulk
acceptance/preview can disambiguate an explicitly chosen project. An explicit scope
does not fall back to other projects if its target is absent. Supplied graphs are
checked against authoritative issued records after project authorization.

Before provider dispatch, after the provider await, and before response disclosure,
the helper checks the enabled current account, mutating role and configured project
membership. Local-only mode remains available only while no configured credential
or user account activates authentication. Bootstrap/admin bypass semantics remain
explicit; ordinary enabled accounts still need membership when configured. Removing
membership or disabling an account while the fake provider is pending returns 403,
with no proposal attachment or provider text in the response. Authenticated acceptors
use stable account IDs; local labels and original imported source provenance remain.

Native AI writes additionally bind the account/membership requirement to the concrete
request store. `SaveProjectAuthorized` checks the current enabled role, membership,
or anonymous no-users state inside the same nondeferred SQLite write transaction as
the optimistic project update. A revocation committed before that transaction prevents
the native write. Persisted admins still need an enabled account; only the explicit
bootstrap path uses the existing unguarded binding. Ordinary legacy `IProjectStore`
callers and other endpoint semantics are unchanged.

The independent pre-fix controller/store proof returned 403 after revocation at the
save forwarding boundary yet advanced the issue project from version1→2 and the
acceptance project from2→3, persisting the issued record or L2/receipt. Frozen
before evidence is under `.artifacts/reviews/native-ai-midday-2026-11-05/`.
Five actual HTTP interceptor cases now reach that boundary exactly once and refuse
issuance, single/bulk acceptance, disabled persisted-admin writes and anonymous writes
after account activation, retaining the exact native document/version/evidence state.

This atomic boundary covers SQLite authorization state and the native project save,
not global configuration, provider dispatch or network delivery. A revocation that
commits after an authorized save but before the final response check can still yield
403 after a legitimate save; 403 alone is not a universal no-write receipt. Replays
remain idempotent. Previously dispatched input cannot be recalled from a provider.
Preview responses also recheck current access after ledger/merge work.

## Generated evidence

Nineteen native HTTP scenarios cover:

- Native create/import → source-only request → actual JSON → single accept → restart
  and idempotent replay → diplomatic/CSV/native-bundle export. L1 and original source
  attachments remain exact; the accepted span changes only the expected L2 text.
- Simple choice and zero-width restoration reconstruct both L1 and L2 through TEI.
  An unrelated existing unlinked L2 prefix survives acceptance and produces an explicit
  `diplomatic-layer` export loss instead of a false lossless claim.
- Tampered graph, route/body mismatch, unknown issued ID, stale source, advisory task,
  cross-project/duplicate batch, ambiguity and outsider refusal.
- Atomic two-line batch, authenticated acceptor identity, revocation/disable during
  provider work, in-flight source change, unknown provider configuration, and refusal
  to treat demonstration-chain records as native authority.
- Commit-time revocation for issuance/single/bulk, persisted-admin disabling and
  anonymous user activation immediately before the actual guarded write.

Provider execution uses an in-process `HttpMessageHandler` double. It records prompts
and returns invented text; it never contacts LM Studio, a hosted service or a model.
The browser contract test uses the actual `ApiClient` and `ProposalPanel` with HTTP
testing to consume the object shape and submit the supported acceptance URL.

Results:

- Native HTTP plus updated attribution cases: **50/50 passed**.
- Combined native/authentication/attribution/order/structural/request-scope and native
  unknown-provider contract checks: **75/75 passed**, zero source warnings.
- Browser proposal/contract tests: **10/10 passed**; complete Angular: **140/140 passed**.
- `git diff --check` passed for owned API/client/test files.

C# commands used isolated output at
`/Users/johnbaima/Code/LeviathanText/.artifacts/midday-native-ai`, `-m:1` and
`-p:UseSharedCompilation=false`. Browser checks used headless Chrome. Earlier API
attribution counts describe the preceding source snapshot; the two bulk attribution
controls now use native issued proposals rather than a seeded demonstration chain.
No model fitting, live inference, collection acquisition, rights changes or scholarly
acceptance occurred. Root owns final integrated build, package and release acceptance.

## Remaining limits

Proposal/receipt attachments retain the bounded inline attachment size contract.
There is no new distributed scheduler, global job ownership policy, SSO or external
provider assurance. Ordinary browser IDs are unique; the optional explicit-project
client argument supports API disambiguation without introducing a new UI selector.
Actual provider capability, natural Greek performance, correction effort and eligible
reviewed benchmark releases remain their separate gates. Complex or conflicting
editorial/restoration contexts are refused rather than inferred or flattened.
