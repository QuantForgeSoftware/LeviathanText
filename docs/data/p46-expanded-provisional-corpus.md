# P46 expanded provisional training corpus — 2026-11-02 evening

The renewed two-hour session authorizes a separately versioned **provisional engineering experiment**. Its new corpus contains **27 source-reference lines / 926 reference scalars** from six Michigan surfaces, proposed leaves 83r/v–85r/v. Added to the unchanged 17 earlier training lines, that is **44 training lines / 1,504 scalars** on proposed physical leaves 82–85. The seven original development lines on page 1440 / leaf 81 stay unchanged. The earlier metadata-only leaves 78–80 were not opened.

This does **not** publish `P46-reviewed-v1`, complete NT-15, accept visible readings or establish broader Greek accuracy. Every accepted `visible_text` and reviewer field remains empty, and every row has `reviewedTrainingEligible=false`. The 27 allowed IDs have only `provisionalTrainingCandidate=true`, for the owner's renewed provisional study. The original source-review staging package remains unchanged and unassigned; this new fork records the provisional training designation explicitly.

## Frozen deliverable and interface

Corpus root: `.artifacts/datasets/ntvmr-pilot/expansion-provisional-2026-11-02-evening/`. Version: **`expansion-provisional-evening-v1`**, frozen before training. The producer scripts refuse to alter a folder containing `FREEZE.json`; future corrections require a new version.

| File | Role |
| --- | --- |
| `FREEZE.json` | Status, counts, source grouping and SHA-256/size for all 132 frozen content files |
| `line-ledger.json` | All 162 source rows, flags, exclusions, provisional references, source/crop hashes, geometry and pending review status |
| `train-allowlist.json` | Explicit object with `ids`, count and provisional-only use/profile/exposure statement |
| `lines/<id>.png` | Authoritative raw RGB geometric derivative for an allowed candidate |
| `source-maps/<id>.u16le` | Exact source-Y lookup for every output pixel; source-X is 480 + output column |
| `source-raster-receipt.json` | Source JPEG and decoded-RGB digests, dimensions, orientation and decoder context |
| `pages/`, `sources/` | Unchanged copies of exact official JPEGs, TEIs, item manifests and original source evidence |
| `index.html`, candidate sheets, `context/`, `overlays/` | Static offline review views; no server, script or online dependency |
| `SOURCE-ATTRIBUTION.md` | Accurate item rights, attribution and image/label modification record |

