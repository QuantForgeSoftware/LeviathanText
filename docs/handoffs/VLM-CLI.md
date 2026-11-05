# VLM benchmark CLI handoff — 2026-11-04

Owner: CLI agent. Implemented only `src/LeviathanText.Cli/VlmBenchmarkCommand.cs` and
`tests/LeviathanText.Cli.Tests/VlmBenchmarkCommandTests.cs`, plus the later
`VlmCancellationProcessTests.cs` regression; root retains command routing,
project references, shared plans and live execution. No model service was called by this
agent. Tests use generated PNGs and a mocked HTTP handler; no natural labels were opened,
no project database was created, and no model was downloaded or trained.

The command surface remains `benchmark vlm-validate`, `benchmark vlm`, and
`benchmark vlm-score`. Runs require fresh output directories and scoring requires a fresh
report path. Input manifests reject unknown fields, keeping labels separate. V1 sends only
the image and fixed instructions; V2 sends the same image plus the baseline string captured
in the frozen input snapshot. Mutating the original manifest cannot change later requests.
Every request rechecks the crop hash. This freezes the submitted baseline; its provenance as
an actual R0 output remains the responsibility of the separate R0 receipt.

New run receipts retain the original absolute input-manifest path and image base. The exact
input bytes remain in `input-manifest.json`; a separately hashed `resolved-input-manifest.json`
uses absolute image paths for replay. Optional `sourceEvidenceBase` is a bounded absolute
filesystem directory, defaulting to the original manifest directory, and is preserved in the
resolved manifest and run receipt. Relative rights/geometry references keep that base;
remote/symbolic references and JSON-pointer fragments remain unchanged. These references
are not opened or fetched. Replaying from the resolved manifest validates the original image
bytes and preserves the provenance base across a second output directory.

An additive `producerAssemblies` array records the name, assembly version and SHA-256 of
the actual loaded CLI, AI, Evaluation and ImageSharp assembly files. The existing
`codeAssemblySha256` remains the CLI hash for compatibility. This distinguishes changes
such as the AI empty-SSE parser correction even when the CLI binary is unchanged. Older
receipts remain readable; their missing dependency identity requires the integrator's
external artifact map, not an inferred current dependency version.

Scoring now validates the frozen input-manifest SHA, dataset/protocol identity, exact
sample/physical-unit/material reference cohort, and a complete mode-by-repetition plan
(1–4000 requests, 1–3 repetitions). Duplicate, unknown, changed-image and changed-identity
rows are rejected before creating a score report. Retained transport receipts must match
the outer row's model, prompt version, text and status. The frozen system-prompt SHA and
canonical `[system,user]` prompt SHA are checked with exactly the transport's Unicode JSON
encoder, including Greek and the uncertainty marker. A changed V2 hypothesis cannot borrow
another request's receipt. Every captured text and attempted model result needs its receipt;
empty local input rejection and unattempted rows remain supported. Unknown statuses are errors.

`Completed` means transport-protocol completion, not compliance with diplomatic-transcription
instructions or image grounding. Run, summary and score receipts now say
`completionScope=transport-protocol-only` and `contentFormatAndGroundingReview=pending`.
Completed prose/markdown is not stripped or silently marked scholarly-valid; its full text
contributes to CER. Existing raw runs without these new descriptive fields remain readable.

Cancelled and unattempted requests stay in the denominator. Empty `NotAttempted` row text
is adapted to the scorer's null representation; forged nonempty unattempted text is
rejected. Missing rows, including an absent results file after an interrupted start, are
scored as missing output. Failed/truncated nonempty model text is scored in full. Score
receipts record planned/recorded/missing counts and SHA-256 values for the references,
frozen input, run manifest and exact results bytes read.

JSONL parsing bounds bytes before accumulating a row. The 24 MiB per-row limit allows the
provider's <=1 MiB wire response to appear as raw wire plus two text copies even under
sixfold JSON escaping. At most 4000 rows are accepted. The cumulative retained hypothesis
text is capped at 64 Mi UTF-16 code units (128 MiB of character storage, excluding object
and parser overhead), checked before retaining each row. The whole score is rejected with
an explicit error before the output file is created; no row or text is trimmed. The retained
list contains canonical plan identity plus only scoring status/text, so wire bodies, Error
and repeated source/review strings are not accumulated. This is a retained-text bound, not
a universal process-memory or scoring-runtime guarantee; the scorer also owns edit-work limits.

Evidence is under `.artifacts/vlm-benchmark/vlm-bench-1/root/cli-review/`:

