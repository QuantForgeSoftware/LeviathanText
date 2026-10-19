# Throughput measurements (pilot machine)

Date: 2026-10-19. Pilot machine: macOS arm64, .NET 10, local SQLite. Measured commands, not estimates.

| Operation | Measurement | Method / caveats |
| --- | --- | --- |
| PAGE + image row-profile import (CLI, one process per page) | **10 pages in 5.3 s → 0.53 s/page ≈ 113 pages/min** | `leviathan import page` loop over 10 P01 files into a fresh store; includes process startup per page, so a resident worker will be faster; no recognition included |
| Training (paper/photo lines, CRNN h=64) | 0.105 s per training line per epoch-set run (see pilot report) | P01 pilot, 300 epochs, CPU |
| Recognition (greedy CTC, CPU) | minutes-scale per 50-line dev set (see ML records) | Batch size 8; beam search ~6–12× cost (B's measurement) |

Not measured: sustained collection-scale runs (thousands of pages), peak RSS under load, GPU backends. Those remain release-checklist items.
