# Fallback source-dimension guard

Root source review found that `DecodeBounded` enforces12000 source dimensions for native codecs but not the ImageSharp fallback. A valid12001×1 TGA can therefore bypass the documented source limit. Full `Decode` rejects that source only after raster allocation. This is separate from the returned-pixel budget and does not close the package's six advisories.

Use the existing fallback library's first-frame metadata identification on the exact encoded snapshot before allocating its raster. Reject nonpositive/over-limit width or height with the established typed archive error and source scope; preserve that error through the private fallback catch. Keep first-frame policy, existing in-range pixels and public signatures. Do not introduce a smaller limit, replace the backend or alter archive export validation.

Generated acceptance: both byte-array and path bounded routes must fail before/pass after on tiny12001×1 uncompressed TGA; native codec creation must be null so the intended fallback is exercised. An admitted12000×1 control must retain exact pixels, original dimensions, source checksum and literal returned budget. Then run the full Imaging group. No huge image, model, fitting, labels or provider is needed. Separate baseline/precondition scopes remain frozen for the worker/native-AI comparisons; this fallback-only correction must not be silently inserted into a running package.

## Executed evidence

The same three generated cases report2failures/1pass before and3passes after. The complete current Imaging suite passes68/68 in `.artifacts/overnight-2026-11-05/fallback-dimensions-after/imaging.trx`, with no fitting. Current source/test hashes are frozen in `fallback-dimensions-freeze.json`; the earlier central1935-case gate predates this separate three-case addition. All benchmark package copies remain immutable.