For a training loader, intersect this allowlist with [the independent QA allowlist](../evaluation/p46-expanded-corpus-review.md#provisional-only-allowlist), then read matching ledger entries. Fields include `id`, `page`, `split`, `text`, `cropPath`, `cropSha256` and `provisionalTrainingCandidate`. `cropPath` is relative to this corpus root; `cropRepositoryPath` is relative to the repository. `text` is a provisional **source-reference target**, not accepted diplomatic text. Source files or crops merely present in a folder do not imply eligibility.

`sourceImagePath` and `sourceTeiPath` are repository-relative provenance pointers to the unchanged staged inputs. The corpus also carries byte-identical copies as `pages/<page>.jpg` and `sources/ntvmr-10046-<page>.tei.xml`, so an extracted review copy can use those local files without following the original repository pointers.

| Page | Allowed provisional row suffixes | Count |
| --- | --- | ---: |
| 1470 | L03, L05, L07, L12, L13, L14, L15, L16 | 8 |
| 1480 | L09, L13, L14, L15 | 4 |
| 1490 | L01, L06, L14, L18 | 4 |
| 1500 | L05, L07, L08, L09, L14, L15 | 6 |
| 1510 | L09, L10, L14 | 3 |
| 1520 | L02, L03 | 2 |

Full IDs are `P46-<page>-L<row>`. All six new surfaces are designated training-only for this experiment, keeping proposed sibling surfaces together. There are no new development or acceptance assignments.

## Source selection and image association

Independently parse the original TEI boundaries and inspect images. Mark **both sides** of a continued word, `w@part`, unclear/supplied text, apparatus, abbreviations/overbars, gaps and other unsupported markup. No line break occurs inside an apparatus branch in these six files. This leaves 28 plain-reference rows; 134 rows have a source hazard. One additional plain row, **1500 L13**, is conservatively excluded because a photographic hole may intersect upper strokes. The final count is 27 candidates / 135 exclusions, with original sources and reasons retained. No familiar edition supplies or corrects a target.

Image geometry is derived independently of those counts. Central ink projections in manually inspected body windows produce 27/27/27/26/25/26 candidate peaks, compared with 27/27/27/28/26/27 source TEI rows. Those are **projection detections, not an accepted census of physical rows**: lower remnants can be absent from a central stripe. Do not shift upper rows or fabricate extra geometry to force agreement. Selected rows are all at or above L18 and are individually compared with their source references and wider image context.

The [independent AI source/image QA](../evaluation/p46-expanded-corpus-review.md) inspected all six full pages, all 27 candidate crops, source text, terminal extents and wider context. It requested a wider final right bound than an initial detection window and supported the 1500 L13 exclusion. It found no remaining obvious row mismatch or crop truncation in the final subset. This is **engineering QA**, not Greek-specialist or independent-human adjudication. Generic Recto/Verso caption conflicts and proposed folio identities remain documented rather than silently resolved into scholarly facts.

## Exact geometry and pixels

Each candidate PNG is **1,695 × 64 RGB pixels**, drawn from the retained width-2200 image frame, source X **480 through 2174 inclusive**. This matches the previous provisional crop width/height; the shared C# luminance recipe at height 16 yields 424 × 16 tensors. Pixel preparation belongs to the experiment's declared C# profile; this corpus contains raw RGB crops, not pre-normalized model tensors.

Per-line processing uses image-detected central row centers, 100-pixel horizontal windows, luminance cutoff 0.22, vertical search ±31 pixels and bounded centroid adjustment ±20 pixels. Interpolate anchors and copy nearest source pixels by vertical column translation. There is no sharpening, binarization, restoration or generative enhancement.

`sourceQuad` is the **initial flat search prior**, not the final pixel polygon or an accepted annotation. The authoritative transform is `sourceMapFile`: a row-major 64 × 1695 array of **little-endian unsigned 16-bit source-Y indices**, with source-X = 480 + output column. This retains exact sampling despite nearest-neighbor rounding details. Every output RGB pixel in every crop has been checked against the recorded source raster and map. Original JPEGs are unchanged; decoded raster hashes and **Pillow 12.2.0 / libjpeg-turbo 3.1.4.1** context record the source RGB interpretation. No EXIF transpose is applied, and the original JPEGs declare no orientation value.

## Reference labels, coverage and limitations

Labels remove editorial whitespace and use **NFD**, retaining source lowercase spelling and distinct conventional **σ/ς**. They do not claim literal modern lowercase glyphs, sigma-shape adjudication, word division or overline coverage. No restored, abbreviated, apparatus-ambiguous or continued-word target is included. Accepted visible text remains empty throughout.

The new lines provide 24 distinct reference labels; combined with the old training set, the alphabet remains 25 Greek labels plus CTC blank. Descriptive occurrence counts improve γ from 6 to 35, ξ from 2 to 5, χ from 3 to 7 and φ from 3 to 6. **ψ remains represented only once** in the old training set. These are reference counts, not adjudicated sign coverage. No additional polytonic or visible-mark support is established.

This expands training characters by approximately **2.60×** and proposed physical training leaves from one to four. It remains a tiny, source-exposed sample of one Koine witness and likely one hand. No adequate coverage of page 1440's faint conditions, unseen witnesses/hands, Classical Greek or inscriptions is established. The source-to-row mapping, uncertain traces, target state, crop geometry and all relevant scholarly distinctions still require qualified human review. Development selection remains exposed; no blind acceptance conclusion is available.

## Rights, checks and receipts

The exact institutional item manifests for **APIS3615–3620**, captures `6238_162.TIF`–`6238_167.TIF`, identify their material as public domain / **PDM1.0**. Preserve **University of Michigan Library Digital Collections**, P.Mich.inv.6238 attribution. PDM is a copyright-status mark, not CC0. This does not extend rights to other institutions' captures.

Each exact NTVMR10046/page1470–1520 TEI retains **Institut für Neutestamentliche Textforschung, ©2026**, with its linked **CC BY4.0** notice. Original notices, exact URLs/hashes and the transformation record remain in the new corpus. The source image and TEI copies are byte-identical to the staged evidence. Model-weight redistribution terms are not assigned by this preparation.

Checks verify all original staging content against its manifest, all new content hashes, unique 162 row IDs, 27 explicit train IDs, empty accepted/reviewer fields, flags and exclusions, source/crop/map digests, and every pixel in all 27 crops. Independent review also verifies exact label agreement with the plain TEI segments and equality of the two 27-ID allowlists. Neither instance claims expert acceptance.

| Receipt | SHA-256 |
| --- | --- |
| `FREEZE.json` | `7be31665294845287b7c3b5561308302599796722e32ea89cc1531952b058e1b` |
| `line-ledger.json` | `25371c738192344de80aa8e5004f98c516300595ccde3c03965bc6aa02332380` |
| `train-allowlist.json` | `aba74900fd6ae8b5ad891a2b82ab8e5ea16b51bf7dfd3b1630bb2d577c249c0d` |
| Independent `provisional-qa-allowlist.json` | `999cdba92ef727a971d3db77133a5b138dfcfee230c8e789f5986fbaae5faa48` |

The corpus is frozen before any training outcome is consumed. Correct it through a new version if a later reviewer finds a mismatch. The integrator owns experiment registration, any baseline comparison, `project-plan.md`/`todo.md` status and checkpoint decisions. This preparation writes no production code, fits no model, modifies no earlier data/receipt and contacts no institution or reviewer.
