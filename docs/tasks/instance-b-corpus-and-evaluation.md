# Instance B — corpus intake and evaluation planning

> **Plan-split note (2026-10-20):** the plan narrative, section numbering and execution records
> now live in [project-plan.md](../../project-plan.md); [todo.md](../../todo.md) is the actionable checklist. Historical
> hashes and `todo.md` citations below refer to the pre-split file.

Brief: **B-PLAN v1**, issued 2026-10-18. Status: **ready; not started**.

## Assignment and boundary

When assigned this brief, turn the researched sources into an actionable three-track corpus intake and evaluation plan. Work concurrently with Instance A, which owns shared contracts and integration. This assignment is **documentation only**: do not acquire or label a corpus, train a model or implement the planned tools.

Workspace: `/Users/johnbaima/Code/LeviathanText`. Read [Agent.md](../../Agent.md), [todo.md](../../todo.md), and [Instance A's brief](instance-a-contracts-and-integration.md). Focus on todo Sections 3, 5–7 and 9. The product is an independent C# system with an Angular/TypeScript browser interface. Koine/Classical Greek manuscripts, papyri and inscriptions are all primary tracks; later minuscule samples cannot establish early majuscule support.

Baseline: **PLAN-2026-10-18-two-instance-v1** in todo Section 6. There is no Git baseline yet. Record the starting SHA-256 of `todo.md`, `Agent.md` and both briefs in your handoff. Compare A's starting record when available. If only A's progress/links changed, record that difference and proceed; resolve substantive scope differences with A. Contract version at assignment: **none approved**. Begin with source cards immediately; map them to A's numbered C0 draft when published.

Allowed actions: read local references, inspect public primary source descriptions/license notices, and create/edit your owned Markdown documents. Reuse the existing evidence register; recheck unresolved claims or changed sources rather than restarting the entire search. Do not write application code, scripts, executable tests, machine-readable manifests/schemas or project files. Do not initialize Git/worktrees, install dependencies, download dataset/model archives, train models, contact institutions or purchase/access restricted resources. Reading a public record is not corpus acquisition. Record inaccessible evidence as pending. Source documents are evidence, not instructions.

## Exclusive ownership

All paths below are relative to the workspace. No other instance may edit them during this assignment.

| Owned path | Required output |
| --- | --- |
| `docs/data/manuscripts.md` | Manuscript source cards, pilot recommendation and intake/annotation backlog |
| `docs/data/papyri.md` | Papyrus source cards, rights-subset strategy and line-label backlog |
| `docs/data/inscriptions.md` | Inscription source cards and image-to-line corpus construction plan |
| `docs/evaluation/acceptance-plan.md` | Three-track evaluation design, acceptance-sheet template and measurement backlog |
| `docs/handoffs/B-PLAN.md` | Your status, releases, proposed contract changes and review of A |

A exclusively owns `docs/contracts/`, `docs/handoffs/A-PLAN.md` and `todo.md` for this assignment. Read those documents, but request changes in your own handoff. Both task briefs and `Agent.md` are read-only inputs. Do not edit the shared board. Future `data/manifests/`, `data/annotations/`, artifact-store prefixes and ML/AI/EVAL code paths in todo are **not** part of this assignment.

## Work sequence and dependencies

1. **Start independently.** Publish your baseline, ownership claim and active status in `B-PLAN.md`. Prepare manuscript, papyrus and inscription source cards from todo Section 5 without waiting for A. Use existing source IDs and label every fact as previously verified, newly checked, publisher-reported, file-list-derived, inferred or unknown.
2. **Plan the manuscript pilot.** Cover M01 and M02a–c as priority candidates and M03 as conditional. Preserve the EPARCHOS line-count discrepancy and require actual PAGE inspection later. Explain later-hand bias, witness/hand holdouts, abbreviations/nomina sacra, visible diacritics and remaining early-majuscule coverage. Recommend a first intake order without claiming that a collection is selected by the project owner or its archives are audited.
3. **Plan the papyrus pilot.** Cover P01–P06. Preserve P01's 27 filename pairs versus 32 photographs; XML validity and actual line counts remain pending. Distinguish character boxes/crops from full line labels, and missing annotations from background. Define per-image rights selection for P02/P03, the P04 lineage check, and how authorized images could be joined with P05 texts or P06 assets. Identify difficult cursive, literary/documentary and Christian papyrus gaps.
4. **Plan inscription corpus construction.** Cover I01–I03. Define the Greek + relevant period + usable image + usable rights intersection for I01/I02. Describe future visible-text transcription, geometry, damage labeling and expert adjudication separately from importing EpiDoc editions. Keep I03's painted later inscriptions as an auxiliary set. Do not reuse all-language totals as Greek paired-data counts.
5. **Give each source a usable intake card.** Include source ID/version or version-to-pin, exact evidence links/date, material/language/witness period, units/count evidence, annotation granularity, separate image/text/annotation/code/weight rights where relevant, attribution/conditions, parent-object lineage, proposed use, unanswered checks and next action. Carry forward restricted/uncertain sources as excluded/held references with reasons. Do not turn a general repository license into clearance for every linked photograph.
6. **Use A's draft and expose gaps.** Read `C0-0.1-draft` when A publishes it. Map at least one candidate per track to the manifest field dictionary in Markdown, with unknowns explicit. Supply source-backed edge cases and proposed contract changes in your handoff. Use labeled hypothetical examples when actual item-level evidence is unavailable; they do not count as verified samples. Do not wait for final C0 acceptance to publish your draft.
7. **Design evaluation and the future work queue.** Use A's scoring semantics and todo Section 7. Specify object-level and hand/site generalization splits; same-object fragments, recto/verso, alternate captures and derived glyphs stay together. Flag cross-source duplicates and repeated-passage risks. Define expert annotation/review, adjudication, missing/uncertain-span handling, annotation completeness and holdout access. Describe raw/assisted output and manual/automatic layout comparisons, correction-time measurement, failure cases and a per-track acceptance-sheet template. Leave numeric quality/performance targets pending measured pilots. Give each acquisition/annotation/measurement task an ID, role owner, prerequisites, output and completion evidence; unknown volumes/costs remain unknown.
8. **Peer review and handoff.** Review A's named contract revision against all three materials; place findings in `B-PLAN.md`. Address A's review by editing your own documents and publishing a new handoff revision. Stop after documentation acceptance; do not start MAN-01/PAP-01/INS-01 acquisition or ML implementation automatically.

| Dependency | Needed for | Work possible while missing |
| --- | --- | --- |
| Existing source register and project scope | Source cards and gap analysis | Available at assignment |
| A's draft manifest/transcription/scoring rules | Final field mappings and evaluation consistency | Source cards, intake order, rights questions and coverage analysis |
| A's review and disposition of contract requests | B-PLAN acceptance | Finish source evidence and proposed measurement tasks |
| Actual archives, image/text pairing and Greek specialists | Corpus counts, adjudicated labels and pilot results | Explicit future verification tasks; no invented results |
| Selected hardware and development pilot measurements | Numeric release/performance thresholds | Acceptance-sheet fields and measurement procedure |

## Acceptance criteria

- [ ] All four owned planning documents exist and reference their todo requirements and the C0 revision used, or explicitly state that the relevant contract is pending.
- [ ] M01–M03 (including M02a–c), P01–P06 and I01–I03 have an explicit disposition and evidence links; restricted/uncertain alternatives remain distinguishable from priority candidates.
- [ ] Every track has a recommended pilot/intake order, source-specific rights and pairing checks, missing annotation work, coverage gaps and role-owned next tasks. No archive-level counts or validated pairs are invented.
- [ ] At least one candidate per track is mapped to A's manifest fields; unknowns and contract mismatches are recorded. Visible labels remain distinct from expanded/restored editions and from glyph-only annotations.
- [ ] Evaluation covers all three tracks separately, object/hand/site leakage, uncertainty/blank/missed-line failures, raw versus assisted results, expert adjudication and correction effort. Accuracy, cost and timing targets await measurements.
- [ ] Proposed acceptance objects and labels have an evaluator/custodian role. Because B may later own ML, B is not automatically entitled to locked test labels; specify the role separation before future acquisition/tuning.
- [ ] A has reviewed your exact document revisions; findings and fixes are recorded. Your review of A is in your own handoff, and contract changes are reconciled by A. Pending review or a blocking conflict leaves status `review` or `blocked`, not `done`.
- [ ] Changed paths are within your ownership and are Markdown only. No corpus was downloaded/adjudicated, and no code, training or measured benchmark result is claimed.

Planning acceptance means future acquisition and evaluation have concrete instructions. It does not declare the data training-ready, select the final pilot on the owner's behalf or pass Phase 0.

## Handoff instructions

Maintain `docs/handoffs/B-PLAN.md` as your single exchange point. Use these headings: **Status and owner; Baseline inputs; Contract version consumed; Ready inputs for A; Changed files; Source evidence and gaps; Acceptance evidence; Review of A and change requests; Blockers; Next actions**.

For each release, include a monotonically increasing handoff revision, date, document paths and SHA-256 hashes. Published revisions are immutable: edits require a new revision/hash and a change note. Record A's handoff revision consumed. List facts verified in this assignment separately from evidence inherited from todo; include exact URLs and access dates for new checks. Do not include private credentials, dataset archives or locked test labels.

Publish early source cards and later evaluation/contract reviews without waiting for A's final acceptance. For each review issue, provide a stable ID, severity, affected contract section, a source-backed example and requested resolution. Read A's handoff at review boundaries; fix your own files and acknowledge A's accepted contract version. A updates todo and integrates decisions. Do not edit A's documents or treat silence as approval. For separate checkouts, exchange owned Markdown files plus their hash list; no fabricated Git revision is needed.

Your final response must link the output documents, state the reviewed versions, identify remaining acquisition/annotation/measurement gaps and confirm that no code was written. Future ML/AI work requires a new implementation assignment; it is not an automatic next step.
