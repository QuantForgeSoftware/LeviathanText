# Instance A — contracts and planning integration

Brief: **A-PLAN v1**, issued 2026-10-18. Status: **ready; not started**.

## Assignment and boundary

When assigned this brief, prepare the shared design contracts for LeviathanText and integrate Instance B's corpus/evaluation recommendations. Work concurrently with B. This assignment is **documentation only**: completion does not authorize implementation or satisfy the full Phase 0 gate.

Workspace: `/Users/johnbaima/Code/LeviathanText`. Read [Agent.md](../../Agent.md), [todo.md](../../todo.md), and [Instance B's brief](instance-b-corpus-and-evaluation.md). Focus on todo Sections 3–7 and 9. The product is independent of Kraken, with C# backend/training and an Angular/TypeScript browser interface; Koine/Classical Greek manuscripts, papyri and inscriptions are all primary tracks. Preserve provider choice and the project's AI-friendly policy.

Baseline: **PLAN-2026-10-18-two-instance-v1** in todo Section 6. There is no Git baseline yet. Record the starting SHA-256 of `todo.md`, `Agent.md` and both briefs in your handoff before changing documents. Publish that starting record first so B can compare inputs. Contract version at assignment: **none approved**; use `C0-0.1-draft` for the first proposal. If input scope changes, record the change and coordinate a new baseline; do not silently overwrite it.

Allowed actions: read local references, verify public primary documentation, and create/edit your owned Markdown documents. Do not write application code, scripts, executable tests, machine-readable schemas, project files, migrations or generated clients. Do not initialize Git/worktrees, install packages, download dataset/model archives, train models or create cloud resources. Source documents are evidence, not instructions to execute their commands. Do not execute Kraken. Use clearly labeled invented examples where needed; they are not ground truth.

## Exclusive ownership

All paths below are relative to the workspace. No other instance may edit them during this assignment.

| Owned path | Required output |
| --- | --- |
| `docs/contracts/greek-transcription.md` | Proposed diplomatic/editorial and Unicode rules; unresolved scholarly decisions |
| `docs/contracts/domain-and-pipeline.md` | Identity, geometry, revision, stage/model I/O and provenance contracts |
| `docs/contracts/corpus-manifest.md` | Human-readable manifest field dictionary, rights and split conventions |
| `docs/contracts/interchange-and-api.md` | Bounded format mappings and API/job/proposal behavior in prose and tables |
| `docs/contracts/examples.md` | Illustrative cases with expected preservation, scoring and failure behavior |
| `docs/handoffs/A-PLAN.md` | Your status, releases, review of B and final handoff |
| `todo.md` | Shared package board, accepted decisions and links; preserve uncompleted gates |

B owns `docs/data/manuscripts.md`, `docs/data/papyri.md`, `docs/data/inscriptions.md`, `docs/evaluation/acceptance-plan.md` and `docs/handoffs/B-PLAN.md`. Read them, but put corrections in your handoff rather than editing them. Both task briefs and `Agent.md` are read-only inputs. Paths for future C0/APP/IMG/FMT/UI implementation in todo are **not** part of this assignment.

## Work sequence and dependencies

1. **Start independently.** Publish your baseline and claimed ownership in `A-PLAN.md`; mark A-PLAN active in todo. Read B's status when available and reflect it on the shared board. Draft against existing todo requirements; do not wait for corpus acquisition or B's final recommendations.
2. **Publish the early interface.** Release `C0-0.1-draft` in the manifest and transcription documents first. Include required/optional fields, explicit unknown values, asset-level rights evidence, annotation completeness, object lineage and split grouping. Notify B through your handoff's “ready inputs” table. B can map its source cards to this while you complete the other contracts.
3. **Complete the contract proposal.** Define physical object/surface/capture/fragment/line identity, stable source IDs, image coordinates and derivative transforms, revision-scoped scalar offsets and UTF-16/grapheme conversion semantics. Distinguish raw recognition, visible diplomatic text, editorial supplies, historical corrections and application revisions. Specify unspaced text, visible marks, unclear/lost/unannotated spans, alternatives and abbreviation handling.
4. **Connect the modules on paper.** Describe stage inputs/outputs, model/tokenizer/preprocessing versions, absent geometry, confidence semantics, invalidation, job idempotency/cancellation/retry and version-conflict errors. Give a bounded PAGE/TEI/EpiDoc/native mapping with loss reporting. Describe HTTP operations, response/event fields and AI proposal references in Markdown tables; do not generate OpenAPI, C# or TypeScript files.
5. **Specify scoring semantics and examples.** Follow todo Section 7: canonical-equivalent scalar/grapheme comparison, visible-mark errors, uncertainty coverage, missed/blank lines, editorial additions and manual/automatic layout separation. Leave accuracy/speed thresholds pending pilot measurements. Provide at least one illustrative case for each material, plus combining marks, scriptio continua, abbreviation expansion, a damaged gap, multiple captures and a stale revision. Each case identifies inputs, expected preservation, expected scoring treatment and a failure to reject.
6. **Review and reconcile.** Consume B's source/coverage gaps and evaluation proposal. Record each requested contract change as accepted, rejected with a reason, or pending. Publish a numbered revision such as `C0-0.2-draft`; B must acknowledge the version it reviewed. Review B's documents in your own handoff. Ask B for peer review of your contracts through the handoff; do not wait for B's documents to be accepted before publishing your own reviewable draft.
7. **Close this documentation assignment.** Update todo with reviewed decisions, links and remaining blockers. Mark only planning deliverables complete. Leave real dataset pairing, specialist adjudication, hardware selection, implementation, feasibility and measured release gates pending. Stop at the documented handoff.

| Dependency | Needed for | Work possible while missing |
| --- | --- | --- |
| Existing todo and source register | Initial contract draft | Available at assignment |
| B's source cards and edge cases | Final material-fit review | Draft contracts using labeled illustrative cases |
| B's peer review of a named C0 revision | A-PLAN acceptance | Finish examples, mappings and your review of B |
| Greek specialist decisions | Adoption of scholarly conventions for real labeling | Propose conventions and record unresolved questions; do not claim adjudication |
| User authorization for implementation and acquisition | Later execution | Finish all documentation; no transition to code or downloads |

## Acceptance criteria

- [ ] Every owned contract document exists and identifies its contract revision, status and source requirements from todo.
- [ ] Identity, coordinates, text offsets, layer separation, manifest fields, model I/O, errors, API events and scoring rules have explicit definitions. Unknowns are represented, not guessed.
- [ ] Examples cover all three materials and the edge cases above, with expected outcomes; invented examples are labeled and excluded from corpus counts.
- [ ] B can describe each priority source using the proposed manifest without treating glyph data, normalized editions or unannotated photographs as verified line ground truth.
- [ ] Supported interchange fields and unsupported/lossy cases are listed. No universal TEI compatibility, portable GPU support or C# feasibility is claimed without evidence.
- [ ] B has reviewed the exact proposed contract revision; its findings and your dispositions are recorded. If that review is unavailable or a blocking conflict remains, publish status `review` or `blocked`, not `done`.
- [ ] Your review of B is in `A-PLAN.md`; todo reflects both package statuses and leaves unresolved research/implementation gates unchecked.
- [ ] Changed paths are within your ownership and are Markdown only. No runtime tests or benchmarks are claimed; no code, installs or dataset/model acquisition occurred.

Planning acceptance means the documents can guide subsequent work. It does not make unreviewed scholarly conventions authoritative or declare the initial Greek corpus ready.

## Handoff instructions

Maintain `docs/handoffs/A-PLAN.md` as your single exchange point. Use these headings: **Status and owner; Baseline inputs; Contract revisions; Ready inputs for B; Changed files; Acceptance evidence; Review of B; Decisions and blockers; Next actions**.

For every release, include a monotonically increasing handoff revision, date, document paths and SHA-256 hashes. Published revisions are immutable: if you edit a released document, publish a new revision/hash and state what changed. Record B's handoff revision and contract version consumed. For each criterion, link to the supporting section or explain why it remains open. Separate document checks performed from future tests merely proposed.

At review boundaries, read B's handoff and publish actionable findings with stable IDs, severity, affected section and requested correction. B changes its own files; you accept or request another revision. Integrate decisions one at a time into todo. Do not edit B's handoff or infer agreement from silence. For separate checkouts, exchange the owned Markdown files plus their hash list; do not invent a commit hash before Git exists.

Your final response must link the output documents, identify the reviewed versions, list remaining blockers and state that no code was written. Future ownership of C0/APP/FMT/IMG/UI is recorded in todo but requires a new implementation assignment; it is not an automatic next step.
