# Inscription track — source cards and image-to-line corpus construction plan

- Package: **B-PLAN v1**, document `docs/data/inscriptions.md`, draft **rev 1**, 2026-10-18.
- Owner: **Instance B**. Companion documents: [manuscripts.md](manuscripts.md), [papyri.md](papyri.md), [../evaluation/acceptance-plan.md](../evaluation/acceptance-plan.md), [../handoffs/B-PLAN.md](../handoffs/B-PLAN.md).
- C0 contract consumed: **`C0-0.3-draft`** (proposal, not accepted). A published all five `docs/contracts/*.md` mid-assignment; B verified their hashes (`B-REV-A-1` resolved). Field mappings use A's real field names.
- Scope: todo Section 5 (inscriptions) and Section 3. No archive was downloaded; no label was created.
- Governing todo requirements: INS-01, DATA-01, DATA-02, DATA-03; Phase 0 gate.

**Headline correction to todo Section 5:** the claim that **I01 I.Sicily's IIIF imagery is CC BY 4.0** is **not supported** by the sources checked on 2026-10-18. The EpiDoc **data** deposit is CC BY 4.0, but the site's own images policy reserves images **for study** and directs publication requests to the holding museums `[new]`. I01 imagery therefore moves from "verified CC BY 4.0" to **"data verified; imagery unresolved per item."**

## 1. Evidence legend

