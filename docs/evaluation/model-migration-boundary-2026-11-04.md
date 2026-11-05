# Recognition model migration boundary

LeviathanText loads its own versioned C# recognition packages. A Kraken
`.mlmodel` or `.safetensors` file, an ABBYY proprietary checkpoint, and a
weight file without a supported architecture are inventory entries. Keep
their original bytes, source path, size, digest, and any separate architecture,
training-data, redistribution, or usage terms in the source register. Never
rename a checkpoint into the native package directory as a conversion.

## Migration path

1. Inventory and preserve the original collection and exports. Prefer PAGE,
   ALTO, hOCR or bounded TEI/EpiDoc with original raster coordinates. Import
   into a disposable project first. Verify raster identity and report any
   metadata, markup, geometry or reading-order losses. [Reference rehearsal](reference-migration-rehearsal-2026-11-04.md).
2. Pair each allowed image with an independently reviewed visible line and
   its diplomatic profile. Record exposure and assign physical witnesses
   before augmentation. Keep editorial expansions and historic OCR output in
   separately labeled layers. Preserve the supplier's annotation grant and
   image terms separately.
3. After explicit training authorization, freeze the reviewed dataset,
   preprocessing, alphabet, seeds and experiment budget. Use the native C#
   `train lines` workflow for compatible PAGE crops, or a reviewed specialized
   crop loader for profiles that require external geometry. The retained
   validation contract explains how the format 1 and format 2 loaders differ.
   [Operations](../operations.md) · [Preprocessing contract](preprocessing-contract.md).
4. Verify a candidate package by loading it with the native runtime and
   reproducing the frozen development and acceptance evidence. The manifest
   must bind the model identity, exact weights/labels, architecture, recipe,
   metrics and license. Keep acceptance labels with the evaluator and deliver
   the candidate only when the rights and per-material gates are satisfied.

## External comparison record

The local reference checkout inventory contains 13 weight files. Their
presence alone does not establish Greek hand/material coverage or a matched
comparator. This host did not resolve installed `kraken` or `ketos`
commands on November 4. Keep the external comparator gap open for the
intended Greek sample. A future authorized comparison should record the exact
external binary and model digests, model terms and supported scripts, image
hashes, preprocessing, output and errors, then compare raw text, geometry,
reading order, latency and manual correction effort on matched inputs. Keep
external runtime dependencies outside the LeviathanText application.
