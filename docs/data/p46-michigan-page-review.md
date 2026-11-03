# P46 Michigan: item permissions and preliminary visual review

Date: **2026-11-02**. Owner selection: **P46 Michigan approved by John Baima**. Reviewed NTVMR docID **10046**, pageIDs **1440, 1450, 1460**, against the corresponding University of Michigan APIS images. This completes item-rights checking and an initial visual comparison, not expert adjudication or preparation of training labels. **Training has not started and remains blocked on the gates below.**

## Item-level permissions

The ordinary Michigan item HTML pages presented verification/access problems. Their **official institutional IIIF manifests** were accessible and contain full item metadata, including `dlxs_ri` on the canvas. All three explicitly identify the material as public domain and link **Public Domain Mark 1.0**, permitting copying, modification and distribution, including commercial use, without asking permission. No item-specific contrary restriction was found in these records. This resolves the prior uncertainty about whether the collection-level image policy applies to these exact items.

| NTVMR page | Michigan item / authoritative manifest | Detailed side | Image rights | Transcription rights |
| --- | --- | --- | --- | --- |
| 1440 / 81v | [APIS 3612, 6238_159.TIF](https://quod.lib.umich.edu/cgi/i/image/api/manifest/apis:3612) | Section/Side 159; Verso; publication side Verso | Public domain / PDM 1.0, item-level | CC BY 4.0 in the retrieved NTVMR TEI availability block |
| 1450 / 82r | [APIS 3613, 6238_160.TIF](https://quod.lib.umich.edu/cgi/i/image/api/manifest/apis:3613) | Section/Side 160; Recto | Public domain / PDM 1.0, item-level | CC BY 4.0 in the retrieved NTVMR TEI availability block |
| 1460 / 82v | [APIS 3614, 6238_161.TIF](https://quod.lib.umich.edu/cgi/i/image/api/manifest/apis:3614) | Section/Side 161; Verso; publication side Verso | Public domain / PDM 1.0, item-level | CC BY 4.0 in the retrieved NTVMR TEI availability block |

Credit images to **University of Michigan Library Digital Collections**, with shelfmark, item ID and source link. Credit transcriptions to **Institut für Neutestamentliche Textforschung**, preserve the original availability/copyright notice and CC BY 4.0 link, and identify any changes. PDM is a public-domain status statement, **not CC0**. Do not apply these findings to separately photographed CSNTM or Chester Beatty assets merely because they depict P46.

Source transcriptions: [1440](https://ntvmr.uni-muenster.de/community/vmr/api/transcript/get/?docID=10046&pageID=1440&format=teiraw), [1450](https://ntvmr.uni-muenster.de/community/vmr/api/transcript/get/?docID=10046&pageID=1450&format=teiraw), [1460](https://ntvmr.uni-muenster.de/community/vmr/api/transcript/get/?docID=10046&pageID=1460&format=teiraw). [Collection-level guidance](https://lib.umich.edu/find-borrow-request/access-special-collections-materials/guidance-using-reproductions/) remains supporting evidence; the item manifests now provide the specific image evidence.

## Visual comparison method and scope

Inspected all three full-page photographs through **Michigan's manifest-declared IIIF Image service**, rendered at width 2200 pixels; reviewed the line sequence and distinctive text/mark/damage anchors against the source TEI. Separate AI visual passes also checked pages 1450 and 1460. This is preliminary engineering review, not a qualified Greek specialist's letter-by-letter sign-off.

The source TIFF image dimensions are 5059×5832, 5020×5880 and 4932×5888. Review renderings are respectively **2200×2536**, **2200×2577**, **2200×2626**. These are traceable downscaled derivatives, not new scans and not original-resolution training images. Initial downloads of the large NTVMR JPEG copies timed out and left partial files; those partial files are not used or retained in the review package. The institutional TIFF identifiers/manifest link the review derivatives to the same numbered capture family. Byte identity with the complete NTVMR JPEG copies was not established.

Physical-line references below mean top-to-bottom main-text rows, excluding the manuscript page numeral and photographic apparatus. TEI comparison uses the first-hand reading of each apparatus entry; corrected states are inspected separately. No normalized Bible text was substituted. Exact crop polygons and adjudicated character offsets have not been created.

## Page findings

### 1440 / 81v / image 159

**Pairing passes the preliminary check.** The top body text continues Galatians 1:10; the sequence through 1:22 and the line breaks agree broadly with the image. Upper/middle anchors include line 2's `γνωριζω`, the visible overlined nomina sacra in line 6 and the change to the damaged left beginnings in the lower half. The final row contains only limited surviving traces among large supplied passages.

- The photograph has **27 main-text row positions**, including damaged remnants, consistent with the 27 first-hand TEI line segments. APIS's catalogue range **1–25** is inconsistent with this inspection. Preserve the catalogue claim as source metadata; do not use it as the accepted training-line count.
- Galatians 1:13 (row 8) and 1:14 (row 12) have original/corrected alternatives. Preserve the first-hand versus corrected forms separately. The exact correction marks and sequencing require specialist adjudication; neither reading may be concatenated into the other.
- Losses at lower-left line beginnings correspond to supplied text, especially the lower rows through 1:21–22. The visibly incomplete page must not receive a complete restored text string as its visual target.
- No clear new lexical mismatch was established. Faint letters and correction details remain unresolved rather than being declared correct from the familiar passage.

### 1450 / 82r / image 160

**Pairing passes the preliminary check.** The first row begins with the continuation `ζεται την πιστιν`; subsequent text and the lower fragment fit Galatians 1:23–2:9. The 26 main-text row positions broadly agree with the first-hand TEI sequence and the catalogue line count.

- In Galatians 2:4, row 11 ends with a visible cancellation of a word that TEI records as original `τω` and an empty corrected reading. That is a witness correction, not a missing page or permission to flatten both apparatus branches.
- Overlined abbreviated names in rows 2 and 12 match the source's abbreviation/overline treatment. Row 13's manuscript variant is supported by the photograph; do not normalize it to the familiar standard edition.
- Right-edge losses increase substantially in rows **18–26**. The final completion in Galatians 2:9 is supplied rather than fully visible. Exclude unresolved rows from initial exact targets or make smaller, independently verified crops; do not mechanically turn the complete supplied text into labels.
- No wrong-page, gross line-order or clear new lexical mismatch was found in this pass. Faint individual traces still need specialist review.

### 1460 / 82v / image 161

**Pairing passes the preliminary check.** The top row visibly begins within **Galatians 2:12**, consistent with NTVMR. APIS's range **2:9–21** is overbroad for this photograph. The main sequence through 2:21 and the 28 text-row positions broadly fit the first-hand TEI.

- The opening phrase and later anchors in 2:16 and 2:18 establish that this is the expected page, not a scan beginning at 2:9. Do not move earlier verses onto this image merely to satisfy the catalogue range.
- Row 23 / Galatians 2:19 visibly contains overlined **`συνεστραι`**, matching the TEI contraction. Do not expand it to `συνεσταυρωμαι` in a visual label. TEI classifies the abbreviation as `nomSac`; that classification deserves profile review, without changing what the image visibly spells.
- Galatians 2:12 distinguishes a first-hand form from a corrected form with an additional lambda. Small correction marks need specialist assessment; importing both readings as consecutive words would be wrong.
- The lower-left damage, especially rows **24–28**, corresponds to supplied and uncertain portions. The final row is substantially reconstructed; treat it as reference/uncertainty material until visible spans are adjudicated.
- No definitive new lexical discrepancy was established. This does not certify every damaged letter or the proposed correction history.

## Discrepancy and training-risk ledger

| ID | Type / affected pages | Finding | Required treatment before training |
| --- | --- | --- | --- |
| P46-R01 | Catalogue side labels; 1440 and 1460 | Generic item titles/Image Side say Recto; detailed Section/Side and publication fields say Verso. | Preserve both source statements and use the reviewed crosswalk 81v/82v; never derive side from the generic label alone. |
| P46-R02 | Catalogue passage range; 1460 | APIS says 2:9–21; visible start and NTVMR identify 2:12. | Record corrected inspection range and its evidence without editing the upstream record. |
| P46-R03 | Catalogue line count; 1440 | Catalogue 1–25 versus 27 observed/TEI row positions including damaged remnants. | Retain the discrepancy; count only actually placed/adjudicated rows as usable samples. |
| P46-R04 | Edition versus visible text; all pages | Supplied restorations include lettering that the image no longer preserves. | Keep editorial reference distinct; exclude unresolved whole rows initially or create verified subline crops. Deleting brackets alone is not a valid conversion. |
| P46-R05 | Witness correction states; all pages | Alternative readings, insertion/deletion/cancellation semantics. | Choose a documented target state and preserve historical changes separately; obtain expert decisions for ambiguous marks. |
| P46-R06 | Visible abbreviation; 1460 row 23 | Contracted verb and overbar versus expansion; `nomSac` classification is questionable. | Preserve visible spelling/mark; review annotation type; do not silently expand. |
| P46-R07 | Transcription profile; all pages | Lowercase, word spaces and normalized sigma differ from capitals, scriptio continua and lunate forms in the images. | State reversible encoding/segmentation policy; do not misreport conventional representation as a page mismatch or invent visible word gaps. |
| P46-R08 | Image region scope; all pages | Manuscript page numeral, modern caption, ruler/color bar and margins are outside the main-text transcript. | Define body-region geometry; exclude photographic apparatus and decide separately whether manuscript page numerals are annotated. |
| P46-R09 | Review resolution / coordinate frame; all pages | Visual inspection used width-2200 derivatives, not full-resolution copies. | Retrieve/verify complete training-resolution assets and map geometry to their source frames before extraction. Never consume partial downloads. |
| P46-R10 | Evaluation exposure; all pages | These images and labels have now been inspected during development. | Assign them to review/development material; do not later present them as a fresh blind acceptance set. |

These findings distinguish metadata disagreements and label-conversion hazards from actual mispaired pages. **No wrong-page pair or unambiguous new text-reading error was established in this preliminary review.** Do not manufacture a text correction merely to resolve a catalogue inconsistency.

## Preserved evidence

Local, Git-ignored review package: `.artifacts/datasets/ntvmr-pilot/review-2026-11-02/`. It contains three original institutional manifests, three unmodified TEI responses, three official IIIF review renderings and `evidence-index.json`, with source URLs, byte counts, SHA-256 hashes, dimensions and explicit unadjudicated/development status. No full training corpus or model artifacts are created by this step.

| Page | Item-manifest SHA-256 | TEI SHA-256 |
| --- | --- | --- |
| 1440 | `91be67706f450f6584f098595761469b806bc65d9a2fa84a6351f05b1a5299d0` | `a7ff05653a9fc90df92f9204f3b3fab64204b5ca91558828caa55d56eff5bff2` |
| 1450 | `4e6a7d7de0501397f9714199d327c8c6bf8ba4c333c7baccdc6efa4d052ca4fa` | `b03c98e5f9fd924bb2093559b8ed8080ce633f1b3ed8285792b6abeb9c5c3742` |
| 1460 | `e254ea0d88c640ab3d4e236682e8e00464c953e04efec15208338526d67ed9b4` | `ed45977b6ad94c2d7a0696ce09a69543e267aa0893f57d5648a3fa37ccb82239` |

The official manifests identify the source images and their IIIF services. Their generic content-type label may say JP2 while a `.jpg` rendering is delivered; validate actual decoded content and dimensions rather than selecting a decoder from a metadata label.

## Updated disposition before training

- **Owner selection:** complete; P46 Michigan approved.
- **Selected-image rights:** item-level check passes, public domain / PDM 1.0. Selected source TEI remains CC BY 4.0.
- **Page correspondence:** preliminary visual check passes for all three, with the catalogue discrepancies above retained.
- **Scholarly labels and geometry:** incomplete; qualified independent review, exact visible spans and coordinate-linked crops remain required.
- **Training:** hold. First prepare a reviewed line subset with a declared original/corrected target state and mark policy; resolve or exclude uncertain/restored spans; then freeze splits and a fresh evaluation sample. No training was performed in this task.

Next priority is clean upper/middle text regions, particularly on 1450/1460, while retaining damage/correction examples for policy testing. A human reviewer must approve exact selections; no blanket clean-line range is certified here. Expansion to 10–20 pages may proceed as a separate intake step, with the same item-level rights and label checks.

**Specialist-review preparation:** a [portable packet](p46-specialist-review-packet.md) now contains 81 proposed line-context crops, original sources and 18 questions with blank decision worksheets. Last-row associations are explicitly uncertain rather than certified from the TEI count. Specialist answers, accepted labels and training remain pending.
