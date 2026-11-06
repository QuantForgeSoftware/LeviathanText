# Native project AI persistence and span acceptance

Date: 2026-11-05. Instance A of the [parallel task brief](../tasks/native-ai-workspace-2026-11-05.md). Generated native text/geometry and a scripted provider only; no hosted requests, model loading, training, or Greek-specialist decisions.

## Service contract

`NativeAiProposalService` accepts an explicit native project snapshot and an `IProjectStore`:

- `Prepare` finds one unambiguous **current L1 diplomatic revision**, validates scalar offsets, and replaces incoming `SourceText` with the actual selected source span.
- The HTTP layer authorizes before dispatch, invokes its provider using `prepared.Request`, then reauthorizes/reloads the project before calling `Issue`. The service rechecks the exact prepared source and provider request graph before saving an immutable issued record.
- `GetIssued` reads and validates the authoritative record from that project's attachments. `Preview` accepts proposal IDs and optional supplied graphs, validates them without writes, and reports blockers. Supplied graphs must match the original issued graph exactly; returned accepted views do not replace the immutable issued record.
- `Accept` validates every item and preserves the proposal's exact scalar replacement hunk while merging against current L2. The remote editorial alignment must be unique under bounded unit-cost Levenshtein edits; unrelated uniquely mapped edits survive, while overlap or ambiguous alignment refuses acceptance. It never re-diffs the composed proposal in a way that can move the declared source span. At most one proposal per line and 128 proposals per batch are allowed.
- One optimistic `SaveProject` persists every new L2 revision, layer-link annotation, and receipt in the batch. A blocked item or failed version check leaves all candidate changes unsaved. No new SQLite schema, global proposal cache, or demonstration `RevisionChain` dependency is used.

The controller and authorization evidence is recorded separately in [Instance B's report](native-ai-http-2026-11-05.md).

## Durable authority and provenance

Issued object attachments use role **`ai-proposal.v1`**, and acceptance receipts use **`ai-proposal-acceptance.v1`**. Each has a required versioned JSON contract, UTF-8 byte cap of 2 MiB, and SHA-256 content checksum. Readers reject corrupt/oversized records, unsupported versions, ambiguous/duplicate IDs, ownership mismatches, altered source history, and mismatched supplied graphs.

An issued record contains project/object/surface/line identities, the immutable complete L1 revision, and the provider proposal including its exact selected source span. A receipt binds the issued-record checksum, source ID/text digest/range, task/provider/model/prompt/replacement, human acceptor and acceptance time, new L2 ID/parent/fingerprint, and layer-link annotation ID/fingerprint. Existing source attachments and L1 text/history remain unchanged. These checks provide persisted-record integrity within the trusted project store, not a cryptographic signature authenticating arbitrary externally forged projects.

Repeated acceptance returns the same receipt/revision and original acceptance attribution without another save. **This replay is bounded:** issued/source integrity must still hold and the original L1 revision must still be current. If L1 was superseded after prior acceptance, the retry refuses with `StaleAnnotationAnchor`; the historical receipt itself remains intact. Later L2 edits are not overwritten by a replay.

Native L2 head readers currently order by revision creation time. A new accepted revision follows a future-dated imported editorial head in that ordering, while the receipt retains actual acceptance time. A head at the maximum representable timestamp is explicitly refused rather than throwing an overflow or creating an invisible older head.

## Reversible layer policy and limits

- Non-restoration replacements require a nonempty L1 source span. Acceptance appends an L1-anchored `AlternativeReading` with exact range, replacement, human author, and `Description="choice/orig-reg"`. This denotes the explicit editorial choice without claiming a historical corrector. Task/model/acceptance provenance remains in the receipt.
- Restoration accepts only a zero-width L1 position when merged L2 equals the direct source insertion. It appends `SuppliedEditorial` on the exact inserted interval of the new L2 revision. It does not infer physical loss, missing-source geometry, or shifted offsets after other editorial changes.
- Empty non-restoration spans, nonempty restoration targets, and restorations requiring other L2 merges remain issued but acceptance reports `EditorialConflict` with an actionable explanation.
- Translation, structured extraction, lexical annotation, and morphological annotation are advisory; their issued records can be retained but cannot replace native line text.
- Existing L2-anchored annotations remain historical and are not guessed onto a new L2 revision. More complex dual-layer exports therefore still require complete mapping coverage or explicit loss reporting. Unrelated preserved editorial edits do not automatically become new scholarly annotations. The HTTP/export report proves the simple choice and exact restoration cases, rather than claiming universal lossless TEI export.
- Remote alignment is limited to 4,000,000 dynamic-programming cells, with all optimal paths counted up to two, including equal-character skip ties. Oversized or ambiguous remote mappings report `EditorialConflict`; the service does not guess from two extremal LCS paths. This can conservatively reject a genuinely unrelated edit when its source alignment is ambiguous. No existing L2, or L2 equal to source, always uses direct composition even on repeated text. L2 already equal to the composed result still requires unique alignment; exact prior-receipt replay remains the idempotent path. The existing generic full-text `ThreeWayMerge.Merge` and its UI/server semantics are unchanged.

`UnknownIssuedProposal` and `EditorialConflict` were added to the shared preview blocker vocabulary; pure legacy `RevisionChain` behavior and fixtures were otherwise unchanged.

## Verification

- **41 native service cases passed**: authoritative source injection discard, astral/combining/terminal scalar positions, current-source checks at issue/accept, advisory refusal, graph/checksum/source/version tampering, duplicates, same-line and multi-line batch rules, anchored editorial merge/conflict, single-save atomicity, concurrent-store conflict, preview immutability, reload/replay, revision/annotation fingerprints, issued/receipt byte bounds, and editorial chronology.
- **81 related application cases passed**, including native service, transcription and annotation suites. The complete pure Core suite passed **105 cases**, including 17 anchored-merge cases and the unchanged generic merge scenarios.
- The first service implementation passed 23 native persistence tests. Five additional reversible-layer tests then failed before the layer-link/policy correction: two missing annotations and three unproven mappings that had been accepted. They pass after correction. The original production-route baseline failures belong to Instance B's HTTP report; missing new service types are not presented as fail-before behavioral evidence.
- Independent span review exposed an additional real defect: with source `ααα` and proposal `[0,2)→α`, re-diffing the composed `αα` moved its deletion to the final source character. An outside edit `ααβ` falsely conflicted; an inside edit `βαα` was accepted incorrectly. Those two cases and three ambiguous remote insertion/deletion/equal-result cases all failed before the anchored correction and pass afterward. Repeated-text no-L2/source-equal controls remain accepted. The generated independent before receipt is retained at `.artifacts/reviews/native-ai-midday-2026-11-05/before-results.json` (disposable local evidence).
- `git diff --check` passed. Builds used isolated artifacts, one MSBuild process, and no compiler/analyzer warnings.

```sh
dotnet test tests/LeviathanText.Application.Tests/LeviathanText.Application.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:ArtifactsPath=/private/tmp/leviathan-native-ai-build --filter FullyQualifiedName~NativeAiProposalServiceTests
dotnet test tests/LeviathanText.Application.Tests/LeviathanText.Application.Tests.csproj --no-restore --no-build -p:ArtifactsPath=/private/tmp/leviathan-native-ai-build --filter 'FullyQualifiedName~NativeAiProposalServiceTests|FullyQualifiedName~TranscriptionServiceTests|FullyQualifiedName~Annotation'
dotnet test tests/LeviathanText.Core.Tests/LeviathanText.Core.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:ArtifactsPath=/private/tmp/leviathan-anchored-merge-build
```