- `before.trx`: 13 expected failures and 13 passing controls before production edits.
- `after.trx`: 26/26 passing after the fixes.
- `final.trx`: 31/31 passing after missing-file/status/receipt/truncation controls.
- `receipt-before.trx`: 7 expected failures exposing prompt-integrity and missing descriptive/replay metadata.
- `receipt-final.trx`: 38/38 passing after canonical prompt binding, replay and completion-scope fixes.
- `provenance-before.trx`: 3 expected failures for no-receipt InputRejected text and lost provenance base.
- `provenance-after.trx`: 39/39 passing, with no compiler warnings.
- `assemblies-before.trx`: 2 expected missing-field failures and 39 passing controls.
- `assemblies-after.trx`: final 41/41 passing, with no compiler warnings.
- `receipt.json`: exact before/after source hashes, test-result hashes and CLI assembly hash.
- `receipt-assemblies.json`: additive producer-receipt change, final source/test hashes and assembly identities; prior receipt retained.

The regressions also cover no-clobber output behavior, changed crops between repetitions,
label-field rejection, frozen V2 hypotheses, V1 provenance exclusion, cancellation, missing
rows, corrupted cohort identities, and a 500,000-character transport-valid output whose
escaped JSONL previously exceeded the reader limit. Its insertions remain visible in CER.
A provider-rejected over-limit prompt remains an empty scored failure without a network call.
The producer identity regression loads identical copied CLI bytes with two copied AI DLLs
whose inert PE overlays differ: only the AI receipt hash changes, and the original shared
assemblies remain untouched. This checks file identity independently of the CLI hash.

The later cumulative-retention correction is recorded under `root/cli-text-budget/`.
A behavior-preserving private budget parameter enabled tiny regression fixtures; it cannot
be configured through CLI options or raised above the fixed production cap. Before the
guard, BMP and surrogate-pair cases reached a malformed third row and failed with
JsonException instead of the required earlier budget error. `before.trx` retains those two
failures and 42 controls; `after.trx` passes all 44 focused cases, including the existing
500,000-character output. An exact-budget control retains every hypothesis and the complete
missing-row denominator while accepting large unused diagnostics/source metadata. Original
JSONL bytes remain unchanged. Independent review confirmed the cap and minimal retained
record; the full CLI check excludes both fitting tests, as recorded in the final receipt.

The final receipt-size correction is recorded under `root/cli-receipt-size/`. A large
dataset identity and 1000-sample, three-repetition plan fit the input/replay limits but
previously produced a 4,460,670-byte run receipt that its own scorer could not read.
`before.trx` preserves that failure and 45 controls. The exact serialized run receipt is
now checked against the same 4 MiB limit used by the scorer before creating output or
making a request. `after.trx` passes all 46 focused cases, including a valid 2 MiB dataset
identity that still runs and scores. No identity or plan is truncated to fit the limit.

The later actual-console regression exposed a missing Ctrl-C cancellation bridge: the child
CLI exited 130 after a mocked local image request instead of closing its evidence. Root's
scoped `Program.cs` handler now supplies the cancellation token and unsubscribes on exit.
The Unix SIGINT regression passes with exit 3, one Cancelled row, five NotAttempted rows,
a complete summary and no project database. The test signals only its owned child and uses
its own loopback listener, never LM Studio. Actual Windows console signalling is explicitly
outside this Unix test. Evidence is in `root/cli-cancellation/`: before binary/source/TRX,
passing focused TRX, and the final 77/77 CLI checks excluding both training-epoch tests
(`Train_command_packages_requested_diagnostics` and
`Compatible_development_keeps_training_order_and_excludes_locked_only_labels`).

The graceful handler is now restricted to actual `vlm` inference. Validation/scoring
retain normal console interruption because their synchronous work does not consume the
inference cancellation token. An owned validation child writes a large summary into an
undrained pipe; observing its first character establishes readiness before SIGINT. The
old routing swallowed that signal and hung until bounded test cleanup. Root's routing
fix restores exit 130 for that offline child while the inference child still closes all
rows with exit 3. `root/cli-offline-cancellation/` preserves the failing-before/passing-after
process tests and the final full CLI check excluding both fitting tests.

The [operator runbook](../evaluation/vlm-lmstudio-runbook.md) covers fresh-checkout setup,
exact model/projector selection, manifests, separate references, genuine frozen R0 input,
run/score/replay and remaining external receipts. Its template JSON was parsed and the
manifest was exercised with the existing CLI validator against an owned generated PNG;
there were no model calls, training or database writes in that documentation check.

Validation command: `dotnet test tests/LeviathanText.Cli.Tests/LeviathanText.Cli.Tests.csproj
-c Release --no-restore -m:1 -p:UseSharedCompilation=false --filter
'FullyQualifiedName~VlmBenchmarkCommandTests'`. Root coordinates broader checks and all
live/R0 evidence; this handoff makes no Greek accuracy, image-grounding or adoption claim.
