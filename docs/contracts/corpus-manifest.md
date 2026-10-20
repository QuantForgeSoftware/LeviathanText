# Corpus manifest contract

Contract: **C0-0.4-draft** (proposal, not accepted). Issued 2026-10-18 by Instance A (A-PLAN). Status: **draft, open for review** by Instance B and the project owner; Greek-specialist adjudication pending. Revision history: 0.1 (initial) → 0.2 (added domain/pipeline, interchange, examples; no change to 0.1 documents) → 0.3 (StageRun I/O artifacts became `StageArtifact` records; native JSON serialization conventions added to interchange §2.0) → 0.4 (**B-CCR-6 accepted**: `catalog/metadata-text` asset kind + `preservation_tags` field; **B-CCR-7 accepted**: `Proposal` entity kind, prefix `prop`).

Source requirements: project-plan Section 5 (`DATA-01`, `DATA-02`, `DATA-03`, intake backlog), Section 6 (C0, dataset-worker ownership, artifact-store prefixes), Section 7 (split hashes, leakage checks). This document defines the **field dictionary and conventions** for human-readable corpus manifests. It defines no file formats, code, or schemas; when implementation is authorized, a machine-readable serialization of exactly these fields will be derived from this revision.

## 1. Purpose and layering

The manifest records what a corpus **contains, where it came from, what rights permit, and how it may be used** — separately for images, transcriptions, annotations, code and weights. A source being online, published, or CC-licensed does not by itself make it a usable training or evaluation set; the manifest forces that distinction to be explicit.

Three record levels:

1. **Source record** — one upstream publication/repository (M01, P01, I01, …) at a pinned version.
2. **Item record** — one physical witness (object) within a source: manuscript, papyrus, inscription.
3. **Asset record** — one retrievable or derivable file/unit tied to an item: a photograph, a PAGE XML file, an EpiDoc edition, a derived line crop, an annotation file.

Every record carries `record_status`; a record with missing mandatory evidence is **pending**, never silently treated as usable.

## 2. Source-record fields

| Field | Req | Representation | Semantics and unknown handling |
| --- | --- | --- | --- |
| `source_id` | req | registered ID (`M01`, `P03`, …) | Stable key used across all documents. Never reused for a different publication. |
| `source_version` | req | version string **or** `pin:<commit>/<release-tag>` **or** `unpinned` | The exact archived/repository version considered. `unpinned` is a defect that blocks intake. DOI-only sources must record the **specific version**, because DOIs may follow the newest release. |
| `url_doi` | req | URI(s) | Canonical landing page and/or DOI of the pinned version. |
| `retrieval_date` | req | ISO-8601 date | Date the pinned version was inspected, not first discovered. |
| `content_checksums` | req per retrieved file | SHA-256 per file | Recorded when a file is actually retrieved. If nothing has been downloaded, the value is `not-retrieved`; the source may still be inventoried. |
| `material_track` | req | `manuscript` \| `papyrus` \| `inscription` \| `auxiliary` \| `mixed` | Primary evaluation track. `auxiliary` = useful for transfer/imaging experiments but cannot establish track accuracy (e.g. P04 glyph crops, I03 painted inscriptions). |
| `language_period` | req | free text + coverage tags | e.g. "Koine, Hellenistic documentary", "Classical, literary". Unknown components recorded as `unknown`. |
| `witness_dating` | opt | range or single era, with `basis` note | Date of the physical witness, distinct from the language period (project-plan §2). |
| `units_evidence` | req | quoted publisher/file-list counts with provenance tag: `publisher-reported` \| `file-list-derived` \| `inspected` | e.g. "Zenodo: 32 JPGs, 27 XMLs (publisher-reported)". Counts are never upgraded to `inspected` without actually inspecting. |
| `paired_line_counts` | req | integer or `unverified` | Number of image/transcription-aligned **line pairs** actually audited. Publisher line totals (e.g. 9,285 vs 2,272 for M01) are held in `units_evidence`, not here. |
| `license_image` / `license_text` / `license_annotation` / `license_code` / `license_weights` | req each | license ID **or** `institutional:<notice-ref>` **or** `mixed-per-item` **or** `unverified` | Five independent rights tracks. A repository license never blankets linked photographs. `mixed-per-item` requires per-asset rights fields (§4). |
| `license_evidence` | req per rights track | exact URL/notice text + access date | The rendered license/rights statement actually seen, quoted or excerpted. |
| `attribution` | req | required credit string(s) | What downstream distribution must display. |
| `conditions_exceptions` | opt | list | ShareAlike obligations, NC limits, registration requirements, per-item caveats. |
| `allowed_intended_use` | req | `train` \| `dev` \| `test` \| `eval-only` \| `reference-only` \| `excluded` | Disposition under current evidence; matches the todo status vocabulary (priority intake / construct / conditional / auxiliary / exclude default / hold). |
| `unresolved_questions` | req (may be empty) | list of questions + evidence pointers | Every open check (e.g. "P01: XML contents not yet inspected"). Non-empty means the source cannot be counted as verified. |
| `provenance_notes` | opt | free text | e.g. M03's contradictory SLUB wording; P02's count discrepancy (185 vs 184). |

## 3. Item-record fields (per physical witness)

