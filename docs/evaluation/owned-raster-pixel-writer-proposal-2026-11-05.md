# Owned raster destination writer — isolated v17

Status: source-only implementation for independent review. No v17 compile, fixture evaluation or resource workload has run. Main Imaging and its ImageSharp dependency are unchanged by this slice; the current owner-B NativeAI API full24 quiet window remains authoritative.

## Bounded change

Frozen v17 copies v16 and changes only `FallbackRasterDecoder.cs` plus new `PremultipliedRasterWriter.cs`. The writer acquires one native pixmap/span for an actual PNM/QOI/TGA decode and disposes that view in `finally`, keeping its bitmap alive. Each destination pixel uses existing `SKPMColor.PreMultiply` and copies its native BGRA fields. It introduces no independently approximated alpha/color arithmetic, package, unsafe compiler option, source-format parser or new admission cap.

The null-destination validation walk creates no pixmap and performs the original source checks. PNM sample normalization, QOI unassociated color/index/opcode state, and TGA palette/RLE/origin/attribute interpretation remain unchanged. TIFF, precise samples, JPEG, dimensions, bounded-target fitting, metadata guards and vendor source are byte-identical to v16. The new ref-struct span neither escapes nor becomes captured by a closure; the TGA local output routine receives it explicitly by reference.

The motivating evidence is root's separate copy-only native-API experiment, `.artifacts/experiments/pixel-transfer-v2/`: twelve6MP raw hashes match, with recorded copy allocation/time improvement. That experiment did not execute these parsers or a decoder pipeline. Its results are not claimed as v17 performance or parity.

Source freeze: `.artifacts/experiments/sec01-adapter-v1/candidate-v17-source-freeze.json`, SHA256 `a5e92bfa9fe6826509134601e042e0e26a2eb1792182cc8c4aade62e6f2b062a`. Review patch SHA256 `b33034e1d687fcf4ab220023f1932942853d9dfc05a9932693bc13151e944f33`. Both precede any v17 execution. Original v16 and its independently rebuilt154+17 functional controls remain separate evidence; v16's exact frozen A-DLL/resource gates are not silently closed by this source work.

## Proposed acceptance registration

Before v17 output assessment, R should freeze the actual baseline/candidate binaries and exact input/oracle manifests. This section is a proposed workload, not an approved or executed benchmark:

- Reuse all frozen original/extension owned-format cases: P1–P6 including already approved source-correctness exceptions and unseen leading-binary holdouts; raw/RLE/indexed/gray/alpha/origin TGA variants; and all QOI opcodes, alpha and runs. Compare exact native stored bytes and canonical RGBA, dimensions, original-frame/checksum metadata and relevant Decode/DecodeBounded/DecodeBand outputs against frozen v16/source oracles. Preserve the budget-correctness policy; do not compare an intentionally oversized old output as the target.
- Add source-registered unseen TGA/QOI alpha/color grids covering every alpha value, zero/opaque alpha, mixed color rounding and repeated/indexed runs. Obtain actual v16 outputs before observing v17. Keep PNM normalization/source oracles unchanged. Native premultiplication is the oracle; no hand unpremultiplication approximation or relaxed color tolerance.
- Preserve malformed-input decisions across full decode and snapshot traversal: PNM bad samples/truncation/overflow, TGA palette bounds/packet overruns, QOI run overruns/end marker/truncation. Validation must retain the same source walk and exact source bytes. Header/Dimensions controls stay separate; source hashes alone do not prove runtime behavior.
- Independently inspect lifetime/error paths and constructor failure cleanup. Where a focused writer probe is useful, compare against actual `SKBitmap.SetPixel` for padded row strides and mixed alpha, and demonstrate that a null sink neither allocates a pixmap nor changes parser refusal. Do not weaken the original public decoder assertions to accommodate this helper.
- All TIFF source files and fork inputs must hash-identically against v16; an exact approved frozen A-DLL probe remains needed separately for v16 snapshot validation. Do not relabel a rebuilt same-source assembly as byte-identical coverage.

After functional acceptance and a coordinated CPU window, propose matched fixed-order ABBA processes for actual owned decoder calls on registered1024×768 and3001×2000 QOI/PNM/TGA inputs. Reuse known frozen resource inputs where available; add only independently frozen missing format counterparts. Warmups, number of calls, source hash placement, output validation, GC observation, watchdog/RSS/output caps and all source/native binary hashes must be fixed before execution. Keep correctness/hash collection outside timed sections while preserving complete output validation.

Measure elapsed/CPU, managed allocations, peak RSS and exact output/source closure. A measured allocation reduction does not prove lower total RSS; a small process series does not establish universal speed or cross-platform/native-code safety. No adaptive repetitions, larger attacks or tolerance changes follow from a failed result. Root/R must approve the actual schedule and limits before any timing. Production integration remains blocked on the separate TIFF/resource, license/provenance and fork-maintenance decisions even if this writer succeeds.

## Independent static review

R reviewed the frozen v17 source without execution and found no high-confidence blocker: the native view is per decode, BGRA/premultiplied representation and row-span bounds are checked, owner lifetime and failure cleanup compose, ref-only plumbing avoids copying view ownership, and the null validation walk has no native destination. This is static review only. Exact frozen-assembly format/alpha/palette/RLE/orientation/derived-output controls and cleanup/resource checks remain required after the coordinated quiet release.

## Imaging-only compilation (2026-11-06 04:23 UTC)

The frozen v17 source now compiles in an isolated explicit-source project against the **exact frozen A16 Core, Contracts and fork DLLs**. None of those dependencies was rebuilt. The current compile has zero warnings/errors; the earlier vendor-build CS8981 warning remains historical and is not erased by an Imaging-only build. No production dependency was adopted.

`.artifacts/experiments/sec01-adapter-v1/candidate-v17-matched-runtime/` copies the20-file frozen A16 runtime layout, replacing only `LeviathanText.Imaging.dll` and its PDB. Every other registered binary and the baseline dependency manifest are byte-identical. `candidate-v17-compiled-freeze.json` SHA-256 is `8631e28cd7b7de672ea8296c565233cc4e7ad98b884686496afe0785c3383472`; Imaging DLL SHA-256 is `5672568a40436b80753ec7843f047aca36946826855453222d5e3d908c4f40ba`. Build registration and full log are under `v17-binary-reference-build/`.

This is compilation/staging evidence only. The independent evaluator must first freeze additional A16 alpha/color/origin/opcode oracles, then compare the exact v17 assembly on the unchanged owned-format matrix. No v17 pixel or performance acceptance is claimed at this point. These point-in-time16/17 packages intentionally precede the later main crop/ownership fixes.
