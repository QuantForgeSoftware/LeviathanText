# Greek inscription intake audit

November 4, 2026. This audit counted the existing I.Sicily XML snapshot and the already staged Petrie catalogue. Four live IIIF manifest metadata records were checked. No image was downloaded, extracted from the PDF, cropped, or used in inference. The rights-cleared image and reviewed-line counts are zero for this intake.

## I01 I.Sicily

All 5,136 inscription XML files match the bytes of the local archive. Its SHA-256 is `cdb72eb024c9f3705e4290205488d93eb3a4bdfcceada29dca934cff30b8c681`. Use this snapshot digest for reproducibility. The archive name alone does not pin a Git commit. XML parsing ignores commented graphics and finds 871 records with at least one active `facsimile/graphic` URL.

| Selector | Records | With active facsimile graphic |
| --- | ---: | ---: |
| Greek language tag in any edition element or descendant | 3,131 | 446 |
| Unicode Greek letters in any edition text | 3,267 | 477 |
| Unicode Greek letters in primary edition text | 3,248 | 474 |
| A primary edition has Greek letters and a Greek tag, plus primary-edition line markers | 2,789 | 438 |

The earlier 477 result is reproduced by the broad letter-based selector. The older 448 count used a different comment-aware language filter. It is close to the parsed language-tag count of 446, but the earlier script's exact selector is unavailable. Use 438 as a conservative **metadata shortlist**. Edition content can include supplements, multilingual passages, and editorial apparatus. Facsimile graphics can include drawings or auxiliary views. The shortlist is a queue for screening those details and photograph reuse.

Of those 438, 335 records are marked draft, 97 edited, five lack status, and one is deprecated. The edition notice says CC BY 4.0 on 433 records and CC BY-NC 4.0 on five. Preserve these five notices on ISic090056, ISic090066, ISic090093, ISic090110, and ISic090135. For chronological triage, the source date bounds place 15 shortlisted objects fully within 500–323 BCE, 111 within 322–31 BCE, and 76 within 30 BCE–300 CE. These are explicitly bounded date bins and do not identify the dialect of an edition. All 15 objects in the earliest bin are draft. Within the complete snapshot, four paths differ from the declared EpiDoc filename. Keep both IDs pending reconciliation. The local shortlist contains two repeated TM groups, 644954 and 492868. Keep these grouped pending object inspection. The 112 shortlist records with no TM field need a collection and inventory concordance.

### Four checked photographic leads

The four manifests resolve and list front photographs matching the local EpiDoc graphic identifiers. No checked manifest, object-photo canvas, or image body provides an explicit rights URI. Required statements carry credits or scope-limited wording. The project permits attributed IIIF embedding and directs downloading or other reuse to the relevant authorities. Keep the linked photos in this metadata queue pending permission. [I.Sicily reuse guidance](https://kingsdigitallab.github.io/corpus-building/about).

| Item and local edition status | Date and line markers | Front canvas | Intake role |
| --- | --- | --- | --- |
| ISic001087, draft | 475–450 BCE, 11 | 5,520 × 3,680 | Classical-period candidate. Front and right views are separate from petrographic canvases. Check rights and whether any line straddles the photographed faces. [Manifest](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001087.json). |
| ISic001439, draft | Early fifth century BCE, 3 | 1,314 × 1,059 | Small Classical-period starting candidate. The Museo Paolo Orsi photograph credit identifies negative 19982. The local EpiDoc cites regional authorization for the image. Clarify whether a separate downstream grant applies. [Item](https://sicily.classics.ox.ac.uk/inscription/ISic001439) · [Manifest](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001439.json). |
| ISic001035, edited | Second century BCE, 6 | 5,520 × 3,680 | Hellenistic candidate with visible-trace and damaged-line questions. The edition records autopsy and variant readings. Keep the rear view and petrography outside the inscription-face label set. [Item](https://sicily.classics.ox.ac.uk/inscription/ISic001035) · [Manifest](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001035.json). |
| ISic001151, edited | Second–third centuries CE, 3 | 5,520 × 3,680 | Existing workflow smoke, previously exposed. J. Prag photo courtesy of Museo civico di Termini Imerese. Inspect the potentially corrected letter in line three after permission. [Item](https://sicily.classics.ox.ac.uk/inscription/ISic001151) · [Manifest](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001151.json). |

## I02 Petrie Museum

The existing 30-page PDF matches the publisher file ID 36632112 and MD5 `b82ea5953f54647961cdb8406321df03`. Its SHA-256 is `958c11a394214a25098ade0db931eb330fa29d2d095cefe134dbd16bfe1fb77c`. The 24 numbered entries cover 21 catalogued entries with 22 explicit UC identifiers, plus three entries with no known accession number. One entry groups UC16504 and UC16505. Pages 2, 8, 11, 20, and 24 continue their preceding entry. The PDF has one blank numbered page and 24 embedded image occurrences across 21 entries. Eighteen of those 21 entries also have Greek letters in the PDF text layer. This is a mechanical text-and-image intersection, with quoted letters and supplements retained as edition evidence. The image-only or English-only text-layer rows are UC2373, UC16504/UC16505, and UC71137. None has aligned line targets. [Manchester deposit](https://research.manchester.ac.uk/en/datasets/petrie-museum-greek-inscriptions/) · [Figshare file manifest](https://api.figshare.com/v2/articles/20466087).

The inventory records entry number, pages, image dimensions, inventory and explicit TM references. Eighteen entries have a TM reference. None matches a numeric TM field in the frozen I.Sicily snapshot. This check leaves unkeyed objects and linked fragments open. The catalogue spans Ptolemaic, Roman and later Greek material, with limestone, marble, sandstone and other supports. It includes worn and fragmentary text. The earlier candidate scan verified embedded CC BY-NC-SA notices on UC16506 and UC34710 despite the deposit-level CC BY 4.0 statement. Hold all photograph derivatives until scope is clear, and separately verify terms for the cited modern editions. [Earlier item and notice checks](../evaluation/vlm-natural-intake-candidates-2026-11-04.md#inscriptions).

If the item terms clear, UC34710 offers a small Ptolemaic rim test. Check whether the single visible line follows the curve and whether the 400 × 300 raster preserves each stroke. UC16506 offers a first-century funerary text with restoration issues, also at 400 × 300. Larger rights-cleared source photographs would support better review. After that, an epigraphist should map visible lines to source coordinates, flag uncertain and supplied letters, and keep catalogue editions separately as reference text.

## Reproduction

The local `.artifacts/corpus-intake-2026-11-04/` directory contains `audit-inscriptions.py`, `inscription-inventory.json`, the four exact manifest receipts, and their SHA-256 receipt. The inventory includes each I01 XML hash and per-entry I02 metadata. Replay from the repository root using Python and PyMuPDF. The script reads PDF text and embedded image metadata. It never extracts image pixels. Remaining INS-01 work is authorization for exact assets, verification of photograph views and period/register, and independently adjudicated geometry and visible line labels.
