# Human decisions and needs

Status: 2026-10-23. Decisions below were made by the project owner, who also acts as Greek
specialist and pilot evaluator/custodian. They authorize pilot work; they do not create
rights, reviewer evidence, or independent validation.

## Decisions adopted

| ID | Decision |
|---|---|
| D1 | Ratify `greek-diplomatic-1.0-draft` as the pilot profile. The executable policy is `GreekDiplomaticProfile.cs`. |
| D2 | Pilot scope is P01 (papyri), M01/M02a (manuscripts) and I01 (inscriptions). M03, P02–P06 and other collections are out of scope until separately cleared. |
| D3 | Use only verified image/text pairs. Missing pairs are `unknown`, never blank or negative training examples. |
| D4 | The P01 locked split is spent and must not be reused for a new claim. |
| D5 | No accuracy/release claim until a fresh held-out witness is adjudicated and a threshold sheet is frozen. Until then all numbers are pilot measurements. |
| D6 | The project owner accepts the custodian role for pilot evaluation. This is a role conflict; an external claim requires an independent custodian. |
| D7 | No CSNTM image ingestion or training until written permission covers the intended use. |
| D8 | Restricted P02/P03/P04/P05/P06 and I01 material stays local-only and out of release packages until rights are confirmed. |
| D9 | Do not distribute model weights. Ship code only; keep trained models local until rights and capability review are complete. |
| D10 | Keep the ImageSharp TIFF fallback for the pilot. Commercial distribution requires a Six Labors Split License decision or a different backend. |
| D11 | Deployment remains single-user/local. Target osx-arm64 only for now; no SSO, tenant isolation, shared-server commitment, or ONNX export decision. |
| D12 | Freeze feature scope to core Greek HTR. Printed editions, other scripts, searchable PDF, distributed execution, fragment joining and advanced imaging are deferred. |
| D13 | Remote registries are disabled/opt-in; no model is downloaded or trusted without an explicit SHA-256 and owner approval. |
| D14 | Hosted AI is disabled. Local providers are the only approved pilot configuration; no restricted material leaves the machine. |
| D15 | Abbreviation expansions, corrections and word boundaries are L2/L3 proposals; L1 remains visible evidence and is never silently rewritten. |

## Still required: reviewer evidence

### H1. Complete the reviewer decisions

**Owner:** project owner / Greek reviewer.

- Review and return:
  - `.artifacts/review/p01/packet-50/decisions.csv`
  - `.artifacts/review/m02a/packet-50/decisions.csv`
  - `.artifacts/review/m01/packet-50/decisions.csv`
- Save as `.artifacts/review/<track>/decisions-returned.csv`.
- Use `agree`, `correct`, `unclear` or `not-visible`; corrections are diplomatic readings.
- Do not force a decision where the crop is insufficient.

This cannot be replaced by an owner policy decision: it requires looking at the actual
images. These packets are the critical path to adjudicated labels.

### H2. Adjudicate the pilot and measure effort

**Owner:** project owner / Greek reviewer.

- Perform independent second review of candidate transcriptions.
- Retain unresolved alternatives instead of forcing agreement.
- Record ambiguity, annotation completeness and correction time.
- Inventory uncommon signs, ligatures and damage states.

Until this is done, `D5` applies: pilot measurements only, no release claim.

## Rights and permissions

### H3. CSNTM permission

**Owner:** project owner + CSNTM + holding institutions.

Request written permission covering local research/training, derived annotations and crops,
model weights, redistribution and holding-institution rights. Until then `D7` applies.

### H4. P01/P02–P06 and I01 rights

**Owner:** project owner / rights holders.

Resolve image, transcription, annotation and weight redistribution terms per object. Until
then `D8` applies. Do not treat online availability as permission.

### H5. Weight and dependency distribution

**Owner:** project owner / legal.

`D9` means no weights are packaged. Before any commercial release, decide whether to retain
ImageSharp under the Six Labors Split License or replace the TIFF fallback.

## Data and evaluation

### H6. Complete corpus inventories

**Owner:** data workers / project owner.

- MAN-01: count valid M01/M02 image-PAGE pairs and propose witness/hand holdouts.
- PAP-01: audit P01's 27 named pairs and label the five extra images unannotated.
- PAP-02: produce the P02/P03 include/exclude ledger and P04/P05/P06 rights mapping.
- INS-01: produce I01 image-aligned readings and geometry.
- DATA-02: deduplicate by object/TM/inventory ID and image lineage.

Only verified pairs may be used (`D3`).

### H7. Fresh witness and threshold freeze

**Owner:** project owner / custodian.

`D5` requires a fresh held-out witness before a new accuracy claim. None is cleared yet.
Acceptable routes:

1. written CSNTM permission for a selected witness;
2. a rights-cleared P02/P03 source;
3. a reviewer-transcribed object owned by the project.

Until one is available, keep all results labeled as pilot/development measurements. Do not
reuse the spent P01 locked split (`D4`).

## Product/deployment

### H8. Scope and platform confirmation

`D11`/`D12` close these for the current cycle:

- single-user/local only;
- osx-arm64 only;
- no SSO/shared deployment;
- no ONNX decision;
- no printed-edition/other-script/searchable-PDF/distributed/fragment/advanced-imaging work;
- remote registries opt-in and untrusted by default;
- local AI providers only.

Reopen only with an explicit owner decision and a new target/budget.

## Return format

- **Reviewer work:** return completed CSVs plus reviewer identity.
- **Rights:** provide exact permission text and scope (images, annotations, weights,
  redistribution).
- **New witness:** provide source, object IDs, rights basis and a statement that it was not
  used for tuning.
- Do not send restricted images or unpublished editions through third-party services.
