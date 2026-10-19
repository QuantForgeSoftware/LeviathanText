# Papyrus track — source cards, rights-subset strategy and line-label backlog

- Package: **B-PLAN v1**, document `docs/data/papyri.md`, draft **rev 1**, 2026-10-18.
- Owner: **Instance B**. Companion documents: [manuscripts.md](manuscripts.md), [inscriptions.md](inscriptions.md), [../evaluation/acceptance-plan.md](../evaluation/acceptance-plan.md), [../handoffs/B-PLAN.md](../handoffs/B-PLAN.md).
- C0 contract consumed: **`C0-0.3-draft`** (proposal, not accepted). A published all five `docs/contracts/*.md` mid-assignment; B verified their hashes (see [B-PLAN.md](../handoffs/B-PLAN.md), `B-REV-A-1` resolved). Field mappings use A's real field names.
- Scope: todo Section 5 (papyri) and Section 3. No archive was downloaded; no label was created.
- Governing todo requirements: PAP-01, PAP-02, DATA-01, DATA-02, DATA-03; Phase 0 gate.

**Headline correction to todo Section 5:** the ICDAR2023 papyrus-letter release (**P02**) carries a **record-level CC BY-NC 4.0** license `[new]`, and **P03 Hell-Date** likewise (`CC BY-NC 4.0`) `[new]`. "No uniform image license" understated the restriction: the **deposit** license itself is non-commercial. These are now carried as **conditional**, not priority, for the default bundled distribution.

## 1. Evidence legend

