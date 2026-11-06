# A17 owned raster writer: public Decode resource proof

The exact A17 writer preserved all192 outputs across24 registered fresh-process visits and reduced Decode latency and managed allocation on the six generated PNM/QOI/TGA workloads. This is a narrowly matched end-to-end Decode result, not a general decoder, native-memory or dependency-adoption claim.

Registration SHA-256: `aacf00efd2684cdc9f871ef5023d96630e14e6a6d341e9a00ad2ee9854e23a20`. Artifacts: `.artifacts/experiments/sec01-independent-v1/writer-resource-v2/timed/`. The approved ABBA set completed in160.415seconds; all owned processes exited zero and were reaped. Source, helper, managed-dependency and native hashes remained unchanged. Baseline and candidate differed only in Imaging DLL/PDB (exact A16 versus exact A17).

## Inputs and correctness

The four QOI/PNM inputs are immutable earlier representative fixtures. Two TGA inputs are NEW24-bit, uncompressed, top-left containers derived directly from the frozen P6 rasters, with provenance and source hashes recorded. The old QOI scenes are not assumed identical to PNM scenes merely because dimensions match: an initial zero-decode preparation assertion caught that false assumption and is preserved separately under `writer-resource-v1/`. QOI retains its own earlier fixed raster oracle. PNM/TGA use an independent direct-P6 RGBA oracle with alpha255.

All six inputs passed untimed exact A16 pixel and identity checks before the timed registration. A failed first functional helper launch omitted Imaging from its dependency graph; the one owned child exited/reaped without reaching Decode. That negative is preserved in `writer-resource-v2/functional-results/`; the corrected dependency-metadata-only stage and six passing checks are in `functional-results-v2/`. No failed visit is used as a timing result.

Each fresh process made one cold, one warmup and six measured Decode calls, with no forced GC between calls. Decode elapsed/CPU/allocation stop at its return. Canonical pixel hashing, identity validation and output disposal occur afterward, outside the timer. Every cold/warm/measured output matched fixed full-raster hashes/dimensions and its own encoded checksum/length.

## Every measured visit

Medians below summarize six measured calls within each fresh process. Cold and all individual samples remain in the raw JSON.

| Visit | Input | Arm | Median ms | CPU ms | Managed MiB/call | Sampled peak RSS MiB |
|---:|---|---|---:|---:|---:|---:|
|0|1024x768.ppm|A16|346.547|346.736|146.257|81.77|
|1|1024x768.ppm|A17|7.821|7.958|2.256|68.89|
|2|1024x768.ppm|A17|7.493|7.533|2.256|68.89|
|3|1024x768.ppm|A16|347.867|351.069|146.256|81.80|
|4|1024x768.qoi|A16|338.426|338.170|144.569|78.91|
|5|1024x768.qoi|A17|3.541|3.599|0.569|62.80|
|6|1024x768.qoi|A17|3.464|3.508|0.569|62.91|
|7|1024x768.qoi|A16|342.259|342.544|144.569|79.02|
|8|1024x768.tga|A16|349.268|349.333|146.256|97.50|
|9|1024x768.tga|A17|19.742|20.389|2.256|80.39|
|10|1024x768.tga|A17|23.937|24.434|2.256|80.34|
|11|1024x768.tga|A16|352.525|352.375|146.256|97.55|
|12|3001x2000.ppm|A16|2681.752|2679.925|1116.178|181.00|
|13|3001x2000.ppm|A17|57.805|59.044|17.178|131.41|
|14|3001x2000.ppm|A17|57.524|58.750|17.178|131.42|
|15|3001x2000.ppm|A16|2744.283|2745.572|1116.179|181.08|
|16|3001x2000.qoi|A16|2663.847|2662.363|1103.308|117.06|
|17|3001x2000.qoi|A17|25.944|26.431|4.312|92.64|
|18|3001x2000.qoi|A17|26.207|26.607|4.308|92.83|
|19|3001x2000.qoi|A16|2677.577|2675.270|1103.309|117.16|
|20|3001x2000.tga|A16|2890.747|2888.082|1116.177|266.86|
|21|3001x2000.tga|A17|42.474|43.733|17.178|252.22|
|22|3001x2000.tga|A17|42.278|43.404|17.178|251.97|
|23|3001x2000.tga|A16|2739.133|2736.483|1116.177|266.95|

## Interpretation and limits

The sole code delta replaces repeated owned-format pixel writes with one native premultiplied pixel view. All149 prior functional cases/1,341 operations also matched exact A16 before this comparison. Together, those controls support the writer change as a useful isolated optimization. They do not authorize production adoption of the broader permissive-decoder fork.

The six-megapixel TGA sampled RSS remains about252MiB even after allocation and latency improvements; encoded snapshots, native destinations/scratch, process/runtime behavior and hashing remain. Managed bytes are cumulative allocation per Decode, not retained heap. Sampled RSS can miss short peaks. Raw GC counters are preserved without reinterpretation or post-hoc rescaling. Two fresh-process replicates per arm/input do not establish all-platform throughput or a universal RSS bound.

Limits were60seconds wall/45CPU seconds/512MiB observed RSS/64KiB logs per process, sequential dispatch and one-hour batch cap. No limit was reached. No training, model, corpus/locked label, natural-accuracy or provider work occurred. No adaptive retries, changed budgets or metric-selected repeats were performed.

The independent archive-validation regression, generic TIFF scratch cap, OJPEG coverage, gray-alpha deferral, vendor/fork maintenance, license/security inventory and exact later-feature composition remain separate open gates.
