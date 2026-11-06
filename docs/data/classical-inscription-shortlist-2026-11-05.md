# Classical-period inscription metadata shortlist

November 5, 2026. This is a concrete **metadata and rights-review queue**, not an
accepted Classical Greek corpus. It expands the 15 chronologically selected I01
objects in the [November 4 intake](inscription-intake-2026-11-04.md). All 15 source
records remain **draft**. Every primary edition explicitly carries `xml:lang="grc"`;
that source tag does not establish a Classical Greek dialect/register assignment.
No image, image-info endpoint, pixel, crop, model, inference, training, accepted
reading, accepted geometry or institutional contact is part of this work.

## Registered scope and evidence

Before the first manifest request, the registration recorded the existing inventory,
archive, prior selector script and all 15 selected XML SHA-256 hashes, the exact
selector, 15 URLs and request limits. The selector requires an active facsimile
graphic, Greek letters and a Greek tag within a primary edition, and primary-edition
`lb` markers. At least one raw `origDate` pair must satisfy
`-500 <= notBefore-custom <= notAfter-custom <= -323`. This replays the existing
chronological bin; it does not revise source dating or infer language.

Exactly 15 official IIIF manifest metadata GETs ran 17:53:57–17:53:59 UTC, one per
registered item, with no retry or redirect following. All 15 returned HTTP200 JSON.
Each response was limited to 4 MiB/30 seconds; the largest was 24,529 bytes. Raw response
bodies, headers, stderr (including empty successful stderr), timestamps, status,
byte count and SHA-256 are retained. There were no failed manifest requests.
No image bodies or image-service `info.json` resources were fetched.

The responses describe 79 canvases: 19 object-view canvases and 60 whose IDs explicitly
mark petrographic auxiliary content. All 19 object-view image-service identifiers
match active graphic references in the frozen XML. This is a metadata join, not
visual verification. None of the 15 manifests,79 canvases or79 image bodies has an
explicit `rights` URI. Attribution text is present, including one explicit
photographic license described below. Zero photographs are cleared for a general
open corpus; zero labels or geometry are accepted.

## Fifteen source records

Raw bounds and human wording are kept separately, including spelling and apparent
inconsistencies. `lb` means source XML marker occurrences, **not verified visible
lines or image-aligned training pairs**. Every row below is source-status `draft`,
has one primary-edition `xml:lang="grc"` tag, and the edition license target
`http://creativecommons.org/licenses/by/4.0/` ([CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)).
The exact edition notice is “Licensed under a Creative Commons-Attribution 4.0
licence.” on 13 records and the same notice without its final period on
ISic001439/ISic003657. This covers edition data; it is not a photograph grant.
Exact material descriptions/types, repositories, raw TM whitespace, line-marker
attributes/IDs, licenses and active graphics are retained in the JSON/CSV.