Same as [manuscripts.md §1](manuscripts.md#1-evidence-legend): `[prior]`, `[new]`, `[PR]`, `[filelist]`, `[inf]`, `[unk]`. All `[new]` checks were performed by Instance B on **2026-10-18**.

## 2. Source cards

### P01 — Zenon Papyri HTR v0.9

- **ID / version to pin:** `P01`; DOI `10.5281/zenodo.6565706`, version **0.9**, published 2022-05-20 `[new]`.
- **Evidence link (checked 2026-10-18):** `https://zenodo.org/records/6565706`.
- **Material / language / witness period:** diplomatic transcription of papyri from the **Zenon archive** (Hellenistic); "Ancient Greek (to 1453)" `[new]`. Exact papyrus date not stated on the record `[unk]` (Zenon archive is 3rd c. BCE `[inf]`).
- **Units and count evidence:**
  - Record description gives **no** image/line/character totals `[unk]`.
  - File listing: **32 `.jpg` + 27 `.xml` = 59 files** `[filelist]`.
  - **Five images have no paired XML** (III.59436, 59437, 59438, 59439, 59440r) `[filelist]` — matching-name pairs = **27** `[filelist]`.
  - Files block "5.2 MB" vs stats "811 MB total volume" — **record inconsistency** `[new]`.
- **Annotation granularity:** PAGE XML prepared in **Transkribus**; line-level `[prior]`; whether geometry and text are complete is not stated `[unk]`.
- **Rights (split):** single record-level **CC BY 4.0** `[new]`; **no** separate image/text statement `[unk]`; source-image credits unstated `[unk]`.
- **Attribution / conditions:** CC BY 4.0 `[new]`.
- **Parent-object lineage:** multiple Zenon-archive items; each JPG/XML pair `[inf]`.
- **Proposed use:** the **real diplomatic papyrus seed** `[prior]`. Tiny: count **27 pairs**, not 32 photographs, and not 32 ground-truthed samples. Does not constitute papyrus coverage.
- **Unanswered checks:** XML validity; actual line/character counts; source-image notices; the 5 extra images' provenance; whether any abbreviation/editorial supply is in the labels.
- **Next action:** **PAP-01**.

### P02 — ICDAR2023 Greek Letters on Papyri (2024 answer release)

- **ID / version to pin:** `P02`; DOI `10.5281/zenodo.13825619`, version "Competition Dataset with Answers", **published 2024-09-22** `[new]`.
- **Evidence links (checked 2026-10-18):** `https://zenodo.org/records/13825619`; related paper `10.1007/978-3-031-41679-8_29`.
- **Material / language / witness period:** Greek literary **book hands on papyri**, "a millennium time span"; Iliad-centric (Homer) `[new]`.
- **Units and count evidence:** **185 surfaces / 136 manuscripts (TMs)**; training **150 images** (108 texts); test **34 image sides** (31 TMs) `[PR]`. The description mixes 150/185/34 subsets `[new]`. Single zip 423.9 MB.
- **Annotation granularity:** **character-level** boxes and classes with preservation tags in READ `[prior]`. Labels remove accents/editorial marks and sometimes use edition-assisted readings `[prior]` — **glyph supplementation, not line labels**.
- **Rights (split) — restricted:**
  - Record-level license: **CC BY-NC 4.0** `[new]` (non-commercial).
  - Image rights are **separate and per-institution**: a JSON `licenses` field (as found Feb 2023) and `1. CompetitionOverview.xlsx` columns "License from the Institution webpage" / "Information on the image usage from the institution" `[new]`.
  - No uniform image license verified `[new]`.
- **Attribution / conditions:** CC BY-NC 4.0 (non-commercial) on the deposit; per-image institutional terms apply independently.
- **Parent-object lineage:** per-TM (Trismegistos) identifiers; surfaces of one fragment stay together `[inf]`.
- **Proposed use:** **conditional glyph supplement only** `[prior]`. A **documented reusable subset** must be selected image-by-image; labels are not visible diplomatic line labels.
- **Unanswered checks:** reconcile 185 vs 150 vs 34; resolve per-image terms; remove accents/editorial-mark stripping before any line use; confirm TM IDs across train/test.
- **Next action:** **PAP-02** (include/exclude ledger).

### P03 — Hell-Date (EGRAPSA Hellenistic Dated Papyri Dataset)

- **ID / version to pin:** `P03`; **concept DOI `10.5281/zenodo.15083589` now resolves to latest version record `10.5281/zenodo.15830980` (v1.2, 2025-09-01)** `[new]`. Earlier v1 = `10.5281/zenodo.15083590` (2025-03-25), now superseded `[new]`.
- **Evidence links (checked 2026-10-18):** `https://doi.org/10.5281/zenodo.15083589`; `https://d-scribes.philhist.unibas.ch/en/hell-date/dataset/`; `.../dataset-summer-2024-version/`.
- **Material / language / witness period:** Hellenistic, **-310 to -3 BCE**; cursive documentary Greek; most from Egypt, a few Near East; TM + checklist identifiers `[new]`.
- **Units and count evidence:** **194 images of 157 papyri** (train 176, test 18) on Zenodo v1 and v1.2 `[PR]`; the summer-2024 page says **187 images of 155 papyri** `[new]` — versions differ.
- **Annotation granularity:** per-character annotation and dating metadata; **missing boxes may mean unannotated, not background** `[prior]`. Not a full line dataset.
- **Rights (split) — mixed:**
  - Record-level: **CC BY-NC 4.0** `[new]`.
  - Per-image mixture (~22 collections) in `data.csv` and a JSON `Licences` array: **CC0** (Turin), **CC BY-NC 3.0/4.0** (Duke, Columbia, Sorbonne), **Public Domain** (British Library, Hamburg, Ann Arbor, St. Louis), **"unknown license"** (Cairo Egyptian Museum, Manchester John Rylands, Pierpont Morgan), **In Copyright** (Oxford) `[new]`.
  - Internal contradiction: Zurich entry text "CC BY-NC 4.0" links to the **CC BY 4.0** deed `[new]`.
- **Attribution / conditions:** the record's NC license cannot relicense images marked "In Copyright"/"unknown"; comply with each source's terms `[new]`.
- **Parent-object lineage:** per-papyrus checklist/TM IDs; images of one papyrus stay together `[inf]`.
- **Proposed use:** **conditional subset** `[prior]`. Only independently reusable (PD/CC0/CC BY) images may enter the default pool; NC/unknown/InC images are excluded or permission-gated.
- **Unanswered checks:** pin the exact version (v1.2 vs v1); reconcile 187/155 vs 194/157; build the per-image rights ledger; resolve the Zurich link contradiction.
- **Next action:** **PAP-02**.

### P04 — Hell-Char / PaLit-Char / Med-Char (diachronic Greek letterforms)

- **ID / version to pin:** `P04`; GitHub `ipavlopoulos/diachronic-greek-letterforms`, branch `main`, **pin commit** (no tag/DOI) `[unk]`.
- **Evidence links (checked 2026-10-18):** repo + `LICENSE` (`creativecommons.org/licenses/by/4.0/`), `CITATION.cff`.
- **Content:** individual Greek **letter crops ("cliplets")** across three periods: **Hell-Char 13,014 images / 24 classes** (3rd–1st c. BCE; 13,046 total incl. a merged non-alphabetic class), **PaLit-Char 384 / 24** (2nd–5th c. CE), **Med-Char 574 / 24** (9th–14th c. CE) `[PR]`.
- **Rights (split):** repository LICENSE = **CC BY 4.0** for "code, datasets (Hell-Char, PaLit-Char, Med-Char) and accompanying material" `[new]`. LICENSE also asks users to "credit the originating resources (Hell-Date, and the source papyri/manuscripts)" `[new]`. A trained model is hosted separately on Hugging Face `[new]`.
- **Parent-object lineage — critical:** Hell-Char **derives from P03 Hell-Date**, whose source-image rights are mixed `[new]`. Track parent-image IDs and overlap with P03.
- **Proposed use:** **conditional auxiliary** — character pretraining/transfer experiments, **not** line recognition `[prior]`.
- **Unanswered checks:** reconcile the downstream CC BY 4.0 grant with each source image's actual rights; enumerate parent-image overlaps with P03; confirm model weights' separate terms.
- **Next action:** **PAP-02** (source-rights mapping).

### P05 — Papyri.info IDP data

- **ID / version to pin:** `P05`; GitHub `papyri/idp.data`, default branch **`master`**, continuously updated — pin a **commit SHA** at intake `[new]`.
- **Evidence link (checked 2026-10-18):** `https://github.com/papyri/idp.data`; README LICENSE section.
- **Content:** DDbDP documentary + DCLP literary **EpiDoc text**, APIS/HGV metadata, RDF, bibliography `[new]`. **No images** `[new]`.
- **Rights (split):** README: "**Creative Commons Attribution 3.0 License**, with copyright and attribution to the respective projects" `[new]`. **No machine-readable `LICENSE.md`** — `master/LICENSE.md` returns **HTTP 404** and GitHub detects no license file `[new]`. Treat the README prose as the grant.
- **Parent-object lineage:** identifiers (TM, HGV, APIS) link texts to objects; images are external `[new]`.
- **Proposed use:** **construct** `[prior]` — the **text side** of a future image/transcription join. The grant does **not** cover externally linked images `[new]`.
- **Unanswered checks:** match text to authorized photographs; strip editorial supplies from visual labels; add line coordinates (absent).
- **Next action:** **PAP-02**.

### P06 — Turin Papyrus Online Platform (Museo Egizio)

- **ID / version to pin:** `P06`; institutional platform, **no DOI**; policy page "last update **27 June 2026**" `[new]`.
- **Evidence link (checked 2026-10-18):** `https://collezionepapiri.museoegizio.it/en-GB/section/Papyrus-Database/Policy-on-access-and-publication-of-papyri/`.
- **Content:** institution-hosted papyrus/ostraca images; Greek subset size **not verified** `[unk]`.
- **Rights (split) — separated by asset type:**
  - Images in the Turin Papyrus Online Database: **CC0** ("No Rights Reserved") `[new]`.
  - **Partner-provided PDFs:** **CC BY-NC** (link resolves to **CC BY-NC 2.5**) `[new]`.
  - Additional registered-user PDF uploads: **CC0** `[new]`.
  - Text content: attribution "when citing the texts"; no license string `[unk]`.
- **Attribution / conditions:** credit partners (Griffith Institute / BBAW); registration required for some access; credentials are personal and must not be shared `[new]`.
- **Proposed use:** **construct** `[prior]` — CC0 images are the most reusable papyrus imagery found; join with P05 texts and create diplomatic line labels.
- **Unanswered checks:** Greek object count; paired-transcription availability; per-item `edm:rights` notices; confirm CC0 applies to the specific images chosen.
- **Next action:** **PAP-02** (Greek-only inventory).

### Restricted papyrus reference — GRK-Papyri

- **Status:** **exclude default** `[prior]`; re-checked `[new]`.
- **Rights:** "prepared and made **freely available for non-commercial research**"; image reproduction rights held by owners (BNUS, British Library, BIPAb) `[new]`. **No license identifier, no version, no DOI** `[new]`.
- **Content:** 50 handwriting samples / **10 scribes**, ~6th c. AD, writer-identification use case (not transcribed lines) `[new]`.
- **Treatment:** non-commercial only; does not fill the line-recognition gap.

## 3. Rights-subset strategy for P02/P03/P06

1. **Build a per-image rights ledger first.** Neither P02 nor P03 has a uniform image license; both override the deposit license per item.
2. **Three buckets:** `default_bundle` (PD / CC0 / CC BY / CC BY-SA), `permission_needed` (CC BY-NC / unknown / In Copyright), `exclude` (unresolvable terms).
3. **Never transfer a record-level license to a linked image.** P05 text ≠ P05 images; P03 deposit license ≠ its "In Copyright" images; P06 metadata ≠ partner PDFs.
4. **Track lineage upstream:** P04 crops inherit P03 image rights; a CC BY 4.0 grant on the derived crops does not cure an upstream "unknown license."

## 4. Annotation granularity: glyph boxes vs line labels

| Source | Granularity | Can it serve line recognition? |
| --- | --- | --- |
| P01 | PAGE line XML (Transkribus) `[prior]` | Yes — the only real line seed; count pairs in PAP-01 |
| P02 | Character boxes/classes `[prior]` | **No** — glyph supplement only; accents stripped |
| P03 | Per-character + dating `[prior]` | **No** — subset reusable for glyphs/pretraining |
| P04 | Letter crops `[PR]` | **No** — pretraining only |
| P05 | Text only `[new]` | **No** — content side; needs geometry + authorized images |
| P06 | Images (Greek subset unknown) `[unk]` | Not yet — needs labels |

**Missing annotations ≠ background.** A region without boxes is "not yet annotated" until proven empty (todo Section 3 gap vocabulary).

## 5. Pilot recommendation and intake order

1. **P01 Zenon** — the only ready real diplomatic **line** seed; scope = 27 pairs.
2. **P06 Turin** — CC0 images; the cleanest imagery to pair with P05 texts for future line labels.
3. **P03 Hell-Date** — reusable per-image subset only; blocks on the rights ledger.
4. **P05 IDP** — text join; pin commit.
5. **P02 ICDAR** — conditional glyph supplement; NC-aware subset.
6. **P04** — auxiliary pretraining; upstream-lineage check.
7. **GRK-Papyri** — excluded default.

## 6. Coverage and gap analysis (papyri)

| Dimension | Covered | Gap |
| --- | --- | --- |
| Period | Hellenistic (P01, P03), Roman/Medieval glyphs (P04) | Koine **Christian** papyri; late-antique documentary |
| Script | Literary book hands (P02), cursive documentary (P03), 6th-c. notarial (restricted) | Difficult cursive beyond Hell-Date; damaged lines |
| Task | Line (P01 only) | Almost all other sources are glyph/character or text-only |
| Damage | Fibers/noise to be tested | Lacunae, partial letters, correction layers under-labeled |
| Rights | P01 CC BY 4.0, P06 images CC0 | P02/P03 NC + per-image mixture is the dominant constraint |

Papyrus coverage is **thin and rights-constrained**; the release must not advertise broad papyrus recognition from P01 alone.

## 7. Manifest field mapping — P01 (against `C0-0.3-draft`)

| Field | P01 value |
| --- | --- |
| `source_id` | `P01` |
| `source_version` | `0.9` (Zenodo) |
| `url_doi` | `https://doi.org/10.5281/zenodo.6565706` |
| `retrieval_date` / `content_checksums` | `not-retrieved` |
| `material_track` | `papyrus` |
| `language_period` | Ancient Greek (to 1453); documentary/Hellenistic; witness date `unknown` |
| `witness_dating` | Zenon archive period not stated → `unknown` |
| `units_evidence` | "Zenodo listing: 32 JPG, 27 XML (file-list-derived)"; archive 5.2 MB (files block) vs 811 MB (stats) → note conflict |
| `paired_line_counts` | **27** candidate pairs (`file-list-derived`); line count `unverified` |
| `license_image` | `unverified` — no separate image statement |
| `license_text` / `license_annotation` | `CC-BY-4.0` |
| `license_code` / `license_weights` | n/a |
| `license_evidence` | `https://zenodo.org/records/6565706` (2026-10-18) |
| `attribution` | Zenon/D-Scribes dataset creators |
| `allowed_intended_use` | `train` + `dev` (priority intake) |
| `unresolved_questions` | XML validity; line count; source-image credits; provenance of the 5 unpaired images |
| `provenance_notes` | 32 photographs ≠ 32 ground-truthed samples |
| `item_id` (item record) | per papyrus item; `external_ids` = `TM:<n>` where present |
| `object_type` | `roll/papyrus-sheet` |
| `material` (item) | papyrus |
| `asset_kind` | `photograph` + `transcription-xml` |
| `label_granularity` | `line` |
| `pairing_status` | `paired` for 27; `unpaired-unsynced` for the 5 extra images |
| `annotation_completeness` | `partial` (5 images unannotated) |
| `transcription_profile` | `pre-project` |

Repo sources without a DOI (P04, and P05 with its moving `master`) must use `source_version: pin:<commit>`; `unpinned` blocks intake — matches A's `source_version` rule.

**Resolution of prior change requests:** `B-CCR-3` (per-item rights) satisfied via `license_image = mixed-per-item` + §4 asset-level `license`/`evidence`; `B-CCR-4` satisfied by `label_granularity` (`character-box`, `glyph`, `line`). Residual: `asset_kind` has no value for a text-only catalogue/metadata asset (I02 PDF, P05 metadata) → see handoff `B-CCR-6`.

## 8. Line-label and rights backlog (papyri)

| Task | Owner | Prerequisite | Output | Completion evidence |
| --- | --- | --- | --- | --- |
| **PAP-01** | D-P | P01 card | Inspect 27 pairs; count lines/characters; validate XML; flag the 5 unpaired images as unannotated | Per-file report; documented label policy; no volume target |
| **PAP-02** | D-P | This card | Include/exclude ledger for P02/P03 images; P04 lineage mapping; P05↔image match; Greek-only P06 inventory | Per-image rights table; overlap report; estimated missing-label effort |
| **PAP-03** | D-P + epigraphist/papyrologist | PAP-02 | Diplomatic line labels for the reusable P06/P03 subset | Label count + adjudication notes |
| **DATA-02** | Integrator + workers | All | Dedup via TM/inventory IDs; audit P01/P02/P03/P04 overlap | Overlap report |
| **DATA-03** | Greek reviewer | PAP-03 | Locked papyrus acceptance objects | Counts + effort |

## 9. Open questions handed to A / the owner

1. Per-item rights — **resolved** by `C0-0.3-draft`: `license_image = mixed-per-item` + §4 asset-level `license`/`evidence`.
2. Granularity vocabulary — **resolved**: `label_granularity` covers `line`/`word`/`character-box`/`glyph`/`region`/`none`. Residual: no `asset_kind` value for a text-only catalogue/metadata asset → `B-CCR-6`.
3. Whether NC (P02/P03) material may be used for **research-only** training that is not redistributed; recommend recording both bundled and research-only tracks rather than silently dropping NC data (todo Section 2 permits AI use; NC limits redistribution).
4. Suggested source-backed replacement for A's invented EX-5: a real P01 lacuna line once PAP-01 inspects the XML.
