# Human decisions and needs

Status: 2026-10-23. This is the human-input register for LeviathanText. It lists what
engineering cannot decide or obtain without the project owner, Greek specialist, reviewer,
rights holder, or custodian. The code-only work queue is tracked in `todo.md`.

## 0. Critical path

### H1. Return reviewer decisions for the known-transcription packets

**Needed from:** Greek reviewer / project owner.

- Complete the decisions CSV in:
  - `.artifacts/review/p01/packet-50/decisions.csv`
  - `.artifacts/review/m02a/packet-50/decisions.csv`
  - `.artifacts/review/m01/packet-50/decisions.csv`
- Return as `.artifacts/review/<track>/decisions-returned.csv`, with a reviewer identity.
- Mark `agree`, `correct`, `unclear` or `not-visible`; do not force a decision where the crop
  is insufficient.
- For corrections, supply the diplomatic reading, not an expanded/normalized edition.

**Why it blocks:** these are the only image-aligned, reviewer-ready labels currently on the
critical path. No new accuracy claim or adjudicated release set can be created without them.

**If unavailable:** keep using them only as unadjudicated pilot data; do not claim expert
ground truth.

### H2. Approve the Greek transcription profile and open conventions

**Needed from:** Greek specialist / reviewer.

Decide or approve the first versioned profile (`greek-diplomatic-1.0-draft`) on:

1. Case handling and lunate/final/medial sigma.
2. Historical letters, epichoric forms, digraphs and ligature representation.
3. Punctuation, numerals, strokes, marginal signs and nonalphabetic symbols.
4. Abbreviations/nomina sacra: visible letters and overbar in L1; expansion policy in L2;
   whether a dedicated sign ID is required.
5. Supralinear/interlinear letters, erasures, additions, overwrites and different hands.
6. Canonical combining-mark order; iota subscript vs adscript.
7. Underdot scope: per-character, span-level `unclear`, or both.
8. Scriptio continua and word division: dedicated boundary annotations, not spaces in L1.
9. Rule for recording correction order only when evidence supports it; how to mark
   `unknown`/`inferred` sequences.
10. `UnidentifiedMark` vs `Sign` vs `GlyphForm` boundary.

**Why it blocks:** labels can be stored now, but not consistently adjudicated or released
under a named profile.

**If unavailable:** keep the draft profile, label every output as draft/unadjudicated, and do
not claim profile conformance.

### H3. Freeze acceptance thresholds and name a custodian

**Needed from:** project owner, Greek specialist, evaluator/custodian.

- Select the pilot collections and final evaluation objects.
- Name a custodian for locked labels distinct from the training owner.
- Freeze per-track/per-profile thresholds for CER, mark errors, coverage and correction time.
- Decide which fresh held-out witnesses are acceptable for the next claim.
- Decide whether the P01 locked split remains spent and cannot be reused for a new claim.

**Why it blocks:** no release accuracy claim, model comparison or final acceptance report.

**If unavailable:** continue development measurement only; label every number as pilot,
unadjudicated, and non-acceptance.

## 1. Rights and permissions

### H4. CSNTM permission and scope

**Needed from:** project owner + CSNTM + holding institutions.

Request written permission covering:

- local research use and training;
- creation of derived annotations and crops;
- publication of model weights trained on the images;
- redistribution of images, crops or derivatives;
- holding-institution rights for each selected manuscript.

**Decision needed:** if weight distribution is not permitted, keep CSNTM-derived models
local-only and exclude them from release artifacts.

**If unavailable:** do not ingest CSNTM images beyond permitted viewing; use only existing
project tracks.

### H5. Rights for P01/P02/P03/P04/P05/P06 and I01

**Needed from:** project owner / rights holders.

- Confirm reusable image and transcription rights per object.
- Resolve P02/P03 inclusion, P04 source mapping and P05/P06 text-photo matching.
- Confirm I01 image/edition rights and whether derived crops/weights may be distributed.
- Record exceptions in the corpus ledger; do not treat “available online” as permission.

**If unavailable:** keep restricted material out of training, release packages and model
cards, or mark it local-only.

### H6. Model-weight distribution and dependency licences

**Needed from:** project owner / legal.

- Decide the distribution licence for project-trained weights.
- Decide whether CSNTM/P01-derived weights may be published.
- Review the **Six Labors Split License** for ImageSharp (TIFF fallback) against the intended
  distribution model; if incompatible, approve removal or a different TIFF backend.
- Confirm attribution requirements for all bundled dependencies.

**If unavailable:** do not publish weights; ship code only with a local-model workflow.

## 2. Data and annotation work

### H7. Complete the corpus inventories

**Needed from:** data workers / project owner.

- **MAN-01:** inventory M01/M02 image-PAGE pairs and lines; reconcile counts; propose
  witness/hand holdouts.
- **PAP-01:** audit P01's 27 named pairs, count lines/characters, label the five extra images
  as unannotated.
- **PAP-02:** produce P02/P03 include/exclude ledger; resolve P04 rights; match P05/P06.
- **INS-01:** produce I01 image-aligned readings and geometry; keep I03 auxiliary.
- **DATA-02:** deduplicate by object/TM/inventory ID and image lineage; audit overlap.

**If unavailable:** use only verified pairs; report missing pairs as unknown, never as blank
or negative examples.

### H8. Adjudicate a development pilot and measure effort

**Needed from:** Greek reviewer + data workers.

- Independent second review of proposed test transcriptions against exact images.
- Adjudicate disagreements; retain unresolved alternatives.
- Record ambiguity, annotation completeness and actual correction time.
- Inventory uncommon signs, ligatures, damage states and their counts.

**If unavailable:** do not expand training volume targets or claim generalization.

## 3. Product and deployment decisions

### H9. Deployment scope and platforms

**Needed from:** project owner.

- Confirm the current single-user/local target remains sufficient.
- Decide whether shared deployment, SSO, tenant isolation and collaboration are in scope.
- Choose the release target matrix and whether installers/updaters/cross-platform rollback
  are required now.
- Decide whether ONNX export or another inference route is required.

**If unavailable:** remain single-user/local, osx-arm64-first, and do not claim cross-platform
support.

### H10. Scope priorities

**Needed from:** project owner.

- Priority for printed editions/critical apparatus and additional scripts.
- Priority for searchable PDF, distributed execution, fragment joining and advanced imaging.
- Whether remote model registries are trusted, and which registries/checksum policy apply.
- Whether hosted AI providers are allowed; credentials, spend caps, data-retention rules and
  which tasks may leave the local machine.

**If unavailable:** keep these features disabled/out of scope and retain local-only AI
configuration.

## 4. What can continue without these decisions

- RTL/BiDi display and record work that does not claim RTL recognition.
- Full task-model API and segmentation task wrappers.
- Official XML schema validation with pinned XSDs.
- Installer/updater and migration rehearsal work that does not need restricted data.
- Publication docs, model cards for existing local models, and known-failure documentation.
- Engineering hardening, resource limits and test coverage.

## 5. Return format

For decisions, reply with the ID and the chosen option, or edit the relevant ledger/CSV.
For rights, provide the exact permission text and scope (images, annotations, weights,
redistribution). For reviewer work, return the completed CSV and reviewer identity. Do not
send restricted images or unpublished editions through third-party services.
