# Throughput measurements (pilot machine)

Date: 2026-10-19. Pilot machine: macOS arm64, .NET 10, local SQLite. Measured commands, not estimates.

| Operation | Measurement | Method / caveats |
| --- | --- | --- |
| PAGE + image row-profile import (CLI, one process per page) | **10 pages in 5.3 s → 0.53 s/page ≈ 113 pages/min** | `leviathan import page` loop over 10 P01 files into a fresh store; includes process startup per page, so a resident worker will be faster; no recognition included |
| Training (paper/photo lines, CRNN h=64) | 0.105 s per training line per epoch-set run (see pilot report) | P01 pilot, 300 epochs, CPU |
| Recognition (greedy CTC, CPU) | minutes-scale per 50-line dev set (see ML records) | Batch size 8; beam search ~6–12× cost (B's measurement) |
| Bulk PAGE import (`import dir`, in-process, 27 P01 files) | **27 pages in 0.6 s ≈ 45 pages/s** | CLI single process; includes image path resolution, no image decoding |
| `train lines` package build (P01, 5 epochs) | **3.5 s total** (train + package) | Pilot sanity run; `model.best.*`, metadata, manifest and card written |
| `layout.detect` job (API enqueue → worker → 12 lines) | **~4 s first run** | Includes one-time remote image fetch (~3 MB) into the checksummed cache; later runs are offline |
| Batch recognition (`recognize project`, 27 P01 pages / 321 lines) | **~1.3 s** | Tiny 5-epoch pilot package; inference is dominated by process start at this scale |
| Peak RSS, single-page recognition (CLI) | **200 MB** | `/usr/bin/time -l` on macOS arm64; includes .NET runtime + CPU libtorch |
| Peak RSS, 27-page batch recognition (CLI) | **~244 MB** | no growth observed across surfaces (models loaded per surface and disposed) |

Not measured: sustained collection-scale runs (thousands of pages), peak RSS under load, GPU backends. Those remain release-checklist items.
