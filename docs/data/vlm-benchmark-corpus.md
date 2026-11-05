# VLM-B1 corpus release and remaining gaps

Prepared 2026-11-04 under the activated `VLM-BENCH-1` engineering assignment. The immutable
local release is **`VLM-B1-r1` / `p46-exploratory-v1`**, at
`.artifacts/vlm-benchmark/vlm-bench-1/b/VLM-B1-r1/`. It contains metadata for **51 existing
P46 line crops on five proposed physical leaves**, with separate source references. It is
usable for explicitly provisional, local inference diagnostics. **There are zero reviewed
selection or confirmation lines in this release.** No training is authorized by it.

The benchmark [specification](../evaluation/vlm-lmstudio-benchmark.md) remains authoritative.
The [scoring implementation and policy](../evaluation/vlm-benchmark-scoring.md) keep raw
source-reference distance separate from a claim about visible Greek transcription. The
original [specialist packet](p46-specialist-review-packet.md) and
[expanded provisional corpus](p46-expanded-provisional-corpus.md) remain unchanged.

## Frozen files and joins

| File | Contents |
| --- | --- |
| `manifest.json` | Request-facing samples: opaque ID, image path/hash, physical unit, material, review/exposure state and evidence pointers; no reference text or R0 hypothesis |
| `references.json` | Offline provisional source-reference targets, target UTF-8 hashes, original IDs, `reviewed=false`, null reviewers and explicit ineligibility for scholarly accuracy |
| `provenance.json` | Original page/line IDs, source TEIs and item rights, image hashes, geometry, physical grouping, exposure and all 192 excluded source rows |
| `integrity-audit.json` | 316 source/crop/map file-hash checks, 81 unchanged pending original decisions and eligibility counts |
| `FREEZE.json` | Hashes and sizes of the four files above and the preparation script; changes require a new release |

`imagePath` resolves relative to the containing manifest directory. Evidence pointers use
the same path base plus JSON Pointer fragments, for example
`provenance.json#/samples/0/geometry`. The local receipt can resolve original identities;
the model sees only image bytes and the frozen prompt, never provenance, verse anchors,
shelfmarks, source text or reference labels. `baselineText` is absent until a separately
recorded frozen R0 replay supplies it. A derived R0-assisted manifest must have a new hash;
do not edit this release.

