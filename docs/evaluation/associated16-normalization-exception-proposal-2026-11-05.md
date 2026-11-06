# Associated 16-bit TIFF normalization exception proposal

Status: root approved this exact exception on2026-11-05 after observing strict failures; unseen holdout acceptance remains required. No production source changed. No fitting, corpus, labels, or model inference.

## Observation and timing

The completed v8 comparison still records 21 strict failures across three TIFF encodings: `tiff-u16-ch4-a1-bigtiff-mm.tif`, `tiff-u16-ch4-a1-planar.tif`, and `tiff-u16-ch4-a1-planar-lzw.tif`. This proposal is explicitly **after observing those differences**, not a preregistered success threshold. Original strict comparison files remain unchanged. All other TIFF lossless expectations stay byte-exact; no generic tolerance is proposed.

The independently parsed source samples are identical to the original little-endian contiguous `tiff-u16-ch4-alpha1.tif` in all three cases. `associated16-physical-equivalence.json` records exact source hashes and full canonical little-endian uint16 sample arrays. Parsing reads classic/BigTIFF endianness, numeric fields and contiguous/planar strips; the LZW case was independently converted to uncompressed with installed `tiffcp`, then parsed and compared. All three arrays match, with `ExtraSamples=1` and the same dimensions. No candidate decoder supplies this sample oracle.

For example pixel 13 has raw associated RGBA `(19506,24963,30419,32768)` in every encoding. Existing little-endian baseline/candidate canonical blue is235, but big-endian/planar old-baseline blue is237. Physical sample identity does not justify preserving encoding-dependent projection artifacts.

## Exact proposed projection

For **associated unsigned 16-bit RGB+alpha only**, independently normalize each raw associated color channel and alpha to byte with nearest-half-up integer arithmetic:

`byte = (sample * 255 + 32767) / 65535`

Write these bytes directly to a Skia `Bgra8888/Premul` bitmap, in BGRA order. Canonical comparison continues to use the actual pinned native Skia `GetPixel` RGBA projection; do not approximate its unpremultiplication with Python float/int arithmetic, and do not unassociate 16-bit channels before quantization. Derived crops and legacy in-budget resizes use the same pinned Skia operations as before. This is an explicit deterministic application projection, not a claim that TIFF specifies a particular 8-bit quantization rule.

For the three named old encodings, adopt the exact source-equivalent little-endian original raster/derived oracle. The current strict21 differences would be reclassified as explicit approved normalization corrections, while preserving the strict failure receipt. Unsigned16 unassociated, palette, gray, other depths, JPEG tolerances, source checksums, dimensions, and raw orientation rules do not change.

## Independent holdouts frozen before candidate outputs

Four new source-generated TIFFs form two physically identical LE/BE pairs,129x31, with varying alpha and samples around independent rounding boundaries. `associated16-unseen-source-oracles.json` records full raw sample arrays; `associated16-unseen-derived-oracles.json` records canonical Skia raster and all eight operation expectations. `associated16-projection-proposal-freeze.json` pins these and the oracle source before any holdout candidate execution.

Baseline-only execution demonstrates encoding dependence independently: both LE cases match the declared oracle exactly; BE pair0 differs2242 bytes, pair1 differs2243. All baseline processes exit0 and preserve source bytes. Candidate holdout outputs have not been observed when this proposal was written. Require all four candidate raster/crop/in-budget derived expectations to match exactly after approval; no adaptive tolerance if they fail. Future corrected budget operations need separately frozen approved integer-fit oracles before execution.

## Evidence paths

All artifacts are under `.artifacts/experiments/sec01-independent-v1/`: the physical-equivalence JSON, unseen source/derived oracles, proposal freeze, `runs/baseline-associated-holdouts/`, and `associated16-unseen-baseline-comparison.json`. The generator is `harness/associated-projection.py`; the pure source oracle is `harness/AssociatedOracleProgram.cs`. Existing source hashes, candidate registrations, and original comparison failures are retained.