| Field | Req | Semantics |
| --- | --- | --- |
| `item_id` | req | Stable project ID. Provenance IDs (TM number, inventory number, shelfmark) are separate fields, never the primary key. |
| `source_id` | req | Parent source record. |
| `external_ids` | opt | `TM:<n>`, `inventory:<n>`, `shelfmark:<s>`, `Trismegistos`/`APIS`/`ISicily` numbers as applicable. Used for deduplication (`DATA-02`). |
| `object_type` | req | `codex-leaf` \| `roll/papyrus-sheet` \| `inscribed-object` \| `other:<text>` |
| `material` | req | papyrus, parchment, stone, ceramic, etc.; `unknown` allowed. |
| `language` / `script` | req | May be `unknown`. Non-Greek items in a mixed source are recorded and dispositioned, not hidden. |
| `hand_style` | req (may be `unknown`/`unattributed`) | Named style or hand label when a source supplies one; never invented. |
| `holds_alternate_views` | req | Whether recto/verso, alternate lighting, or fragments of this object exist. Drives split grouping. |
| `duplicate_of` | opt | `item_id` of a record in **another** source representing the same object (cross-source dedup). |
| `item_disposition` | req | `include` \| `exclude` \| `pending-rights` \| `pending-pairing` \| `reference-only`, with reason. |

## 4. Asset-record fields (per file/unit)

| Field | Req | Semantics |
| --- | --- | --- |
| `asset_id` | req | Stable ID. |
| `item_id` | req | Owning witness. |
| `asset_kind` | req | `photograph` \| `scan` \| `transcription-xml` \| `edition-text` \| `annotation-layer` \| `derived-crop` \| `derived-image` \| `model/glyph-set` \| `catalog/metadata-text` (B-CCR-6 accepted) |
| `preservation_tags` | opt | tag + evidence per tag | Per-item preservation/condition tags a source supplies without images or line labels (e.g. P02's tags, I03 damage states); such assets are `catalog/metadata-text` with `label_granularity: none` and never count as recognition data. |
| `file_checksum` / `size` | req once retrieved | SHA-256 + bytes; `not-retrieved` until then. |
| `parent_image_lineage` | req for derived assets | Full chain: derived crop → capture → original photograph. Derived assets never break the chain to the original. |
| `coordinate_frame` | req for geometry-bearing assets | Which image the geometry is expressed against (project-plan §4: every annotation identifies its capture and frame). `none` for text-only editions. |
| `license` / `evidence` | req | Asset-level rights if they differ from the source record; else `inherit-source`. |
| `label_granularity` | req for transcription/annotation assets | `line` \| `word` \| `character-box` \| `glyph` \| `region` \| `none`. Character boxes are **not** line labels; editions are **not** visible-text labels. This field is the primary guard against the project-plan §5 mislabeling risks. |
| `transcription_profile` | req for text assets | Which proposed profile version (see `greek-transcription.md`) the labels conform to, or `pre-project` for imported labels not yet converted. |
| `pairing_status` | req | `paired` (image + audited label) \| `image-only` \| `label-only` \| `unpaired-unsynced` (e.g. P01's five extra images). |
| `annotation_completeness` | req for labeled assets | `complete` \| `partial:<what is missing>` \| `unknown`. "Missing character boxes" is recorded as `unknown`, never assumed to be background (P03 risk). |
| `credit_required` | req | Attribution string for this asset if different from source. |

## 5. Split conventions

- **Split unit is the physical object (item), never the line or page.** All assets of one item — fragments, recto/verso, alternate captures, crops, derived glyph sets — share the item's split assignment (`DATA-02`).
- Splits: `train` / `dev` / `test`, plus `holdout:<name>` for witness/hand/site generalization probes and `reserved-acceptance` for locked acceptance objects chosen in `DATA-03`.
- `hand_or_site_holdout`: for generalization tests, entire hands (manuscripts) or monuments/sites (inscriptions) are assigned to `test`/`holdout` as units. The manifest records the grouping key used.
- Repeated passages / same text in multiple witnesses are tagged `textual-overlap:<id>` so evaluation can treat them separately (project-plan §5).
- Every split assignment change requires a **new manifest version**; `split_hash` (SHA-256 of the sorted assignment list) is published in evaluation reports so leakage can be checked (project-plan §7).
- Locked acceptance labels live with the evaluator role, not in distributed training manifests; the manifest records their `asset_id`s and custodian role but not their contents.

## 6. Manifest versioning and change rules

- The manifest is versioned as a whole: `manifest-v<n>` with a change log entry per revision (what changed, why, evidence links).
- `pending` is the default state for anything unevidenced. Promoting a field to a verified value requires recording the evidence (URL/notice/inspection date) in that same revision.
- Disposition changes (`include` → `exclude`) keep history; a source excluded for rights reasons stays visible as `excluded:<reason>` rather than deleted.
- The manifest is **not** the annotation store: annotation contents live under artifact-store prefixes (`datasets/<track>/`), referenced by checksum; the manifest references, and never embeds, label data.

## 7. Open questions for reviewers

1. B: are the `asset_kind` and `label_granularity` vocabularies sufficient to express every source card (esp. P02 character boxes + preservation tags, and I01's EpiDoc editions vs derived visible readings)?
2. Greek specialist: does `hand_style` need a controlled vocabulary for split-by-hand, or is free text with a grouping key acceptable for planning?
3. Owner: confirm that `reserved-acceptance` objects are selected by the project owner (per `DATA-03`) and that the evaluator custodian role is a distinct named role.
