# Handoff — B-PLAN (Instance B: corpus intake and evaluation planning)

## Status and owner

- Package: **B-PLAN v1**, brief at [`docs/tasks/instance-b-corpus-and-evaluation.md`](../tasks/instance-b-corpus-and-evaluation.md).
- Owner: **Instance B** (this session). Status: **review** — the four owned planning documents are published at **rev 2**, now mapped to Instance A's accepted-for-review contract `C0-0.2-draft`; awaiting A's review of the exact revisions and A's dispositions of the remaining change request. Not `done` because “a different instance reviews each handoff” (todo Section 6) has not yet happened.
- Scope performed: **documentation only**. No code, scripts, executable tests, machine-readable manifests/schemas or project files were created; Git/worktrees were not initialized; no dependencies installed; no dataset/model archives downloaded; no models trained; no institutions contacted; no restricted resource accessed. `todo.md`, `Agent.md`, `docs/tasks/*`, `docs/contracts/*` and `docs/handoffs/A-PLAN.md` were **read but not edited** (the shared board is A's; see `B-NOTE-1`).
- Contract version consumed: **`C0-0.2-draft`** (all five `docs/contracts/*.md`; hashes verified against A's record — see “Contract version consumed”).

## Baseline inputs

Baseline: **PLAN-2026-10-18-two-instance-v1** (todo Section 6). No Git repository. SHA-256 captured by Instance B on **2026-10-18**:

| File | SHA-256 | Notes |
| --- | --- | --- |
| `todo.md` (at B start) | `61ad97a2d3f3bc15e407b651bba81b59ffd4ad70d6dfbdf641e429e75283ce07` | A's starting hash `6466…`; drift is A's board edits — recorded, proceeding per brief |
| `todo.md` (now) | `f1313ac4224fa5182d5a4f6e889cb85650d7dda7ede0ec45c66398fcf4d994ae` | A's rev-3 board update landed mid-assignment |
| `Agent.md` | `536b3919dd168840fa014535223f86d801eef82c9a9fe4a62405e42f8f7b02cf` | matches A |
| `docs/tasks/instance-a-contracts-and-integration.md` | `72f52abf6f27f5be482035795b0d4c437670a734e2b5f06e333da27314a83e97` | matches A |
| `docs/tasks/instance-b-corpus-and-evaluation.md` | `1a88f2e17655c4bcb5ad1ee22a52db0bb4bfa733848581643c8a7b7307dcdc2c` | matches A |
| `docs/handoffs/A-PLAN.md` consumed | `1dd4461714b6794b148e342b3b856f843828019b06d938104a3220b2d72b0f31` | A rev 3 (final); its rev-3 entry records pre-entry hash `794efc05…` |

Only `todo.md` and `A-PLAN.md` advanced, and only because A updated its board/handoff. No substantive scope difference.

## Contract version consumed

**Consumed: `C0-0.2-draft`** — A published all five contract documents mid-assignment. B fetched and hashed each; every hash **matches** A's rev-3 record, so the reviewed revision is unambiguous:

| Contract document | SHA-256 (B-verified) | A's recorded hash | Match |
| --- | --- | --- | --- |
| `docs/contracts/corpus-manifest.md` | `e504c6ab63551ea2bc998ce4f579d94c9659f5fcc7a6ca4edde9231cd1b09874` | `e504c6ab…` | ✅ |
| `docs/contracts/greek-transcription.md` | `c5a01dbf07ee1f9ba9e33c6bc3032a3201964a1f5f5a13f9012c2d2a85555776` | `c5a01dbf…` | ✅ |
| `docs/contracts/domain-and-pipeline.md` | `8b14ef1e51761edbaed875ccb49a0d1a1315fbe2db1ae66d8b4c11af61313d9e` | `8b14ef1e…` | ✅ |
| `docs/contracts/interchange-and-api.md` | `2ba8fb5185d988f1f3f6a2626b6eb4d0bd17752642ee0853efebe4743a492f18` | `2ba8fb51…` | ✅ |
| `docs/contracts/examples.md` | `a38c956f9f5bbf6ec0227f93fb921a23f6ebfea9d6f4b8e8089a977745604a71` | `a38c956f…` | ✅ |

B's rev-1 documents were written before the contracts were readable and used provisional field names. **Rev 2 replaces them with A's real field names** and this handoff reviews `C0-0.2-draft`. No revision is accepted: acceptance is A's recorded disposition of this review.

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

Reviewed: `docs/handoffs/A-PLAN.md` rev 3 (hash `1dd44617…`) and **all five `C0-0.2-draft` documents** (hashes verified above).

| ID | Severity | Affected section | Finding / example | Status |
| --- | --- | --- | --- | --- |
| `B-REV-A-1` | High → **resolved** | A-PLAN “Contract revisions” | A-PLAN initially listed contracts as published while `docs/contracts/` was absent in this checkout; A then published all five and the hashes match. | **Resolved** — no action beyond the verified-hash table above. |
| `B-REV-A-2` | Low | A-PLAN “Baseline inputs” | `todo.md` drift `6466…`→`61ad…`→`f131…` explained by A's board edits. | No action. |
| `B-REV-A-3` | Medium → **satisfied** | `greek-transcription.md` §1–§2 | The contract **already forbids** expansion/restoration in L1 (layers; §2.7 abbreviations; §3 no supplied accents), covering M01–M03 abbreviations and nomina sacra. Residual is the specialist's sign inventory, which A flags. | **No change requested.** |
| `B-REV-A-4` | Medium → **satisfied** | `examples.md` §1, EX-1 | Scoring explicitly excludes edition-assisted/expanded text from the visual-recognition score and keeps the expansion as a separately-labeled result (the P02 risk). | **No change requested.** |

**Contract change requests (`B-CCR-n`):**

| ID | Request | Status against `C0-0.2-draft` |
| --- | --- | --- |
| `B-CCR-1` | Pin canonical-equivalence + grapheme/boundary + scalar↔UTF-16 mapping. | **Resolved** — `greek-transcription.md` §4 (NFC-only, revision-scoped scalar offsets, UAX #29 graphemes). |
| `B-CCR-2` | Record count provenance + dual figures instead of one line count. | **Resolved** — `corpus-manifest.md` §2 `units_evidence` (provenance tag) + `paired_line_counts` (audited only). |
| `B-CCR-3` | Per-item rights instead of a single rights field. | **Resolved** — `license_image = mixed-per-item` + §4 asset-level `license`/`evidence`. |
| `B-CCR-4` | Explicit annotation-granularity vocabulary. | **Resolved** — `label_granularity` (`line`/`word`/`character-box`/`glyph`/`region`/`none`). |
| `B-CCR-5` | Correct todo Section 5: I01 IIIF imagery is **not** verified CC BY 4.0. | **Open** — A owns `todo.md`; request stands. |
| `B-CCR-6` | (new) Add a `preservation_tags` asset field (P02 tags, I03 damage) and a text-only `catalog`/`metadata-text` `asset_kind` (I02 PDF, P05 metadata), or an `other:<text>` escape on `asset_kind`. | **Open** — proposed in `corpus-manifest.md` §7 Q1 and `acceptance-plan.md` §10. |

`B-REV-A-5` (informational): `greek-transcription.md` §2.2 proposes `ϲ` U+03F2 for lunate sigma; this is a specialist decision (A's own open question 1) and B records no objection, only that annotator speed/ambiguity must be tested.

## Blockers

1. **`BLK-B-1 (peer review pending).`** A has not reviewed the exact rev-2 documents; B-PLAN stays `review`.
2. **`BLK-B-2 (no archives / no labels).`** All counts are publisher-reported; no line-label count exists. MAN-01/PAP-01/INS-01 remain future work.
3. **`BLK-B-3 (no specialist/custodian named).`** Scholarly conventions are proposals; the locked-test custodian is unassigned.
4. **`BLK-B-4 (rights-constrained tracks).`** Papyrus default pool is thin (P01 + P06 CC0); inscription imagery rights unresolved for I01; P02/P03 are NC.
5. **`BLK-B-5 (resolved mid-assignment).`** Contracts absent → published; retained for history only.

## Next actions

1. **A:** review the rev-2 documents and record `A-REV-B-<n>` findings with dispositions; accept/reject `B-CCR-5` and `B-CCR-6`.
2. **A:** correct todo Section 5's I01 IIIF-imagery statement (`B-CCR-5`) and, if accepted, add the `B-CCR-6` fields as a numbered revision (`C0-0.3-draft`).
3. **Owner:** name the Greek specialist and the locked-test custodian before acquisition/tuning.
4. **B (next, on a new assignment):** begin MAN-01/PAP-01/INS-01 **only** if acquisition is authorized — not automatically from this planning package.

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
