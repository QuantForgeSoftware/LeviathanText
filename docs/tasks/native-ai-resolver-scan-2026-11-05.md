# MID27: bounded proposal-target resolution

## Proposed change

Keep validation of batch size and duplicate IDs before any store access. For proposal batches only, stringify requested IDs once, scan candidate projects once and parse each issued-role attachment once per project. Retain at most two matching project IDs per requested proposal ID, never all project payloads or a persistent cache. Duplicate attachments within one project count as one project match. Explicit project selection restricts this same scan to the named project. `ForRevision` is unchanged.

After scanning, inspect results in original request order: missing target, per-ID ambiguity, current authorization and cross-project batch refusal retain their existing precedence. Load the one selected project for the returned native snapshot, bind the request store with the existing authorization policy, and preserve full authoritative attachment/source/graph checks in `NativeAiProposalService` and the transaction-time permission guard. No candidate JSON alone becomes acceptance authority.

This is a per-project scan, not a database-wide transaction snapshot. Each project is loaded at most once for matching; the selected project is reloaded once before use. Concurrent later edits remain subject to full native record validation and optimistic save conflicts. No cache crosses requests, so later revocation or project edits are read again.

## Before/after evidence

Register generated resolver-only cases before editing production: 10, 64 and 128 requested IDs, both one-project and several relevant/irrelevant-project stores. Use checksummed minimal candidate attachments, no source images, no provider or model calls. Record warmups, repeated elapsed samples and current-thread allocations. Preserve exact fixture seed/configuration and source snapshots/hashes. Performance assertions will not use unstable wall-clock thresholds; retain comparative receipts and report negative results if no useful improvement is demonstrated.

Add semantic controls for size/duplicate validation, missing targets, explicit scope, per-ID ambiguity, duplicate records in one project, cross-project batches and error precedence with denied membership. Run existing native HTTP fixtures unchanged to verify source/record validation, authorization, durable acceptance and atomic permission protections. No historical `NativeAiWorkspaceTests` edits are planned.
