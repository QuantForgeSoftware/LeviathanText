# NT-PILOT-B — current P46 source-expansion handoff

Prepared **2026-11-02** against repository baseline **`fc96d5bb2f901f8610ad421d5f95e640c784ea67`** (`fc96d5b`). This handoff covers the current additional-source discovery and full-page review packet only. It does not declare the historical corpus-instance pilot complete, accept specialist decisions, or report the engineering instance's training study as corpus work.

## Scope and ownership

Corpus/reviewer ownership is the six proposed Michigan surfaces **NTVMR docID10046/pageIDs1470–1520**, candidate physical leaves **83r/v, 84r/v and 85r/v**. The current owner prepared evidence and blank review materials. A Greek specialist and independent second reviewer still own scholarly decisions; the integrator owns task/plan status and any authorization for subsequent training or publication. No institutional correspondence was sent.

Changed or newly created paths owned by this source-expansion work:

- [Source inventory and staging report](../data/p46-expansion-candidates.md).
- This handoff, `docs/handoffs/NT-PILOT-B.md`.
- `.artifacts/datasets/ntvmr-pilot/expansion-discovery-2026-11-02/`: retained metadata, six original TEIs, six item manifests, six original HEAD responses and the discovery index. Previously prepared discovery evidence was not modified during staging.
- `.artifacts/datasets/ntvmr-pilot/expansion-staging-2026-11-02/`: six unchanged official JPEG derivatives, copied original evidence, acquisition plan and response headers, source ledger, full-page contact sheet, offline HTML, attribution, six pending page records, eleven pending question records and content manifest.
- `.artifacts/datasets/ntvmr-pilot/P46-Michigan-expansion-source-review-2026-11-02.zip` and matching `.zip.sha256` sidecar.

The artifact paths are Git-ignored local evidence; transfer the complete archive separately when an authorized reviewer needs it. No application code, training runner, accepted dataset, `project-plan.md` or `todo.md` was changed for this source-expansion handoff.

## Rights, evidence and current dataset status

All six exact Michigan image manifests, APIS processing/image IDs **3615–3620**, explicitly identify the corresponding material as **public domain/PDM1.0**. The service/capture IDs were checked before acquisition. Image attribution: **University of Michigan Library Digital Collections**, P.Mich.inv.6238. PDM1.0 is a status mark, not a CC0 dedication. These checks cover these Michigan captures; they do not cover separate CSNTM or Chester Beatty photographs.

Each exact published TEI response attributes **Institut für Neutestamentliche Textforschung, ©2026**, with a linked **CC BY4.0** notice. Original availability wording, URLs and hashes remain in the packet. Preserve attribution and a modification record for any later derivatives. No model-weight distribution license is assigned here.

The packet retains complete **width-2200 official IIIF review derivatives**, not native-resolution TIFFs. It preserves downloaded JPEG bytes without rotation, enhancement or crop. Only contact-sheet thumbnails are resized. `source-ledger.json` links image IDs/URLs/hashes, proposed folio/leaf, native dimensions, exact item rights evidence and TEI hashes/notices. The original witness-level TEI header lists both holding institutions and is not used as page-level ownership proof.

Every candidate remains **unreviewed/source-exposed**, with identity and expert decisions pending, visible-row counts null, `trainingEligible=false` and `split=null`. There are no accepted text fields, new line crops, new training labels, adjudicated geometries or acceptance partitions. These sources cannot be represented as a fresh blind acceptance set. The closed two-hour training matrix used the earlier 1440/1450/1460 material; none of these six new pages entered it.

## Portable review inputs and checksums

Extract the [48-file ZIP](../../.artifacts/datasets/ntvmr-pilot/P46-Michigan-expansion-source-review-2026-11-02.zip) completely, then open [index.html](../../.artifacts/datasets/ntvmr-pilot/expansion-staging-2026-11-02/index.html). No internet connection or server is required. Review the complete leaf pairs in order83,84,85; the order is a review convenience, not a split or quality judgment. Full-page views link the original source TEI and institutional metadata. Use `page-review.csv` and `adjudication-questions.csv` as review worksheets, not application/training import formats; browser viewing does not save answers.

SHA-256 receipts at handoff:

