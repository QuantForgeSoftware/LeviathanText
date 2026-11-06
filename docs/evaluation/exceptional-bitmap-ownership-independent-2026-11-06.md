# DERIV-02 independent construction ownership verification

The isolated repaired imaging implementation releases every captured destination at the 30 registered generated exception boundaries. Eight successful handoff controls retain the returned bitmap and release captured non-returned intermediates. Its 29 public pixel, dimension, checksum, coordinate and metadata records exactly match the untouched current implementation. Permanent generated xUnit coverage passes 39/39, with no skips.

No production source or dependency was changed by this verification. Root owns integration. R owns the isolated engine and its local ownership repair; the independent source, inputs, tests, runtime staging and observations are under `.artifacts/experiments/deriv02-independent-v1/`.

## What was compared

Before any engine edits, 71 original source/managed/native files were frozen. The current main and Imaging.Tests Imaging DLLs were identical: `03215acf08d40b2900eb8cde59d97e7a76625e61e49d46e7fb3bdac05de2ecf6`. The copied native scope is the installed macOS Skia/PDFium assets, not all RIDs.

The untouched original public binary first produced 29 records using tiny generated PNG, JPEG and P6 fixtures: full decode; budgets 1, 4, 12 and 96; negative/middle/final bands; exact crop and deskew/chain pixels and mapped points; no-op source alias; source survival. These original outputs were frozen before extracted or repaired output assessment.

Exception injection needs a separate, explicitly labeled matched extraction. R's instrumentation-only old tree adds the same internal per-call stage observations to the old control flow, without fixing its destination ownership. The repaired tree adds local flags/finally scopes. Both build only Imaging against the exact original dependency assemblies. Their staged packages differ only in Imaging DLL/PDB; no target dependency was recompiled or substituted. All three public-output files are byte-identical: SHA-256 `1f73b4006daf7217aca35fa4751c3a88bbdedd0b51984cbb372ea3cec9ec8430`.

## Exception and handoff results

| Control | Extracted old flow | Repaired copy |
|---|---:|---:|
| Generated fault boundaries | 30 destinations remain undisposed at observation | 30/30 release captured destinations |
| Successful handoffs | 8/8 | 8/8 |
| Caller bitmap live and exact (15 derivative cases) | 15/15 | 15/15 |
| Existing exception contracts | 30/30 | 30/30 |

The cases cover codec-scaled and fitted JPEG destinations, PNG canvas resampling, JPEG scanline bands, P6 fallback subsets, crop, deskew and a failing second transform. They observe allocation, real canvas lifetime, native initialization and publication boundaries where those stages exist. In the two-transform failure, the previous owned intermediate is released as well as the failing target. Bounded decode retains its existing `RejectedArchive`/unsupported-format mapping and generated exception detail. Other routes propagate the same generated exception instance.

Managed references retain captured wrappers. Tests record handle state before their own finally cleanup, then dispose deliberately retained extracted-old wrappers. Canvas handles are released. In the 15 crop/deskew/chain cases, the actual caller-owned source handles/pixels remain usable. Decode/band cases take bytes or paths instead; their input files remain byte-identical, and the independently held generated bitmap is only a separate liveness control. The eight success cases also verify that the returned wrapper remains live until caller cleanup. Crop's permanent public control checks exact pixels, coordinate mapping, metadata and no-op/source ownership.

These are real tiny native wrappers with controlled managed callback exceptions. They model failure edges; they do not demonstrate a real native constructor/GetPixels failure, OOM, permanent leak, all-platform correctness, throughput improvement or reduced RSS. Full `Decode` and fallback `Resize` metadata-only handoffs are outside this repair. Allocation failure before a wrapper is returned remains outside the controlled seam.

## Registration, limits and closure

Root approved exact fault registration `f0a3cf2d53ab0f01643a726d4004d0e790d80f234f122366acd69c63a05c2f4e` and CI registration `e268b50f271cad3e2b02fe72e04439c6da8cc1351696a03cf55d9d6ebcc258ff` before dispatch. No cases, oracles, caps or retries changed during execution.

The 38 cases per arm ran sequentially, each with 15 s wall, 10 s CPU, observed 512 MiB RSS and 64 KiB per-log caps, plus a 600 s cohort maximum. All 76 subprocesses exited 0 without a limit or unexpected outcome. The exact public comparisons also exited 0. The permanent xUnit subprocess used 60 s wall, 45 s CPU, observed 512 MiB owned-tree RSS and 64 KiB log limits; 39 passed, 0 failed, 0 skipped. Original/extracted/candidate/harness compilation reports zero warnings/errors.

All registered source, fixture, package and original frozen-file hashes remain unchanged. Owned subprocesses were reaped, and the xUnit process group had no remaining members. Test scratch fixture directories were removed; immutable inputs, binaries and receipts remain as evidence. No fitting ran, and no provider, model, corpus, user image or browser workload was involved.

Key receipts:

- `original-source-runtime-freeze.json`, `original-public-oracle-freeze.json`
- `fault-registration.json`, `root-approval.json`, `old-cohort.json`, `candidate-cohort.json`
- `results/{original,extracted,candidate}-public.stdout`
- `ci-results/deriv02-ci.trx`, `ci-execution.json`, `final-proof.json`
- `BitmapExceptionalOwnershipTests.cs` — artifact-only permanent regression source, ready for root review/integration

The companion engine source and instrumentation/ownership-only patches are under `.artifacts/experiments/deriv02-engine-v1/`. The earlier derivative-chain cleanup and crop-overflow fixes remain separate work.

## Permanent test formatting adaptation

The original compressed artifact and its 39-case proof remain unchanged. A second file, `BitmapExceptionalOwnershipTests.readable.cs`, uses Roslyn `NormalizeWhitespace` and the file-scoped namespace `LeviathanText.Imaging.Tests`. Parsing reports 0 errors; all 3,177 original member token kinds/values are identical after the namespace wrapper is excluded. No assertions, expressions, callbacks or case data changed. The readable source SHA-256 is `253eb17afd5125dc21cf54be7cf56ac8698cbddf89682816dbafb5b3454e777b`.

Its compile passes with 0 warnings/errors. The first validation attempt exited 153 when the longer fully qualified test names expanded structured TRX to exactly65,536 bytes: the harness had inadvertently applied its console-log file-size cap to every output file. That capped attempt and incomplete TRX remain preserved; this is a reporting-harness failure, with no claim that invocation completed 39 tests. Root approved exact rerun registration `67e420c226bb295c43e154ac17ee9f57bd1076028125110bf71630134666ba03`, separating a 256 KiB structured TRX cap from the unchanged 64 KiB console cap. The unchanged candidate assembly then passes 39/39, 0 skips, all runtime hashes unchanged and no remaining owned processes. Wall 60 s, CPU 45 s and observed owned-tree 512 MiB limits remain unchanged. See `format-only-adaptation.json` and `ci-readable-results-v2/deriv02-ci.trx`.
