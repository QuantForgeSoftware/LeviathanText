# Instance B — Greek corpus, grounding and VLM evaluation

Assignment: `VLM-B1`, issued 2026-11-04; contract `VLM-BENCH-1`.
Read `Agent.md`, `CONTRIBUTING.md`, `lessons.md`, todo VLM-01 through VLM-08, the
[benchmark specification](../evaluation/vlm-lmstudio-benchmark.md),
[P46 corpus brief](p46-reviewed-corpus.md) and [Instance A's brief](vlm-lmstudio-engineering.md).
Originally a planning-only brief. The owner's subsequent three-hour assignment activated
the corpus inventory, C# scorer, regression tests and frozen R0 inference replay on
2026-11-04 (07:29:19–10:29:19 CST). B does not run VLMs or train. B is not the Greek
specialist or reserved-set custodian; those assignments and all unreturned decisions remain
pending. Current evidence is in [VLM-B1](../handoffs/VLM-B1.md).

## Next scope after the execution handoff

The owner's 10:15 CST extension activates independent synthetic schema evaluation through
**13:00 CST on 2026-11-04**, under the [extension registration](../../.artifacts/vlm-benchmark/vlm-bench-2/root/session.json).
B owns the new paired probe/report; reviewed natural releases remain dependent on returned
specialist decisions. Closed benchmark-1 evidence stays frozen.

The provisional inventory and scorer are implemented; read the
[session handoff](../handoffs/vlm-session-2026-11-04.md),
[review packet guide](../evaluation/vlm-review-packet.md) and
[output diagnostic inventory](../evaluation/vlm-output-diagnostics-2026-11-04.md).
B owns the next versioned corpus release, exposure/rights records and scoring evidence.
Neither B nor A may fill specialist decisions by model agreement. Root retains shared
plans and live model operation; A can independently prepare synthetic format/geometry tests.

Prioritize exact visible readings for the current lines: source-to-folio mapping, case,
lunate/final sigma, spaces, marks, damaged traces and crop boundaries. Use the unchanged JSON
worksheet for exact Unicode decisions; PDF is a viewing aid. Diagnostic flags identify
review targets, not adjudicated errors. Preserve rejected alternatives and reviewer identity.
Acceptance requires returned independent decisions, an exact new image/reference version,
explicit ambiguity policy, per-object exposure and rights records, and a mechanical hash/join
audit. Missing decisions remain pending; no current provisional line becomes confirmation
data. Publish A's input manifest without reference labels and keep reserved labels with the
separately designated custodian. This release unblocks a registered paired natural geometry
comparison; until then, finish intake and report the gap without rerunning inference.

Prepare the blinded correction-effort worksheet and fresh Classical/manuscript/inscription
candidate inventory in parallel with review. The existing unblinded diagnostic packet and
repeated P46 outputs cannot establish correction-time savings or new-material support.

## Objective and exclusive ownership

Specify a defensible comparison of visible Greek transcription and correction effort,
with independently checked references and explicit exposure/rights accounting. Own new
`docs/data/vlm-benchmark-corpus.md`, `docs/evaluation/vlm-benchmark-scoring.md`,
`docs/handoffs/VLM-B1.md`, and a future versioned artifact namespace
`.artifacts/vlm-benchmark/vlm-bench-1/b/`. Activated code ownership is limited to
`src/LeviathanText.Evaluation/VlmBenchmarkScorer.cs` and its new focused test file.
Propose a versioned manifest path before creating data releases; reference NT-PILOT-B2
records by immutable ID/hash instead of rewriting that instance's corpus.

A owns transport/provider/harness implementation and raw run receipts. Root alone updates
the shared benchmark spec, `project-plan.md`, `todo.md` and `completed.md`. Do not modify
code outside that scope, A's outputs, old P46 reports or existing uncommitted fixes. Record starting commit,
input hashes, ownership and scholarly/custodian roles at the beginning of work.

## Work that can proceed independently

1. Inventory candidate P46, manuscript, documentary/literary papyrus and inscription records
   already covered by project source registers. Record actual rights, image/target pairing,
   reviewed geometry, material, Greek variety, hand, rare marks and physical group.
2. Identify missing specialist decisions explicitly: all 51 current P46 proposed targets
   remain provisional; the original 81 decisions are pending. Prepare worksheets and
   exclusions; do not sign acceptance or treat a VLM reading as a returned decision.
3. Propose setup/selection/confirmation physical-unit splits and an exposure ledger. P46
   1440/1450/1460 and other inspected leaves remain development-exposed. Keep recto/verso,
   fragments, repeated captures and derived crops together. Record passage overlap and
   unknown foundation-model pretraining exposure, even on fresh images.
4. Specify the scorer: NFC scalar CER, I/D/S, length ratio, exact-line accuracy, per-unit and
   per-track aggregation, failures/abstention, visible marks and unsupported completions.
   Do not use word error as a primary measure of unsegmented scriptio continua.
5. Specify blank/damage/variant controls and synthetic masked diagnostics separately from
   natural accuracy. Prepare an image-versus-reference audit worksheet and blind,
   counterbalanced correction-effort protocol; named reviewers must adjudicate references.
6. Agree the input/result schema with A and propose numerical quality/resource criteria
   before looking at model outputs. The benchmark's proposed thresholds need a recorded
   rationale and coverage assessment; inadequate sample size means inconclusive evidence.

## Later release and scoring sequence

After activation, publish immutable `VLM-B1-rN` references with asset-level rights, image/crop
hashes, source geometry, transcription profile, reviewer decisions, ambiguity masks,
physical grouping and exposure history. Report actual counts against the 20-setup,
120-selection and 60-confirmation planning targets. Unsupported material or missing
comparator is an open gap; do not pool it away. Scope any P46-only result to that witness.

Keep reference text out of A's VLM request manifests and keep confirmation labels with the
designated evaluator. Check that requests contain no verse/shelfmark hints, retrieval or
conversation history. Compare image-only versus OCR-assisted results separately; restoration
and normalization are outside the primary transcription target.

Score every eligible result, including failed, empty, malformed and truncated responses,
under the frozen policy. Recompute R0 on the same dataset rather than citing historical CER.
Use physical-unit paired uncertainty and treat repetitions as repeated measurements, not
new independent samples. Audit each claimed variant correction against the image. Retain
raw responses, all edits and negative results; report per-track/per-condition effects.

Select at most one model/route after the full selection matrix. Freeze it before the
custodian runs confirmation. A selection gain cannot close scholarly release gates or
establish unseen-hand/Classical/inscription coverage. A broad support claim needs the
appropriate additional reviewed witnesses, hands and materials.

## Dependencies and acceptance criteria

- Planning is complete with an inventory/gap register, proposed split/coverage table,
  reviewer worksheets, scorer/control specification, proposed decision thresholds,
  agreed manifest contract and custody/handoff instructions.
- Corpus readiness later requires exact rights evidence, independent Greek review,
  accepted geometry/target versions, reconciled IDs/hashes and no split/exposure conflicts.
  Unreviewed/ambiguous material remains labeled and cannot establish primary accuracy.
- Scoring depends on A's complete versioned output/capability receipt; verify hashes and
  request counts before reading scores. A failed run cannot disappear from the denominator.
- Adoption screening requires the frozen recognition, no-regression, grounding,
  correction-time and resource criteria; report pass/fail/inconclusive per criterion.
  Model agreement and confidence are not scholarly adjudication.
- Confirmation needs a named custodian, fresh eligible units and frozen candidate/prompt;
  no automatic specialist access, acquisition, publication, training or model promotion is
  authorized by a planning brief.

## Handoff to A and integrator

Publish `VLM-B1-rN` with owned paths, baseline/input/manifest hashes, protocol version,
actual eligible counts by stratum, rights and exposure records, named review/custody roles,
scoring rules/fixtures, exclusions and unresolved questions. A reviews the mechanical schema;
the Greek specialist signs only their actual returned decisions. Later attach the complete
per-line/per-unit report, paired intervals, error audit, correction effort and comparison to
A's resource logs. Send proposed shared-plan edits to root. If a dependency is absent,
deliver the finished preparation plus a precise gap; never invent labels to unblock a run.
