# Instance A — LM Studio transport and benchmark engineering

Assignment: `VLM-A1`, issued 2026-11-04; contract `VLM-BENCH-1`.
Read `Agent.md`, `CONTRIBUTING.md`, `lessons.md`, todo VLM-01 through VLM-08 and the
[benchmark specification](../evaluation/vlm-lmstudio-benchmark.md). Coordinate with
[Instance B](vlm-corpus-evaluation.md). Originally a planning-only brief. The owner's
subsequent three-hour assignment activated engineering and bounded inference diagnostics
on 2026-11-04 (07:29:19–10:29:19 CST); benchmark authorization does not imply training.
A owns the standalone vision transport and tests; the integrator alone runs LM Studio.
Current evidence is in [VLM-A1](../handoffs/VLM-A1.md); reviewed corpus/confirmation gates
remain open even when engineering tests pass.

## Next scope after the execution handoff

The owner's 10:15 CST extension activates this scope through **13:00 CST on 2026-11-04**.
See [the extension registration](../../.artifacts/vlm-benchmark/vlm-bench-2/root/session.json)
and the shared plan. Closed benchmark-1 evidence stays frozen; new work uses benchmark-2.

The transport and CLI foundation are implemented; read the
[session handoff](../handoffs/vlm-session-2026-11-04.md) before starting another batch.
Do not rerun the original matrix merely to obtain more favorable outputs. The next useful
engineering comparison is exact output-format enforcement and input geometry, with a new
registration and artifact namespace. Preserve the original plaintext prompt as a control.
Verify the installed model/runtime's constrained-output capability before assuming that a
JSON instruction enforces a schema. Record any actual constraint in the request hash;
fenced, repaired or substring-extracted JSON must not count as a strict response pass.

A can prepare synthetic capability tests and immutable experiment manifests independently
of B's Greek review. A owns new transport tests/notes and its experiment artifacts; root
owns CLI integration and the single live LM Studio caller. Acceptance requires fail-first
coverage for any implementation defect, complete failure accounting, unchanged raw
responses, no text-only regression, and no silent pixel/character transformations.
Natural paired raw/padded/scaled comparisons depend on B's reviewed visible references and
mark-preserving geometry. No default preprocessing change follows from printed controls.
Hand B all conditions and exact source/request/configuration hashes; B scores them without
dropping malformed output. This is a follow-up scope, not authorization to continue past
the current session or to train.

## Objective and exclusive ownership

Specify a reproducible C# path from authorized line-image crops to the owner's LM Studio
VLM, with complete provenance and measured failures/resources. Own new engineering notes
`docs/evaluation/vlm-lmstudio-transport.md`, handoff `docs/handoffs/VLM-A1.md`, and the future
artifact namespace `.artifacts/vlm-benchmark/vlm-bench-1/a/`. The activated source scope is
`VisionInference.cs`, `VisionImage.cs`, `VisionHttpProvider.cs`, `VisionResponseParser.cs`
in `src/LeviathanText.AI/`, the AI project dependency and `VisionHttpProviderTests.cs`.
The integrator owns CLI wiring; a separately scoped reviewer owns
`VlmBenchmarkCommand.cs` and `VlmBenchmarkCommandTests.cs` for this cycle.

Propose exact C# harness/provider/test paths before implementation. A is the only instance
that may modify the AI request/provider code once that scope is activated; changes to
shared contracts, API wiring or central build files go through the integrator. B owns
corpus/scoring evidence. Root owns `project-plan.md`, `todo.md`, `completed.md` and the shared
benchmark specification. Preserve all pre-existing uncommitted code and closed study files;
record the starting commit plus relevant file hashes, not only HEAD.

## Work that can proceed independently

1. Document the existing text-only request/HTTP serialization and the required image-evidence
   extension. Describe compatibility for old callers and the separate L0 versus L2/L3 routes.
2. Complete the candidate receipt template for Qwen3-VL-8B-Instruct, Qwen2.5-VL-7B-Instruct
   and hardware-gated Mistral Small 3.1 24B, using the exact packages/projectors in the plan.
   List unresolved local versions, hashes, RAM/VRAM and vision capability explicitly.
3. Specify contrasting synthetic-image/blank fixtures, serialization assertions, limits,
   missing-projector/unsupported-model behavior, cancellation/timeouts, output truncation,
   stale source identity, and credentials/log redaction. No fixture needs a Greek adjudication.
4. Agree the input/result manifest fields with B. A receives images, opaque IDs, metadata
   needed for scoring joins and R0 hypotheses; never confirmation reference labels.
5. Produce a run-budget/resource measurement proposal and a dependency register. Do not
   claim a runtime is compatible merely because its package exists or its endpoint returns 200.

## Later implementation and execution sequence

After activation, verify LM Studio version, backend, authenticated/local endpoint, loaded
model ID and image support before real collection requests. Prefer C# OpenAI-compatible
chat completions with inline PNG bytes; if incompatible, report the failure and specify the
native LM Studio endpoint separately. Never silently reroute to text-only or a cloud model.
Record exact package, projector, tokenizer, prompt/template, image recipe and runtime hashes.

Implement request/input bounds and immutable per-request receipts, with no automatic
retries and no L1 mutation. Meaningful regression tests must first demonstrate any existing
defect or unsupported image behavior, then preserve text-only requests, Unicode, old model
loading and source-anchor checks. Scope tests to the actual changes; build without warnings.

Rerun the frozen existing recognizer on B's same eligible crop manifest. Compare V1 image-only
and V2 image-plus-R0 using the frozen prompts/settings and three repetitions. Keep model
changes, cold loading, warm requests, transfer/encoding time and OS/GPU memory separate.
No simultaneous model runs or training on the measurement host. Stop at the registered
budget; retain all failures and incomplete conditions.

## Dependencies and acceptance criteria

- Planning completion requires an implementable transport/fixture specification, exact
  proposed paths, package/rights references, a manifest agreement and unresolved inputs.
- Engineering readiness later requires contrasting-image evidence that vision is consumed,
  zero silent text-only fallbacks, zero missing request records, bounded failure handling,
  exact source/image/model identities and passing compatibility tests/builds.
- Corpus selection runs depend on B's immutable reviewed manifest/scoring release and the
  integrator's frozen run manifest plus execution assignment. Provisional P46 material may
  only enter separately labeled exploratory diagnostics.
- No accuracy or speed claim is accepted from A's selected examples. B/evaluator scores all
  eligible outputs and applies the registered per-track, grounding and resource gates.
- Confirmation runs use an evaluator-controlled input path and frozen selected condition;
  A must not inspect reserved labels or tune after results. No training, release, publication,
  model promotion or default-route replacement belongs to this brief.

## Handoff to B and integrator

Publish `VLM-A1-rN`: base commit and candidate hashes, changed/owned paths, consumed B
release, protocol revision, capability receipt, exact model/runtime configuration,
commands, input/result hashes, all raw outputs/statuses, resource logs, test evidence and
remaining gaps. Include negative results and every attempted condition. B checks source
joins, omissions and scoring eligibility; A reviews B's schema mechanically without
certifying Greek readings. Send proposed shared-plan edits to the integrator rather than
editing shared files. If B lacks labels, finish synthetic engineering evidence and stop at
that dependency; do not invent targets or restart the old P46 training study.
