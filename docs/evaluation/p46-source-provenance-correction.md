# P46 provisional package provenance correction — 2026-11-02

This note corrects a copied description in the preserved provisional P46 training packages. It does not change source rights, images, labels, checkpoints or metrics. Preserve it alongside any research-package transfer. No model redistribution permission is assigned here.

## Affected description and actual image transformations

`SOURCE-ATTRIBUTION.md` in the v1/v2 and subsequent study packages was copied from the initial specialist packet. Its statement that the crops are “unaltered rectangular selections” does **not** describe the actual training inputs. The packet's source-context crops and the later rectified training crops are distinct derivatives.

The 24 selected training/development rasters are derived from the official Michigan **width-2200 JPEG review images**, APIS3612/3613/3614, associated provisionally with NTVMR10046 pages1440/1450/1460. Actual preparation is:

1. Use the original packet's proposed source-frame line slope and horizontal extent.
2. Refine line centers from ink centroids in 100-pixel windows, with luminance cutoff 0.22, prior window ±31 pixels and center adjustment bounded ±20 pixels; interpolate the retained center anchors.
3. Extract a **64-pixel-high rectified raster by nearest-neighbor vertical translation of individual columns** in that width-2200 coordinate frame. These are geometric derivatives, not untouched rectangles or native-resolution polygons. The line ledger retains source quad, refined center anchors, crop height, rectification description and crop hash.
4. Apply the versioned pixel recipe declared by the particular experiment: v2 ordinary grayscale at height32; the subsequent controls/matrix use their recorded height32/16 and raw/local-normalization profiles. T2 additionally applies bounded random gamma during training only. Keep that random training transform separate from deterministic inference preparation.

No generative restoration, sharpening or hallucinated strokes are introduced by this preparation. Geometry is an AI-reviewed **proposal**, with no independent specialist acceptance. Wider margins, clipped or damaged edges and source-to-line associations still require review. A package's `externally-prepared-line` profile executes the pixel transform after a supplied crop; generic page recognition does not automatically reproduce this rectification.

## Reference-label transformations

Targets are provisional source references from the published INTF TEI, not adjudicated visible diplomatic text. The subset excludes flagged supplied/unclear/apparatus/abbreviation/continued-word lines, uncertain terminal associations and four further lower-page lines rejected for clipping, tears or neighboring ink. These filters do not establish that remaining labels are correct.

The retained labels remove editorial whitespace, normalize to **NFD**, retain the source's lowercase spelling and keep conventional **σ and ς distinct**. Those encodings are not a claim that the papyrus visibly uses modern lowercase forms, editorial word boundaries or those sigma shapes. No source restoration or abbreviation expansion is inserted into a target. The plain subset does not encode overbars, historical correction layers or general polytonic coverage. All 81 specialist decisions remain pending.

## Rights and preserved records

Images retain attribution to **University of Michigan Library Digital Collections**, P.Mich.inv.6238. The exact institutional item manifests identify their material as public domain / PDM1.0; PDM is a status mark, not CC0. The source TEIs retain **Institut für Neutestamentliche Textforschung, ©2026**, and the linked **CC BY4.0** notice. The derived training rasters and labels must be described with the transformations above when retained or shared.

The original copied attribution files are intentionally unchanged so frozen artifact receipts remain valid. The detailed [provisional training report](p46-provisional-training.md), [two-hour study report](p46-two-hour-study.md), v2 `line-ledger.json`, experiment receipts, revision-3 `protocol.json` and frozen source snapshots supply the actual preparation record. The old attribution description is superseded by this correction for training-input provenance, not silently edited into historical evidence. This correction does not affect the separate six-page expansion packet, whose source JPEGs are retained unchanged and which contains no training-line crops.