| Item | Raw bounds | Source date wording | TM / inventory | Material summary | `lb` |
| --- | --- | --- | --- | --- | ---: |
| [ISic001087](https://sicily.classics.ox.ac.uk/inscription/ISic001087) | `-0500`…`-0451` | 475—450 BCE | 492716 / 8753 | Yellowish limestone/calcarenite | 11 |
| [ISic001089](https://sicily.classics.ox.ac.uk/inscription/ISic001089) | `-0500`…`-0401` | Mid 5th century BCE | 285084 / 8785 | Plaster on yellowish calcarenite; type `mortars` | 3 |
| [ISic001430](https://sicily.classics.ox.ac.uk/inscription/ISic001430) | `-0500`…`-0451` | 475—450 BCE | 284788 / 8752 | Plaster on whitish calcarenite; type `mortars` | 5 |
| [ISic001432](https://sicily.classics.ox.ac.uk/inscription/ISic001432) | `-0475`…`-0425` | mid-5th century BCE | 285049 / H.65.36 | Limestone | 1 |
| [ISic001439](https://sicily.classics.ox.ac.uk/inscription/ISic001439) | `-0500`…`-0451` | Beginnning 5th century BCE | 644982 / 7735 | Whitish limestone/calcarenite | 3 |
| [ISic001469](https://sicily.classics.ox.ac.uk/inscription/ISic001469) | `-0500`…`-0451` | 475—450 BCE | 284889 / 8806 | Whitish limestone/calcarenite | 2 |
| [ISic001470](https://sicily.classics.ox.ac.uk/inscription/ISic001470) | `-0475`…`-0425` | Mid-5th century BCE | 284912 / 8803 | Yellowish limestone/calcarenite | 7 |
| [ISic001573](https://sicily.classics.ox.ac.uk/inscription/ISic001573) | `-0500`…`-0451` | Second quarter 5th century BCE | 284947 / 17053 | Whitish limestone/calcarenite | 3 |
| [ISic003031](https://sicily.classics.ox.ac.uk/inscription/ISic003031) | `-0500`…`-0401` | 5th century BCE | 645387 / 12336 | Text: volcanic rock; type: limestone (unreconciled) | 2 |
| [ISic003107](https://sicily.classics.ox.ac.uk/inscription/ISic003107) | `-0475`…`-0451` | c. 475—450 BCE | 284789 / 5422 | Text: sandstone; type: limestone (unreconciled) | 4 |
| [ISic003656](https://sicily.classics.ox.ac.uk/inscription/ISic003656) | `-0500`…`-0451` | early 5th century BCE | absent / AG S 156 | Limestone | 3 |
| [ISic003657](https://sicily.classics.ox.ac.uk/inscription/ISic003657) | `-0480`…`-0470` | c. 480—470 BCE | absent / absent | Unspecified stone | 9 |
| [ISic004487](https://sicily.classics.ox.ac.uk/inscription/ISic004487) | `-0500`…`-0475` | Early fifth century BCE | 492399 / 13316 | Compact white limestone | 2 |
| [ISic020600](https://sicily.classics.ox.ac.uk/inscription/ISic020600) | `-0500`…`-0451` | 500—450 BCE (so Arena on letters) | 696305 / 546.167 | Ceramic | 1 |
| [ISic030116](https://sicily.classics.ox.ac.uk/inscription/ISic030116) | `-0500`…`-0401` | 5th century BCE | 493029 / GR 1875.810.3 | Bronze | 1 |

The 57 markers include nonsequential `1a`/`4a`, `break="no"`, and a lost-line
marker `n="0"`. ISic003657 has nine separate textpart sections `a`–`i`, each with
`lb n="1"`, including sign encodings; its nine markers must not become nine
assumed full text lines. ISic003656's `n="0"` precedes an unknown lost-line gap.
ISic001432's single-line edition contains editorial supplied text. These are
concrete reasons to keep published editions separate from visible-only labels.

ISic001087, ISic001430 and ISic001469 have raw bounds`-0500`…`-0451` but human wording
`475—450 BCE`; neither field was silently preferred. Other records use broad raw
bounds with narrower wording. ISic003031/ISic003107 also have material-description
versus material-type differences. ISic003656/ISic003657 lack TM IDs; ISic003657
also lacks a holding inventory ID. ISic004487's raw TM has trailing whitespace,
preserved separately from normalized lookup text. These are concordance questions,
not corrections made by this intake.

## Manifest views and credits

Dimensions below are declared metadata, not decoded-pixel checks. “Regional
permission” abbreviates the recorded Sicilian-authorities wording and does not
assert permission granted to this project. The saved JSON retains exact strings
at manifest, canvas and image-body levels. Each manifest link is a checked primary
metadata endpoint; the item links above are source identifiers, not newly fetched
item pages.

| Item / checked manifest | Object-view canvas metadata | Auxiliary canvases | Credit/terms summary |
| --- | --- | ---: | --- |
| [ISic001087](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001087.json) | front 5520×3680; right 5520×3680 | 5 | J. Prag: front 23.05.2022; right 21.07.2017; regional-permission statement |
| [ISic001089](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001089.json) | front 5520×3680; right 5520×3680 | 9 | J. Prag 27-09-2021; regional-permission statement |
| [ISic001430](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001430.json) | front 5520×3680; right 5520×3680 | 5 | J. Prag 27-09-2021; regional-permission statement |
| [ISic001432](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001432.json) | front 4080×3072 | 0 | J. Prag 2023-07-08; regional-permission statement |
| [ISic001439](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001439.json) | front 1314×1059 | 1 | Paolo Orsi museum negative 19982 (1965); regional authorization 10681, 06/05/2014 |
| [ISic001469](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001469.json) | front 5520×3680 | 10 | Regional-permission statement; no named photographer in checked image-body credit |
| [ISic001470](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001470.json) | front 5520×3680 | 8 | Regional-permission statement; no named photographer in checked image-body credit |
| [ISic001573](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic001573.json) | front 5520×3680 | 9 | V. Mignosa 13.01.2022; regional-permission statement |
| [ISic003031](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic003031.json) | front 5520×3680 | 0 | Regional-permission statement; no named photographer in checked image-body credit |
| [ISic003107](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic003107.json) | front 4032×3024 | 0 | M. Metcalfe 2022-10-30 |
| [ISic003656](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic003656.json) | front 5520×3680 | 0 | J. Prag; courtesy Pietro Griffo museum, Agrigento |
| [ISic003657](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic003657.json) | front 831×512 | 0 | Marconi 1926, fig. 27; regional-permission statement |
| [ISic004487](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic004487.json) | front 6299×5615; rear 6299×5615 | 13 | Museo archeologico regionale di Siracusa; regional-permission statement |
| [ISic020600](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic020600.json) | front 5520×3680 | 0 | Regional-permission statement; no named photographer in checked image-body credit |
| [ISic030116](https://iiif.csad.ox.ac.uk/manifests/ISicily/ISic030116.json) | front 7360×4912 | 0 | Trustees of the British Museum; image 256673001; CC BY-NC-SA 4.0 stated in attribution |

The frozen XML has 46 active graphic references (23 tiled/print view pairs).
Four additional tiled views do not appear in these live manifests: the adjusted
view for ISic001089, detail for ISic001432, and left/right for ISic003656. Their
active XML references remain in the handoff, but no image URL was fetched or
availability presumed. Conversely, the 60 petrographic canvases must not be added
to inscription-face geometry or counted as extra inscriptions.

## Photograph permissions and metadata priority

I.Sicily's primary [reuse guidance](https://kingsdigitallab.github.io/corpus-building/about)
distinguishes attributed IIIF embedding from downloads and other reuse, for which
it directs users to the relevant Sicilian authorities. Its edition-data openness
therefore does not clear photograph downloads, derivatives, training or corpus
redistribution. Credits and historical authorization numbers are evidence to
retain when defining a proposed use; they are not downstream grants inferred here.

ISic030116 is a specific exception to “no explicit photo terms found”: its
manifest attribution identifies British Museum image 256673001 as CC BY-NC-SA 4.0.
The primary [image-specific museum page](https://www.britishmuseum.org/collection/image/256673001)
corroborates noncommercial sharing under that license and treats commercial
licensing separately. Its edition is CC BY 4.0. Preserve both scopes and the
Trustees attribution; do not turn the photo into CC BY data or assume the intended
open distribution/training use satisfies NC/SA. The web tool's museum-page text
was cached; the manifest is a fresh dated response. Exact derivative identity,
intended-use compatibility and any additional source restrictions remain for
review. No image was downloaded to compare the two assets.

For **metadata/permission review only**, ISic001432 is a manageable first lead:
one declared front canvas at 4080×3072, one primary line marker, a TM and an
inventory identifier at Parco Archeologico di Himera e Antiquarium. Its missing
manifest detail view and supplied text must remain explicit questions. This
priority is based on limited metadata complexity, not readability or label quality.
ISic001439 remains a useful second lead: three markers, a named museum negative
and a dated authorization reference, but a smaller 1314×1059 front and no downstream
reuse decision. ISic030116 merits a separate license-scope review because its
photo terms are identifiable but restricted; that does not make it the preferred
generally redistributable pilot.

Before any new image intake, establish the exact photograph/view/derivative and
permitted uses, retaining credits and conditions. Then a Greek epigraphist must
confirm period/language/register and physical-unit identity; map visible text and
signs to image coordinates; distinguish supplied, uncertain and lost material;
and resolve multiline/face/section mappings. Keep linked views and derivatives
of each object together for exposure/split custody. None of those scholarly or
permission gates is closed by the 15-record shortlist.

## Reproduction and input preservation

Local evidence is in `.artifacts/classical-inscription-shortlist-2026-11-05/`:
`registration.json` and `registered-before-fetch.json`;15 exact manifest JSON,
header and stderr receipts;`fetch-receipts.json`;`selected-source-records.json`;
`handoff.json`;`handoff.csv`;`summary.json`; extraction/fetch/report scripts;
`primary-web-source-notes.json`;`input-preservation.json`; and
`receipt-sha256.json`. JSON retains per-record active source graphics and complete
manifest body/service/canvas IDs without requesting those assets. The CSV is an
index; JSON and original XML preserve full nested semantics.

The registered archive remains SHA-256
`cdb72eb024c9f3705e4290205488d93eb3a4bdfcceada29dca934cff30b8c681`.
Final rehash confirms all 18 registered inputs unchanged (archive, inventory,
selector script and 15 XML files). A separate final rehash also confirms all 5,136
XML files still match the original inventory. No shared plan, training artifact, source XML,
archive or production source was edited. This report advances an inspection queue,
not a training-ready corpus or scientific acceptance claim.