| Evidence | Bytes | SHA-256 |
| --- | ---: | --- |
| `docs/data/p46-expansion-candidates.md` | 13,843 | `948b85f5edcdfa6163964b3b206d374ca3f9d294ca00ac48a4663714635959fd` |
| Staged `source-ledger.json` | 30,327 | `3a6e7f5e28885bcf59639cec51cf669f60f7927ed2725778d332fb3c5520e7e2` |
| Staged `package-manifest.json` | 7,522 | `d8aaed1ff47f5e9e438d52825c08ad42e48c7afc6967de312edfd5747ebdf244` |
| Discovery `evidence-index.json` | 21,680 | `8c1c603ecc2894050779b8b325e63bb341ab04d7f9b0afb597e1ab1ec1a818a0` |
| `P46-Michigan-expansion-source-review-2026-11-02.zip` | 8,778,841 | `4a80e1995f7df14dd3df5b0ce05ca50d33c98b76a0faddfcb378733b92ab42a8` |

The content manifest lists 47 content files and excludes itself; the archive includes it as file48. QA verified all content sizes/hashes, local HTML links, six successfully decoded JPEGs, unchanged source copies, worksheet record counts and pending/blank answer fields, and archive CRCs. Visual inspection of the contact sheet checked whole-page presentation and labels, not manuscript identity or text acceptance. The source document is intentionally outside the packet; this table records its exact handoff revision rather than adding it to the archive retrospectively.

## Decisions still required

1. Confirm each proposed page/folio/physical-leaf mapping against visible incipit, explicit, pagination and damage; investigate **1480's detailed Recto/Verso conflict** and all generic Recto labels.
2. Count visible body rows independently. Explain **1500's catalogue27 versus TEI28** and **1520's catalogue26 versus TEI27**, distinguishing terminal remnants, continuation markers, page numerals and photographic captions. The 162 TEI `<lb>` elements are not a usable-line count.
3. Adjudicate terminal correction states for1490/1500 and define the target layer, treatment of cancellation/additions, uncertain traces, supplied restorations, abbreviations, overbars, sigma/case, word division and Unicode normalization. Source edition readings must not substitute for visible ink.
4. Record clipped edges, holes, faintness and legibility, with exclusions or requests for separately authorized higher-resolution evidence. Nothing yet establishes coverage of the faded conditions observed on1440.
5. Produce reviewed geometry only after source identity and usable rows are established. Keep the packet originals intact; return versioned decisions with stable source IDs, reviewer/date and evidence. Arrange independent second review and record unresolved disagreements explicitly.
6. Expand volume and diversity as required by the pilot gates. Three more proposed leaves do not establish the planned eight-leaf sample or reviewed100/30/30-line partitions. A genuinely fresh acceptance set, unseen witness/hand and Classical Greek coverage remain absent.

## Exact inputs needed by the training instance

Before any larger reviewed-corpus fit, the corpus instance must hand off a frozen, versioned dataset revision containing:

- An approved source ledger with exact image and TEI hashes, individual rights/attribution, confirmed page/folio/physical-leaf IDs and source-exposure history. If a new raster is acquired, supply its authorization and dimensions plus the exact transform from review-image coordinates.
- Stable per-line revision IDs tied to independently reviewed image-frame polygons, reading order and reproducible crop geometry. Include the raster hash/coordinate frame and evidence that crops preserve target marks and avoid neighboring rows; flag rejected or unresolved geometry rather than silently including it.
- Independently reviewed, adjudicated **visible diplomatic target text**, a declared target-state/mark/Unicode profile, reviewer and second-review identities/dates, disagreements and explicit exclusions. Keep unmodified reference TEI and its editorial/restored readings separate. Returned packet CSVs need an explicit reviewed conversion; they are not existing importer schemas.
- Frozen partition membership grouped by physical leaf and every derivative of each page, with sources exposed during development disclosed. For unseen-witness claims, group the entire witness. Supply a genuinely fresh acceptance set independently of these already inspected sources, and report actual reviewed line/character/mark counts, usable volume and remaining coverage gaps.
- A machine-checkable dataset receipt/allowlist linking every accepted target, geometry and crop to those revisions/hashes, plus reviewed exclusions and provenance. The integrator and training instance must agree the loading adapter and verify exact line IDs/counts before fitting; merely placing images or TEI in a folder is insufficient.

The training instance then owns the registered configuration, train-only learned preprocessing/alphabet, alignment-feasibility checks, matched baselines, checkpoint/reload parity, per-line diagnostics, regression/resource measurements and preservation of failed results. It must not infer acceptance, generate restored labels, assign hidden splits or substitute this source-review packet for those inputs. Resume work on these source candidates at specialist review, not at another model fit.
