# A17 owned raster writer: proposed resource comparison

Planning only; no timing dispatch authorized. Exact A17 functional149-case/1,341-operation parity is complete. This proposal compares actual public Decode with exact A16, rather than treating the earlier pixel-copy microbenchmark as an end-to-end result. Production migration and advisory closure remain blocked.

## Inputs and scope

Use frozen `representative-resource` QOI and P6 PNM at1024×768 and3001×2000, preserving original hashes and provenance. The manifest already includes both PNM inputs as archive-only; this is a newly registered Decode workload, not a claim they appeared in the prior14 decoder-resource inputs.

There are **no existing representative TGA inputs** in that frozen manifest. Before sealing, propose deriving two uncompressed24-bit top-left TGA containers from the exact existing P6 raster bytes: standard18-byte header, BGR byte order, descriptor0x20, no metadata/footer, same width/height and raw coordinate frame. Preserve the PNM inputs; pin generator/source/output hashes. This preparation needs parent acceptance before execution. Do not label newly derived TGA bytes as previously measured frozen inputs.

Total proposed workload: six inputs × ABBA(A16,A17,A17,A16) =24 fresh owned processes, sequential. Only public Decode is timed. Bounded/Band correctness is covered by functional proof and is not extrapolated into this performance result.

## Freeze and correctness before timing

Stage the existing ResourceProbe helper with exact A16 Core/Contracts/fork/native closure. Build nothing into measured packages. Make the candidate differ only in exact published A17 Imaging DLL/PDB. Hash every helper, dependency/native byte, decoder source manifest, runtime version and input before dispatch and verify after.

Before any timed A17 output is read, derive fixed canonical RGBA and dimensions from source P6 bytes using a small independent streaming oracle; freeze alpha255 and per-input encoded checksums. Verify QOI Decode matches its existing source-raster oracle and the same pattern before treating related containers as equivalent. Collect untimed A16 admission and identity controls, preserving failures. No labels/models/corpus or fitting are involved.

Use the existing public Decode ResourceProbe sequence: one cold, one warmup, six measured calls per fresh process, no forced GC between calls. Start timing immediately before Decode and stop immediately after it returns. Record allocation/CPU deltas at that boundary. Compute canonical GetPixel RGBA SHA and source identity **after** timing, then dispose the output. Validate all eight outputs against preregistered hashes/dimensions; no optimistic omission of cold/warmup correctness. Source input remains unchanged. Do not move hashing/disposal costs into one arm's timer alone.

## Limits, cleanup and analysis

Per process:60seconds wall,45CPU seconds,512MiB observed RSS,64KiB stdout/stderr; sequential dispatch, no uncontrolled concurrency. Batch:60minutes wall including process overhead; abort immediately on first parity, admission, source/binary-drift or cap failure. Poll owned PID RSS at20ms nominal intervals; report monitoring overhead and missed short peaks. Always kill/wait only owned groups in finally. Preserve aborted receipts; no adaptive input/order/budget retry.

Freeze exact24-visit order and registration SHA before dispatch; get root approval and coordinate a CPU-quiet window. No build/test/browser/provider workload may overlap the measured set. Keep all per-process medians, cold timing, allocation, CPU, sampled peak RSS and raw post-GC counters, not only pooled improvements. Two process replicates per arm/input are limited evidence. A17 may lower managed pixel-transfer cost without fixing encoded snapshots, native allocations, RSS, parallel admission or first-frame validation.

An adverse result remains a result. Do not choose thresholds from the already observed A16 negatives or retune a candidate inside this batch. Main dependency adoption, OJPEG, grayscale-alpha, full bundle validation, source limits, workspace caps, vendor maintenance and license notices remain separate gates.
