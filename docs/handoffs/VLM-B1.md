# VLM-B1 handoff — exploratory corpus and offline scoring

Prepared **2026-11-04**, activated under `VLM-BENCH-1` for the session ending 10:29:19 CST.
Starting commit: `6c9265f6fb82f3472c9d7faa013e79c35fe6e155`, with the existing uncommitted
Nov4 review fixes preserved. This package adds a frozen **51-line provisional P46 metadata
release** and a tested pure C# scorer. A subsequent explicitly assigned **R0-only inference
replay** is recorded below. This package supplies no reviewed natural benchmark data and
performs no VLM calls, model downloads, training, specialist decisions or model promotion.

That statement describes B's package, not every integration command in the session.
Root subsequently identified two earlier CLI regression invocations that each performed
one epoch of an existing temporary synthetic black-rectangle fixture fit. No collection,
P46 or VLM model was trained, but a blanket session-wide "no training" claim would be
incorrect. The [integrator's deviation receipt](../../.artifacts/vlm-benchmark/vlm-bench-1/root/synthetic-test-training-deviation.json)
preserves the exact tests/times and corrective filter; the
[first-sweep qualification](../../.artifacts/vlm-benchmark/vlm-bench-1/b/comparison-report/first-sweep-session-qualification.json)
supplements the frozen report without rewriting its evidence.

## Ownership and integration

B owns only these new tracked files:

- `src/LeviathanText.Evaluation/VlmBenchmarkScorer.cs`
- `tests/LeviathanText.Evaluation.Tests/VlmBenchmarkScorerTests.cs`
- `docs/data/vlm-benchmark-corpus.md`
- `docs/evaluation/vlm-benchmark-scoring.md`
- `docs/handoffs/VLM-B1.md`
- `docs/evaluation/vlm-pilot-results-2026-11-04.md` (B's first-sweep report, handed to root for runtime/deviation integration)
- `docs/evaluation/vlm-repeatability-results-2026-11-04.md` (separate versioned repeatability companion)
- `docs/evaluation/vlm-resource-observations-2026-11-04.md` (offline host/resource appendix)

Evidence lives under `.artifacts/vlm-benchmark/vlm-bench-1/b/`. Root owns the CLI/run manifest,
LM Studio execution, benchmark specification and shared plans. Instance A owns image
transport. B did not edit existing source/test files, the other instance's output, old P46
artifacts or shared plans. No commits or DCO certifications were made.

The proposed scorer API and request-facing schema were agreed with root before coding.
The user-activated engineering assignment supersedes the original planning-only task brief
for these specific new files; training and private acceptance access remain excluded.

## Ready interfaces

Use `.artifacts/vlm-benchmark/vlm-bench-1/b/VLM-B1-r1/manifest.json` for an explicitly
provisional exploratory run. Its 51 images are exact existing PNGs, with paths relative to
the manifest directory, opaque sample IDs and physical units, material, review/exposure
state and rights/geometry evidence pointers. It contains no reference targets and no R0
hypotheses. The [corpus report](../data/vlm-benchmark-corpus.md) documents exact fields,
rights, transformations, limitations, local inventory and subsequent review requirements.

The offline `references.json` uses `references[]` with the approved C# constructor fields
`sampleId`, `physicalUnit`, `material`, `referenceText`, `controlKind` and
`uncertaintyMarker`, plus provenance/review metadata. Do not send it to a VLM. The separate
`provenance.json` retains original IDs and source text/geometry; model requests do not.

Call `VlmBenchmarkScorer.ScoreRun(references, outputs)` once per model/route/repetition.
Outputs are `(SampleId, Status, CapturedModelText)` with the whole captured model text,
including nonempty invalid/truncated/failed generation. Use the complete intended reference
list even if a request/time cap stops execution. The result exposes `PolicyVersion`,
`Rows`, `Overall`, `ByMaterial`, `ByPhysicalUnit` and `Controls`. It rejects wrong/duplicate
joins and distinguishes explicit completed empty text from absent text. See the
[scoring report](../evaluation/vlm-benchmark-scoring.md) for status mapping, nullable
denominators, exact-text versus valid-completion semantics and manual-control gates.

## Frozen R0 pointer for the same 51 crops

`.artifacts/vlm-benchmark/vlm-bench-1/b/r0-baseline-inventory.json` records the original
51-row dataset, all crop and exact prepared-float hashes, profile, architecture, decoder,
checkpoint and an existing inference-only replay example. All **51 crop joins and 51
424 × 16 little-endian float files** were verified against their frozen receipts. Receipt
SHA-256: `b4c1f09e6f192aa19a92fd75d0e8dbedfe402605be1e3800c8b7f1b2c72ea7b4`.

The concrete primary diagnostic checkpoint is under
`.artifacts/experiments/p46-expanded-study-2026-11-02-evening/runs/expanded-42/checkpoints/`:

| Artifact | SHA-256 |
| --- | --- |
| `p46-expanded-expanded-42.model` | `e25845ad1f29d7ec125f57ca83f7cb5d5f2ddc5fc1bd226bf3a3a25193c8fc39` |
| `p46-expanded-expanded-42.json` metadata | `adc18ad146b891179a1a12c0fa1dee7bb523da0a4eed96d02a91a00cffa30d77` |
| Study `dataset.json` | `c3ba27a85b5c77d8d8e1e5c57a6a6d7381f5af8a855d9d2557ed4ed8e9bb00cc` |
| Study `input-manifest.json` | `94f438f2bcf67ecbcb9e70be5497e1652edb6f9e76765298c5eeeede734a995e` |

This is seed 42, epoch 544, global step 5984, the fixed-budget final checkpoint. Use metadata
labels, hidden size 64, width downsample factor 4 and dropout 0 and preprocessing version1,
`luminance-grayscale;height=16;geometry=externally-prepared-line`, with the original
default greedy CTC decoder. Recreate inputs from the exact manifest crop bytes and verify
float hashes before comparing outputs. The crop already includes external rectification;
ordinary page recognition cannot substitute for it.

The neighboring `selected-diagnostic-package/` is ready for `RecognitionModelLoader`, but
its weights `fc85b8243ab8f0686c43ded3d3e47a488c68b9e6ea20eeab79238015d6a37106` are the
**secondary development-selected checkpoint**, not the final checkpoint above. Preserve
that distinction and do not choose a favorable checkpoint from new VLM results. Existing
`.artifacts/experiments/p46-evening-replay/Program.cs` shows loading both forms without
training. A new replay must write elsewhere and retain old packages unchanged.

The assigned replay is complete under `b/r0-replay/run-1/`. Its new `manifest-r0.json`
contains verified absolute image paths and actual `baselineText` for all 51 opaque samples;
no reference label enters the request manifest. Derived manifest SHA-256:
`58777e68e99cbbee08c8df33f238fac0742ffa3aa2f745f4bf91b99dde7cd97a`.
The original r1 manifest remains unchanged. Evidence includes a pre-execution plan with
source/assembly/input/checkpoint hashes, three separately timed warmups, all 153 measured
requests (51 samples ×3 shuffled-order repetitions) and a linkage receipt. **All153 strings
agree across repetitions; all 51 agree exactly with the original final-checkpoint strings.**
This is compatibility evidence, not an accuracy claim.

**Use `b/r0-replay/cli-handoff-v1/manifest.json` with the strict CLI**, SHA-256
`01ff250a9705f2810f0316edd123baf172f5e75bd6da742242540fe4ee57e12b`. The original replay
manifest above also contains baseline provenance fields, which the CLI intentionally
rejects as unknown request fields. This versioned schema adapter moves those fields to
`cli-handoff-v1/receipt.json`, keeps the same 51 actual hypotheses and image hashes, and
uses the exact supported `pathBase`. Its sidecar links the parent manifest and replay
receipt. `r0-replay/prepare_cli_manifest.py` records this metadata-only derivation; it does
not rerun recognition or rewrite the original outputs.

The single-process CPU run uses two Torch threads and batch size 1. Input verification took
40.04 ms, cold model loading 1962.60 ms; warmups took 10.55/1.27/1.21 ms. Measured inference-only
p50/p95 were 1.15/1.23 ms per line over 153 requests. Those timings exclude file verification,
image preparation, loading and warmup; the frozen floats were consumed directly. No matched
speed-improvement claim follows. Peak RSS is **unavailable**: the runtime API returned zero
(recorded as null), and the sandbox denied `/usr/bin/time`'s `kern.clockrate` query after
successful harness completion. The wrapper exits1 for that resource-query failure; the
completed run log and receipt distinguish it from an inference failure. Receipt SHA-256:
`06d84227de9abc3490267ecd9b2237df32c4c03857431b965a05fad15e65644f`.

A separately authorized **run-2**, using the already built harness with reviewed process
permissions, closes the **R0 process-only RSS gap**. `/usr/bin/time -l` reports maximum RSS
**186,515,456 bytes (177.88 MiB)**, peak memory footprint 119,210,968 bytes and 0.55 s wall time.
Its153 measured outputs exactly match run-1 and the same 51 historical strings. Warm
inference-only p50/p95 were 1.01/1.76 ms; model loading took 171.02 ms. The internal peak-RSS API
still returns zero/null, so the external Darwin byte measurement is authoritative here.
`r0-replay/resource-followup.json` links both preserved receipts and the raw resource log;
run-2 receipt SHA-256 is
`4a5d87deb577bb9d564befb624cbfadb768ceff0fb98b69d70055ec3076131e2`.

The host was not isolated: root reported Qwen3-VL already resident in LM Studio and a
Mistral download in progress. These RSS figures cover **only the R0 process**, excluding
LM Studio/GPU/combined-host memory. Prior replay warmed filesystem/library caches, and
permissions differ from run-1. Neither the faster loading nor timing differences establish
a performance improvement. The CLI baseline remains the run-1-derived strict manifest;
its hypotheses are identical, and no first-run evidence was overwritten.

The subsequent **offline R0 scoring** is in `b/r0-score/run-1/`: all three repetitions and
all 51 lines are scored with the current bounded C# scorer, including per-physical-unit
reports and original dataset split joins. Each repetition yields **123/1,504 edits on the
44 training lines (8.18%)**, **179/219 on the seven repeatedly used development lines
(81.74%)**, and **302/1,723 pooled (17.53%)**. The pooled result mainly measures training
fit and cannot serve as unseen-text or diplomatic transcription accuracy. See the
[scoring report](../evaluation/vlm-benchmark-scoring.md#frozen-r0-diagnostic-context) for all
five physical units and profile limitations. No new inference or training was involved.
Scores SHA-256: `4edd8ccc947083fc57634f2c056a35440ee9fefbf36e33eb20a28557389abcf3`;
receipt: `c4402bb06bc54f70d3dbc5a38ae6d23a286e841d871dc1e288325eed79456245`.

Reproduce in a **new output directory**, with no fitting:

```sh
dotnet build .artifacts/vlm-benchmark/vlm-bench-1/b/r0-replay/Replay.csproj -p:NuGetAudit=false -c Release
dotnet .artifacts/vlm-benchmark/vlm-bench-1/b/r0-replay/bin/Release/net10.0/Replay.dll /absolute/repository /absolute/new-output-directory
```

The harness refuses an existing output directory and checks pinned inventory/manifest,
weight/metadata, crop and float hashes before inference. It never invokes a trainer or
writes old corpus/checkpoint files. The artifact build passed with zero warnings/errors.

## Evidence and remaining boundaries

| Check | Result / receipt |
| --- | --- |
| Source release integrity | 316 file-hash checks; all 51 crops and27 exact maps match; `VLM-B1-r1/integrity-audit.json` |
| Corpus request manifest | SHA `b5fcf13a65b56e61e6a70427b7558b6f2268d684a19febd63a8b41f9f3b3ea09` |
| Corpus freeze | SHA `88338ff885e05b6dbed8a94651f70baefc3242983f86ed4287780fd2842dc89b` |
| Existing-API protocol reproduction | 2 expected behavioral failures, 3 controls passed; `baseline-probe/results/baseline.trx` |
| Scorer work-budget reproduction | 6 expected failures, 33 controls passed; `tests/budget-baseline.trx` |
| Current scorer focused tests | 39 passed, 0 failed/skipped; `tests/budget-candidate-focused.trx` |
| Current full Evaluation regression suite | 78 passed, 0 failed/skipped; `tests/budget-candidate-full.trx` |
| Current scorer source snapshot | `b0f0ef435119bf560a71c5dd714aee85d12f21ffd104ff1988e78d984f98b1f0` |
| Current scorer test snapshot | `76c2d672a66f252dc1718abc459fd03ea319fceb9db20465a739ceea5161a35e` |

Build logs contain no compiler warnings. `scorer-verification.json` retains the baseline
commit/source hashes and first candidate evidence; `budget/verification.json` records the
subsequent work-bound fix and preserves its pre-fix source. The two initial baseline failures probe unchanged
old APIs because the new scorer type did not exist; a missing-type compile error is not
claimed as evidence. The old page scorer's intentional page-layout denominator is preserved.

Instance A read the scorer implementation independently and found no concrete defect in
full-denominator/status handling, blank controls, masked-manual outcomes or scalar I/D/S.
Its requested clarification—that exact text can match on a failed attempt while completion
coverage remains failed—is explicit in the scoring report. Full solution/CLI integration
and any runtime inference are root's separate responsibility.

Root's subsequent resource review identified that rolling memory alone did not bound
quadratic edit work. The final scorer preflights at most 1,000 samples, 8,192 NFC reference
scalars, 1,048,576 NFC hypothesis scalars and 16 million cumulative initialized alignment
cells before aligning any row. An over-budget run fails as a whole and retains raw evidence;
no hypothesis truncation or denominator reduction occurs. A full 1,048,576-scalar failed output
against a short two-scalar reference remains supported. The
[scoring report](../evaluation/vlm-benchmark-scoring.md) documents the exact limits and
distinguishes transport completion from prompt obedience or scholarly correctness.

All 81 original specialist decisions remain pending. All51 current targets are provisional,
all five proposed physical-leaf/unit groups are source/development exposed, and 44 lines
trained this R0. Folio/source-caption mapping remains pending; numeric grouping is not a
certification of physical-leaf identity.
Historical lowercase/sigma/spacing references differ from literal visible P46. Report raw
scores as **diagnostic source-reference distance**, never as natural visible-transcription
accuracy or a basis for an adoption claim. B made no case/sigma/space fold to improve scores.

The new `specialist-audit-worksheet.csv` has 51 pending rows and blank scholarly fields; it
cross-references the original packet and does not supersede it. `correction-effort-worksheet.csv`
is a header-only future blinded-workflow template. `other-corpus-inventory.json` records
existing P01/M01/M02a/I01 source registers without opening their images, PAGE labels or
test-split contents. It adds no eligible lines. Reserved P46 leaves78–80 remain unopened.

The outstanding human/corpus work is exact visible-text/geometry adjudication, second
review, named custodian and fresh disjoint material-specific sets; natural grounding controls,
correction effort, mark fidelity and statistical precision remain unestablished. Runtime
and model capability, package identity, resource limits and actual comparative outputs are
recorded by A/root. No training, model downloads, publication or release follows from this
handoff.

## First-sweep comparative report

The [2026-11-04 first-sweep report](../evaluation/vlm-pilot-results-2026-11-04.md)
freezes all six natural VLM conditions: 306 planned rows, 301 transport completions and
5 retained nonempty truncations. It joins the same 51 provisional source references and
authentic R0 outputs, reports the original 44 training and 7 development lines separately,
and provides all five physical-unit groups. These are raw source-reference distances,
not diplomatic transcription accuracy or evidence of model superiority.

Artifact-only C# reporting lives under `b/comparison-report/`. Register r2 pins each closed
run, resources and original data; `run-r2/comparison.json` and its receipt retain full
denominators and statuses. No new model calls, training or decisions were made by this
reporter. This frozen revision did not include later secondary controls. The completed
repeatability extension below is reported separately and preserves these first-sweep results.

The report compiled with no warnings/errors and passed 13 self-tests. Read-only peer
review found a pending diagnostic-rendering crash; the failing baseline, corrected guard
and candidate result are retained. Synthetic denominators derive from the actual plan.
Independent validation passed 209 arithmetic/partition/status checks. The frozen managed
runtime under `producer-r2/runtime/` passes the same 13 tests and reproduces byte-identical
comparison JSON, Markdown and register without native training/image libraries.

The corrected Mistral synthetic run is primary; its original run is retained as a client
diagnostic because valid empty SSE was initially misclassified. Original statuses were
not rewritten. Root's four live producer snapshots distinguish the AI parser revisions.
All original specialist decisions and manuscript accuracy gates remain pending.

## Repeatability companion and independent packet audit

The separately [registered repeatability extension](../../.artifacts/vlm-benchmark/vlm-bench-1/root/repeatability-registration.json)
planned six conditions with three repetitions each, using the same 51 provisional lines.
All six are now closed in the [companion report](../evaluation/vlm-repeatability-results-2026-11-04.md),
final register/scoring/stability revision **r6**. Its **918 planned slots** have 895 Completed,
16 Truncated, one deadline Cancelled and six explicit NotAttempted records; no JSONL rows
are missing. Qwen2.5 V2 reached its eight-minute allocation after 147 attempts. The four
earlier conditions had 20-minute caps and Qwen2.5 V1 had 12 minutes, so unequal allocations
and deadline tails preclude a clean full-condition rate/quality comparison. Every planned
denominator remains intact; unattempted slots are not observed model errors. This extension does
not change the first-sweep reports, source labels, historical train/development split,
recognizer weights or specialist decisions.

Artifact-only programs under `b/repeatability-report/` reuse the preserved strict CLI
scorer and evaluate exact ordinal raw-string/status stability separately. Each of the
three repetitions remains visible, with source-distance ranges, proposed physical-unit groups,
original 44/7 split groups and timing quantiles. Null/missing/unattempted triplets cannot
become stable successes; stable failed output is distinct from all-completed stability.
The comparison entry point passes 13 self-tests and the separate stability program 14;
final builds have zero warnings/errors. Managed runtime/source snapshots are preserved.
The final revision passed **408 arithmetic/partition/status checks** and **5,579 independent
raw-hash/group/stability checks**. Five proposed physical-leaf/unit groups, with folio/source-caption
mapping pending, are not five certified independent leaves; all seven development lines
belong to one proposed group.

Read-only peer review found no concrete defect in repeatability identity joins,
denominators, missing outcomes, status separation or timing. Changed host/cache/download
conditions remain explicit confounds; the Qwen3 first-sweep/repeatability latency change
is not a code speedup or a clean comparison between models. Stable text does not establish
visible correctness, image grounding or absence of memorized biblical completion.

B also independently audited A's frozen 51-line specialist packet with **2,886 mechanical
checks**: crop/reference/R0 joins, all 306 exact raw model strings/statuses/transport
objects/row hashes, 86 generated files and 94 original inputs. All reviewer and decision
fields remained null, acceptance flags false and the original 81-decision hash unchanged.
The receipt is `b/specialist-packet-independent-audit.json`. This was an evidence-integrity
audit, not Greek adjudication; the packet itself was not modified.

Further read-only audits verified **1,309 checks** over C's 195 synthetic prompt receipts,
**4,980 checks** over A's 306-output Unicode inventory, and **1,996 checks** over all 90
geometry outputs and their paired metrics. The receipts are respectively
`b/prompt-report-independent-audit.json`, `b/output-diagnostics-independent-audit.json`
and `b/geometry-final-independent-audit.json`. The original Mistral empty-SSE client defect
remains a separate historical diagnostic, and no raw status was rewritten. These checks
establish receipt/scoring consistency, not scholarly correctness or a preprocessing default.

## Final resource appendix

The [resource appendix](../evaluation/vlm-resource-observations-2026-11-04.md) reads only
existing traces for all 12 closed natural conditions. Frozen
`b/resource-observations/run-r4/summary.json` has SHA-256
`9f8112bef273b11af3e97fd056f153a9ed7408dc44d46405f6010744da65b352`;
its receipt pins the original traces and three separately scoped idle-worker footprint
snapshots. Eight parser/boundary assertions and **281 independent raw timing, denominator,
VM/RSS and exact footprint-counter checks** passed. Earlier revisions remain intact.

Latency reports include nearest-rank p50/p95, maximum, first request and elapsed sums/wall
time, retaining failed durations and leaving unattempted durations unavailable. Global
Pageins/Swapins/Swapouts and displayed swap-used deltas are unattributed host observations.
Sampled summed RSS can double-count shared pages and miss peaks; it is not GPU memory.
The footprint snapshots preserve `phys_footprint`, worker-lifetime peak and the tool's
`swapped`, `clean` and `wired` counter names. They cover different worker workloads:
the final Mistral worker ran only geometry controls. None establishes model-specific
memory requirements, a code speedup or a clean cross-model resource comparison.
