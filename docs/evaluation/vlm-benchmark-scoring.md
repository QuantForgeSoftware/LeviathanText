# VLM-B1 offline scoring policy and implementation

Version **`VLM-NFC-SCALAR-1`**, prepared 2026-11-04 for `VLM-BENCH-1`. The pure C#
[`VlmBenchmarkScorer`](../../src/LeviathanText.Evaluation/VlmBenchmarkScorer.cs) scores a
single model/condition/repetition against a frozen expected-sample list. It performs no
inference, training, I/O, label acceptance, candidate selection or model promotion.
Its [focused tests](../../tests/LeviathanText.Evaluation.Tests/VlmBenchmarkScorerTests.cs)
exercise real scorer behavior with invented Greek strings; no reserved data is used.

The optional CLI `VLM-TRANSCRIPTION-JSON-1` contract adds a separately named strict-derived
view around this unchanged scalar scorer. It preserves the complete raw representation
score, validates the sole-property JSON object without repair, and labels decoded results
separately. It never changes the original transport status or legacy plain-text scoring.
See the [structured-response runbook](vlm-lmstudio-runbook.md#optional-structured-responses)
for provenance, failure and compatibility rules. JSON syntax distance is a representation
diagnostic; it must not be described as diplomatic transcription accuracy.

## Contract and full-denominator behavior

`ScoreRun(IEnumerable<VlmBenchmarkReference>, IEnumerable<VlmBenchmarkOutput>)` consumes
offline references with `SampleId`, `PhysicalUnit`, `Material`, `ReferenceText`, `ControlKind`
and optional `UncertaintyMarker`. Outputs carry `SampleId`, `Status` and
`CapturedModelText`. Reference targets never belong in V1/V2 model-request payloads.

The caller supplies exactly one condition/repetition at a time and keeps model, prompt,
image, manifest and run hashes in its receipt. Duplicate reference IDs, unknown output IDs,
duplicate outputs/repetitions and invalid control contracts are rejected. A missing output
becomes `NotAttempted` with an empty hypothesis; every expected natural line remains in
the reference-character and line-coverage denominators. A bounded run stopping early must
score its entire preregistered intended set, not only completed rows.

Completion statuses are `Completed`, `InvalidOutput`, `Truncated`, `Timeout`, `TransportError`,
`Cancelled`, `InputRejected` and `NotAttempted`. The transport/runner maps its more detailed
failures into these while retaining the original detailed status in the raw receipt.
`Completed` means a complete, protocol-valid response, not Greek correctness. A null model
text paired with `Completed` is downgraded to `InvalidOutput`; an explicit empty string can
be a valid completed response. `NotAttempted` cannot carry model text.

No captured text means an empty hypothesis and deletion of the entire natural reference.
**Nonempty text is always scored**, even after truncation, timeout, invalid prose or another
failed status. The caller passes the entire captured model text, not just a parsed answer
or repaired fragment. HTTP error bodies are diagnostics and must not be supplied as model
text. Keep raw responses and parsing/recovery details alongside the score.

For the current CLI, provider completion establishes **transport-protocol completion**,
not obedience to the diplomatic prompt. A completed response can still contain prose,
invented letters or the wrong sigma form. Those remain errors/control failures or require
review; a high completed-request percentage alone cannot establish adoption eligibility.

## Recognition metrics

Normalize reference and hypothesis to Unicode **NFC**; compare Unicode scalars with unit-cost
Levenshtein insertion/deletion/substitution. Preserve case, accents, `σ/ς/ϲ`, real whitespace,
line breaks, punctuation and unsolicited prose. No trimming, compatibility normalization,
word joining, case folding, sigma conversion, accent removal or CER cap is applied. A
supplementary-plane character is one scalar. Invalid UTF-16 fails existing NFC validation;
the runner must preserve and report that invalid scoring input, never silently drop its
row or substitute a fabricated score. The normal JSON/UTF-8 transport must validate text
before this boundary.

The deterministic tie order matches existing `EditScript.Compute`: match, substitute,
delete, insert. A rolling two-row computation retains exact I/D/S counts without a
quadratic traceback allocation. All captured text contributes, including very long invalid
generation. The implementation is independent of the existing page-layout scorer: that
scorer intentionally includes blank insertions in a page-level denominator, whereas this
benchmark separates grounding controls from natural-line CER.

Returned `Rows` retain effective status, raw-normalized scalar counts, I/D/S, uncapped
natural CER, exact-text match, valid-completed flag and control outcome. `Overall`,
`ByMaterial` and `ByPhysicalUnit` report natural-line totals, micro CER, mean physical-unit
CER, valid-completion coverage, nonempty-output coverage, exact-line accuracy and
output/reference scalar-length ratio. Undefined ratios, including an all-control or empty
release, are **null**, never an invented perfect zero. Empty references must explicitly be
controls and cannot enter the natural CER denominator.

Before any alignment, the scorer validates the **whole run's resource budget**: at most
1,000 expected samples, 8,192 NFC reference scalars per sample, 1,048,576 NFC hypothesis
scalars per sample and **16,000,000 initialized alignment cells in total**, summing
`(reference scalars + 1) × (hypothesis scalars + 1)`. Counting initialized axes includes
empty-reference controls and missing outputs. A rolling two-row matrix bounds storage;
this additional preflight bounds quadratic work and prevents a long reference/output pair
from consuming trillions of operations.

Exceeding a bound rejects the **entire score before alignment**, with an explicit exception;
there is no truncation, dropped difficult row or partial report. Raw request/results and
reference artifacts remain intact. A revised limit or declared partitioning requires a
new recorded evaluation configuration that preserves the complete intended denominator,
never silent exclusion. These bounds do not affect the short 51-row P46 release. A
1,048,576-character failed output against a two-scalar reference remains within budget and
is scored in full, including 1,048,574 insertions. This keeps cheap, transport-bounded failure
evidence while rejecting expensive alignments.

Exact-text match is purely textual: a failed/truncated attempt that happens to contain the
entire correct string can have zero distance and count as exact text, while its valid
completion count remains zero. Always report both; textual agreement does not repair a
failed request. Similarly, a valid empty natural response has full deletion cost and zero
nonempty coverage, even though the protocol completed successfully.

## Grounding controls

Controls are excluded from natural accuracy and summarized separately by kind. Every
control retains its scalar edit counts even though its primary CER is undefined.

| Kind | Reference contract | Automated outcome |
| --- | --- | --- |
| `NoCharacterTraces` | Exactly empty reference | Pass only for a valid completed, explicitly empty response; whitespace, letters, failures and absent attempts fail |
| `UnreadableTraces` | A non-letter/non-digit/non-whitespace uncertainty marker and reference comprised only of its frozen repetitions | Pass only for valid completion matching that exact marker pattern; guessed letters, missing/excess markers or failed requests fail |
| `MaskedCounterfactual` | Frozen altered-evidence target, separately audited | Completed output always needs manual review, even if text matches; failed requests fail |

The marker and any known count/pattern must be preregistered by the reviewer. The scorer
cannot infer trace presence or how many missing letters to mark. Natural unreadable
controls have not been released, and synthetic marker tests are not specialist decisions.
The proposed benchmark gate remains successful empty output on **all 40 reviewed no-trace
controls** plus separate trace/masked audits; passing finite controls does not prove absence
of hallucinations on Greek texts.

## Statistical and scholarly limits

The scorer supplies counts, not an adoption decision. Each repetition is scored separately;
do not treat three repetitions as three independent witnesses or pool them into a bootstrap
sample. Future paired inference uses physical-unit resampling, all repetitions retained,
10,000 resamples and per-material candidate-minus-R0 intervals. A preselection precision
assessment and adequate independent units remain mandatory; no interval or significance
claim is implemented by this component. Future correction-effort, visible-mark fidelity,
abbreviation audits and resource gates likewise need their own independent evidence.

Current [VLM-B1-r1 references](../data/vlm-benchmark-corpus.md) are **provisional source
references**, with historical lowercase/sigma/spacing conventions that differ from a
literal visible P46 transcription. Applying this numerical scorer yields a diagnostic
**source-reference distance**, not comparable visible-transcription accuracy. It cannot
decide whether a VLM's uppercase/lunate output is more faithful, or whether a fluent source
match is image-grounded. No normalization projection or source-label correction is hidden
inside the metric. Reviewed natural benchmark and adoption claims remain blocked.

## Verification evidence

Artifacts are under `.artifacts/vlm-benchmark/vlm-bench-1/b/`.

- The artifact-only `baseline-probe/` exercises the unchanged existing page/recognition
  APIs. Two VLM requirements fail as expected: natural CER is 0.5 instead of 0 when a blank
  control inserts a letter, and an absent blank response has no failure-status distinction.
  Three existing-behavior controls pass: NFC equivalence, uncapped insertion errors and
  full missing-natural-line deletion cost. These demonstrate why a separate status-aware
  protocol scorer is required; they do not declare the page-layout scorer defective.
- Candidate focused suite: **32 passed, zero failed/skipped**. Coverage includes both
  baseline cases, all failed statuses, failed nonempty prose, astral Greek, canonical
  equivalents, full missing-output denominators, exact/coverage distinctions, all control
  kinds, invalid joins/contracts, empty releases and 50,000 retained inserted characters.
  One test checks **930** short Greek reference/hypothesis alignments against the existing
  deterministic I/D/S algorithm.
- Complete Evaluation suite: **71 passed, zero failed/skipped**. Build logs contain no
  compiler warnings. `baseline-probe/results/baseline.trx`, `tests/candidate-focused.trx`
  and `tests/candidate-full.trx` retain outcomes. The baseline and candidate test harnesses
  differ because the new public API did not exist; only executed behavioral failures,
  not a missing-class compile error, count as baseline evidence.

These synthetic tests establish scoring correctness for the covered cases, not recognition
quality, scientific adequacy, independent Greek review or performance of a VLM.

The subsequent resource-bound review added **seven tests**. Against the first scorer,
six bounded reproductions failed as expected and 33 controls passed; they exercise one
expensive row, cumulative work, blank-control initialization, scalar bounds and sample count.
No trillion-cell workload was executed. The candidate passes **39 focused /78 complete
Evaluation tests**, with zero failures/skips/compiler warnings. The large-output control
still scores the entire 1,048,576-scalar generated string. Evidence:
`tests/budget-baseline.trx`, `tests/budget-candidate-focused.trx`,
`tests/budget-candidate-full.trx` and `budget/verification.json`; the pre-fix scorer source
is preserved separately. These are resource-bound correctness checks, not throughput claims.

## Frozen R0 diagnostic context

The artifact-only C# harness in `b/r0-score/` scores the actual frozen R0 replay outputs
against the same 51 provisional references, once for each of the three retained
repetitions. It joins original source IDs to the unchanged historical dataset to retain
the **44 training /7 development** distinction. It performs no inference or fitting and
does not fabricate CLI/VLM transport receipts. Each repetition has the same results:

| Historical cohort | Lines | Reference scalars | Scalar edits | Raw source-reference distance |
| --- | ---: | ---: | ---: | ---: |
| Training, leaves 82–85 | 44 | 1,504 | 123 | 8.18% |
| Repeatedly used development, leaf 81 | 7 | 219 | 179 | 81.74% |
| Pooled, both exposure classes | 51 | 1,723 | 302 | 17.53% |

The pooled figure is dominated by examples that trained R0 and **must not be presented as
unseen-text performance**. The development group is also exposed and selected against in
earlier studies. Neither group has accepted visible-text targets; historical case, sigma,
spacing and mark conventions remain a profile mismatch for diplomatic VLM output.
These are diagnostic edit distances, not evidence that a model is better at reading Greek.

| Physical unit | Historical split | Lines | Reference scalars | Scalar edits | Raw distance |
| --- | --- | ---: | ---: | ---: | ---: |
| `unit-01` / proposed leaf 81 | Development | 7 | 219 | 179 | 81.74% |
| `unit-02` / proposed leaf 82 | Training | 17 | 578 | 38 | 6.57% |
| `unit-03` / proposed leaf 83 | Training | 12 | 418 | 45 | 10.77% |
| `unit-04` / proposed leaf 84 | Training | 10 | 332 | 23 | 6.93% |
| `unit-05` / proposed leaf 85 | Training | 5 | 176 | 17 | 9.66% |

All three repetitions retain full line denominators and transport-independent R0 completion
status. Repeated identical outputs do not create 153 independent lines or new physical
units. `b/r0-score/run-1/scores.json` retains every line/unit/split report; SHA-256
`4edd8ccc947083fc57634f2c056a35440ee9fefbf36e33eb20a28557389abcf3`.
Its receipt pins the current scorer source/assembly, harness, raw replay outputs, references,
crop joins and historical dataset, SHA-256
`c4402bb06bc54f70d3dbc5a38ae6d23a286e841d871dc1e288325eed79456245`.
The build passed with zero warnings/errors, and all original evidence remains unchanged.
