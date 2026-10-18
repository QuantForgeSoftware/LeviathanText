# Handoff — A-PLAN (Instance A: contracts and planning integration)

## Status and owner

- Package: **A-PLAN v1**, brief at [`docs/tasks/instance-a-contracts-and-integration.md`](../tasks/instance-a-contracts-and-integration.md).
- Owner: Instance A (this session). Status: **active** (claimed 2026-10-18).
- Assignment scope: **documentation only**. No code, no scripts, no machine-readable schemas, no Git/worktree initialization, no dependency installation, no dataset/model downloads, no training, no cloud resources.
- Peer instance B: not started at assignment time. No `docs/handoffs/B-PLAN.md` and no `docs/data/*` files existed when this handoff was created. All cross-package expectations below are therefore recorded as pending B, not assumed.

## Baseline inputs

Baseline: **PLAN-2026-10-18-two-instance-v1** (todo Section 6). There is no Git repository; the SHA-256 hashes below are the recorded starting state, captured 2026-10-18 before any A-PLAN edits.

| File | SHA-256 |
| --- | --- |
| `todo.md` | `6466461445bf973d493566c5ff74f50d88fd6c91f63250d7e4a1928b9cc78ac9` |
| `Agent.md` | `536b3919dd168840fa014535223f86d801eef82c9a9fe4a62405e42f8f7b02cf` |
| `docs/tasks/instance-a-contracts-and-integration.md` | `72f52abf6f27f5be482035795b0d4c437670a734e2b5f06e333da27314a83e97` |
| `docs/tasks/instance-b-corpus-and-evaluation.md` | `1a88f2e17655c4bcb5ad1ee22a52db0bb4bfa733848581643c8a7b7307dcdc2c` |

Contract version at assignment: **none approved**. First proposal uses `C0-0.1-draft`.

B may compare this record against its own hash capture. If only `todo.md` differs because of A's progress/board edits, B should record the difference and proceed per its brief; substantive scope differences go through both handoffs.

## Contract revisions

| Revision | Documents | Status | Handoff revision announcing it |
| --- | --- | --- | --- |
| `C0-0.1-draft` | `docs/contracts/corpus-manifest.md`, `docs/contracts/greek-transcription.md` | published, open for B review | A-PLAN handoff rev 2 |
| `C0-0.2-draft` | all five `docs/contracts/*.md` | published, open for B review | A-PLAN handoff rev 3+ |

No revision is accepted yet. `C0-0.1-draft` and `C0-0.2-draft` are proposals for B's material-fit review; adoption requires B's recorded review and the dispositions in this handoff.

## Ready inputs for B

| B can consume | Where | Notes |
| --- | --- | --- |
| Manifest field dictionary (source cards → fields) | `docs/contracts/corpus-manifest.md` | Includes per-field required/optional status, unknown-value representation, rights-evidence fields, split grouping, annotation-completeness fields |
| Transcription profile proposal | `docs/contracts/greek-transcription.md` | Diplomatic/editorial layering, Unicode rules, uncertainty vocabulary |
| Worked mapping examples | `docs/contracts/examples.md` | Labeled invented cases; B should propose source-backed replacements |
| Scoring semantics | `docs/contracts/interchange-and-api.md` + `examples.md` | For B's acceptance-plan alignment |

The early-interface release intended for B is `C0-0.1-draft` (manifest + transcription). B may begin source cards before reading anything from A; that is its brief's step 1.

## Changed files

Maintained per release; see the current revision entry under "Releases" below. All changed paths are inside A's exclusive ownership and are Markdown only.

## Acceptance evidence

Filled per the brief's acceptance criteria; each maps to supporting sections of the contract documents:

| Criterion | Evidence |
| --- | --- |
| Every owned contract document exists with revision, status, source requirements | All five `docs/contracts/*.md` carry `C0-0.x-draft` headers with status lines and named todo source requirements (see Changed files / hashes below) |
| Identity, coordinates, offsets, layer separation, manifest fields, model I/O, errors, API events, scoring explicitly defined; unknowns represented | `domain-and-pipeline.md` §1–§6; `corpus-manifest.md` §2–§6; `greek-transcription.md` §1–§6; `interchange-and-api.md` §2; unknowns via explicit `unknown`/`pending`/`not-retrieved` conventions throughout |
| Examples cover all three materials + edge cases with expected outcomes; invented, excluded from counts | `examples.md` EX-1–EX-9 + coverage checklist §3; each case labeled invented and excluded from corpus counts |
| B can describe priority sources via the manifest without overstating ground truth | `corpus-manifest.md` §4 `label_granularity`, `pairing_status`, `annotation_completeness` fields, plus §7 open question 1 addressed to B |
| Supported interchange fields and unsupported/lossy cases listed; no unproven claims | `interchange-and-api.md` §1 (format inventory, EpiDoc table, loss reporting §1.3, IIIF §1.4); no GPU-portability or C#-feasibility claims made anywhere in the contracts |
| B's review of the exact revision + dispositions recorded | **Open (BLK-A-1).** B has not started; no B handoff or documents exist as of closure. Status therefore `review`, not `done` |
| Review of B in this handoff; todo reflects both statuses, gates unchecked | See "Review of B" below; todo board updated accordingly; all Phase 0/1/2 gates left unchecked |
| Markdown-only, owned paths; no runtime tests/benchmarks claimed | Changed files list below; documents explicitly state no code, tests, benchmarks, downloads or installs were performed |

## Review of B

Pending — **B has not started.** As of A-PLAN closure (2026-10-18): no `docs/handoffs/B-PLAN.md`, no `docs/data/manuscripts.md`, `docs/data/papyri.md`, `docs/data/inscriptions.md`, and no `docs/evaluation/acceptance-plan.md` exist in the workspace. A's own review of B therefore cannot be performed yet.

