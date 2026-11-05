# Item-level intake candidates for the natural VLM benchmark

Source review dated 2026-11-04. These are intake candidates for specialist and rights review. The review established pair availability and, where stated, file identity. It did not adjudicate readings or crop geometry, assign splits, or run inference. Apply the [reviewed-run preflight](vlm-reviewed-run-preflight.md) before any benchmark release.

| Track | First intake action | Remaining release gap |
| --- | --- | --- |
| Classical literary manuscripts | Keep two publisher-aligned Homer folios as conditional leads for a separate literary control | A strict Classical Greek witness with permitted images and a paired diplomatic transcription remains open |
| Documentary papyri | Check the two Zenon PAGE pairs as calibration material | Find fresh physical objects outside P01 for confirmation |
| Inscriptions | Resolve image and edition permissions for two Petrie pairs and one I.Sicily lead | Secure reusable source photos, establish diplomatic lines and recruit independent physical objects |

## Classical literary manuscripts

The Homer Multitext facsimiles pair photographs with page-specific diplomatic transcriptions and identify the corresponding edition passages. Two concrete examples from Venetus A, Venice, Biblioteca Nazionale Marciana, Gr. Z. 454, are:

| Folio | Image identifier | Text anchor |
| --- | --- | --- |
| [12r](https://www.homermultitext.org/facsimiles/venetus-a-2020/pages/urn_cite2_hmt_msA-v1_12r.html) | `urn:cite2:hmt:vaimg.2017a:VA012RN_0013` | Iliad 1.1 |
| [24r](https://www.homermultitext.org/facsimiles/venetus-a-2020/pages/urn_cite2_hmt_msA-v1_24r.html) | `urn:cite2:hmt:vaimg.2017a:VA024RN_0025` | Iliad 1.602 |

The publisher's folio pages state CC BY-NC-SA 3.0 for the original and derivative images. The manuscript edition introduction states CC BY-NC-SA 3.0 for its transcriptions. The two folios belong to the same tenth-century vellum witness and contain Archaic Greek epic. Track their language separately from Classical Attic and Koine. Use only with an approved scope consistent with the notices or additional permission. No images or transcription files were acquired. [Venetus A edition introduction](https://www.homermultitext.org/manuscripts/venetusA/VenA-Introduction-2014/).

For a conditional literary control, keep the whole codex in one split and mark the canonical passage and published edition as exposure risks. The exact training exposure of local VLMs is unknown. A reviewer must distinguish main text from scholia, confirm whether any source text has expanded abbreviations or regularized accentuation and identify image coordinates for each usable line. Identify an Attic prose witness with a separately verified item-level image grant and diplomatic transcription before claiming Classical Greek manuscript coverage. [Manuscript source register](../data/manuscripts.md).

## Documentary papyri

Zenon Papyri HTR v0.9 provides exact photograph and PAGE transcription pairs under a record-level CC BY 4.0 license. Both local JPG and XML files below matched the deposit MD5 values. The publisher has no separate photo-origin rights statement in the record. Preserve the record attribution and check any upstream image-holder notice before adding a crop to a shareable release. [Zenodo deposit and file manifest](https://zenodo.org/records/6565706) · [Zenodo API record](https://zenodo.org/api/records/6565706).

| Pair in `.artifacts/datasets/p01-v0.9/` | Raster and labeled lines | Deposit MD5, JPG then XML | Identity |
| --- | --- | --- | --- |
| `P.Cair.Zen.III.59376.{jpg,xml}` | 362 × 1043, 21 PAGE lines with Unicode text | `ab8aacdfebec02b3562e6585007284e6`, `fbb5591f0acc36c81187ed414646f2bc` | Publisher-paired basename. External catalogue concordance pending because the item resolver returned a bot challenge |
| `P.Cair.Zen.III.59377.{jpg,xml}` | 573 × 550, 11 PAGE lines with Unicode text | `6a65636d0cdcdb52064489b233c1c43f`, `529a217286cdd5a22df7d4eb25cca2c6` | TM 1020, APIS `oxford-ipap.apis.856`. [DDBDP catalogue concordance](https://papyri.info/ddbdp/p.cair.zen%3B3%3B59377) |

Have a papyrologist inspect each PAGE line polygon and associated transcription against the photograph, recording clipped neighboring ink, damage, ambiguous glyphs, and any editorial supplements, expansions or normalized forms. Deposit line counts describe the available annotations. They do not establish acceptable benchmark counts. P01 has already been used for training and development, and the original locked test has been used three times. Treat these objects as exposed calibration material and record the exact model-specific split memberships before any comparison. A new confirmation cohort needs separately sourced objects with permission and review. [Papyrus source register](../data/papyri.md) · [P01 baseline and split history](model-card-p01-h16-aug.md).

## Inscriptions

Manchester's *Petrie Museum Greek Inscriptions*, DOI `10.48420/20466087`, is a candidate source for paired photos and published editions on the same PDF page. The checked file is Figshare file `36632112`, MD5 `b82ea5953f54647961cdb8406321df03` and SHA-256 `958c11a394214a25098ade0db931eb330fa29d2d095cefe134dbd16bfe1fb77c`. Page numbers below are one-based PDF indices. [Manchester deposit](https://research.manchester.ac.uk/en/datasets/petrie-museum-greek-inscriptions/) · [Figshare article manifest](https://api.figshare.com/v2/articles/20466087).

| Object | Verified photo and text | Specialist or geometry check |
| --- | --- | --- |
| **UC16506**, PDF p. 12 | Menandros funerary stele, Hawara, first century CE, TM 8647, I.Fayoum 1:58 / SB 1.5760. Embedded photo 400 × 300 alongside a Greek edition and apparatus | Map visible stone lines to the edition. Flag supplied or obscured characters and verify whether the small raster supports tight crops |
| **UC34710**, PDF p. 15 | Ptolemaic limestone rim, embedded photo 400 × 300 alongside a one-line printed Greek reading | Verify surviving letter forms and punctuation against the curved surface. Request a higher-resolution licensed source if the available photo cannot support adjudication |

Manchester's deposit states that the images are released with Petrie Museum permission under CC BY 4.0. The embedded photographs on both pages visibly retain a CC BY-NC-SA © 2017 UCL notice. Record both statements and get confirmation that the later grant covers the specific photographs, crop derivatives, benchmark processing and any intended release. Verify the permission for the modern edition separately. Do not use or extract these images while that question is open. If cleared, isolate the artifact photo so the adjacent printed Greek and apparatus cannot leak into model input. Record the exact embedded raster or new source ID, original pixel coordinates and hashes. Check the actual model and comparator provenance before assigning exposure. The publisher PDF was inspected locally. No image crops were extracted for the benchmark.

I.Sicily item **ISic001151** has an edited Greek text and a photograph credited to J. Prag, courtesy of the Museo civico di Termini Imerese. Its EpiDoc and an image are present in the project's earlier I01 smoke material. Current I.Sicily guidance distinguishes attributed embedding from downloading and reusing images, for which it directs users to the relevant authorities. Earlier guidance also conditions publication. Keep this object as an exposed lead and seek item-level permission for benchmark processing and release. Review the edition's separate terms and adjudicate actual line geometry and visible letters before accepting any rows. Apply this distinction when interpreting the broader I.Sicily license description in the project plan. [ISic001151 item](https://sicily.classics.ox.ac.uk/inscription/ISic001151) · [Current I.Sicily reuse guidance](https://kingsdigitallab.github.io/corpus-building/about) · [Earlier I.Sicily data policy](https://isicily.org/data-in-isicily/) · [Inscription source register](../data/inscriptions.md).

## Handoff

1. The rights owner records item-level image and edition grants separately, with attributable provider, exact item or file IDs, scope of local inference and permissible derivative release. Hold any conflicting or incomplete grant out of release.
2. The Greek specialist receives source photos and separately labeled editions, then confirms crop-to-line alignment and adjudicates visible diplomatic targets. Preserve editorial readings independently and document exclusions and unresolved features.
3. The evaluation custodian freezes the source and crop hashes, geometry transforms, diplomatic target revision and paired baseline receipt. Assign physical objects, witnesses, and overlapping views to disjoint partitions, with model and prior-project exposure documented. Confirmation needs fresh objects and enough independent units per claimed material.

## Additional manuscript leads

- E-Rhetoric v3 lists 265 PAGE XML files with line transcriptions from ÖNB Phil. gr. 130 and Vatican Vat. gr. 2228. The deposit omits the source images. The ÖNB viewer is the first alignment lead because the deposit reports web-sized originals for that witness. The Vatican web copies need a validated scale and watermark check against the rasters used for annotation. Confirm item-specific image permissions, the annotation grant, and the textual register before intake. Record the published whitespace normalization separately from a reviewed diplomatic target. [Dataset](https://zenodo.org/records/22850225) · [ÖNB item](https://viewer.onb.ac.at/13228923) · [ÖNB terms](https://www.onb.ac.at/en/use) · [Vatican item](https://digi.vatlib.it/view/MSS_Vat.gr.2228.pt.1).
- NOMIKOS Grec 1360 provides 204 aligned lines from five pages of the 1351 *Hexabiblos*. The scholarly transcriptions and annotation geometry carry CC BY 4.0. Gallica applies separate image terms, including an AI research provision for non-profit academic use and a separate license for other AI uses. Treat this as a conditional medieval calibration lead. [Dataset card](https://huggingface.co/datasets/nomikos-project/greek-manuscript-htr) · [Gallica terms](https://gallica.bnf.fr/accueil/fr/html/conditions-dutilisation-de-gallica).
