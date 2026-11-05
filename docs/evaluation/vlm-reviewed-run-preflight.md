# Reviewed VLM run preflight and correction-effort worksheet

This procedure turns the existing VLM-04 review requirements and VLM-06 correction-effort gate into a release check and an editor assignment. The benchmark specification remains the source for quality, grounding and resource gates. [VLM benchmark specification](vlm-lmstudio-benchmark.md).

## Release preflight

| Check | Evidence the evaluator signs before execution | Current state |
| --- | --- | --- |
| Crop and rights | Stable source and holding IDs, item-level image and transcription rights, source-image SHA-256, crop SHA-256 and dimensions, reproducible source coordinates or transform, and retained attribution | The exploratory P46 release has source and crop evidence. Its proposed line boundaries and leaf identities await review. |
| Diplomatic reading | Independently reviewed target revision, reviewer and adjudicator, diplomatic profile version, exact Unicode target, and recorded uncertainty or exclusion decisions. Preserve the editorial source separately. | The 51 P46 targets are provisional. The original 81 specialist decisions remain pending. |
| Exposure and splits | Physical unit and witness lineage, earlier use and known passage overlap, split assignment, and audited disjointness by unit. Track language variety, material and hand separately. | P46 has five proposed leaves within one exposed Koine witness. There are zero reviewed selection or confirmation lines in its release. |
| Comparator | Frozen R0 checkpoint and preprocessing receipt for each claimed material, replay output keyed to the same crop identity, and V1/V2 request receipts with their planned slots | A frozen R0 replay exists for the 51 exploratory P46 crops. A reviewed Classical Greek release and its appropriate R0 comparator are still required for a Classical improvement claim. |
| Custody | Named evaluator and confirmation custodian, release and scoring hashes, separately stored confirmation labels, and permitted access list | Assign the people and release custody before selecting a model or condition. |

For each proposed line, record visible case and sigma form, written spaces, diacritics, overbars and abbreviation marks, damage, and any proposed crop-boundary correction. The specialist decides which features are visible in the image. A supplied edition can document a reading without establishing that every letter is visible. Freeze unresolved spans and the scoring treatment before collecting candidate results. For Classical Greek, apply the same visible-evidence rule, with the appropriate manuscript, papyrus or inscription profile and separately recorded conventions for editorial restorations, line breaks and signs. Group any views of a physical object together and document its witness lineage and text overlap. [P46 evidence and exposure](../data/vlm-benchmark-corpus.md) · [Unblinded specialist packet](vlm-review-packet.md).

## Assign editors to matched lines

1. The evaluator freezes the eligible corpus, candidate or conditions, crop recipe, editor instructions, timing rules, quality checks and analysis. Run a short practice session on separate setup lines. Require reviewers with the relevant Greek and material expertise, and record who can cover each track.
2. An assignment custodian makes blocks of comparable lines within a material and difficulty stratum. Use a seeded permutation of eligible lines and opaque task IDs. For a three-condition study with three editors, rotate the condition across editors within each block as shown below. Each line appears once per editor, and each editor receives each condition. Use separate seeded orderings of the assigned tasks and audit the distribution of conditions across early and late positions. For an R0-versus-one-candidate trial, the same rotation works with two editors and two lines. Record the actual balance and any undercovered stratum before collection.
3. Show the identical crop, agreed image context and editor interface for every route. Present only the starting transcription for that assignment and the fixed diplomatic instructions. Remove model names, route labels, reference answers, shelfmarks and timing information that could identify a route. Output style may still reveal a source. Record an optional post-task route guess and confidence separately from edits. Keep the mapping and original raw outputs with the custodian.
4. Give a separate Greek evaluator the final text and image under opaque IDs for residual-quality review. Keep route and editor identities hidden until the review records are frozen. Preserve uncertainties, adjudicated variants and feature-level errors. Do not assign an editor a line they previously adjudicated or practiced on.

| Example block | Editor 1 | Editor 2 | Editor 3 |
| --- | --- | --- | --- |
| Line A | R0 | V1 | V2 |
| Line B | V1 | V2 | R0 |
| Line C | V2 | R0 | V1 |

The table is a custodian-only example. The editor worksheet carries an opaque task ID, crop and editable text, without the assignment labels above.

## Capture the work and score it

| Record | Fields to retain |
| --- | --- |
| Assignment | Task ID, block, material, reviewer code, order position, image hash, display version, and custodian-only join to route, request ID, starting-text hash and status |
| Timing | Request start and ready time from the frozen inference receipt, editor task-open and submit time, interruptions with reason, and any timeout or incomplete task |
| Editing | Exact final UTF-8 text and hash, uncertainty markings, edit actions or start-to-final scalar changes, and whether the starting suggestion was used, corrected or replaced |
| Residual review | Adjudicated reference revision, exact and scalar error counts, separate marks and invented-text checks, evaluator code, unresolved decision and reason |

With pregenerated outputs, keep editors blind to model wait and add the recorded request-to-ready duration to their measured correction time to estimate serial end-to-end effort. Include the corresponding R0 wait. Report editing time and waiting time separately. Specify whether timing includes loading and session setup and how interruptions will be handled before assigning lines. Preserve elapsed and interruption durations so active editing and wall time remain distinguishable. Treat batch or parallel service timing as a separate workload.

Keep all assigned tasks in the accounting. Before the trial, specify the starting state and timed manual fallback for failed or unavailable responses. Record the incurred wait and failure status. A missing review, timed-out edit or incomplete assignment remains visible as such. The evaluator reports both completed-pair analysis and the full planned counts, with missingness by condition and material. Inconclusive coverage remains inconclusive.

