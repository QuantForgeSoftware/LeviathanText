# Handoff — B-PLAN (Instance B: corpus intake and evaluation planning)

## Status and owner

- Package: **B-PLAN v1**, brief at [`docs/tasks/instance-b-corpus-and-evaluation.md`](../tasks/instance-b-corpus-and-evaluation.md).
- Owner: **Instance B** (this session). Status: **review** — the four owned planning documents are published at **rev 3**, mapped to A's current contract `C0-0.3-draft`; awaiting A's review of the exact document revisions and A's dispositions of the open change requests. Not `done` because “a different instance reviews each handoff” (todo Section 6) has not yet happened.
- Package scope: **planning** (this handoff). B's **implementation** package is the separate [B-CODE.md](B-CODE.md) handoff. This planning package created no code or schemas; the code handoff did, and its changes are listed there.
- Contract version consumed: **`C0-0.3-draft`** (all five `docs/contracts/*.md`; hashes verified against A's A-PLAN rev 5 record — see “Contract version consumed”).

## Baseline inputs

Baseline: **PLAN-2026-10-18-two-instance-v1** (todo Section 6). No Git repository. SHA-256 captured by Instance B on **2026-10-18**:

| File | SHA-256 | Notes |
| --- | --- | --- |
| `todo.md` (at B start) | `61ad97a2d3f3bc15e407b651bba81b59ffd4ad70d6dfbdf641e429e75283ce07` | A's starting hash `6466…`; drift is A's board edits — recorded, proceeding per brief |
| `todo.md` (now) | `6a72a51e9f1db4a4160b8410c55ebda9c76e72d26b74dcd128922bd9eb341907` | A's rev-6 board update (commit `e770081`) |
| `Agent.md` | `536b3919dd168840fa014535223f86d801eef82c9a9fe4a62405e42f8f7b02cf` | matches A |
| `docs/tasks/instance-a-contracts-and-integration.md` | `72f52abf6f27f5be482035795b0d4c437670a734e2b5f06e333da27314a83e97` | matches A |
| `docs/tasks/instance-b-corpus-and-evaluation.md` | `1a88f2e17655c4bcb5ad1ee22a52db0bb4bfa733848581643c8a7b7307dcdc2c` | matches A |
| `docs/handoffs/A-PLAN.md` consumed (rev 6) | git `62eda17` | A's increment-3 handoff; A-PLAN rev 3 (`1dd44617…`) was consumed at B rev 2 |

A's board and handoff advanced only because A recorded its own work; no substantive scope difference from B's brief.

## Contract version consumed

**Consumed: `C0-0.3-draft`** (set-level revision; 0.3 adds the native-JSON conventions `interchange-and-api.md` §2.0 and the `StageArtifact` stage I/O shape, and records no change to manifest/transcription rules). B fetched and hashed all five documents; **every hash matches A's A-PLAN rev-5 record**, so the reviewed revision is unambiguous:

| Contract document | SHA-256 (B-verified) | A's recorded hash | Match |
| --- | --- | --- | --- |
| `docs/contracts/corpus-manifest.md` | `e3142749ea3748b929f87346e8f89c5d2d95df15d572adf3403012c3bccfeb74` | `e3142749…` | ✅ |
| `docs/contracts/greek-transcription.md` | `868c98082dd95ef538f9c3163bc2dabb13d95d3e859e8a65643abb235bd8f654` | `868c9808…` | ✅ |
| `docs/contracts/domain-and-pipeline.md` | `14b03ec33789afee90f13cc42b7e267cdad26f58cba8b0860d69f7fa03b9fdba` | `14b03ec3…` | ✅ |
| `docs/contracts/interchange-and-api.md` | `81d3fa38b5c71bfbd085d0d7814e3483858516bb0308680ca2bcbd529618607f` | `81d3fa38…` | ✅ |
| `docs/contracts/examples.md` | `ec08c7c4af26ae536a4783e03cf26dd6b1d1f898e9eb66c90470bf4343c1ddfd` | `ec08c7c4…` | ✅ |

Rev 2 of B's four planning documents was mapped against `C0-0.2-draft`. Because 0.3 changed no manifest or transcription rule, those mappings and the evaluation plan remain valid; **rev 3 updates their consumed-version references to `C0-0.3-draft`** and adds the review below. No revision is accepted: acceptance is A's recorded disposition of this review.

## Ready inputs for A

| A can consume | Where (rev 2) | Notes |
| --- | --- | --- |
| Per-track source cards with split rights | `docs/data/manuscripts.md`, `docs/data/papyri.md`, `docs/data/inscriptions.md` | M01–M03(+M02a–c), P01–P06, I01–I03 with provenance tags and 2026-10-18 access dates |
| Manifest field mappings at all three record levels | manuscripts §7, papyri §7, inscriptions §7 | Now use `corpus-manifest.md` field names |
| Material-fit review of `C0-0.2-draft` | this handoff, “Review of A and change requests” | `B-REV-A-*`, `B-CCR-*` |
| Answers to A's reviewer open questions | `docs/evaluation/acceptance-plan.md` §10 | manifest §7 Q1–Q3, interchange §4 Q1, domain §7 Q1, examples replacements |
| Rights-ledger requirement & edge cases | papyri §3, inscriptions §2/§3 | P02/P03 NC + per-image mixture; I01 data-vs-image split |
| Evaluation design + acceptance-sheet template + measurement backlog | `docs/evaluation/acceptance-plan.md` | Aligned to `examples.md` §1 |
| Source-backed corrections to todo Section 5 | this handoff, “Source evidence and gaps” | e.g. P02 NC license; I01 imagery not CC BY 4.0; Hell-Date DOI→v1.2 |

## Changed files

All under B's exclusive ownership; Markdown only.

| File | Rev | SHA-256 (rev 2, 2026-10-18) |
| --- | --- | --- |
| `docs/data/manuscripts.md` | 2 | `1bd086468621d56968697e997d3a42b6166d5c53484206c5134d511030fb8766` |
| `docs/data/papyri.md` | 2 | `6f8bfac197078102571a465aa38e99a6d9401affc323414a333dae67a405abd7` |
| `docs/data/inscriptions.md` | 2 | `1b4a0d35afd09aee7d8cb1870c5f6bc93e9630d2c410ca8e59819b5b8b37e064` |
| `docs/evaluation/acceptance-plan.md` | 2 | `ded14899fa210b6c7d69955ab955682304e21dcd18923188ed894d26f8aa6528` |
| `docs/handoffs/B-PLAN.md` | 2 | `pending` (self-hash; this file) |

Rev 1 hashes (superseded, kept for the record): manuscripts `4a04508b…`, papyri `03114592…`, inscriptions `4bf7e49e…`, acceptance-plan `6bfb9287…`. No other paths were created or modified. `data/manifests/`, `data/annotations/`, artifact-store prefixes and any code paths remain untouched.

## Source evidence and gaps

### Facts newly re-checked this assignment (`[new]`, 2026-10-18)

Corrections/additions to todo Section 5:

1. **P02 ICDAR2023** — **record-level license CC BY-NC 4.0** (non-commercial), not merely “mixed image rights”; per-image terms additionally in a JSON `licenses` field and `1. CompetitionOverview.xlsx`. Counts: 185 surfaces / 136 TMs; 150 training / 34 test. → **conditional**. `https://zenodo.org/records/13825619`.
2. **P03 Hell-Date** — concept DOI `10.5281/zenodo.15083589` now resolves to **v1.2, record `15830980` (2025-09-01)**; record license **CC BY-NC 4.0**; per-image mixture includes “unknown license” and “In Copyright”; summer-2024 page says 187/155 vs Zenodo 194/157. `https://zenodo.org/records/15830980`.
3. **P05 idp.data** — README grants **CC BY 3.0**, but `master/LICENSE.md` is **HTTP 404** and GitHub detects no license file; default branch `master`. `https://github.com/papyri/idp.data`.
4. **P06 Turin** — policy (2026-06-27) grants **images CC0**, **partner PDFs CC BY-NC (deed → 2.5)**, registered-upload PDFs CC0. `collezionepapiri.museoegizio.it`.
5. **I01 I.Sicily** — license file is **`licence.txt`** (not `LICENSE.md`); data/Zenodo deposit **CC BY 4.0**; concept DOI → **v1.0.1, record 23210545 (2026-10-05)**; **IIIF imagery CC BY 4.0 NOT confirmed** (site reserves images for study; museums hold rights). `https://github.com/ISicily/ISicily`.
6. **I02 Petrie** — v1 (2022-09-02), **CC BY 4.0**; **single PDF** (~872 KB); no counts. `research.manchester.ac.uk`.
7. **I03 Bessarion** — `master/LICENSE.md` = **CC BY-SA 4.0** (122/504/2776/10414/37 confirmed); no DOI.
8. **M01 EPARCHOS** — CC BY 4.0; Zenodo **9,285 lines** vs HTR-United **2,272 lines / 116,894 chars / 120 files / <11 hands** — conflict persists.
9. **M02a/b/c Stavronikita** — CC BY 4.0; 54/1,038, 40/803, 44/1,051; parchment/paper/paper.
10. **M03 Vienna** — repo **CC BY-SA 4.0**; README contradicts itself on image rights and writes “Creative Commons 4” without variant; 66 ground-truth images.
11. **Restricted re-checked:** PHI personal-study; I.PHI code Apache-2.0 only; IGCyr/GVCyr v0.2 **CC BY-NC 4.0** (v0.3 exists); Cornell Eleusis study-only with per-item rights; Pandektis metadata CC0 but **images In Copyright**, 1,037 records; GRK-Papyri non-commercial.

### Inherited, not re-checked (`[prior]`)

Codex Sinaiticus NC terms; P02 accent/editorial stripping; P03 “missing boxes may mean unannotated”; P04 derives from Hell-Date; no broad aligned Classical/Koine inscription photo-to-line corpus exists.

### Remaining gaps

- No archive downloaded → **zero** verified line-label counts; all counts publisher-reported or file-list-derived.
- Early-majuscule manuscripts absent; papyrus line seed is one 27-pair archive; inscription imagery rights unresolved for I01.
- Greek specialist, custodian and owner selections **not named**.

## Acceptance evidence

Mapped to B's brief acceptance criteria:

| Criterion | Evidence | Status |
| --- | --- | --- |
| Four documents exist, cite todo + C0 revision used | headers of all four (rev 2) | ✅ |
| M01–M03, P01–P06, I01–I03 dispositions + links | manuscripts §2, papyri §2, inscriptions §2/§3 | ✅ |
| Per-track pilot order, rights checks, missing work, gaps, role-owned tasks | manuscripts §4–§8, papyri §5–§8, inscriptions §5–§8 | ✅ |
| ≥1 candidate mapped to real manifest fields; unknowns recorded | manuscripts §7, papyri §7, inscriptions §7 | ✅ (prev. provisional issue `B-CCR-2/3/4` now resolved) |
| Evaluation covers three tracks, leakage, uncertainty/blank/missed, raw vs assisted, adjudication, correction effort | acceptance-plan §2–§8 | ✅ |
| Custodian role separation | acceptance-plan §1, §11 | ✅ proposed; **owner must name** |
| A reviewed B's exact revisions; findings recorded | — | ⛔ **pending A** (`BLK-B-1`) |
| Markdown-only; no corpus/code/benchmark claimed | Changed files; this handoff | ✅ |

Document checks performed: internal cross-links resolve; every `[new]` fact carries an access date and URL; the five contract hashes recomputed and matched against A's record; no numeric threshold invented; no restricted material placed in the default bundle.

## Review of A and change requests

Reviewed: **all five `C0-0.3-draft` documents** (hashes verified above), `A-PLAN.md` rev 6 (git `62eda17`), and A's compiled C0 code in `src/LeviathanText.Contracts` + `src/LeviathanText.Core`.

**Verified positive (0.3):** the five documents cross-reference consistently; `ContractVersion.Current` = `"C0-0.3-draft"`; `Contracts.Serialization.ContractJson` implements §2.0 (camelCase, string enums, strict numbers, `"none"` confidence, `EntityId`/`ScalarRange`/`Checksum` shapes); B's `Recognition`/`Training`/`Evaluation`/`AI` code compiles against the compiled contracts and consumes `Core.Unicode.ScalarText`/`Graphemes` directly. 0.3 changes no manifest or transcription rule, so B's rev-2 field mappings stand.

| ID | Severity | Affected section | Finding / example | Requested resolution |
| --- | --- | --- | --- | --- |
| `B-REV-A-6` | **Medium** | `domain-and-pipeline.md` §1.2, §2.1, §5 | **Compiled contracts are ahead of the prose.** Increment 3 added `CoordinateFrame` (checksum-pinned; a re-capture is a *different* frame), `Registration`, `JobRecord`/`LeaseInfo`/`JobStatus`, `StageArtifact`, and Core `FrameRegistry`/`StageValidation`/`JobTable`, but the prose diff is header-only: §1.2 never says frames are checksum-pinned; §2.1 still reads "Entity IDs + artifact checksums" rather than `StageArtifact`; §5 omits the divergent-finalized-publication hard error and the status/lease fields. | Document these in `domain-and-pipeline.md` (§1.4 frames/registration, §2.1 `StageArtifact`, §5 job record/status/lease/idempotency), or state that the compiled types extend the prose and schedule it as a `0.4` increment. Evidence: `git diff 728ec9c..HEAD -- docs/contracts/` (header-only for this doc); types in `src/LeviathanText.Contracts/JobsAndFrames.cs`. |
| `B-REV-A-7` | Low | `ContractVersion.cs`; `todo.md` board | `ContractVersion.Current` = `"C0-0.3-draft"` but its XML-doc comment still says "C0-0.2-draft … (A-PLAN handoff rev 3)", and the todo execution record still says `ContractVersion.Current = "C0-0.2-draft"`. | Update the comment and the todo record. |
| `B-REV-A-8` | Low | `domain-and-pipeline.md` §2.2 Layout | The Layout row flags `layout_source` but not the confidence rule; §4 requires absent confidence to be explicit. Answering A's §7 Q3: **yes** — emit `layout_source` **and** confidence (`none` when manual/imported) per region. | Add the confidence clause to the Layout row. |
| `B-REV-A-9` | Low (cosmetic) | `interchange-and-api.md` §2.0 | `§2.0` is inserted before `§2.1`, an odd sub-number. | Renumber (fold into the §2 intro, or `§2.5`). |
| `B-REV-A-10` | Info | `examples.md` §2 | Examples remain invented (as labeled). | No action now; B supplies source-backed cases when real labels exist. |

**Contract change requests (`B-CCR-n`):**

| ID | Request | Status against `C0-0.3-draft` |
| --- | --- | --- |
| `B-CCR-1` | Pin canonical-equivalence + grapheme/boundary + scalar↔UTF-16 mapping. | **Resolved** — `greek-transcription.md` §4. |
| `B-CCR-2` | Record count provenance + dual figures instead of one line count. | **Resolved** — `corpus-manifest.md` §2 `units_evidence` + `paired_line_counts`. |
| `B-CCR-3` | Per-item rights instead of a single rights field. | **Resolved** — `license_image = mixed-per-item` + §4 asset-level `license`/`evidence`. |
| `B-CCR-4` | Explicit annotation-granularity vocabulary. | **Resolved** — `label_granularity`. |
| `B-CCR-5` | Correct todo Section 5: I01 IIIF imagery is **not** verified CC BY 4.0 (site policy is study-only). | **Open** — `todo.md` line 221 still asserts the CC BY 4.0 imagery claim; A owns todo. |
| `B-CCR-6` | Add a `preservation_tags` asset field and a text-only `catalog`/`metadata-text` `asset_kind` (or `other:<text>` escape). | **Open** — `corpus-manifest.md` is unchanged in 0.3; re-raised via A's §7 Q1. |
| `B-CCR-7` | Add a `Proposal` entity kind (or state proposals use string ids). | **Open** — `EntityKind` still has no `Proposal`; B's `AiProposal.Id` is a string (`prop-<guid>`). |
| `B-CCR-8` | Name model-package tokenizer/preprocessing hash fields. | **Withdrawn — already satisfied** by `domain-and-pipeline.md` §3 ("tokenizer/label set (+ hash) … preprocessing recipe (+ version) … metrics, checksums, license metadata"). B will align its model card to §3. |

`B-REV-A-5` (informational, still open): `greek-transcription.md` §2.2 proposes `ϲ` U+03F2 for lunate sigma; a specialist decision (A's own §7 Q1), no B objection, only that annotator speed/ambiguity be tested.

**Answers to A's open reviewer questions:**

| Question | B's answer |
| --- | --- |
| `domain-and-pipeline.md` §7 Q1 — per-grapheme confidence sufficient? | **Yes** at contract level; do **not** reserve word-level now (WER applies only where an explicit word layer exists; aggregation is a later derived view). |
| `domain-and-pipeline.md` §7 Q2 — job hierarchies at contract level? | Flat job + per-item stage checkpoints is sufficient for Phase 1; hierarchies deferred. |
| `domain-and-pipeline.md` §7 Q3 — layout `layout_source` + confidence even when manual? | **Yes** — see `B-REV-A-8`. |
| `interchange-and-api.md` §4 Q1 — fix export/round-trip fixture names now? | **Yes**, fix names not content: `FX-MSS-nomina-sacra-overbar`, `FX-PAP-lacuna-unknown-extent`, `FX-INS-stoichedon-scriptio-continua`, `FX-shared-abbrev-expansion`, `FX-shared-lunate-sigma`, `FX-shared-combining-mark-offsets`, `FX-shared-blank-region`, `FX-shared-alternate-capture`. |
| `interchange-and-api.md` §4 Q2 — AI proposal task vocabulary open/closed? | Closed + `other:<text>` escape is correct. |
| `interchange-and-api.md` §4 Q3 — SSE-only through Phase 2? | Acceptable; revisit WebSocket at Phase 3 shared workspaces. |
| `corpus-manifest.md` §7 Q1 — vocabularies sufficient? | Mostly; residual is `B-CCR-6` (preservation tags, catalogue asset) + `B-CCR-7`. |
| `corpus-manifest.md` §7 Q2 — `hand_style` controlled vocabulary? | Free text + a split-grouping key is sufficient for planning; do not invent a vocabulary before the specialist. |
| `corpus-manifest.md` §7 Q3 — reserved-acceptance / custodian? | Owner selects reserved-acceptance objects; the custodian must be a **distinct named role** (`acceptance-plan.md` §1). |

**Response to A's review of B.** A's `A-REV-EVAL-1` is fixed in B's code handoff ([B-CODE.md](B-CODE.md)): `Evaluation.CanonicalText.ToScalars`/`FromScalars` now route scalar segmentation through `Core.Unicode.ScalarText` and treat a dangling surrogate half as its own scalar, matching Core; a regression test was added (`Evaluation.Tests` 8/8). B also accepts the ownership note A recorded (`A-PLAN` rev 4): B created the extra test projects and made two disclosed C0-path repairs; central files (`Directory.Build.props`, `.gitignore`, the solution) are A's, and B has made no further central-file changes.

## Blockers

1. **`BLK-B-1 (peer review pending).`** A has not reviewed the exact rev-3 documents; B-PLAN stays `review`.
2. **`BLK-B-2 (no archives / no labels).`** All counts are publisher-reported; no line-label count exists. MAN-01/PAP-01/INS-01 remain future work.
3. **`BLK-B-3 (no specialist/custodian named).`** Scholarly conventions are proposals; the locked-test custodian is unassigned.
4. **`BLK-B-4 (rights-constrained tracks).`** Papyrus default pool is thin (P01 + P06 CC0); inscription imagery rights unresolved for I01; P02/P03 are NC.
5. **`BLK-B-5 (resolved mid-assignment).`** Contracts absent → published; retained for history only.
6. **`BLK-B-6 (board stale).`** `todo.md` line 297 still lists B-PLAN as "ready; not started" though B's documents and handoffs exist. A owns the board.

## Next actions

1. **A:** review the rev-3 documents and record `A-REV-B-<n>` findings with dispositions; accept/reject `B-CCR-5`, `B-CCR-6`, `B-CCR-7` (and note `B-REV-A-6..9`).
2. **A:** apply `B-CCR-5` (correct todo Section 5's I01 IIIF-imagery statement); decide `B-CCR-6`/`B-CCR-7` and, if accepted, publish them as a `C0-0.4-draft` increment together with the `B-REV-A-6` prose documentation.
3. **A:** update the stale board row for B-PLAN (`BLK-B-6`) and check off todo's "B's review of `C0-0.3-draft`" (line 314) once dispositions are recorded.
4. **Owner:** name the Greek specialist and the locked-test custodian before acquisition/tuning.
5. **B (next, on a new assignment):** begin MAN-01/PAP-01/INS-01 **only** if acquisition is authorized — not automatically from this planning package.

---

## Note on the shared board

`B-NOTE-1`: B's brief forbids editing `todo.md` (A owns it), while the workspace `Agent.md` asks workers to track progress in `todo.md`. B followed the **brief**: the board was not edited; B's plan/status live in this handoff and the four documents. Requested board edits for A are under “Next actions.”

---

## Releases

### Handoff revision 1 — 2026-10-18

- Action: baseline recorded and compared with A's; four planning documents published with provisional field names; status `review`.
- Contract revision consumed: **none** (`docs/contracts/` not yet readable).
- Hashes: manuscripts `4a04508b…`, papyri `03114592…`, inscriptions `4bf7e49e…`, acceptance-plan `6bfb9287…`; `B-PLAN.md` self-hash `pending`.

### Handoff revision 2 — 2026-10-18

- Action: consumed and material-fit-reviewed **`C0-0.2-draft`** (all five documents, hashes verified); re-mapped the three manifest tables to A's real fields; resolved `B-CCR-1..4`; raised `B-CCR-5` (open) and `B-CCR-6` (new); answered A's reviewer open questions in `acceptance-plan.md` §10; status remains `review`.
- A handoff revision consumed: **A-PLAN rev 3 (`1dd44617…`)**.
- Changed files (rev 2): `docs/data/manuscripts.md` `1bd08646…`, `docs/data/papyri.md` `6f8bfac1…`, `docs/data/inscriptions.md` `1b4a0d35…`, `docs/evaluation/acceptance-plan.md` `ded14899…`, `docs/handoffs/B-PLAN.md` `pending`.
- Verified this assignment (2026-10-18): all `[new]` facts in “Source evidence and gaps”; contract hash equality; no code, no archive download, no training, no restricted access.

### Handoff revision 3 — 2026-10-18 (C0-0.3-draft material-fit review)

- Action: consumed and reviewed **`C0-0.3-draft`** (all five documents; hashes verified against A-PLAN rev 5); recorded `B-REV-A-6..10`, dispositions for `B-CCR-5..8`, and answers to A's reviewer questions; updated the four planning documents' consumed-version references to `C0-0.3-draft`; status stays `review`.
- A handoff revision consumed: **A-PLAN rev 6 (git `62eda17`)**.
- Contract delta reviewed: `interchange-and-api.md` §2.0 (native JSON conventions) + `StageArtifact` stage I/O shape + set-level re-version; **no manifest or transcription rule change**, so the rev-2 field mappings and evaluation plan stand.
- Changed files (rev 3, SHA-256):
  - `docs/data/manuscripts.md` `432cb8883a67c24955046cda396e0ca5e151a28b56618480e84b0148b972b90d`
  - `docs/data/papyri.md` `1ae1b19a6c91c6861377ee95df14bf494aef7a05ec3a23be198bdf4dda2e9616`
  - `docs/data/inscriptions.md` `b7daeb0104e8bd60948ea3409116641bf546b3f80627eacb7eccca177f0700af`
  - `docs/evaluation/acceptance-plan.md` `654c2488293b3fdb99212b825a6c39165180e9e2913b91f4846bf1bae40696c1`
  - `docs/handoffs/B-PLAN.md` `pending` (self-hash)
- Response-level change (B's code package): `A-REV-EVAL-1` fixed — see [B-CODE.md](B-CODE.md).
- Checks: five contract hashes recomputed and matched A's record; internal cross-links resolve; no numeric threshold invented.
