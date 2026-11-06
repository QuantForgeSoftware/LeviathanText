# Additive authoritative principal discriminator

Status: isolated source proposal only. Root/R review and post-quiet regression execution are required before integration. Main Program, ApiClient and identity/draft storage have not been changed by this proposal. No build, test, provider, model or training workload ran while preparing it.

Artifacts: `.artifacts/experiments/me-principal-contract-v1/`. `source-review-before.json` pins the inspected main source. The combined patch and proposed regression files are prospective; they are not a claim of executed fail-before/after proof.

## Contract

`GET /api/v1/me` preserves its existing fields and adds explicit `mode` and nullable `accountId`:

| Current authority | authenticated | name | role | mode | accountId |
| --- | --- | --- | --- | --- | --- |
| Anonymous local, no bootstrap/users | false | local | admin | local | null |
| Actual configured bootstrap admission | true | bootstrap-admin | Admin | bootstrap | null |
| Current enabled persisted account | true | current stored name | current stored role | account | exact persisted account ID |

The distinction comes from a trusted marker placed in the actual middleware admission branch. Neither a name, role nor synthetic-looking ID establishes it. A real persisted account named `bootstrap-admin` can have exactly the same three legacy fields as the synthetic bootstrap principal, yet must be `mode=account` with its real persisted ID. No synthetic `usr-bootstrap`/`usr-local` identity is promoted into persisted-account continuity.

Existing clients ignore the additive fields. Proposed TypeScript fields are optional for old servers, whose missing/unknown/contradictory discriminator must remain unverified. IDs are compared exactly; names/roles are never a continuity substitute. The ID identifies the current store's persisted principal, not project membership, a deployment fingerprint, a key or proof of source-document ownership.

## Authority and cache behavior

- Set `Cache-Control: no-store, private, max-age=0`, `Pragma: no-cache`, past `Expires`, and credential-header `Vary` before authentication on the exact normalized `/api/v1/me` path, including supported case/trailing-slash aliases. Denied responses therefore receive the same policy. Remove validators on this response; do not introduce a304/profile-cache path.
- Re-read the current exact request credential/configuration/account at the endpoint and require the same trusted admission mode/account. A disabled/rotated account, changed bootstrap configuration, anonymous first-account activation or different current account must fail closed. Do not retrospectively upgrade an anonymous admission into a newly created account just because a header now matches.
- No new raw key, credential hash, key history or identity token is serialized/persisted. Existing transient `UserKeys.Hash` lookup and constant-time bootstrap comparison retain the current credential precedence. The marker is request-local; the endpoint adds no store write or project mutation. Existing middleware authentication audit behavior remains separate.
- The existing middleware captures bootstrap configuration at startup. Endpoint rechecking can reject an old admitted bootstrap key after configuration changes; this proposal does **not** claim to implement hot admission of a new bootstrap key without the existing deployment/restart policy. Persisted account key rotation already uses fresh store lookup and has actual HTTP regression source.
- Client reauthorization must be a new network read, never `cachedRequest` or a stored profile. Proposed ApiClient headers explicitly request no-cache/no-store. The response must also belong to the current credential/request generation before any caller uses it. A plain network request does not by itself fence an old response; the later workspace transition must implement that guard.
- `/me` is not project authorization. After identity verification, obtain the selected project/membership/role through a fresh authorized project read before resuming editing. Offline/401/403/missing discriminator is unresolved access, not continuity approval.

Local/bootstrap continuity deliberately remains undefined/quarantined. This seam neither clears drafts nor reassigns ownerless legacy drafts, recovers geometry, suspends existing components or grants the new principal the old account's work. The separate [workspace review](../evaluation/workspace-credential-review-2026-11-05.md) retains those requirements.

## Proposed sources and ownership

- `runtime/PrincipalDescriptor.cs`: request-local admission enum/marker, additive DTO, idempotent no-store headers, fresh current-principal check and endpoint result.
- `program.patch`: only early `/me` headers, markers in the existing three admission branches and the existing `/me` handler replacement. It does not alter role/project policy, bootstrap admission precedence, other routes or NativeAI behavior.
- `api-client.patch` plus client type/guard: additive optional reply typing and explicit fresh HTTP cache directives. No existing cache helper, auth interceptor, recovery storage or view lifecycle is changed.
- `tests/MePrincipalDescriptorHttpTests.cs`: generated in-memory actual-host HTTP source for legacy field preservation, all four account roles, synthetic-vs-real same-name distinction, distinct-store same-name IDs, rotated/disabled/first-account denial and no-store headers/aliases. Within one store `users.name` is UNIQUE, so the same-name continuity control uses two genuinely separate stores rather than inventing an invalid fixture.
- `tests/MePrincipalDescriptorFreshnessTests.cs`: deterministic admission→current-store/config changes using the internal helper and real LocalStore; no sleep/race timing or provider.
- Client pure discriminator/transport spec sources: unknown/legacy/local/bootstrap quarantine, exact distinct IDs and repeated network-only `/me` with authoritative denial. They do not claim whole-workspace suspension or cryptographic identity authenticity.

Root owns integration and current Program/ApiClient. R owns independent static review. No new dependencies or schema changes are proposed. To establish actual fail-before, the HTTP-only regression class can compile against the baseline API without the new helper; preserve those failures before running the wired candidate. The direct helper tests require the new source and must not be mislabeled as baseline execution.

## Acceptance before integration/release

Review exact markers/header ordering and current credential semantics, then run the generated HTTP/helper/client controls after the active NativeAI full24 quiet window releases. Use the engineering entrypoint or exact six fitting exclusions; no whole ML/Pilot run. Verify every old field and auth-denial behavior remains compatible, current account ID survives account-key rotation, known local/bootstrap never become persisted IDs, and no key/hash/private path leaks into JSON/cache metadata. Run the existing API auth/role and full frontend suites and the central no-fitting gate after any main integration. Preserve all negative evidence and source hashes.

Compliant browser/proxy no-store behavior is the requested cache contract; it is not a claim against a malicious intermediary or compromised same-origin script. Workspace/draft ownership and the ongoing SEC prototype gates remain independent.

## Main integration checkpoint

Root integrated the reviewed v2 discriminator and no-store handling. All13 original HTTP regressions failed before (missing required cache policy); all20 HTTP/freshness cases pass after, and main engineering-v5 passes2024 with18 intentional skips and zero warnings/errors. The client adds an optional explicit captured credential parameter; omitted callers retain existing behavior. Current full frontend363 and production build pass. Three transport controls prove repeated fresh reads, authoritative denial and explicit account/anonymous header pinning. A current response generation and fresh project access remain the future coordinator's responsibility; mounted workspace/draft ownership is not fixed by this seam. Source contract artifacts remain historical, immutable proposals.