When B publishes its first handoff revision, A (or a successor instance holding A-PLAN) must review at least: (a) source-card field coverage against `corpus-manifest.md` §2–§4, (b) proposed contract changes with stable IDs and dispositions, (c) evaluation-plan alignment with `examples.md` §1 scoring semantics, (d) that no archive counts are presented as verified pairs. Findings go here as `A-REV-B-<n>` entries; A edits none of B's files.

## Decisions and blockers

Blockers recorded so far:

1. **BLK-A-1 (peer review unavailable).** B has not started; A-PLAN cannot reach `done`. Final status will be `review` until B reviews a named contract revision and A records dispositions.
2. **BLK-A-2 (Greek specialist).** Scholarly conventions in the transcription contract are proposals; no expert has adjudicated them. Nothing in `docs/contracts/greek-transcription.md` is authoritative for real labeling yet.
3. **BLK-A-3 (implementation authorization).** All contracts describe future C0/APP/FMT/IMG/UI behavior. Todo's implementation and acquisition gates remain unchecked; completing this package does not authorize code or downloads.

## Next actions

1. Mark A-PLAN active on the shared board in `todo.md`.
2. Publish `C0-0.1-draft` (manifest + transcription contracts) for B's early use.
3. Complete `C0-0.2-draft`: domain/pipeline, interchange/API, examples.
4. Review B when its handoff appears; record dispositions.
5. Close with final hashes and status `review` (blocked on B).

**Closure state (2026-10-18):** steps 1–3 and 5 complete. Step 4 remains open pending B (BLK-A-1). Final status: **`review`** — awaiting B's material-fit review of `C0-0.2-draft` and A's resulting dispositions. To resume: start Instance B per its brief, or re-brief an instance to hold A-PLAN open for the review exchange.

---

## Releases

### Handoff revision 1 — 2026-10-18

- Action: baseline recorded; ownership claimed; status `active`.
- Documents: `docs/handoffs/A-PLAN.md` (this file, initial publication).
- Hashes: see "Baseline inputs" for the four input hashes. This file's own SHA-256 at publication: `pending` (computed by readers; hashes of *other* documents are listed per release below).
- No other files changed yet.

### Handoff revision 2 — 2026-10-18

- Action: published **`C0-0.1-draft`** — the early interface for B (manifest field dictionary + Greek transcription/Unicode contract). Marked A-PLAN `active` on the shared board.
- Changed files and SHA-256:

| File | SHA-256 | Change |
| --- | --- | --- |
| `docs/contracts/corpus-manifest.md` | `e504c6ab63551ea2bc998ce4f579d94c9659f5fcc7a6ca4edde9231cd1b09874` | new (C0-0.1-draft) |
| `docs/contracts/greek-transcription.md` | `c5a01dbf07ee1f9ba9e33c6bc3032a3201964a1f5f5a13f9012c2d2a85555776` | new (C0-0.1-draft) |
| `todo.md` | `61ad97a2d3f3bc15e407b651bba81b59ffd4ad70d6dfbdf641e429e75283ce07` | A-PLAN status → active |

- Note for B: `C0-0.1-draft` will be superseded by `C0-0.2-draft` when the remaining three contract documents are complete. B may map source cards to 0.1 now; please review the final revision named in a later handoff release for acceptance.

### Handoff revision 3 (final for this assignment) — 2026-10-18

- Action: published **`C0-0.2-draft`** completing the five-document contract proposal; recorded acceptance evidence; B confirmed absent (no handoff/documents); A-PLAN closed at status **`review`**; shared board and execution record updated in `todo.md`.
- Changed files and SHA-256:

| File | SHA-256 | Change |
| --- | --- | --- |
| `docs/contracts/corpus-manifest.md` | `e504c6ab63551ea2bc998ce4f579d94c9659f5fcc7a6ca4edde9231cd1b09874` | unchanged since rev 2 (C0-0.1-draft) |
| `docs/contracts/greek-transcription.md` | `c5a01dbf07ee1f9ba9e33c6bc3032a3201964a1f5f5a13f9012c2d2a85555776` | unchanged since rev 2 (C0-0.1-draft) |
| `docs/contracts/domain-and-pipeline.md` | `8b14ef1e51761edbaed875ccb49a0d1a1315fbe2db1ae66d8b4c11af61313d9e` | new (C0-0.2-draft) |
| `docs/contracts/interchange-and-api.md` | `2ba8fb5185d988f1f3f6a2626b6eb4d0bd17752642ee0853efebe4743a492f18` | new (C0-0.2-draft) |
| `docs/contracts/examples.md` | `a38c956f9f5bbf6ec0227f93fb921a23f6ebfea9d6f4b8e8089a977745604a71` | new (C0-0.2-draft) |
| `todo.md` | `f1313ac4224fa5182d5a4f6e889cb85650d7dda7ede0ec45c66398fcf4d994ae` | board status → review; A-PLAN execution record added |
| `docs/handoffs/A-PLAN.md` | `794efc05bfe8ae8823a2d5c5f4b9ab20bc71e5cd643c354a7ec3ae9aaedbf578` | hash immediately before this final entry (this entry is not self-hashing) |

- Contract version to review: **`C0-0.2-draft`** (which incorporates `C0-0.1-draft` unchanged). B's review requests: stable IDs per issue, affected section, source-backed example, requested resolution — delivered in B's own handoff, per B's brief.
- Document checks performed: internal cross-consistency of the five contracts (layer/offset/frame/scoring references), coverage checklist in `examples.md` §3 against the brief's required case list, hash recomputation at each release, and verification that no owned-path or no-code boundary was crossed. No runtime tests or benchmarks were run — none are possible under this assignment, and none are claimed.
