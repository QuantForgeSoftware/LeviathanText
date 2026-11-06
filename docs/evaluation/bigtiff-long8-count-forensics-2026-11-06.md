# Big-endian BigTIFF LONG8 array refusal

A new independent RGB8 holdout exposed a genuine isolated-baseline fork defect. The exact A16 fork throws `IndexOutOfRangeException` while reading external LONG8 offset/count arrays in a valid small big-endian BigTIFF. No A19 output was inspected, and production files/dependencies remain unchanged.

The original tiled input `valid-none-MM-big-tile.tif` has SHA-256 `b3301502a70621f43622e0858c50a21cc2ff5948dcf08e4be757923416231656`, dimensions19×17, a14-entry IFD at16,16×16 tiles, external LONG8 tables at312/344 and raw tiles at376+768n. The IFD, tables and blocks are8-byte aligned. Independently installed native `tiffinfo -D` reads/decompresses it without diagnostics. Pillow refusal is retained as contrary independent-reader evidence; native metadata/decompression alone is not an exact pixel oracle.

The original strip generator had an independent8-byte-alignment defect. The evaluator preserved that source and produced a separate corrected pair. The corrected valid strip has SHA-256 `be3419e2e7c8611205fdab12ca8a91997b774053478db84ae2285cc9170ad38e` and still triggers the same exact fork exception. The alignment issue does not explain the tiled refusal or corrected strip refusal.

## Actual baseline proof

`.artifacts/experiments/bigmm-long8-forensics-v1/` contains the preregistered tiny metadata-only direct fork probe, build log, runtime seal, exact input hashes, results and closure. Its binary references the exact frozen A16 fork DLL, not rebuilt source. Build succeeds with zero warnings/errors. Both sources throw during `Tiff.ClientOpen`, before any raster decompression:

`SwabArrayOfLong8` → `fetchData` → `fetchLong8Array` → `fetchStripThing` → `ReadDirectory` → `ClientOpen`.

The stack identifies fork `LibTiff/Internal/Tiff_DirRead.cs` line371 and `Tiff.cs` line5968. Input/runtime closure remains exact, process exits0 after capturing the exceptions, and the bounded probe does not invoke any candidate19 code or model/provider.

## Cause and separate proposal

The `fetchData` branch for LONG8/SLONG8/IFD8 converts `count*8` bytes into a `long[count]`, but then passes **2×count** to both `SwabArrayOfLong8` and `Long8ToByteArray`. Their count arguments are numbers of64-bit elements, not32-bit halves. An external array with four entries therefore requests eight swaps on four elements. The branch executes on byte-swapped files, explaining why little-endian external arrays and earlier BigTIFF cases using32-bit offsets or a single inline offset can pass.

The separately versioned source-only proposal is `.artifacts/experiments/sec01-adapter-v1/libtiff-fork-patched-v5/`, with exactly two semantic substitutions: each erroneous2×count becomes count. `Fork.csproj` and `SourceVersion.cs` additionally identify the prototype as `2.4.660-leviathan.4-long8count`; complete upstream notices remain untouched. The source freeze SHA-256 is `3988d335578b6e7169275a1d6e3c5ad60a714e835962f5305db494ed1a646b89`; patch `libtiff-fork-v5-long8-count.patch` is retained separately. No new candidate is compiled or accepted by this forensic report. Frozen16/17/18/19 and forkv4 remain unchanged.

Before assessment, independent unseen II/MM external arrays of lengths2/3/5, native layout/pixel references, the original tiled/corrected-strip negatives, and the existing full matrix must be registered. Inline/single-value arrays, LONG32 offsets, ClassicTIFF, unrelated signed/IFD8 values where supported, late malformed blocks and metadata budgets remain controls. Any successfully decoded new valid layout requires source-oracle qualification rather than being relabeled old lossless parity. No tolerance relaxation or production dependency adoption follows from this small fix. Other parser paths and the larger fork maintenance/security ownership obligations remain open.

## Approved isolated compilation and pairing

Parent subsequently authorized forkv5 build/staging only. Compilation succeeds with zero errors and the one retained vendor CS8981 warning (`my_2pass_cquantizer.box`). The paired packages under `fork-v5-paired/{baseline,candidate}` preserve exact A16 Imaging/Core/Contracts and dependency manifests. Only fork DLL/PDB differ, in each of the two duplicated baseline runtime locations; four paths change, representing two unique binary files. `fork-v5-paired-compiled-freeze.json` SHA-256 is `f3d60fb1ce74c70c3a2bdb285847fd751b8a0657acc62e74a1f4bcc38d437b20`; new fork DLL SHA-256 is `ebf562d8d92efa6ef7d5af6287d3254f1723b7817c51ebf00747ea8cd25a5485`.

A separately registered reflection-only probe confirms the same39 exported types/public-member reflection shape, hash `3185015ff3ff0a915c4d414196c729005c87f9b7f8d94411e260e5c704fb32d4`, with AssemblyVersion2.4.660.0 unchanged. FileVersion advances to2.4.660.4 and the informational version records the prototype suffix. `fork-v5-build/abi/` retains source, pre-execution hashes, result and unchanged-input closure. This does not certify every possible binary ABI/platform behavior and did not decode images. Baseline dependency manifests are deliberately retained for the controlled binary pair; they are not a newly generated release SBOM.

Patched image outcomes remain unassessed at this handoff. Independent unseen input/source-oracle freezing and parent acceptance registration precede that step; no17/19 candidate combination or production migration has occurred.
