# P46 provisional expansion — independent image/source QA

Review **QA-E1, 2026-11-02 evening** covers the six staged Michigan surfaces NTVMR10046/pages 1470–1520, proposed leaves 83r/v–85r/v. This is independent **AI engineering/source QA**, not Greek-specialist adjudication or publication of `P46-reviewed-v1`. No accepted visible-text or human-review fields are populated. All proposed folio/side identifications and scholarly readings still require specialist review.

The owner renewed a two-hour training session. For that separately authorized provisional study only, the reviewer finds **27 conservatively filtered source-reference rows** suitable for the proposed image/crop experiment, with one additional plain-source row excluded for damage uncertainty. This does not complete NT-15, the reviewed technical-control gate or any accuracy/release gate. Existing development page 1440 remains unchanged, all six new surfaces are training-only, and the earlier metadata-only leaves 78–80 were not opened.

## Evidence and method

Read the retained original TEIs and exact institutional JPEGs under `.artifacts/datasets/ntvmr-pilot/expansion-staging-2026-11-02/`. Independently verify all 18 image/TEI/item-manifest hashes against that source ledger. The item-specific image PDM notices, INTF CC BY4.0 notices and attributions remain in the source packet; this review assigns no model-weight distribution license.

Independently traverse source `<lb>` structure, marking both sides of continued words, `w@part`, unclear/supplied spans, apparatus, abbreviations/overbars and gaps. No `<lb>` occurs inside an apparatus branch in these six TEIs, so apparatus alternatives do not themselves create an internal row-number offset. Twenty-eight rows have no such source flags. Apparatus text flattened by the structural audit is never used as a target; only unflagged rows are considered.

Inspect all six full-page images, each selected row in the six candidate sheets, and source-context bands for the initial large glyphs, tears and terminal letters. Compare visible row sequences with source references rather than forcing the image to match a published line count. The candidate row matches are preliminary source associations, not authoritative manuscript readings. No standard biblical edition supplies or corrects a target.

For all 27 final candidates, independently check the provisional text against the original unflagged TEI segment, the crop SHA-256, all RGB pixels against the recorded nearest-column translation, and blank `visible_text`/`reviewer` plus `reviewedTrainingEligible=false`. Text retains source lowercase spelling, distinct σ/ς, whitespace removal and NFD, as in the prior provisional experiment; these are model reference encodings rather than literal glyph-form claims.

Final crops use the retained 2200-wide image frame, source x 480 through 2174 inclusive, width 1695 and height 64. Individual column translations follow the recorded refined center anchors; no sharpening, restoration or generative enhancement occurs. Source pixels are preserved relative to the recorded image decoder. A flat `sourceQuad` is an initial prior, not an accepted polygon or the authoritative final sampling map; the per-column/pixel mapping records that transform. Generic PAGE inference must not be assumed to reproduce it.

## Findings and dispositions

| Finding | Evidence / action | Disposition |
| --- | --- | --- |
| QA-E01: initial page 1500 body window was too narrow for final text crops | A right bound 1960 could truncate endings such as L05's `της` and L13's `των`. Requested a distinction between detection and final crop bounds. | Final crops widened to x 480–2175; all 27 are inspected at the final bounds. |
| QA-E02: plain-source 1500L13 lies near a photographic hole | The full image/context around source x880–990,y1280–1360 creates uncertainty over upper strokes despite unflagged TEI. | **Exclude `P46-1500-L13`** from this provisional training allowlist. No target repair or inferred restoration. |
| QA-E03:1480L13 has an unfamiliar source reading | Full image and crop visibly retain the ΘΗϹΕΤΑΙ sequence, agreeing with `ζωοποιηθησεται`. | Retain the source reading; do not replace it from a familiar edition. Specialist judgment remains pending. |
| QA-E04: image/source terminal counts differ | Central image projections detect27/27/27/26/25/26 rows, against27/27/27/28/26/27 TEI breaks. Damaged lower remnants may not appear as peaks. | Do not force equal counts or globally shift the upper rows. Selected rows are all at or above source L18 and individually inspected; uncertain terminal rows remain excluded. No final physical-row census is claimed. |
| QA-E05: large initial glyphs and tears need crop checks | Inspect 1490L01 initialΥ,1480L13 initialζ,1510L14 initialε and1490L18 terminal letters. The initialΥ's substantial connected ink components have zero pixels outside the64px column map in the checked source region. | No remaining obvious truncation/neighbor-row mismatch in these final crops. This is engineering QA, not a claim that every ambiguous trace is adjudicated. |
| QA-E06: generic side captions conflict with folio metadata | Existing detailed source/manifest evidence proposes83r/v–85r/v while generic captions say Recto. | Preserve conflicts and source IDs; grouping all new surfaces into training avoids a new cross-split leak but does not prove physical identity. |

No adequate coverage of page 1440's faint conditions is established by this expansion. These remain one witness and likely one hand; the corpus supports neither unseen-hand/witness nor Classical Greek/inscription claims. Higher-resolution/native-image and mark/correction-state review remain separate work.

## Provisional-only allowlist

| Page | Row IDs allowed for this provisional study | Count |
| --- | --- | ---: |
|1470|L03, L05, L07, L12, L13, L14, L15, L16|8|
|1480|L09, L13, L14, L15|4|
|1490|L01, L06, L14, L18|4|
|1500|L05, L07, L08, L09, L14, L15|6|
|1510|L09, L10, L14|3|
|1520|L02, L03|2|

IDs have the full form `P46-<page>-L<row>`. Intersect this independent allowlist with the corpus instance's exact frozen allowlist; do not treat files merely present in the crop folder as eligible. Every other row is excluded from this new provisional subset, with the original source and exclusion reason retained. Human-reviewed eligibility remains false for every row.

Review evidence lives under `.artifacts/experiments/p46-evening-corpus-review/`: `source-lines-independent.json`, `source-integrity.json`, `draft-crop-checks.json`, the 1500 hole context, and `provisional-qa-allowlist.json`. The crop check records all 27 exact hashes/pixel correspondences; the independent allowlist records each label/crop/source digest. The separate corpus instance owns the final ledger, source maps, dataset receipt and original-source preservation. Training may consume only the new frozen version explicitly registered by the integrator; no experiment is run by this review.

## Frozen-version verification

At 18:19 CST, independently rehash all **132 frozen content files** and verify every per-output-pixel uint16 little-endian source-Y map against its ledger digest, dimensions and original RGB pixels. All27 maps/crops match. The corpus allowlist and independent QA allowlist have exactly the same 27 IDs. No frozen files are edited by the reviewer.

| Receipt | SHA-256 |
| --- | --- |
| Corpus `FREEZE.json` | `7be31665294845287b7c3b5561308302599796722e32ea89cc1531952b058e1b` |
| Corpus `line-ledger.json` | `25371c738192344de80aa8e5004f98c516300595ccde3c03965bc6aa02332380` |
| Corpus `train-allowlist.json` | `aba74900fd6ae8b5ad891a2b82ab8e5ea16b51bf7dfd3b1630bb2d577c249c0d` |
| Independent `provisional-qa-allowlist.json` | `999cdba92ef727a971d3db77133a5b138dfcfee230c8e789f5986fbaae5faa48` |

The 27 proposed rows contain **926 reference scalars**, with no accepted visible-text fields. `final-source-map-checks.json` records the independent final map/content verification and source-raster-receipt digest. This closes the scoped AI engineering/source QA with no additional exclusions; all specialist and release limitations above remain unchanged.
