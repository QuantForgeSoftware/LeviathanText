# Gray8 and padded-row layout regression

A valid 241 × 188 grayscale PNG decoded through `ImageDecoder.DecodeBounded` as `Gray8` with 241-byte rows. The previous projection detector read three channel bytes for each pixel and threw `IndexOutOfRangeException`. The old address calculation also ignored row padding, which changed row density and line extents for padded Gray8, BGRA and RGBA rasters.

Projection now shares one stride-aware snapshot across the page histogram, content bounds, row and column density, and band extents. The learned row profile and ink bounds use the same luminance read. These scans step by the actual row stride and read a single channel for Gray8. Packed color retains its prior channel arithmetic. Projection also avoids copying the entire pixel buffer per detected band. Row feature thresholds, feature vectors, geometry and existing model weights retain their contracts.

## Regression evidence

The same test assembly failed all six new projection cases against the saved baseline assembly. With projection corrected, all eight new feature cases failed before the row-feature fix. On the generated image, the learned scan reported bounds 24–213 rather than 25–214 and produced different row feature hashes for identical Gray8 and packed BGRA pixels. The final implementation matches the equivalent packed-color output for row density, run lengths and bounds. The decoded Gray8 projection returns all six expected line bands and preserves both column orders. The Release build had zero warnings or errors, and all 44 tests in the no-fit layout selection passed. The two test classes that fit learned models were excluded.

## Matched packed-color probe

The local C# probe used one deterministic colored 1,600 × 1,200 raster in packed BGRA and RGBA on the same macOS arm64 host and .NET runtime. The same executable and dependencies were used for both routes, swapping only the baseline and candidate layout assemblies. Three fresh processes per route ran in alternating order. Each case used two unmeasured calls and five measured calls per process. All six cases had exact baseline/candidate output hashes. Projection hashes cover line geometry, density, order, method and parameter hash. Feature hashes cover the full row profile and one fixed ink-bound result.

| Case | Median elapsed baseline → candidate | Median managed bytes allocated, MB baseline → candidate |
| --- | ---: | ---: |
| BGRA projection | 9.620 → 3.912 ms | 99.87 → 15.38 |
| BGRA two-column projection | 26.054 → 6.333 ms | 176.75 → 15.47 |
| BGRA profile and one ink-bound scan | 6.322 → 3.813 ms | 30.74 → 23.06 |
| RGBA projection | 10.635 → 3.869 ms | 99.87 → 15.38 |
| RGBA two-column projection | 26.406 → 6.376 ms | 176.75 → 15.47 |
| RGBA profile and one ink-bound scan | 5.839 → 3.717 ms | 30.74 → 23.06 |

The timings describe these generated workloads. Managed allocations are measured on the calling thread and do not represent peak RSS or native/GPU memory. No model fitting, full learned-inference timing, natural text scoring, or layout default selection was performed.

The local `.artifacts/layout-gray8-2026-11-04/` directory retains both assemblies, fail-first logs, the no-fit suite log, PNG fixture, probe source, per-call data, SHA-256 receipt and `combined-verification.json`. Re-run `verify-combined.py` against the saved receipts to reproduce the numeric summary.
