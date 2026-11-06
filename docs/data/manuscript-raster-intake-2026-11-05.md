# M02b/c manuscript raster intake — 2026-11-05

Both complete publisher archives were acquired into isolated research audit storage
and verified against their fresh Zenodo API records. This closes the mechanical
raster-integrity gap from the PAGE-only intake. It does not clear upstream photo
rights, approve source transcriptions, assign a split or establish Classical/Koine
coverage. No model was loaded or run and no training occurred.

## Exact source and integrity

Fresh metadata identifies both deposits as version1.0 with record-level
`cc-by-4.0`. The records retain creator attribution to Ioannis Pratikakis,
Aleksandros Papazoglou, Symeon Symeonidis and Lazaros Tsochatzidis. The archive size
and publisher MD5 match the earlier PAGE-only receipt. Every non-directory ZIP
member was read completely with CRC validation, and a SHA256 inventory was saved.
Extraction rejects absolute/traversing/duplicate/symlink/encrypted entries and
bounds member, aggregate and image sizes. Only raster audit copies are extracted.

| Source | Archive bytes / validated members | Exact PAGE–PNG pairs | Lines | Actual pixel dimensions |
| --- | --- | ---: | ---: | --- |
| [M02b: Stavronikita79](https://zenodo.org/records/5578136) | 227,109,926 /83 |40 |803 | Width1495–1767, height1806–1871 |
| [M02c: Stavronikita114](https://zenodo.org/records/5578251) | 241,138,300 /91 |44 |1,006 | Width1464–1515, height1831–1864 |

All84 PNGs pass header verification and full decode. Each PAGE image filename joins
one distinct raster and every decoded width/height exactly matches its PAGE
record. Source XML/Unicode and polygon coordinates were not rewritten. There are
no missing or additional raster joins. The1,006 M02c line count remains different
from the published1,051; raster verification does not explain that discrepancy.

| Archive | Publisher MD5 | Complete archive SHA256 |
| --- | --- | --- |
| M02b | `7f283a2271aa63ed94a48666321c8020` | `32629cfa34293d292839037cd7446f834db08f6b9f72db421fe3d6c5149fd997` |
| M02c | `b5038bc798a6907313bf4f9b01503bb8` | `1f5c712a4e195be3354429d040ce83d7b8206a87d0253cd12d56edbde162feb1` |

## Duplicate and rights boundaries

Exact decoded RGB pixel hashes, qualified by dimensions, show zero duplicate
groups across all84 new rasters and the174 existing M01/M02a source rasters.
This rules out only exact normalized pixel duplicates in those258 local files.
It does not identify cropped/recompressed/rescaled copies, alternate lighting,
recto/verso, joined fragments, repeated passages, shared hands or common physical
objects. Whole witnesses remain grouped; no new train/dev/confirmation assignment
was made. The earlier source split lists mix pages and leaves and are not adopted.

Neither full archive contains a README/license/notice/copyright/credit file;
examined PNG textual metadata is empty. The fresh deposit description supplies no
separate photograph grant or source imaging credit. Keep the publisher's record-
level CC BY4.0 evidence separate from unresolved upstream item/photo permissions.
The [authors' article](https://pmc.ncbi.nlm.nih.gov/articles/PMC8704904/) provides
collection descriptions and deposit references, but no separate imaging permission
was found in its acknowledgment/availability sections. This audit grants no new
rights and authorizes no redistribution or derivative release.

## Reproduction and next handoff

Evidence is under `.artifacts/manuscript-raster-intake-2026-11-05/`: registration,
fresh API JSON, complete original ZIPs, acquisition receipts, safe acquisition and
audit scripts, per-member hashes, decoded dimensions/pixel hashes and the report.
`receipt.sha256` pins registration, scripts and outputs. Original PAGE-only
receipts and existing M01/M02a datasets remain unchanged. Reproduction requires a
fresh owned audit output directory; the extractor refuses existing raster folders.

MAN-01 now needs upstream photo provenance, M01's catalog/folio/hand concordance,
visible abbreviation/mark/spacing review and eventual accepted physical-unit
holdouts. M02b/c provide1,809 mechanical source line pairs, **zero accepted labels**.
Scholarly review must check actual visible ink and geometry; a correct file join
and checksum are insufficient.
