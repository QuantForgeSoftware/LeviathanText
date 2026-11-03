# NTVMR pilot candidate investigation

Checked **2026-11-02**. Initial scope: source discovery and rights review for [project-plan.md §12](../../project-plan.md#12-ntvmr--csntm-manuscript-pairing-pilot--2026-11-02). Initial discovery inspected metadata/transcriptions and image headers. **Follow-up authorized by the owner:** P46 Michigan is selected; exact item manifests and visual comparisons are now recorded in [the three-page review](p46-michigan-page-review.md). That follow-up retained three official review renderings and source evidence, with no application code, training or institutional contact.

## Recommendation

**Follow-up status:** item-level Michigan rights are now confirmed through official APIS IIIF manifests; preliminary visual page correspondence passes for all three selected pages. Metadata side/range/count discrepancies and supplied-text/correction hazards are recorded in the page review. Human adjudication, usable line geometry and final training labels remain open. Historical discovery limitations below describe what was known before that follow-up, not the current item-rights status.

**Use P46's University of Michigan portion first**, shelfmark **P.Mich.inv. 6238**, NTVMR **docID 10046**. Start visual inspection with **pageIDs 1440, 1450 and 1460**. Their public image endpoints returned HTTP 200 with `image/jpeg`; each actual TEI response contains manuscript-specific Greek text, matching folio markers and an explicit **CC BY 4.0** availability statement. Michigan's own papyrology-image policy declares those collection images **public domain**, with institutional attribution. This is the best combination of verified access and reusable-source terms found in this search.

This is a recommendation for the first page/label review, not an assertion that these are the least damaged pages or already valid training examples. The material is papyrus, so it advances both the Koine manuscript and papyrus tracks, but does not establish coverage of later minuscule books. A reviewer must still compare each image with its transcript, identify visible versus supplied readings and approve usable line geometry.

Keep the **Michigan image set separate from Chester Beatty's portion of the same witness**. Rights and image availability differ by capture; the manuscript number alone cannot select an image license. Do not substitute a CSNTM photograph without separately recording its provenance and applicable terms.

## Verified starting pages

All identifiers/content below come from the same live NTVMR manuscript metadata response. Actual TEI was retrieved individually for all three pages.

| NTVMR pageID | TEI folio marker | Indexed content | Public image filename | Availability and text rights |
| --- | --- | --- | --- | --- |
| **1440** | `081v` | Galatians 1:10–22 | `10046x6238_159JP2_UMich.jpg` | Image HEAD 200, JPEG; TEI contains Greek text and CC BY 4.0 |
| **1450** | `082r` | Galatians 1:23–24; 2:1–9 | `10046x6238_160JP2_UMich.jpg` | Image HEAD 200, JPEG; TEI contains Greek text and CC BY 4.0 |
| **1460** | `082v` | Galatians 2:12–21 | `10046x6238_161JP2_UMich.jpg` | Image HEAD 200, JPEG; TEI contains Greek text and CC BY 4.0 |

Direct source links:

- [P46 metadata, including page/image/transcript associations](https://ntvmr.uni-muenster.de/community/vmr/api/metadata/manuscript/get/?docID=10046&detail=10&format=json).
- Page 1440: [published TEI](https://ntvmr.uni-muenster.de/community/vmr/api/transcript/get/?docID=10046&pageID=1440&format=teiraw), [image](https://ntmss.info/images/webfriendly/10046/10046x6238_159JP2_UMich.jpg).
- Page 1450: [published TEI](https://ntvmr.uni-muenster.de/community/vmr/api/transcript/get/?docID=10046&pageID=1450&format=teiraw), [image](https://ntmss.info/images/webfriendly/10046/10046x6238_160JP2_UMich.jpg).
- Page 1460: [published TEI](https://ntvmr.uni-muenster.de/community/vmr/api/transcript/get/?docID=10046&pageID=1460&format=teiraw), [image](https://ntmss.info/images/webfriendly/10046/10046x6238_161JP2_UMich.jpg).
- [University of Michigan institutional P46 introduction](https://apps.lib.umich.edu/files/collections/papyrus/k12/reading/Paul/index.html).

Do not confuse NTVMR pageIDs, folio labels and image suffixes: `1440`, `081v` and `159` identify this page in different systems. The missing passage ranges in an index are not permission to restore missing letters into visual labels.

## Permissions evidence and limits

### P46 Michigan images

[University of Michigan, Guidance for Using Reproductions](https://lib.umich.edu/find-borrow-request/access-special-collections-materials/guidance-using-reproductions/), under papyri, explicitly describes papyrology collection images as free/public domain with attribution to the University of Michigan Library. This is an image-specific collection statement, not an inference from the age of the manuscript. The separate procedure for assigning publication of papyrus texts is not itself an image copyright license.

The selected NTVMR records identify Michigan as the image source, link its institution and use matching `6238`/`UMich` filenames. This supplies provenance at source-family level. The individual Michigan APIS pages could not be inspected through the web reader (403/inaccessible); therefore full item citation details and any exceptional item notice remain a final intake check. Do not label this as an independently verified item-level APIS rights statement. Public-domain status is not the same as an explicit CC0 dedication.

### P46 transcriptions

Each selected TEI response has an availability block naming **Institut für Neutestamentliche Textforschung**, copyright 2026, and a link to `https://creativecommons.org/licenses/by/4.0/`. Record **CC BY 4.0** for these exact published text responses, retain attribution, source URL, retrieval/version information and modification notices. This finding is not a license grant for every manuscript transcription hosted in NTVMR.

The [NTVMR agreement](https://ntvmr.uni-muenster.de/license-agreement) separately explains that images come from different owners and external use requires proper holding-institution permission. For Michigan's own identified papyrus images, retain its public-domain policy as that source-specific evidence; do not treat general NTVMR viewing access as the grant. No blanket redistribution/model-weight license is inferred from software licensing or from an API returning successfully.

### Other institutional/edition evidence

- [Chester Beatty copyright policy](https://chesterbeatty.ie/about/copyright-2/) publishes a CC BY 4.0 default for museum-object images unless otherwise listed. This does not establish exceptions or availability for each older photograph copied into NTVMR. The policy was readable through the web search cache; direct web opening returned 403. Do not rely on older descriptions calling the entire collection NC, or silently assume the current default resolves every capture.
- [Cambridge's Bezae IIIF manifest](https://cudl.lib.cam.ac.uk/iiif/MS-NN-00002-00041) explicitly licenses images/metadata **CC BY-NC 4.0**. The separate [Birmingham Greek XML edition](https://epapers.bham.ac.uk/id/eprint/2167/) is **CC BY-NC-SA 3.0**; the NTVMR rendered page 140 instead displays CC BY 4.0. Keep these distributions/versions distinct rather than relabeling one from the other's notice. Cambridge's image NC condition alone prevents default broadly reusable inclusion.
- Sinaiticus's [official transcription license](https://raw.githubusercontent.com/itsee-birmingham/codex-sinaiticus/main/license.md) is **CC BY-NC-SA 3.0**; its [image policy](https://codexsinaiticus.org/en/copyright.aspx) limits use and requires appropriate institution permission outside the permitted scope. Do not generalize NTVMR's own contribution policy to this separate edition.
- Washingtonianus's [Smithsonian modern-image record](https://asia-archive.si.edu/object/F1906.274/) says **Usage Conditions Apply**. Generic hidden CC0 explanatory text on that page does not license this asset. The [1912 facsimile scan](https://library.si.edu/digital-library/book/facsimileofwashi02sand) is a separate CC0/public-domain source, requiring a new plate/page mapping; it does not clear modern photographs. See [Smithsonian's asset-specific open-access guidance](https://www.si.edu/openaccess/faq).

## Shortlist and reasons for ranking

Counts are computed from anonymous NTVMR metadata, not manually verified usable pairs. “Public flag” means `accessLevel=0`, no protected flag/placeholder and a transcription entry on the same record. It does not prove that the referenced image is reachable, correctly matched or licensed.

| Candidate | Same-record image/transcript evidence | Rights/access result | Decision |
| --- | --- | --- | --- |
| **P46 / Michigan**, `10046`, P.Mich.inv. 6238 | 60 public-flagged records with `UMich` image references. Three actual page transcripts and JPEG endpoints checked above. | Institutional papyrus-image policy: public domain; selected TEI: CC BY 4.0. Final item citation/exception inspection pending. | **First choice.** Expand to 10–20 reviewed pages only after the first three pass visible-content and rights-record checks. |
| **Washingtonianus / GA032**, `20032`, F1906.274 | 374 page records; 289 public-flagged overlaps. Page 10 actual Greek TEI; image `F1906.274.001.jpg` HEAD 200/JPEG. | Modern Smithsonian photographs: usage conditions, not demonstrated CC0. Rendered NTVMR page 10 says CC BY 4.0, while its older TEI header reserves INTF rights; retain both representations and resolve if using that export. | **Second technical candidate, conditional image rights.** Favorable one-column book format. An open facsimile route is a different capture/matching task. |
| **Bezae / GA05**, `20005`, Cambridge Nn.2.41 | 856 page records; 417 public-flagged overlaps. Examples 80/100/120/140 map Greek text pages to Cambridge IIIF; rendered page 140 has actual text. | Cambridge image grant CC BY-NC 4.0; text versions differ as above. This investigation did not fetch the individual JPEG endpoints. | **Conditional noncommercial comparison**, not default open training/distribution. Greek/Latin alternation adds selection work. |
| **Sinaiticus / GA01**, `20001`, BL Add.43725 in this listing | 296 page records; 267 public-flagged overlaps. Examples 40/42/44 carry Matthew text and image references. Not the whole dispersed codex. | Selected image metadata reserves BL rights; official project has restrictive image terms and NC-SA transcription edition. No per-page image endpoint tested here. | **Later scholarly test**, not the first open pilot; multi-column layout and corrections increase effort. |
| **Alexandrinus / GA02**, `20002` | 294 page records; 261 transcript flags, but only three public-flagged overlaps (121/490/911). Page 90 has text but a protected image placeholder/access level 1. | No anonymous 10–20-page image/transcript set verified; external BL holdings rights/access not cleared by this metadata. | **Defer.** The manuscript's fame or a transcript entry does not establish accessible paired images. |
| **GA2756**, `32756`, Münster Bibelmuseum Ms.10 | 394 page records; only two public-flagged transcript records, 140 and 3080. Page 140 rendered text and JPEG endpoint verified; second record unverified. | Page 140 text displays CC BY 4.0; image attribution names Bibelmuseum with no reuse license verified. | **Small minuscule smoke test only**, conditional image permission; insufficient demonstrated coverage for 10–20 pages. |
| **P46 / Chester Beatty**, also `10046`, CBL BP II | Metadata references public images and text, including pageIDs 10/20/50. Actual TEI succeeds. | All three selected legacy image URLs (`T0005249-2.jpg`, `T0005250-1.jpg`, `T0004420.jpg` under `/images/full/10046/`) returned **404 to HEAD**. Current institutional image default is CC BY 4.0, but selected capture exceptions unverified. | **Fallback after link repair/authoritative source resolution.** Do not count its metadata flags as working pairs. |

Metadata sources for the remaining witnesses use [the live endpoint](https://ntvmr.uni-muenster.de/community/vmr/api/metadata/manuscript/get/?docID=20032&detail=10&format=json), substituting the documented `docID`. Counts are a snapshot and can change. Cover pages and untranscribed surfaces are not included as pairs merely because a witness has transcriptions somewhere.

## Access findings relevant to implementation

- `/community/api/` exposes documentation, but some query URLs returned empty bodies or were inaccessible to the web reader. `/community/vmr/api/` returned the inspected live metadata/TEI. Do not implement a connector around a successful HTTP status alone: require the expected nonempty data/schema and report route/service failures.
- Some metadata transcription URLs expose `http://localhost:8080/...`; those are not usable client URLs. Build the documented public read endpoint from the verified `docID` and `pageID` instead.
- `getCopyright=true` returned empty bodies for the tested witnesses. Empty means no usable response, not public domain or a permission denial. The actual selected TEI/HTML notices supplied the license evidence.
- Metadata `accessLevel=0` is an access flag, not a license. The P46 Chester Beatty 404 checks demonstrate why link reachability must be tested separately.
- A reachable JPEG was not visually compared against text in this research step. No actual image dimensions, crop safety, readable-line count or recognition quality is claimed. TEI line-break/word tags are not adjudicated training-label counts.

## Next intake actions and remaining gate

1. Record the owner's selection of **P46 Michigan** and appoint a Greek reviewer/evaluator; NT-01/G0 remains partial until then.
2. Preserve the relevant institutional rights notices and inspect individual APIS citation/exception metadata when accessible. Treat a contrary item-level statement as a blocker for that asset, not a reason to discard the entire witness.
3. Visually compare pageIDs 1440/1450/1460 with their TEI; check folio/passage identity, damage, supplied text, corrections, abbreviations and capture orientation. These are proposed starting pages, not expert-approved examples.
4. If they pass, choose 10–20 Michigan pages from the same metadata subset and record each image's own lineage/rights. Keep all views of a page, both sides of a physical leaf, and all derived crops together as required by the evaluation design.
5. Prepare a reviewed 50–100-line subset with source coordinates and explicit label status. Do not start training or claim line pairing from page correspondence alone.

## Reproducibility record

The six live JSON metadata responses were saved temporarily for inspection; their SHA-256 hashes identify the observations below. They are not committed as a dataset. Candidate TEI responses and image HEAD headers were also inspected locally; no photographs or weights were saved.

| docID | Metadata SHA-256 |
| --- | --- |
| 10046 | `1bd6e01ac37deb4411d580e53d0896a20cb2c541e2962e1d425334e28e3ffb90` |
| 20032 | `748809fc818dabadfec56a69953b2d0456d228aeb043403ec0e6232d509ec5f8` |
| 20002 | `195379400183d3240880bf53bf765b2bcacfd911333cfa05bf2dbb5dbf5afb33` |
| 20005 | `9eeba12946113de8e4114a38e393941b8e9fb2d5d66f90077d50b6bd3ba37f67` |
| 20001 | `341f50dda672067416dfbceddc5e1fec4f5a9cef009983fd1dedd94ed2530645` |
| 32756 | `db4b0727193d386b471f6bc8e88227474abdb0a5bd6b0431911345757ddb771e` |

Review: independent research checked manuscript-specific versus generic grants and found important exceptions: Smithsonian generic CC0 UI text does not clear Washingtonianus photos; Bezae's Cambridge image terms are NC; a blanket CC BY statement cannot relicense an older external edition. Live checks then separated P46's reachable Michigan images from broken Chester Beatty links. Independent final review found the recommendation supported within the stated collection-level versus item-level limits. G0 access/rights discovery is documented; owner selection, complete asset-level intake and scholarly inspection remain open.
