# Manuscript track — source cards, pilot and intake backlog

- Package: **B-PLAN v1**, document `docs/data/manuscripts.md`, draft **rev 1**, 2026-10-18.
- Owner: **Instance B**. Companion documents: [papyri.md](papyri.md), [inscriptions.md](inscriptions.md), [../evaluation/acceptance-plan.md](../evaluation/acceptance-plan.md), [../handoffs/B-PLAN.md](../handoffs/B-PLAN.md).
- C0 contract consumed: **`C0-0.3-draft`** (proposal, not accepted). Instance A published all five `docs/contracts/*.md` mid-assignment; B verified their SHA-256 against A's record (see [B-PLAN.md](../handoffs/B-PLAN.md), `B-REV-A-1` now resolved). Field mappings below use A's real field names.
- Scope: todo Section 5 (manuscripts) and Section 3 (transcription contract). No archive was downloaded; no line was labeled. This document plans intake, it does not perform it.
- Governing todo requirements: MAN-01, DATA-01, DATA-02, DATA-03 (todo Section 5 backlog); Phase 0 gate (todo Section 6); material-specific workflows (todo Section 3).

## 1. Evidence legend

Every fact carries one tag. Do not promote an `[inf]` claim to a verified count without an archive check.

| Tag | Meaning |
| --- | --- |
| `[prior]` | Recorded in todo Section 5 before this assignment; inherited, not re-checked here. |
| `[new]` | Re-checked by Instance B on 2026-10-18 at the named URL. |
| `[PR]` | Publisher-reported count or claim (the depositor's description), not verified against files. |
| `[filelist]` | Derived from a rendered file listing; provisional, not authoritative. |
| `[inf]` | Inferred by this assignment; not stated by the source. |
| `[unk]` | Unknown / not confirmed. Treat as a required future check. |

## 2. Source cards

### M01 — EPARCHOS v1.0 (Add. 6791)

- **ID / version to pin:** `M01`; Zenodo dataset DOI `10.5281/zenodo.4095301`, version **1.0** `[new]`.
- **Evidence links (checked 2026-10-18):** `https://zenodo.org/records/4095301`; HTR-United catalog entry `_pid fc63dcc30` via `https://htr-united.github.io/htr-united/catalog.json` `[new]`.
- **Title:** "EPARCHOS - Historical Greek handwritten document dataset" `[new]`; the HTR-United catalog renders it as "EPARCHOS" `[new]`.
- **Material / language / witness period:** Greek handwritten codex, **c. 1500–1530**; subset of **British Library Add. 6791**; language field "Ancient Greek (to 1453)" `[new]`. **Two hands:** Antonius Eparchos and Camillos Zanettus (ff. 104r–174v) `[new]`. Texts by Hierocles, Matthaeus Blastares, Michael Psellos `[new]`. **Material not stated** `[unk]` (todo Section 5 assumed "parchment"; the record does not say so — carried as unknown).
- **Units and count evidence — CONFLICT:**
  - Zenodo description: **120 pages, 9,285 lines, 18,809 words (6,787 unique)** `[PR]`.
  - HTR-United catalog: **120 files, 2,272 lines, 116,894 characters**, hands "less than 11 (exact)", time 1500–1530, Page-XML `[new]`.
  - Archive size 114.7 MB `[new]`.
  - **Disposition:** the 120-page count agrees; the **9,285 vs 2,272 line** figure does not (≈4× gap). todo Section 5 flagged this; it is **still unresolved**. Both figures may be honest under different definitions (e.g. geometric line regions vs transcribed lines, or count of text-region objects). **MAN-01 must count actual PAGE `<TextLine>` elements before any volume target.**
- **Annotation granularity:** PAGE XML text-region, text-line and word polygons with transcriptions `[PR]`. Whether abbreviations are expanded or kept visible is **unknown** `[unk]`.
- **Rights (split):**
  - Image: **not separately stated** `[unk]`; the record carries one CC BY 4.0 grant. Source object Add. 6791 is a pre-1600 British Library manuscript (public-domain status likely `[inf]`), but BL imaging terms were **not** checked here.
  - Text/annotation: **CC BY 4.0** `[new]` (record-level "Creative Commons Attribution 4.0 International").
  - Code / weights: n/a (no code or model shipped).
- **Attribution / conditions:** CC BY 4.0 requires attribution and a change notice for adaptations `[new]`.
- **Parent-object lineage:** all pages belong to **one physical codex (Add. 6791)**; the two hands split at ff. 104r–174v `[new]`.
- **Proposed use:** primary development manuscript corpus; **both** a witness-level holdout and a hand-level holdout are available inside this one source (see §5).
- **Unanswered checks:** actual `<TextLine>` count; image/text pairing; whether transcriptions are diplomatic or expanded (nomina sacra, abbreviations); material; BL imaging terms; per-page hand attribution.
- **Next action:** **MAN-01** (count + label-policy audit), then **DATA-02** (lineage).

### M02a — Stavronikita 53 v1.0

- **ID / version to pin:** `M02a`; DOI `10.5281/zenodo.5595669`, version **1.0**, published 2021-10-19 `[new]`.
- **Evidence link (checked 2026-10-18):** `https://zenodo.org/records/5595669`.
- **Material / language / witness period:** parchment, **four-gospel manuscript, dated 1301–1350**; dimensions ≈ 250×185 mm; "elegant minuscule", majuscule rare; tachygraphical symbols and abbreviations; chrysography, epititles, initials `[new]`.
- **Units and count evidence:** **54 pages, 1,038 lines, 5,592 words (2,374 unique)** `[PR]`; archive 242.9 MB `[new]`. Internal consistency is plausible (≈19 lines/page) `[inf]`. Not file-verified.
- **Annotation granularity:** PAGE region/line/word polygons with text `[PR]` (todo); exact element set not inspected `[unk]`.
- **Rights (split):** single record-level **CC BY 4.0** `[new]`; no image/text split `[unk]`.
- **Attribution / conditions:** CC BY 4.0 attribution + change notice `[new]`.
- **Parent-object lineage:** one codex (Stavronikita 53), 54 surfaces `[inf]`.
- **Proposed use:** **Koine gospel text through a later minuscule witness** `[prior]`; candidate dev or held-out witness.
- **Unanswered checks:** how tachygraphic signs, nomina sacra and expanded words are labeled; that Greek is visible-diplomatic not normalized; parchment condition coverage.
- **Next action:** **MAN-01**.

### M02b — Stavronikita 79 v1.0

- **ID / version to pin:** `M02b`; DOI `10.5281/zenodo.5578136`, version **1.0**, published 2021-10-19 `[new]`.
- **Evidence link (checked 2026-10-18):** `https://zenodo.org/records/5578136`.
- **Material / language / witness period:** **paper**, **16th century**, ≈ 220×165 mm; epititles and red initials; tachygraphic symbols and abbreviations `[new]`. Language field absent `[unk]`.
- **Units and count evidence:** **40 pages, 803 lines, 4,389 words (2,069 unique)** `[PR]`; archive 227.1 MB `[new]`.
- **Annotation granularity:** PAGE region/line/word polygons `[PR]`; element set not inspected `[unk]`.
- **Rights (split):** single record-level **CC BY 4.0** `[new]`.
- **Attribution / conditions:** CC BY 4.0 `[new]`.
- **Parent-object lineage:** one codex (Stavronikita 79), 40 surfaces `[inf]`.
- **Proposed use:** additional hand/style coverage; **not** a genre guarantee for Classical/Koine (todo Section 5) `[prior]`.
- **Unanswered checks:** visible vs expanded label policy; text genre; publication-vs-creation date mismatch (published 2021-10-19, page created 2021-10-25) `[new]`.
- **Next action:** **MAN-01**.

### M02c — Stavronikita 114 v1.0

- **ID / version to pin:** `M02c`; DOI `10.5281/zenodo.5578251`, version **1.0**, published 2021-10-19 `[new]`.
- **Evidence link (checked 2026-10-18):** `https://zenodo.org/records/5578251`.
- **Material / language / witness period:** **paper**, **end of the 15th century**, ≈ 218×150 mm; red initials and epititles `[new]`. Language field absent `[unk]`.
- **Units and count evidence:** **44 pages, 1,051 lines, 5,467 words (2,877 unique)** `[PR]`; archive 241.1 MB `[new]`.
- **Annotation granularity:** PAGE region/line/word polygons `[PR]`; element set not inspected `[unk]`.
- **Rights (split):** single record-level **CC BY 4.0** `[new]`.
- **Parent-object lineage:** one codex (Stavronikita 114), 44 surfaces `[inf]`.
- **Proposed use:** additional witness; **kept whole in one split** (todo Section 5) `[prior]`.
- **Unanswered checks:** page/crop geometry and decorative-initial handling; visible vs expanded labels.
- **Next action:** **MAN-01**.

### M03 — Vienna Byzantine Greek school corpus (Dresden SLUB A.151)

- **ID / version to pin:** `M03`; repository `HTR-School-Vienna/2023--byzantine-greek`, branch **`main`**, pin the **commit** (no tag, no DOI confirmed) `[unk]`.
- **Evidence links (checked 2026-10-18):** `https://github.com/HTR-School-Vienna/2023--byzantine-greek`; `.../blob/main/LICENSE.md`; `.../blob/main/README.md`.
- **Material / language / witness period:** Byzantine Greek, **15th-century liturgical** material; codex **Dresden, SLUB A. 151**; Transkribus model "15th c. liturgical" `[new]`. Material not stated `[unk]`; hands not stated `[unk]`.
- **Units and count evidence:** **66 images set as ground truth**; remaining pages automatic transcription with reported **22.3% error** `[PR]`. No line/word/page totals `[unk]`. The file `GroundTruth_Dresden_A_151_30.01.24.txt/.xml` implies a 2024-01-30 date `[filelist]`.
- **Annotation granularity:** text transcriptions; automatic vs ground-truth files distinguished in README but the named files were **not confirmed present** in the `main` listing `[unk]`.
- **Rights (split) — CONTRADICTORY:**
  - Repository license: **CC BY-SA 4.0** `[new]` (LICENSE.md = full CC BY-SA 4.0 text). README calls the transcriptions only "the Creative Commons 4 licence" (no version, no variant) `[new]`.
  - Images: README **both** says "the digitisation is **not** copyright free" **and** says SLUB images are under the **Public Domain Mark – No Copyright Protection** `[new]`.
  - **Disposition:** transcription license is **CC BY-SA 4.0** (ShareAlike); image rights are **unresolved**. Public Domain Mark asserts status, it does not grant a license. Confirm against the SLUB item notice before intake.
- **Attribution / conditions:** CC BY-SA 4.0 ShareAlike on adapted material; attribution; any Transkribus model has separate terms `[new]`.
- **Parent-object lineage:** one codex (Dresden A.151); keep all 66 images together in split design `[inf]`.
- **Proposed use:** **conditional auxiliary only** `[prior]`; imaging/adapter experiment and later-minuscule coverage, not a Classical/Koine acceptance witness.
- **Unanswered checks:** reconcile the image-rights contradiction; confirm image/text alignment; retain only verified ground truth.
- **Next action:** **MAN-01** (M03 only with its distinct obligations retained).

### Restricted manuscript reference — Codex Sinaiticus XML

- **Status:** **exclude default** `[prior]`.
- **Rights:** transcription **CC BY-NC-SA 3.0**; images separate terms `[prior]`.
- **Treatment:** scholarly reference or separately permitted research only; do **not** place in the broadly reusable distributed corpus. No re-check performed this assignment `[prior]`.

## 3. Count reconciliation

| Source | Pages (agreed) | Line figure A | Line figure B | Status |
| --- | --- | --- | --- | --- |
| M01 | 120 `[PR]`=`[new]` | 9,285 `[PR]` (Zenodo) | 2,272 `[new]` (HTR-United) | **unresolved** — count `<TextLine>` in MAN-01 |
| M02a | 54 `[PR]` | 1,038 `[PR]` | — | plausible `[inf]`, not file-verified |
| M02b | 40 `[PR]` | 803 `[PR]` | — | plausible `[inf]`, not file-verified |
| M02c | 44 `[PR]` | 1,051 `[PR]` | — | plausible `[inf]`, not file-verified |

Do **not** publish a manuscript corpus total until M01 is reconciled. The four datasets are **one population type** (later minuscule), not four independent ancient-hand populations (todo Section 5).

## 4. Pilot recommendation and intake order

**Recommended first manuscript intake (most useful first):**

1. **M01 EPARCHOS** — largest page count, two named hands (built-in hand holdout), CC BY 4.0. Blocks: resolve the line-count conflict, then confirm the abbreviation/diacritic label policy.
2. **M02a Stavronikita 53** — Koine gospel, dated, minuscule, same license. Good as a **held-out witness** because it is a different book from M01.
3. **M02b / M02c Stavronikita 79 and 114** — add paper-based later hands; keep for generalization strata.
4. **M03** — conditional; resolve image wording first; treat as auxiliary.

**No collection is selected by the project owner here.** This is a recommendation only; the owner selects (todo Section 5). Later witnesses cannot stand in for early majuscule or damaged material.

## 5. Split design — witness and hand holdouts

- **Object-level split first.** All pages, recto/verso, crops and derived images of one **codex** stay in one split (todo Section 5; A's split-grouping field, *provisional*).
- **Available hand holdout in M01:** the Camillos Zanettus run **ff. 104r–174v** can be held out as an *unseen-hand* test while Antonius Eparchos folios train/develop. Confirm the page-level hand boundary in MAN-01 before using it.
- **Available witness holdout:** hold out at least one entire Stavronikita witness (recommend **M02a**) as an unseen-book manuscript test.
- **Do not** randomly split lines within a book — that leaks page/ink/hand characteristics.
- Because 4 of 5 sources are late minuscule, an honest manuscript acceptance report **must disclose** that no early-majuscule witness is included `[inf]`.

## 6. Coverage and gap analysis (manuscripts)

| Dimension | Covered by pilot | Gap (must be stated in release claims) |
| --- | --- | --- |
| Period/hand | Late 15th–16th c. minuscule; one 14th-c. minuscule | **Early Greek majuscule absent**; patristic/philosophical minuscule beyond M01 absent |
| Material | Paper (M02b/c), one probable parchment (M02a stated) | No palimpsest; no heavily damaged codex; M01 material unknown |
| Signs/marks | Tachygraphic symbols, abbreviations present (M01–M02a) | Coverage counts for ligatures, nomina sacra, overbars, supralinears unknown |
| Diacritics | Polytonic present by nature; treatment **unknown** `[unk]` | Whether labels preserve visible marks vs supply accents is unaudited |
| Genre | Gospels (M02a), liturgical (M03), misc. classical (M01) | Documentary/Christian-letter manuscript gaps; no verse manuscript |

**Abbreviations/nomina sacra** are the highest-risk label-policy area: an expanded abbreviation must never become a visible-text training label (todo Section 3; A's transcription contract `C0-0.3-draft` §1/§2.7).

## 7. Manifest field mapping — M01 (against `C0-0.3-draft`)

Verified field dictionary: `docs/contracts/corpus-manifest.md` SHA-256 `e504c6ab…`. Mapped at the three record levels it defines.

| Field | M01 value |
| --- | --- |
| `source_id` | `M01` |
| `source_version` | `1.0` (Zenodo) — pin the version; the DOI follows the newest release |
| `url_doi` | `https://doi.org/10.5281/zenodo.4095301` |
| `retrieval_date` | not yet inspected → `not-retrieved` (plan the date at intake) |
| `content_checksums` | `not-retrieved` |
| `material_track` | `manuscript` |
| `language_period` | Ancient Greek (to 1453); genre (Koine/Classical) mix → record `unknown` components |
| `witness_dating` | 1500–1530; `basis` = publisher statement `[new]` |
| `units_evidence` | "Zenodo: 120 pages, 9,285 lines, 18,809 words" (`publisher-reported`); "HTR-United: 120 files, 2,272 lines, 116,894 chars" (`file-list-derived`) `[new]` |
| `paired_line_counts` | `unverified` (MAN-01) |
| `license_image` | `unverified` — not separately stated; British Library imaging terms unchecked |
| `license_text` | `CC-BY-4.0` |
| `license_annotation` | `CC-BY-4.0` |
| `license_code` / `license_weights` | n/a |
| `license_evidence` | `https://zenodo.org/records/4095301` (checked 2026-10-18) |
| `attribution` | EPARCHOS Zenodo creators |
| `conditions_exceptions` | attribution + change notice (CC BY) |
| `allowed_intended_use` | `train` + `dev` (priority intake) |
| `unresolved_questions` | material; `<TextLine>` count; visible-vs-expanded label policy; BL terms |
| `provenance_notes` | line-count conflict 9,285 vs 2,272 |
| `item_id` (item record) | project-minted; `external_ids` = `shelfmark:BL Add. 6791` |
| `object_type` | `codex-leaf` |
| `material` (item) | `unknown` |
| `hand_style` | Antonius Eparchos; Camillos Zanettus (ff.104r–174v) `[new]` |
| `holds_alternate_views` | yes (recto/verso) → drives split grouping |
| `asset_kind` (asset record) | `scan`/`photograph` + `transcription-xml` |
| `coordinate_frame` | each PAGE geometry against its own capture |
| `license` / `evidence` (asset) | `inherit-source` |
| `label_granularity` | `line` (+ `word`); regions present |
| `transcription_profile` | `pre-project` (convert or record gaps in MAN-02) |
| `pairing_status` | `paired` after MAN-01 audit |
| `annotation_completeness` | `unknown` until MAN-01; abbreviation policy unknown |
| `credit_required` | source attribution string |

**Resolution of prior change request `B-CCR-2`:** satisfied by `units_evidence` (provenance tag) + `paired_line_counts` (audited only). `B-CCR-4` satisfied by `label_granularity`.

## 8. Intake and annotation backlog (manuscripts)

| Task | Owner (role) | Prerequisite | Output | Completion evidence |
| --- | --- | --- | --- | --- |
| **MAN-01** | D-M (manuscript data worker) | Pending nothing from C0; manifest `C0-0.3-draft` available | Reconciled page/line counts for M01–M03; abbreviation/diacritic label audit; witness/hand holdout proposal | Count of `<TextLine>` per dataset; documented label policy; no "ready" claim without it |
| **MAN-02** | D-M | MAN-01 | Convert M01/M02a PAGE annotations to the agreed diplomatic profile (or record the gaps) | Field-by-field mapping; list of unsupported constructs |
| **MAN-03** | Greek reviewer | MAN-02 | Adjudicate a small development sample; confirm nomina sacra / abbreviation handling | Written adjudication notes; unresolved readings kept |
| **MAN-04** | D-M | M01 held-out hand boundary | Verify the ff.104r–174v hand split and hand-holdout feasibility | Page-level hand list |
| **DATA-02** | Integrator + data workers | All track cards | Cross-source dedup by object ID + image lineage | Overlap report (M01–M03 disjoint books expected) |
| **DATA-03** | Greek reviewer + workers | Pilots | Locked manuscript acceptance objects | Counts + annotation effort; no invented accuracy |

## 10. MAN-02a execution record (2026-10-19, single-instance mode: Instance A absorbed all packages)

- **Acquisition:** M02a v1.0 downloaded from `https://zenodo.org/api/records/5595669` to `.artifacts/datasets/m02a-v1.0/` (retrieval 2026-10-18). **Zip md5 verified** against the record (`d23306723f387845c319cb817e838571`). 111 files extracted; the Greek-lettered, non-UTF-8 zip root was remapped to `m02a/`.
- **Pairing audit (inspected):** **54/54 image/PAGE pairs** (PNG images under `images/`, XMLs under `xml/`) — matches the record's description. `imageFilename` attributes confirmed consistent.
- **Audited line counts:** **1038 real TextLines** (publisher figure confirmed). Text lives in the **Word layer**; the adapter assembles line text from words (assembly recorded in revision provenance). **157 lines excluded** from all splits because their labels contain literal editorial markup (`<+>`-style) — editorial interventions never become visible-text training labels (todo §3 invariant).
- **Rights:** single record-level **CC BY 4.0**; attribution retained in the ledger.
- **Pilot training (green test):** page-level split 571 train / 131 dev / 179 page-holdout test; CRNN/CTC 150 epochs; **page-holdout CER 0.811**; label set 129 grapheme classes (overbarred nomen-sacra composites present). The publisher's line-level `.lst` splits were deliberately **not used** (within-book line randomization, todo §5 warning preserved).
- **Label-policy cautions (pending specialist review):** expansion-lite labels (`ἰῦ`, `χῦ`), diaeresis forms (`ϊ`), and whether label spaces reflect visible spacing are open questions recorded in `data/manifests/manuscripts/m02a.json`.
- **Corpus status:** M02a is **audited, not training-ready**: specialist adjudication (DATA-03) pending; single-codex coverage; early-majuscule gap remains. No release claim.

## 9. Open questions handed to A / the owner

1. Count handling — **resolved** by `C0-0.3-draft`: publish both figures in `units_evidence` (`publisher-reported` vs `file-list-derived`) and keep `paired_line_counts` `unverified`.
2. Visible-vs-expanded abbreviation label policy for the transcription profile — **A's `greek-transcription.md` §1/§2.7 already forbids expansion in L1**; the residual is the Greek specialist's sign-inventory decision (`B-REV-A-3`, now informational, not a change request).
3. Whether the pilot must include **any** early-majuscule witness before a manuscript acceptance report may be published (recommend: disclose its absence rather than blocking).
4. Suggested source-backed replacement for A's invented EX-1: a real M01 nomina-sacra/overbar line once MAN-02 converts the labels.
