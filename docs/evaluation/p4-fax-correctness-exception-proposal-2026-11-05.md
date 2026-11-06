# P4 and TIFF Group3 exact correctness-oracle proposal

The original strict candidate-v2 comparison remains failed; no threshold has been changed. These new exact source-bit oracles attribute the observed mismatches to baseline defects. Candidate-v2 outputs were already observed; that sequence is explicit. The proposal requires root approval and confirmation on separately frozen, previously unmeasured supplemental fixtures rather than claiming that selecting a better-looking output is validation.

## P4 binary data

The primary [PBM specification](https://netpbm.sourceforge.net/doc/pbm.html) separates the numeric header from raster with one whitespace character. Raster bytes are packed most-significant-bit first,1 black and0 white; padding bits end each row. Whitespace and hash bytes after that delimiter are raster, not an instruction to keep skipping the header.

Five immutable8×2 files have exact header `P4\n8 2\n` (hex `50340a3820320a`), followed by exactly two data bytes: first9/10/13/32/35 and second17. The first four are accepted with wrong pixels by the baseline; first35 is refused. The candidate's source-bit behavior is independently checkable without a decoder. The full byte offset, raster hex, RGBA and every old/new pixel are frozen in `p4-fax-correctness-oracles-proposal.json`. First35 is new capability validation, not required backwards acceptance.

The sixth fixture intentionally exposes a policy ambiguity in the original generator: it ends its nominal header with CRLF after ASCII height5. A strict one-character delimiter makes **CR** the delimiter and **LF** the first raster byte. For width13,height5, the first10 raster bytes are decoded in two-byte rows, leaving one unused trailing byte. The baseline instead skips the whitespace sequence. The oracle freezes the exact header/raster hex and offset; it does not rewrite the file. Proposed policy is the same single-delimiter rule as approved P5/P6, with no heuristic that could consume a legitimate first LF sample. This is an explicit compatibility change for permissive CRLF interpretation, not a claim that every historical producer used the same policy. A new canonical-LF fixture with the same intended bit pattern must also pass before adoption.

## TIFF Group3

The independently inspected file declares Compression3,1-bit samples, and PhotometricInterpretation1 (Min-is-Black), consistent with [LibTIFF's primary specification coverage](https://libtiff.gitlab.io/libtiff/specification/coverage.html). Its source was generated from `gray=(x*13+y*29)%256`, black when gray<=127, white otherwise. The baseline Group3 output is the exact inverse of that source. The same source encoded as Group4/LZW/Deflate/PackBits matches the baseline, while the candidate matches it in all tested bilevel variants. This is separate from the known TiffLibrary Group4 inversion, which remains a negative control.

Requested scope: approve only the named Group3 source-exact oracle, six P4 source-bit interpretations above, and future generated regressions of those rules. Keep associated-alpha, palette and16-bit TIFF quantization comparisons strict; no TIFF-wide tolerance is introduced. Raw dimensions, source hashes, first-frame policy, and all other thresholds remain unchanged. Derived band/resize expectations are generated from the fixed correct raster using the unchanged baseline Skia operations; these are also frozen.

Root approved these seven exact source oracles with the disclosed post-observation attribution. New canonical-LF/leading-byte and Group3 MinBlack/MinWhite holdouts remain mandatory. No production adoption or release-gate closure is claimed.
