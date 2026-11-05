# Durable job project membership — 2026-11-04

The shared-server policy scopes non-admin access to projects where the account is a member. Ordinary jobs are available to annotators and reviewers, and authenticated callers own their queued work. The local-only mode and administrator path retain operator access. [Operations policy](../operations.md#shared-deployment-opt-in) · [API job contract](../contracts/interchange-and-api.md#2-http-api-behavior-prose-contract).

## Disposable baseline

Two annotator accounts, one bootstrap administrator, three projects and a generated 120 × 80 capture were created in a disposable SQLite store. The project creator was the only non-admin member of the three target projects. A fresh loopback API served the requests, then a separate worker executed the durable jobs. A layout success was verified by reloading the project's persisted geometry.

| Request | Baseline |
| --- | --- |
| Member reads project | HTTP 200 |
| Non-member reads project or calls direct layout | HTTP 403 |
| Non-member reads project through uppercase route | HTTP 200 |
| Non-member queues surface layout | HTTP 200, succeeded, 1 region and 2 lines persisted |
| Non-member queues project layout | HTTP 200, succeeded, 1 region and 2 lines persisted |
| Member queues surface layout | HTTP 200, succeeded, 1 region and 2 lines persisted |

All layout targets began with zero regions and lines. The worker output and project store independently confirmed both unauthorized mutations. The first replay used a grayscale PNG and admitted both unauthorized jobs, but projection failed on the one-byte pixel layout. The RGB replay preserved the drawn geometry and confirmed the mutations. The two disposable processes exited normally and SQLite integrity passed.

On a separate no-target job, the outsider could list and read the member's job, cancel it and receive its record by submitting the same global idempotency key. These are distinct owner-visibility and control policy gaps.

## Correction scope

Use case-insensitive matching for the static API route segments. For a queued project mutation, resolve the target from the input that the worker will consume and apply the existing opt-in membership gate before persisting a job. The API's existing authentication and role middleware governs whether the caller may submit jobs. For a membership-authorized surface job, record the resolved project ID and have the worker load that exact project. Earlier jobs and unscoped operator requests retain their existing lookup. A missing bound project or missing surface in that project is a failed job rather than a lookup across projects.

Verify direct route variants, denied surface and project enqueues, authorized member/admin paths, membership-disabled/local behavior, and duplicate surface IDs in distinct projects. Replay the same RGB fixture after the change. Job visibility, cancellation ownership, shared idempotency-key policy and revocation of jobs admitted earlier remain separate decisions.

The local evidence is in `.artifacts/experiments/nt20-job-auth-baseline-v1/` and `.artifacts/experiments/nt20-job-auth-rgb-baseline-v1/`. Test credentials were ephemeral.

## Corrected replay

The candidate used the same registered RGB capture and request script. The non-member's uppercase project read and both unauthorized layout job submissions returned HTTP 403. Neither submission created a job record. Both protected targets retained zero regions and lines. The member's admitted surface job carried the correct bound project, succeeded and persisted one region and two lines. Both processes exited zero, and the SQLite integrity check passed.

The affected suites passed API 91, Worker 28 and Infrastructure 29. The API tests cover layout and recognition at both project and surface scope, pre-idempotency authorization, non-editor denial, administrator and member controls, and membership-disabled behavior. Worker tests verify that duplicate surface IDs in different projects do not redirect bound layout or recognition work, including when the bound project disappears. The API run excluded the existing layout fitting fixture.

The corrected replay and independent database reconciliation are under `.artifacts/experiments/nt20-job-auth-rgb-candidate-v1/`.

## Duplicate indirect IDs

A separate disposable test cloned the member's project JSON into a decoy project with the same surface ID and an earlier sort name. The decoy had no members. The member's explicit read of the decoy project returned HTTP 403. A direct surface layout call using the duplicated ID returned HTTP 200 and wrote one region and two lines to the decoy, leaving the member's project unchanged. The new bound worker job then correctly ran on the member's project.

The middleware's membership lookup scans SQLite rows, while the application service chooses the first containing project from a name-sorted list. An unqualified ID can therefore authorize one record and resolve to another. The proposed boundary is to require a unique project match for indirect routes in membership mode, deny ambiguity, and carry the resolved project through request service lookups. An explicit project URL identifies its own scope. The local test and persisted evidence are under `.artifacts/experiments/nt20-direct-membership-ambiguity-v1/`.

The corrected duplicate-ID replay denied the direct layout request with HTTP 403. Both projects still had zero regions and lines immediately afterward. The existing job that had been admitted and bound before the decoy appeared succeeded on its original project, and the decoy remained untouched. Requests that use a uniquely resolved indirect ID receive a request-scoped store bound to the authorized project, so later service resolution stays within that scope. Newly submitted unqualified surface jobs also require a unique project match. Explicit project URLs, admin behavior and unscoped operator routes retain their existing semantics.

The API ambiguity test also covers denied reads and edits, no job insertion, stable project versions, explicit owner access, a successful unique-ID request and HTTP 404 for an absent ID. API 92/92 and Infrastructure 29/29 passed with the fitting fixture excluded from API verification. The identical loopback replay, independently checked persistence and cleanup are under `.artifacts/experiments/nt20-direct-membership-candidate-v1/`.
