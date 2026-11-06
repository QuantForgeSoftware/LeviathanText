# BigTIFF LONG8 count correction: independent functional controls

The isolated fork-v5 correction passed the freshly approved219-process cohort with exact A16 Imaging/Core/Contracts/helpers/native dependencies. Only fork DLL/PDB changed. The observed fix is a typed-array element-count correction, not a production dependency migration, general TIFF security approval or cross-platform ABI claim.

The original two new BigMM holdouts exposed `IndexOutOfRangeException` in the fork's LONG8 data path. Owner A's direct exact-fork forensic probe traced `SwabArrayOfLong8`/`fetchData`: the existing code passed twice the element count to both64-bit swapping and copying. The isolated fix uses the real count on those two lines. Existing BigMM successes did not prove external multi-element LONG8-array coverage.

## Independent inputs and fixed expectations

Six unseen BigTIFF RGB8 controls cover II/MM byte orders and external LONG8 offset/count arrays of2/3/5 entries. Every native `tiffinfo -D -s` invocation exited zero, and every reported table value matched fixed declared offsets/lengths. Exact A16 accepted the three II cases with source-formula RGBA equality and refused the three MM cases. Before any patched output, expected MM pixels/scaling/bands were fixed to independent raw RGB and the matched II controls, with each encoded source's own checksum.

The original16 RGB8 controls and alignment-only pair remain preserved. The strip generator originally placed external BigTIFF tables at unaligned addresses; the [primary BigTIFF design](https://libtiff.gitlab.io/libtiff/specification/bigtiff.html) requires8-byte alignment for external values. A versioned correction changed only that strip valid/corrupt pair; it did not erase the original source or failed baseline gate. The tiled source already had correctly aligned tables and legal16×16 tiles, so changing the fixture to hide its refusal would have been inappropriate.

The final cohort distinguishes **five valid BigMM admission corrections** from **one explicitly tolerated bounded unaligned original strip container**, already decoded by native libtiff. It makes no strict-alignment conformance claim for that last file. Unknown/corrupt source data is not authenticated merely by decodability.

## Preserved failed gate and independently proved ROI follow-up

The classification-corrected v2 cohort stopped at its19th child on an oracle mismatch and remains **FAILED/incomplete**. That child exited normally: two early Band requests on a source with a damaged final tile succeeded, whereas the old baseline had failed during LONG8 metadata processing before reaching any pixels. Whole decode, bounded decode and bands touching the damaged tile still refused. No runtime cap or source drift caused that stop; it was not relabeled a complete pass.

A separate parent-approved policy retained existing ROI semantics for exactly six calls: `bandMiddle` and `bandNegative` across the late-bad BigMM tile, original unaligned strip and aligned strip. Before any further patched output, three independent II damaged counterparts plus their valid controls reproduced the MM encoded data/positions with metadata endianness changed. Exact A16 confirmed early ROI success and whole/late/snapshot refusal on all three. Direct source-formula RGBA matched both early bands exactly. A geometry proof shows they touch complete earlier segments0/1 and never damaged segment3. The last/full requests touch segment3 and remain refused. Candidate pixels were not used as oracles.

The independent ROI freeze SHA-256 is `ec5a3ec2d4f324761636f61fd52332e6effbe82a5862805bd2a8e3dd798bf92e`. Only those six operation expectations changed in a new cohort; all previous outputs and failed registrations remain immutable.

## Final approved result

Registration `.artifacts/experiments/sec01-independent-v1/fork-v5-functional-v3/registration.json`, SHA-256 `29e258554cd7e9a6cd2b34be00fa630e463d56dd250b267fc7fff2950a296eb8`, bound approval before dispatch. Intended fork binary: `ebf562d8d92efa6ef7d5af6287d3254f1723b7817c51ebf00747ea8cd25a5485`.

All219 owned processes exited zero and were reaped:

- Six unseen controls, original16 and aligned pair:24 full pixel/metadata matrix processes and24 snapshot processes.
-154 unchanged prior snapshot controls, including nested JPEG cases.
-17 unchanged deferred-variant/known-format controls.

Every registered source and runtime hash remained unchanged. Full pixels, dimensions, source identities, mode/allocation flags and refusals matched the fixed or explicitly approved expectations. Damaged BigMM whole/late/snapshot paths remained refused. Per-case results, closure and verification are in the final artifact directory. Limits were15seconds wall/10CPU seconds/512MiB observed RSS/64KiB logs per child, sequential,900seconds batch cap; no quantitative performance claim is made.

SLONG8/IFD8 utility paths share the corrected source switch, but no independent public/runtime utility coverage is claimed here. OJPEG, grayscale-alpha, extra-workspace caps, validation optimization, vendor maintenance/notices and full dependency adoption remain separate gates. A19 optimizer outputs were not assessed inside this cohort.