The subsequent frozen R0 replay has produced a separate derived request manifest at
`.artifacts/vlm-benchmark/vlm-bench-1/b/r0-replay/cli-handoff-v1/manifest.json`, with actual raw
`baselineText` and verified absolute image paths for all 51 samples. Its hash and checkpoint
linkage are recorded in [the handoff](../handoffs/VLM-B1.md#frozen-r0-pointer-for-the-same-51-crops).
It changes no source label, crop, original r1 manifest or eligibility decision.

`manifest.json` SHA-256:
`b5fcf13a65b56e61e6a70427b7558b6f2268d684a19febd63a8b41f9f3b3ea09`.
`FREEZE.json` SHA-256:
`88338ff885e05b6dbed8a94651f70baefc3242983f86ed4287780fd2842dc89b`.

## P46 inventory, grouping and geometry

| Opaque unit | Proposed physical leaf and surfaces | Crops | Prior exposure |
| --- | --- | ---: | --- |
| `unit-01` | Leaf 81, page 1440 | 7 | Repeated development evaluation and selection |
| `unit-02` | Leaf 82, pages 1450/1460 | 17 | Training and development/source inspection |
| `unit-03` | Leaf 83, pages 1470/1480 | 12 | Provisional training and source inspection |
| `unit-04` | Leaf 84, pages 1490/1500 | 10 | Provisional training and source inspection |
| `unit-05` | Leaf 85, pages 1510/1520 | 5 | Provisional training and source inspection |

Recto/verso stay together. Generic source-caption conflicts remain in the provenance;
proposed folio identity has not become a specialist decision. These are five leaf groups
within **one biblical witness**, with no independently adjudicated hand diversity. Known
text and repeated passages may have appeared in foundation-model training; exposure is
unknown and cannot be cleared merely by choosing a new photograph.

All 51 PNGs are the original **1695 × 64 RGB geometric derivatives**, not height-16 model
tensors. Source X is `480 + output column` in a retained official width-2200 IIIF JPEG.
The 27 expansion crops retain exact little-endian uint16 source-Y maps, verified by hash
and size against the frozen corpus. Their original pixel-equivalence audit remains linked.
The 24 earlier crops retain the exact frozen centroid anchors and C# recipe, including
rounding and interpolation; they lack a frozen per-pixel source-Y map. No new pixel-level
equivalence claim is made for them. Neither geometric recipe is an accepted line polygon
or a native-TIFF coordinate frame. The historical
[provenance correction](../evaluation/p46-source-provenance-correction.md) accompanies the
earlier crops; they are rectified derivatives, not untouched rectangles.

No image was altered, recropped, enhanced, copied into a new corpus or downloaded for this
release. The 316 checks include the complete original specialist-packet content manifest,
the 132-file expansion freeze, original recipe/ledger receipts, 51 crop digests and 27 maps.
This is integrity verification of existing evidence, not a fresh visual or scholarly review.

## Asset rights and target limitations

Retained institutional manifests for **APIS3612–3620** identify the specific Michigan image
items as public domain / **Public Domain Mark 1.0**. Their exact `dlxs_ri` text, source URLs,
file hashes and University of Michigan Library Digital Collections attribution are in
`provenance.json`. PDM is a status mark, not CC0. These records do not cover another
institution's photographs of P46. Each exact retained NTVMR TEI preserves the **CC BY 4.0**
notice and Institut für Neutestamentliche Textforschung, ©2026 attribution. This local audit
rechecked the retained item-level evidence; it did not make a new network rights check or
assign redistribution terms to model weights.

The 51 targets preserve the prior source-reference convention: editorial whitespace
removed, NFD storage, lowercase source spelling and conventional distinct `σ/ς`. They do
**not** assert the visible case, lunate sigma, spacing, accents or overbars on the image.
The scorer applies NFC only; it does not silently convert VLM majuscules/lunate sigma into
these source labels. A large raw distance may therefore measure a transcription-profile
mismatch as well as recognition errors. A lower distance does not establish more faithful
visible transcription. No case/sigma/space projection has been preregistered or applied.

All **81 original specialist line decisions remain blank/pending**. Expansion rows likewise
have blank accepted text and reviewers and `reviewedTrainingEligible=false`. The preparation
did not invent a reviewer, resolve an adjudication question or create a competing accepted
corpus. Its 51 chosen source references are 24 historical conservative rows plus the exact
intersection of the expansion's 27-ID producer and independent engineering-QA allowlists.
Excluded markup/damage/continued-word rows remain excluded. No familiar edition replaces a
source reading.

## Other local collections and next intake work

This table inventories existing local metadata and prior audit records. Only file names and
counts were checked for the non-P46 collections during this assignment; their images,
PAGE labels and publisher test-split files were not opened. Prior counts are attributed to
the linked registers, not a new specialist acceptance.

| Collection | Existing evidence | VLM release status and specific gap |
| --- | --- | --- |
| P01 Zenon papyri v0.9 | [Papyrus register](papyri.md#10-pap-01-execution-record-2026-10-18-single-instance-mode-instance-a-absorbed-all-packages): 32 JPGs/27 PAGE files, 321 prior audited lines; local counts agree | No VLM lines released. Record-level CC BY 4.0 exists, but separate image-origin notices, visible-text conventions, object grouping, previous exposure and specialist review need reconciliation |
| M02a Stavronikita 53 v1.0 | [Manuscript register](manuscripts.md#10-man-02a-execution-record-2026-10-19-single-instance-mode-instance-a-absorbed-all-packages): 54 image/PAGE pairs, 1,038 source lines, 157 editorial-markup exclusions; 881 previously used pilot lines | No VLM lines released. Later Koine minuscule, one codex, already pilot-exposed. Verify item rights and word-to-line/space/abbreviation policy; no unseen-witness claim |
| M01 EPARCHOS v1.0 | [Manuscript register](manuscripts.md#11-m01-acquisition--cross-witness-generalization-gate-2026-10-19): 120 pairs, 2,272 prior audited lines; existing cross-witness sample exposed | No VLM lines released. Specialist label/hand mapping and item rights pending. The source card names British Library Add.6791 while `data/manifests/manuscripts/m01.json` attribution names Biblioteca Apostolica Vaticana; reconcile holding/attribution evidence before reuse |
| Classical/Koine inscriptions | [Inscription register](inscriptions.md): I.Sicily EpiDoc data rights differ from study-only imagery; Petrie image deposit is a potential CC BY 4.0 seed | Zero aligned, independently reviewed lines released. Verify exact object/image rights, construct image geometry and visible lettering with an epigraphist; editions are not image labels |
| Other papyrus/manuscript candidates | Existing source cards include Turin, letter crops and later manuscripts | No new acquisition or eligibility. Resolve item rights and pairing before any benchmark intake; glyph sets do not provide line-recognition ground truth |

P46 leaves 78–80 remain reserved metadata-only candidates; their images, transcriptions and
acceptance contents were not opened. Nothing in this release assigns them to setup or
consumes the evaluator's future holdout.

## Readiness and human handoff

| Required release | Planned target | Available in VLM-B1-r1 |
| --- | --- | --- |
| Reviewed setup | Up to 20 reviewed development lines plus synthetic fixtures | 0 reviewed natural lines; 51 explicitly provisional diagnostic lines |
| Reviewed selection | 120 lines; material-specific coverage and independent units | 0 eligible lines |
| Independent confirmation | At least 60 fresh reviewed lines on disjoint units | 0 eligible lines; custodian not named |
| Grounding controls | 40 reviewed no-trace, 10 unreadable-trace, 10 masked cases | 0 reviewed natural controls; root owns separate synthetic engineering controls |
| Specialist/custodian roles | Named reviewers and returned decisions | Unassigned/unreturned; an AI instance is neither role |

Next, the Greek specialist returns the original packet decisions and settles visible
case/sigma/spacing/mark/abbreviation policy. A second reviewer resolves contested readings
and geometry; B versions accepted records only from actual returned evidence. For non-P46
tracks, intake must add rights-cleared physical units and material-appropriate R0 baselines.
Before selection, a named evaluator freezes grouping/exposure and a sample-size/precision
assessment based on setup evidence. Five leaves of one exposed witness do not justify
independence or a reliable generalization interval. Until then, reports remain exploratory
with explicit no-adoption and no-scholarly-readiness conclusions.
