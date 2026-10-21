# Greek source register for LeviathanText

Status: candidate source review, 2026-10-21. This replaces the earlier Bibloi
reader-corpus ranking. The question here is narrower: **which sources can improve a
single-user Greek HTR/OCR platform, and in what role?**

LeviathanText needs four different things, and they must not be conflated:

1. **L1 image-aligned visible text** — the only data that can train or evaluate line
   recognition directly.
2. **L2 reference text** — editions and transcriptions used for comparison, reviewer
   context and proposals, never as visible ground truth.
3. **Language prior / lexicon** — character or word models, lemmatization, spell and
   abbreviation support, candidate reranking.
4. **Layout and unlabeled images** — page/region/line detection, reading order,
   binarization, dewarping, domain pretraining.

A source is kept only if it clearly serves one of those roles and its rights are usable
now, or — in CSNTM's case — expected to be usable after written permission. Text-only
corpora cannot create new HTR accuracy claims. They can improve a prior, a reviewer
workflow or post-correction, and must be evaluated separately from visual recognition.

**Current decision:** CSNTM images (permission pending), plus First1KGreek, Perseus,
SBLGNT and papyri.info as L2/prior sources. PapyGreek and OGA are optional prior
experiments; the other candidates are dropped. No external permission other than CSNTM
is being pursued.

## Decision rules

- **Images before texts.** The project's bottleneck is image-aligned Greek, not another
  literary edition.
- **No silent promotion.** L2 text, verse-level metadata and machine morphology never
  become L1 labels.
- **One source, one role.** Record the role in the corpus manifest and enforce it at
  export/training time.
- **Rights are part of the data model.** Track image rights, transcription rights,
  annotation rights and model-weight rights separately.
- **No new permission dependency** unless it is CSNTM. Other permission-gated sources are
  out of scope for now.
- **No locked-set leakage.** Reference text used for tuning stays out of frozen test
  witnesses.

## 1. Image sources

### CSNTM Digital Manuscript Collection — pursue

**What it provides:** high-resolution photographs of Greek New Testament manuscripts,
catalog metadata (GA/INTF number, date, material, holder, shelf number, contents, some
verse-level image tags) and IIIF image access. It does **not** provide diplomatic
transcriptions, line polygons, baselines or image–text alignments.

**Use in LeviathanText:**

- layout, column and line detection;
- binarization, dewarping and degradation testing;
- unlabeled/self-supervised domain pretraining;
- selection of candidate pages for new reviewer-transcribed witnesses;
- verse-level weak context when paired with an open Greek NT edition.

**Do not use it for:** line-level supervised training, exact diplomatic ground truth, or
new recognition accuracy claims without independent transcription and adjudication.

**Rights and next action:** CSNTM's terms make manuscript images copyright CSNTM and/or
the holding institution; viewing is for non-commercial research, while copying,
redistribution, publication and derivative use require permission. Permission is
expected but **not yet granted**. Obtain written terms that explicitly cover local
training, derived annotations, model weights and any redistribution. Do not ingest a
large image set into a distributable package until that is clear.

### Existing project tracks — retain

P01 (papyri), M01/M02a (manuscripts) and I01 (inscriptions) remain the primary
image/text sources. They are not repeated in the candidate tables below. I01 still needs
image-aligned, adjudicated visible readings; CSNTM does not solve that.

## 2. Text sources for L2 reference and language priors

These are **not** substitutes for image-aligned training data.

### Core

| Source | Content | Role | Licence | Decision |
|---|---|---|---|---|
| **First1KGreek** | Broad Greek TEI corpus, roughly 25.6M words: Homer through late antique Christian and classical prose | L2 reference and Greek language prior | CC BY-SA 4.0 | Keep |
| **Perseus `canonical-greekLit`** | Classical canon plus Josephus; overlaps First1K | L2 reference and language prior | CC BY-SA 4.0 | Keep, deduplicate by CTS URN |
| **SBLGNT** | Open Greek New Testament text; MorphGNT supplies morphology | NT reference, reviewer context, verse-level weak alignment to CSNTM metadata | CC BY 4.0; MorphGNT CC BY-SA 3.0 | Keep |
| **papyri.info (DDBDP/DCLP)** | Documentary and literary papyrus EpiDoc texts | Documentary L2 reference and lexicon support for P01 | CC BY 3.0 | Keep |

### Optional, only if a measured prior experiment needs them

| Source | Content | Role | Licence | Decision |
|---|---|---|---|---|
| **PapyGreek Treebanks** | Hand-reviewed documentary papyri text and morphology | Documentary lexicon and post-correction prior | CC BY-SA 4.0 | Optional; small, no image alignment |
| **Opera Graeca Adnotata (OGA)** | Large automatic lemma/parse/syntax overlay on First1K/Perseus/PTA | General morphology/lexicon prior | CC BY-SA 4.0 | Optional; automatic, not authority |

