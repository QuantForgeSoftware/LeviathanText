# RGB8 validation v2: preserve encoded-strip granularity

Source-only candidate21, uncompiled/unassessed. Candidate19's strict functional cohort stopped at process30 on a snapshot-admission mismatch. That failed/incomplete result, original candidate19, and all baseline oracles remain preserved. Main and the separate candidate20 old-JPEG proposal are unchanged.

## Cause and compatibility requirement

The independent PackBits fixture declared valid was nonconforming: its literal runs cross57-byte RGB row boundaries. [TIFF6 §9](https://www.itu.int/itudoc/itu-t/com16/tiff-fx/docs/tiff6.pdf) requires independent row packing. This fixture defect does **not** authorize dropping a previously admitted stream or rewriting a failed comparison as a pass.

Candidate19 changed the vendor request granularity from an entire decoded strip to individual rows. `PackBitsCodec.DecodeRow` and `DecodeStrip` call the same routine with different requested byte counts. A128-literal requested into57bytes emits an overrun/discard error, even if a boolean success is returned; the enclosing diagnostic policy rejects nonzero errors. The old full-strip285-byte request can consume128+128+29 literals without that row error. This source trace explains the observed mismatch. Actual per-row returns/error counters remain an explicit post-quiet diagnostic gate, not inferred measurements.

The compatible correction preserves the old encoded-strip operation rather than relaxing error handling or discarding the fixture. Keep original nonconforming PackBits admission as a fixed compatibility control. Add separately versioned conforming row-packed controls, preserving both histories and their actual outcomes.

## Exact isolated change

Candidate21 copies19, changing **only** the stripped branch of `TryValidateContiguousRgb8` in `TiffSampleDecoder.cs`. Tile reuse, the five-code compression allowlist, layout/unsigned/no-extra-sample gates, all renderers, metadata/offset policies, first-IFD/raw-frame behavior and the enclosing error sink are unchanged.

The strip branch keeps checked `ScanlineSize == width*3`, validates rows, and preserves conservative whole-source-strip allocation flags. Before any `ReadEncodedStrip`, it mirrors A16's in-memory normalization of unsigned RowsPerStrip values greater than `int.MaxValue` to equivalent page height. This is required because the vendor's signed strip arithmetic mishandles the UIntMax default on this API. It changes no source byte and is only equivalent for the admitted one-strip geometry.

Allocate one reusable byte buffer of checked `rowBytes*min(rawRows,pageHeight)`, refusing only values exceeding the **existing** decoded-block cap or integer destination capacity. For each first-IFD strip, request exactly `min(rows,height-top)*rowBytes`, including a shorter final strip, and require the same full returned byte count. Use the vendor's strip index calculation after normalization. No fallback that silently ignores sink errors or incomplete reads is introduced.

This buffer may span the entire frame, up to the existing512MiB decoded-block limit (valid12000² RGB8 can require432,000,000bytes). That limit is per explicit destination, not total managed/native/process memory: source snapshots, encoded buffers, codec state and concurrency remain additional. No proportional-ROI or row-only memory promise is made. The allocation flag stays true for a source strip covering the page. No new compatibility refusal is approved; any new result difference stops the gate.

Source freeze: `.artifacts/experiments/sec01-adapter-v1/candidate-v21-source-proposal-freeze.json`, SHA-256 `ed0b5fe0a7880cc0029cece80959c421229b173d3bc7011fb6b5d49e8aacdd01`. Patch: `candidate-v21-rgb8-strip-validation.patch`. Inherited scratch project metadata still references its historical graph: any future approved matched compile must explicitly use exact qualified fork5 plus frozen A16 Core/Contracts, never silently rebuild an older fork graph.

## Independent controls before candidate output

Source-only preparation is in `.artifacts/experiments/sec01-independent-v1/rgb8-v21-review/`. Its unexecuted builder changes only PackBits encoding to concatenate independently packed rows. A new valid/corrupt pair will verify literal packets finish each57-byte row, with strip byte counts290/290/290/116. The corruption halves only the final declared count116→58 while leaving source length unchanged. Its actual qualified A16+fork5 decodability/ROI outcomes must be frozen before any21 assessment; do not guess corrupt refusals from names.

After quiet release and parent review:

1. Register a tiny diagnostic against the exact existing fork: same original PackBits valid/corrupt bytes, fresh reader per branch, bounded per-instance sink, ReadScanline row0…16 versus exact ReadEncodedStrip counts. Record booleans/byte counts, per-call errors/warnings, source immutability and branch buffer sizes. No global handler and no changed codec source. Preserve incomplete results on first cap/failure.
2. Generate and hash the NEW row-conforming pair, verify declared packet/row structure independently and collect only qualified A16+fork5 public matrix/snapshot baselines first. Preserve original nonconforming pair and original failed gate.
3. Freeze candidate21 source and build Imaging only against exact frozen dependencies; stage a matched pair differing only in Imaging DLL/PDB. No19/20/21 source composition or full vendor rebuild is implicit.
4. Rerun the strict219 functional cohort plus four public processes for the new pair, checking complete rendering/identity and every actual validation outcome, mode/allocation flag, first-IFD/default-row case, deferred variant and late-data control. No new exception or dynamic tolerance is permitted.
5. Only after functional parity, seal a new independently approved archive-resource comparison. Keep A16/19 negative evidence and the original45% large-TIFF slowdown. No timing run or production dependency migration follows from this proposal.

No builds, decoder calls, models, fitting, corpus/labels or provider work occurred while preparing this source proposal during the coordinated heap-diagnostic quiet window.