Same as [manuscripts.md §1](manuscripts.md#1-evidence-legend): `[prior]`, `[new]`, `[PR]`, `[filelist]`, `[inf]`, `[unk]`. All `[new]` checks were performed by Instance B on **2026-10-18**.

## 2. Source cards

### I01 — I.Sicily / Crossreads

- **ID / version to pin:** `I01`; GitHub `ISicily/ISicily`, default branch **`master`** → pin a **commit**; Zenodo concept DOI `10.5281/zenodo.2556743` resolves to version record `10.5281/zenodo.23210545` (**v1.0.1, published 2026-10-05**) `[new]`.
- **Evidence links (checked 2026-10-18):** `https://github.com/ISicily/ISicily`; license file is **`licence.txt`** (British spelling — `LICENSE.md` does **not** exist, both `master`/`main` 404) `[new]`; `https://doi.org/10.5281/zenodo.2556743`.
- **Material / language / witness period:** inscriptions of ancient **Sicily**, end **7th c. BCE – ~7th c. CE**; languages Ancient Greek, Latin, Oscan, Punic, Elymian, Sikel; material mostly stone, expanding to metal/ceramic/instrumentum/coin legends `[new]`.
- **Units and count evidence — inconsistent snapshots:**
  - Zenodo: "almost 5,000 Sicilian records, c. 4,000 on stone/metal" `[PR]`.
  - isicily.org: "over 4,500" (and "4,730 online, almost 5,000 registered"; images online for ~800–900) `[new]`.
  - Crossreads project page: "just under 6,000 items on completion" `[new]`.
  - todo Section 5 (2024 paper): 4,694 records / 3,153 stone `[prior]`.
  - **These are all-language totals.** The **Greek + image + usable-rights intersection** is unknown `[unk]` and is the number that matters.
- **Annotation granularity:** EpiDoc XML editions with line breaks; **IIIF** photography referenced `[prior]`. EpiDoc line breaks are **not** pixel alignment `[prior]`; polygons/baselines/damage labels must be derived.
- **Rights (split):**
  - Data/XML: **CC BY 4.0** `[new]` (`licence.txt` = full CC BY 4.0 legal code; Zenodo Rights = CC BY 4.0).
  - Code: no separate license (deposit is XML data) `[new]`.
  - **Images/IIIF: unresolved** — site policy: "All images are made available purely for the purposes of study … if you require an image for publication, please contact the relevant museum directly"; the CC-BY logo is version-less `[new]`. The 2024 paper's blanket "all material incl. IIIF is CC BY 4.0" claim was **not confirmed** `[new]`.
- **Attribution / conditions:** credit I.Sicily; per-image museum credit required; do not treat the CC-BY site logo as a reusable image license.
- **Parent-object lineage:** per-inscription EpiDoc IDs; IIIF canvases per object/surface/face `[inf]`.
- **Proposed use:** **priority construct** for the **data/text** side and, where per-item image rights permit, the image side.
- **Unanswered checks:** Greek subset count with available images; per-image rights notices; pin the version-specific commit/DOI; build visible-text transcriptions, polygons, baselines and damage labels separate from the edition.
- **Next action:** **INS-01**.

### I02 — Petrie Museum Greek Inscriptions (2022)

- **ID / version to pin:** `I02`; DOI `10.48420/20466087` (**v1, published 2022-09-02**) `[new]`.
- **Evidence links (checked 2026-10-18):** `https://doi.org/10.48420/20466087`; `https://research.manchester.ac.uk/en/datasets/petrie-museum-greek-inscriptions/`; figshare Manchester.
- **Content:** photographs of objects with **Greek** inscriptions in UCL's Petrie Museum `[prior]`.
- **Units and count evidence:** deposit is a **single PDF** `Petrie Greek inscriptions.pdf` (~872 KB) `[new]`; **no record/inscription/image counts** stated `[unk]`. Todo's "file count unverified" is now confirmed as **not enumerable from the record**.
- **Annotation granularity:** photographs plus a catalogue PDF; **no aligned line ground truth confirmed** `[unk]` `[prior]`.
- **Rights (split):** **CC BY 4.0** for the images, shared "with the permission of the Petrie Museum" `[new]`. No IIIF `[new]`.
- **Attribution / conditions:** CC BY 4.0; credit the Petrie Museum / UCL and the dataset creator (Peter Liddel) `[new]`.
- **Parent-object lineage:** per-museum inventory numbers `[unk]` (not confirmed in the PDF without retrieval).
- **Proposed use:** **priority image seed / construct** `[prior]` — actual photographs with a clear CC BY 4.0 grant; requires epigraphist line annotation.
- **Unanswered checks:** extract object list from the PDF; per-image rights confirmation; pair with any published edition text; confirm date/material distribution.
- **Next action:** **INS-01**.

### I03 — Bessarion

- **ID / version to pin:** `I03`; GitHub `Archaeocomputers/Bessarion`, branch **`master`**, pin commit (no tag, no DOI) `[unk]`. Paper: DAS 2024, pp. 393–407 `[new]`.
- **Evidence links (checked 2026-10-18):** repo; `.../blob/master/LICENSE.md` (`main/LICENSE.md` 404); README.
- **Material / language / witness period:** **Byzantine/post-Byzantine medieval Greek donative inscriptions**, Epirus / North-Western Greece, **37 locations** `[new]`.
- **Units and count evidence — all confirmed:** **122 images, 504 outlined lines, 2,776 words, 10,414 characters, 37 locations** `[PR]` `[new]`.
- **Annotation granularity:** PAGE XML line **outlines** + JSON/full transcription `[new]`. Actual text-recognition subsets are smaller than total geometry counts `[prior]`.
- **Rights (split):** repository-wide **CC BY-SA 4.0** (LICENSE.md = full CC BY-SA 4.0) `[new]`; **no separate image license** — images inherit the repo license by default `[inf]`.
- **Attribution / conditions:** CC BY-SA 4.0 = attribution + **ShareAlike** on adapted material; do not present it as a permissive-only set `[new]`.
- **Parent-object lineage:** per-monument/site; keep all images of one location together `[inf]`.
- **Proposed use:** **auxiliary with ShareAlike conditions** `[prior]` — imaging/adapter experiments; **cannot** establish accuracy on Classical/Koine carved stone (it is later painted/high-relief Byzantine material).
- **Unanswered checks:** audit paired usable samples; confirm images are genuinely under the repo license; split by monument/site.
- **Next action:** **INS-01** (kept as its own auxiliary benchmark).

## 3. Restricted / held inscription sources

| Source | Verified restriction (2026-10-18) | Treatment |
| --- | --- | --- |
| **PHI** (`inscriptions.packhum.org`) | Click-through: personal study only, no copies beyond Fair Use; last update 2023-07-12; no counts `[new]` | **Exclude default pending rights** |
| **I.PHI** (`sommerschield/iphi`) | **Code** Apache-2.0; **derived-data terms unspecified**, subject to PHI `[new]`; counts not stated | **Exclude default pending rights** — do not confuse code license with corpus license |
| **IGCyr / GVCyr v0.2** (`amsacta.unibo.it/5698`) | **CC BY-NC 4.0**; v0.2 is superseded by v0.3 (eprint 5863); no linked photography (XML + bibliography only); no counts `[new]` | **Exclude default** (NC) |
| **Cornell Eleusis** | "believed to be in the public domain" **but** digital reproductions are **private study/scholarship/research use only**; possible Greek/EU/holding-institution permission; per-item rights vary (e.g. **© British Museum**); no counts `[new]` | **Permission-dependent lead**, not the default open pool |
| **Pandektis metadata** (EKT) | Dataset field "No License Provided"; distribution **CC0 1.0**; catalog **CC BY 4.0**; **1,037 records**, temporal −570 to 700 CE; **images `edm:rights` = In Copyright (InC)**, explicitly excluded from the dataset license `[new]` | **Metadata discovery only** until each photograph's rights resolve |
| **Codex Sinaiticus XML** | Transcription CC BY-NC-SA 3.0; images separate `[prior]` | Exclude from default bundle (see [manuscripts.md](manuscripts.md)) |
| **I.Sicily imagery (see I01)** | Images study-only; museum permission for publication `[new]` | Per-item rights gate on imagery |

## 4. Image-to-line corpus construction plan

Only **two** potentially reusable image sources exist for inscriptions: **I02** (CC BY 4.0 photographs, no geometry) and whatever subset of **I01** IIIF imagery clears per-item rights. I03 is auxiliary/ShareAlike. Nothing is a ready line-recognition corpus.

**Construction pipeline (per object):**

1. **Select the intersection:** Greek **AND** relevant period **AND** an available photograph **AND** usable per-item image rights. Record the three filters; carry all-language totals separately and never publish them as Greek paired counts.
2. **Import the published edition separately.** Keep the EpiDoc/text edition as a *reading*, not as the visible-text label. Do not reuse expanded or normalized edition text as ground truth for lettering.
3. **Transcribe the visible text** from the photograph (diplomatic layer), preserving scriptio continua, damage, and physical line breaks as they appear.
4. **Add geometry:** line polygons/baselines and orientation, including actual text direction for unusual arrangements; mark worn/illegible spans distinctly from "not yet transcribed".
5. **Label damage/states** (worn strokes, breaks, erasures, additions) as annotations.
6. **Expert adjudication** with a second review; retain unresolved alternatives rather than forcing agreement.
7. **Record per-image credits** and the exact rights notice used.

**Reuse of imported EpiDoc:** importing line breaks from an EpiDoc edition gives **no** pixel alignment and no visible-lettering guarantee; it may inform the editorial layer only.

## 5. Pilot recommendation and intake order

1. **I01 I.Sicily** — largest Greek+image potential; blocks on the per-item image-rights intersection.
2. **I02 Petrie** — clear CC BY 4.0 photographs; blocks on extracting the object list and adding line geometry/labels.
3. **I03 Bessarion** — auxiliary benchmark only, with ShareAlike obligations preserved.

Given the rights state, the **first honest inscription deliverable is a scoped, annotated subset**, not a corpus. If neither I01 imagery nor I02 can be fully cleared, the inscription track remains a **research-only disclosure**, not an accepted automatic-recognition claim (todo Section 3).

## 6. Coverage and gap analysis (inscriptions)

| Dimension | Covered | Gap |
| --- | --- | --- |
| Period | Hellenistic–Roman Sicilian (I01), broad Greek (I02/Eleusis), Byzantine (I03) | Dense Classical Attic/Koine carved corpora with open images |
| Material | Stone (I01/I02), painted/high-relief (I03) | Metal, ceramic, curved surfaces, uneven lighting/RTI |
| Geometry | I03 outlines; I01/IIIF canvases | I01/I02 line polygons, baselines, orientation largely absent |
| Visible text | Editions exist | **Visible diplomatic line labels** must be created |
| Rights | I02 images CC BY 4.0; I01 data CC BY 4.0 | I01 imagery unresolved; I03 ShareAlike; rest restricted |

**No broad, already aligned and audited Classical/Koine inscription photo-to-line corpus was verified** (todo Section 5 conclusion upheld and strengthened).

## 7. Manifest field mapping — I02 (against `C0-0.3-draft`)

| Field | I02 value |
| --- | --- |
| `source_id` | `I02` |
| `source_version` | `v1` (figshare, 2022-09-02) |
| `url_doi` | `https://doi.org/10.48420/20466087` |
| `retrieval_date` / `content_checksums` | `not-retrieved` |
| `material_track` | `inscription` |
| `language_period` | Ancient Greek; period/material `unknown` |
| `witness_dating` | not stated → `unknown` |
| `units_evidence` | "1 PDF (~872 KB); no counts stated (file-list-derived)" `[new]` |
| `paired_line_counts` | `unverified` (no aligned geometry) |
| `license_image` | `CC-BY-4.0` |
| `license_text` | n/a (no aligned transcription) |
| `license_annotation` | n/a yet |
| `license_code` / `license_weights` | n/a |
| `license_evidence` | Manchester institutional record (2026-10-18) |
| `attribution` | Petrie Museum / UCL; dataset creator (Peter Liddel) |
| `allowed_intended_use` | `train` + `dev` after INS-01 |
| `unresolved_questions` | object list (extract from PDF); per-image credit; paired editions; date/material |
| `item_id` / `external_ids` | project-minted; `inventory:<Petrie no.>` once extracted |
| `object_type` | `inscribed-object` |
| `material` (item) | `unknown` |
| `asset_kind` | `photograph` (+ a catalogue PDF with no matching `asset_kind` value → `B-CCR-6`) |
| `label_granularity` | `none` (unannotated) |
| `pairing_status` | `image-only` |
| `annotation_completeness` | `unannotated` (INS-02) |
| `transcription_profile` | `pre-project` |

**I01 mapping note:** I01's EpiDoc editions map cleanly to `asset_kind: edition-text`, `label_granularity: none`, `pairing_status: label-only`, `transcription_profile: pre-project`; its IIIF imagery needs `license_image: mixed-per-item` + per-asset `license`/`evidence`. No new fields needed. The only manifest gap found is the absence of a text-only catalogue/metadata `asset_kind` (`B-CCR-6`).

## 8. Image-to-line backlog (inscriptions)

| Task | Owner | Prerequisite | Output | Completion evidence |
| --- | --- | --- | --- | --- |
| **INS-01** | D-I | This card | Greek + period + image + rights intersection for I01; object inventory for I02 | Counts of usable pairs; per-image credits |
| **INS-02** | D-I + epigraphist | INS-01 | Visible-text transcriptions, line geometry, uncertainty/damage labels | Label counts; published edition kept separate |
| **INS-03** | D-I | INS-01 | I03 auxiliary benchmark kept as its own set | Monument/site split; ShareAlike notice retained |
| **DATA-02** | Integrator + workers | All | Dedup by object/inventory ID; I01↔I02↔I03 overlap check | Overlap report |
| **DATA-03** | Greek reviewer | INS-02 | Locked inscription acceptance objects | Counts + effort |

## 9. Open questions handed to A / the owner

1. Confirm the "I.Sicily IIIF is CC BY 4.0" claim before any image use — todo Section 5 should be corrected (`B-CCR-5`, still open; A owns `todo.md`).
2. Whether inscriptions may proceed to acceptance with **I02-only** imagery if I01 imagery stays unresolved (recommend: yes, disclosed as a narrow profile).
3. Damage/orientation vocabulary for the transcription profile — A's `greek-transcription.md` §5 covers `unclear`/`illegible`/`lost`/`erased`/`added`/`overwritten`; the residual is the inscriptions-specific sign inventory (A's open question to the specialist).
4. Suggested source-backed replacement for A's invented EX-3/EX-6: a real I01/I02 stoichedon or worn-stone line once INS-02 creates it.