Use these to build a **separate** character/word prior, lexicon or candidate reranker.
Measure any effect on held-out image data. Do not train the visual recognizer on them as
if they were transcriptions, and do not let a prior improvement stand in for a
recognition improvement.

## 3. Required separation

| Layer | May train visual model? | May tune decoder/prior? | May support reviewer? | May enter accuracy claim? |
|---|---|---|---|---|
| CSNTM images, no transcription | Layout/pretraining only | No | As page context | No |
| CSNTM images + reviewer transcription | Yes, if adjudicated and split correctly | Yes, outside frozen test | Yes | Yes, as a fresh witness |
| First1K/Perseus/SBLGNT/papyri.info | No | Yes | Yes, as L2 reference | No |
| PapyGreek/OGA lexicon | No | Yes, if measured | Yes | No |

A CSNTM page becomes a usable L1 witness only after a reviewer supplies line geometry
and a diplomatic reading and that reading is frozen before tuning. Until then it is an
image, not a label.

## 4. Acquisition sequence

1. **Secure CSNTM permission in writing.** Clarify holding-institution coverage, local
   training, annotation, model weights and redistribution. If weight distribution is not
   covered, keep CSNTM-derived models local and out of release artifacts.
2. **Ingest the core text sources as L2/prior only.** Start with First1KGreek and Perseus
   for broad Greek, SBLGNT for NT reference, and papyri.info for documentary support.
   Keep source and role in every manifest row.
3. **Build and measure a small language prior separately.** Use a held-out image set and
   report visual CER/WER with and without the prior. PapyGreek/OGA are optional here.
4. **Select a small CSNTM set for layout and pretraining.** Filter covers, spines, color
   targets and non-text pages. Do not call layout improvement recognition improvement.
5. **Create at most a few fresh reviewer witnesses.** Reviewer transcribes and aligns
   selected pages; freeze them before any tuning; keep them out of the locked test set
   until the acceptance sheet is fixed.
6. **Publish only rights-cleared artifacts.** Keep image, transcription, annotation and
   weight licences separate in the package manifest.

## 5. Dropped from the candidate list

These were considered and are not part of the current plan:

| Source | Reason |
|---|---|
| GLAUx | Mixed CC BY-SA/NC files; filtering burden is not justified by marginal gain. |
| AGDT / AGLDT | Small, licence ambiguity across releases; OGA/PapyGreek cover the modest prior need. |
| Diorisis | Clean but redundant with First1K/Perseus and OGA for this project. |
| PTA, CGPG, byzantium.gr | Text-only patristic/Byzantine material outside the current tracks; CGPG is OCR-quality-dependent. |
| Greek Wikisource, CCEL, Brenton, alternative GNT editions | One-off, English-only or redundant; no current HTR benefit. |
| Rahlfs 1935 digital text, Gorman treebanks | NC/restricted or ambiguous; not usable. |
| PHI Greek Inscriptions, CGRN | No open redistribution permission; I01 is the inscription track. |
| TLG, CATSS/CCAT, AGNT, GRAMCORD | Contractually or commercially restricted; cannot be used. |
| NA27/NA28, UBS5, Tyndale House GNT | Copyrighted or NC/ND; use SBLGNT instead. |
| NTVMR transcriptions | Separate project and terms; not assumed. |

## 6. Licensing notes

- **ShareAlike propagates.** First1K, Perseus, PapyGreek and OGA are CC BY-SA; derived
  morphology, lexicons or model artifacts may inherit obligations. Keep attribution and
  licence notices with every derived resource.
- **CC BY 3.0/4.0 sources** (papyri.info, SBLGNT) still require attribution; do not
  relicense their text as project-owned.
- **CSNTM is not open-licensed.** IIIF access is not permission to copy, train on, or
  redistribute images. Permission must be explicit and in writing.
- **Model weights are a separate distribution question.** A permission that allows local
  research may not allow publishing weights trained on the images.
- **No permission from other sources is assumed.** If a future source is not already
  open-licensed or owned by the project, it stays out until rights are cleared.

## Sources

- CSNTM Digital Manuscript Collection — https://collections.csntm.org/
- CSNTM Terms of Use & Copyright — https://www.csntm.org/terms-of-use-copyright/
- CSNTM FAQ (catalog and image metadata) — https://www.csntm.org/faq/
- First1KGreek — https://github.com/OpenGreekAndLatin/First1KGreek · https://doi.org/10.5281/zenodo.596723
- Perseus `canonical-greekLit` — https://github.com/PerseusDL/canonical-greekLit
- SBLGNT licence — https://www.sblgnt.com/license/
- papyri.info / DDBDP — https://github.com/papyri/idp.data · https://papyri.info/docs/ddbdp
- PapyGreek Treebanks — https://doi.org/10.5334/johd.55 · https://zenodo.org/records/8428823
- OGA — https://varro.informatik.uni-leipzig.de/oga/en/learn_more.html · https://doi.org/10.5281/zenodo.14206061
