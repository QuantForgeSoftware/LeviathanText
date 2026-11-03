# P46 Michigan specialist review packet v1

Prepared **2026-11-02**, following the owner's request for a Greek-specialist packet for NTVMR **10046 / pages 1440, 1450 and 1460**. See [item permissions and preliminary visual findings](p46-michigan-page-review.md) and [the pilot plan](../../project-plan.md#12-ntvmr--csntm-manuscript-pairing-pilot--2026-11-02).

## Deliverables

The self-contained packet lives in the Git-ignored artifact store:

- Folder: `.artifacts/datasets/ntvmr-pilot/specialist-packet-v1/`
- Open `index.html` in that folder; internet access is not required.
- Portable archive: `.artifacts/datasets/ntvmr-pilot/P46-Michigan-specialist-packet-v1.zip`. Extract it completely before opening `index.html`.

The packet contains **81 proposed line records/context crops**: 27 for 1440, 26 for 1450 and 28 for 1460. Each has a stable ID, source verse anchors, readable first-hand reference text, source uncertainty/supplied/overline styling, any apparatus alternatives, and links to relevant questions. Original source TEI is retained unchanged, along with the three full-page review photographs and institutional item manifests.

Other contents: **18 adjudication questions**, blank UTF-8 CSV line-decision and question-answer worksheets, numbered proposed-line SVG overlays, a JSON source/crop coordinate map, attribution, instructions and SHA-256 content manifest. These worksheets are specific to this packet and must not be passed directly to the application's existing review importer.

## What the specialist is asked to decide

First confirm each crop-to-line association and the visible extent of the target row. Proposed crops preserve the source pixels without sharpening, restoration or resampling; they are rectangular context bands and may include neighboring ink. Geometry is provisional. Terminal rows have intentionally wider context and explicit uncertain-alignment flags, particularly the last proposed record on 1440. The TEI line count must not be treated as proof that every proposed row retains ink.

Then decide visible readings, mark encoding, historical correction states and whether each line should be retained for second review, excluded, or recropped. All decisions begin **pending**, and every `visible_text` field is blank. The HTML shows source reference, not approved training labels. Corrections are recorded in worksheets rather than silently editing the TEI or source photographs.

Questions address:

- First-hand versus corrected logical text versus all visible ink, and treatment of cancellation/insertion marks.
- Restored text and uncertain traces, including exact surviving spans in the damaged lower rows.
- Case, lunate sigma, scriptio continua/word division, overbars, nomina sacra and line-end continuations.
- Page 1440's 25-versus-27 catalogue/TEI count, verso label and correction alternatives in Galatians 1:13–14.
- Page 1450's cancelled `τω`, the manuscript reading in Galatians 2:5, and right-edge losses.
- Page 1460's 2:9-versus-2:12 catalogue range, lambda correction and overlined contraction `συνεστραι`; classification as `nomSac` is an open question.
- Body-text boundaries versus manuscript page numerals, photographic ruler/colour bar and modern captions.

The questions present source claims for adjudication, not a requirement to agree with the preliminary AI review. An unresolved answer is acceptable. A standard biblical edition must not substitute for the manuscript's ink.

## Returning the review

Return completed `decisions.csv` and `adjudication-questions.csv`, with reviewer/date and supporting notes. Preserve stable record IDs and the original packet. CSV source notation uses `[text]` for supplied restoration, `{text}` for uncertainty and `OVER(text)` for overbars; these are annotations, not accepted label characters. Crop corrections should identify the width-2200 review-image coordinate frame, or be supplied on an annotated copy.

The packet is read-only HTML with editable external worksheets: browser viewing does not save review answers. Import CSV as UTF-8/all-text columns when using a spreadsheet and verify that Greek characters survive the return export. The README gives available statuses and explains how to request higher-resolution evidence.

## Rights and training boundary

Images: University of Michigan Library Digital Collections, P.Mich.inv. 6238, APIS3612/3613/3614; item-level **public domain / PDM1.0**. Transcriptions: Institut für Neutestamentliche Textforschung, **CC BY4.0**; original license notices and exact source links are included. Full attribution and modification notes accompany the packet. This does not cover separate CSNTM photographs of the same leaves.

The retained images are official width-2200 IIIF review derivatives, not full-resolution training captures. Crop coordinates map exactly to those retained review images. They are not accepted source-frame line polygons. All three pages are development/review material, not fresh blind acceptance data.

**No training, source-edition correction, project-label acceptance or institutional sending was performed.** Specialist adjudication, independent second review, verified geometry and a declared label profile remain prerequisites for any later training conversion.

## Verification record

**Subsequent status — 2026-11-02:** the owner later authorized provisional training. [Two local research runs](../evaluation/p46-provisional-training.md) used a separately prepared, filtered 24-line subset. This packet and its pending worksheets were preserved unchanged. The earlier no-training statement describes packet preparation; it is not the current experiment status. Specialist acceptance and the scholarly training/release gates remain open.

Verified the 27/26/28 source-line counts, 81 unique crop/map/worksheet IDs, 18 questions, pending statuses, blank accepted-text fields and retained source notices. Checked local links, HTML span balance, SVG syntax, source/crop hashes and lossless crop pixel correspondence. Visually inspected sampled crop sheets and browser rendering of the correction/apparatus display. Independent packet review found no blocking issue and requested the CSV notation clarification, which is included. Archive integrity passed: **104 files, 33.09 MiB**, including 103 content files and their manifest. ZIP SHA-256: `efafa135bb90a8cd99f5a4199d46c50e403bccd6578c56ae96ea45546c3e471d`; a matching `.zip.sha256` sidecar is available alongside the archive.