For each claimed material, calculate matched line differences with reviewer balance checked. Use the median of line-level relative time savings as the proposed effort statistic, with R0 as denominator. A nonpositive R0 time is an invalid measurement and needs a prespecified exclusion and sensitivity check. Report absolute seconds as well. Account for physical-unit clustering and reviewer assignment when assessing precision. Freeze the interval method, minimum reviewer and unit coverage, fallback and residual-quality adjudication rules before inspection. The proposed effort gate is at least 20% median end-to-end savings with no residual-quality loss. If the uncertainty or quality evidence cannot support that conclusion, report the result as inconclusive. Keep repeated model outputs separate from independent lines and reviewers.

The procedure and worksheet fields are ready for an offline dry run on invented tasks. A real timed study requires the reviewed release, named specialists and custodian, and a frozen assignment and analysis plan. The current specialist packet supports adjudication. Its visible identities, references and earlier exposures disqualify it as a blinded timing trial.

## Offline dry-run evidence

A deterministic fixture exercises 12 invented crops, three simulated editors and three
routes across two placeholder strata. Its Greek strings and timings are invented. The
crop IDs stand in for the image delivery interface. This exercise measures assignment,
export and accounting behavior. It provides no human timing, residual-quality review or
statistical evidence for either Greek variety.

The fixed seed assigns each crop to all three routes through distinct editors. Every editor
receives each route once in each three-line block. The editor export has 36 opaque tasks.
The route, reference, block, material, timing and outcome fields remain in separate
custodian or analysis files. For actual distribution, send editors only their assigned
tasks and the corresponding image assets. The dry run checks fields and separation on disk.
It does not test the delivery channel or whether an editor could infer a route from its style.

| Route | Planned | Completed | Missing | Timed out | Completed through manual fallback |
| --- | ---: | ---: | ---: | ---: | ---: |
| R0 | 12 | 11 | 1 | 0 | 0 |
| V1 | 12 | 11 | 0 | 1 | 0 |
| V2 | 12 | 12 | 0 | 0 | 1 |

The fallback retains the failed request status and its incurred wait. The completed-pair
analysis uses 10 of 12 planned R0/V1 pairs and 11 of 12 R0/V2 pairs. The missing R0 affects
both comparisons, and the V1 timeout affects only R0/V1. The six negative checks reject
an unbalanced plan, a route field in the blind export, a changed frozen plan, duplicate and
unknown outcomes, and a timed-out record carrying completed text.

From the repository root, generate a fresh directory and compare its bytes with the saved
evidence.

```sh
dry_run_dir=.artifacts/vlm-benchmark/blinded-effort-dry-run-v1
python3 "$dry_run_dir/dry_run.py" "$dry_run_dir/reproduction-new"
diff -rq "$dry_run_dir/evidence" "$dry_run_dir/reproduction-new"
```

Two independent invocations produced byte-identical output directories. The receipt pins
the source, assignment, outcome, editor export and analysis files by SHA-256.
[Dry-run source](../../.artifacts/vlm-benchmark/blinded-effort-dry-run-v1/dry_run.py) ·
[Receipt](../../.artifacts/vlm-benchmark/blinded-effort-dry-run-v1/evidence/receipt.json) ·
[Accounting results](../../.artifacts/vlm-benchmark/blinded-effort-dry-run-v1/evidence/analysis.json).

## Mechanical intake of a returned P46 worksheet

Keep the frozen worksheet and each attributed return as separate files. The local intake
checker reads a return against the pinned `VLM-REVIEW-PACKET-1` JSON worksheet and emits a
receipt to standard output. This applies to the 51-row diagnostic worksheet. The original
81 specialist decisions use their separate packet and intake workflow.

The checker requires the exact frozen row ID set and compares every immutable field,
including source readings, crop and provenance fields, model outputs, questions, review
state and acceptance flags. Row order and JSON object key order can differ. Unicode
strings are compared as decoded sequences, with no normalization. The only editable
fields are the worksheet-level reviewer, adjudicator and date, the row-level reviewer,
date and second reviewer, and the existing decision text fields. Duplicate keys or row
IDs, changed field sets, changed frozen values and invalid types reject the whole return.

The receipt lists filled and blank decision fields by ID. It distinguishes untouched rows,
edits with no decision text, decision text needing attribution, and attributed decision
text for human triage. A row-level reviewer/date pair takes precedence. A complete
packet-level pair can supply attribution when both row fields are blank. An incomplete
row pair remains flagged so the checker never combines an individual name with a
packet-level date. These checks measure the presence of text. A human still verifies
identity and date, evaluates partial answers and determines what needs independent
adjudication. The receipt carries no decision text and confers no review or acceptance
state. The original and return files are read only.

From the repository root, pass the returned copy's local path. Redirect the receipt to a
new file only if you need to retain it.

```sh
intake_dir=.artifacts/vlm-benchmark/worksheet-intake-v1
python3 "$intake_dir/check.py" /path/to/attributed-return.json
python3 "$intake_dir/verify.py"
```

The blank frozen worksheet produced 51 untouched rows. Eighteen checks exercised blank
intake, mixed attributed and partial returns, reordered IDs, packet-level attribution and
corruption, including changed geometry, changed reference text, acceptance flag changes,
duplicate identities and malformed JSON. Two invocations produced identical verification
receipts. All temporary mutations used invented content, and the test checked source and
returned bytes for preservation.
[Checker](../../.artifacts/vlm-benchmark/worksheet-intake-v1/check.py) ·
[Blank receipt](../../.artifacts/vlm-benchmark/worksheet-intake-v1/blank-receipt.json) ·
[Verification receipt](../../.artifacts/vlm-benchmark/worksheet-intake-v1/verification.json).
