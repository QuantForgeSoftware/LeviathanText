# VLM diagnostic review packet

The artifact-only C# generator is
`.artifacts/vlm-benchmark/vlm-bench-1/a/review-packet-generator/`. It produces a static,
self-contained HTML packet and a JSON worksheet for the **51 provisional P46 lines** in
`VLM-B1-r1`. It does not change product code, infer new readings, run a model, train, or
alter the original specialist packet and its 81 pending decisions. The first-sweep packet
was generated on 2026-11-04 after the integrator froze all six natural-line result
directories; a printable PDF companion is also available.

This is an **unblinded diagnostic/adjudication packet**, not a blinded correction-time
trial. Source text and model identities are visible. It must not be used to claim measured
correction effort or accepted diplomatic accuracy. The current source transcription,
derived scoring reference, R0 output and VLM outputs remain distinct.

## Inputs and strict joins

The generator consumes the frozen B release (`manifest.json`, `references.json`,
`provenance.json`, `integrity-audit.json`, `FREEZE.json`), the strict R0 CLI manifest and
the named VLM run directories. It checks:

- Frozen release file hashes and exactly 51 unique image/reference/provenance/R0 IDs.
- Image SHA-256, PNG dimensions, physical unit, material, source-record identity and
  reference-text hash. It copies exact PNG bytes without resizing or re-encoding.
- Verified full-page context images and retained attribution records from provenance.
- Each run's input-manifest hash against the frozen V1 release or strict R0/V2 manifest,
  and its planned/result sample, repetition, mode and image joins.
- Nested transport receipt agreement with the row's sample, model, image, text and status.
- Frozen system/user prompt hash agreement under `vlm-diplomatic-1`, and no captured text
  on a locally rejected input without a transport receipt.
- The original decision worksheet's hash before and after generation.

Duplicate/unknown/misjoined rows and changed source bytes reject generation. A missing run,
missing planned sample or missing result remains an explicit row for every affected sample;
it never turns into an inferred empty reading. `CompleteRows` in the packet's artifact
summary means that all expected rows were recorded, not that their generations succeeded.
Each actual status and its full captured model text remain visible.

The output directory must be new. The generator prepares its own temporary directory and
publishes it with a directory move; it never cleans up or overwrites an existing packet.
Run it only after the integrator freezes the input result directories. The source run
files are retained as exact byte snapshots inside the packet, with hashes in its receipts.

## Generation

Build and synthetic verification, from the repository root, during a coordinated .NET slot:

```sh
dotnet run --project .artifacts/vlm-benchmark/vlm-bench-1/a/review-packet-generator/ReviewPacketGenerator.csproj -c Release -p:UseSharedCompilation=false -- --self-test
```

The generator references the current Release Core/Contracts DLLs only to reuse the existing
nonmutating Greek profile inspector. It makes no product project modifications or network
requests. The output records generator/Core assembly hashes. The final self-test passed
17 checks covering HTML escaping, no scripts, 51 unique sections/crops, null decisions,
raw-text preservation, Latin-lookalike code points, missing rows, exact crop hashes,
changed/duplicate/mismatched inputs, wrong prompt receipts, receipt-free rejected text,
and refusal to overwrite an existing directory. The two added guard cases failed before
correction. Evidence: `a/review-packet-generator/guards-baseline.log` and
`a/review-packet-generator/guards-candidate.log` (initial 15-check run: `self-test.log`).

The frozen first-sweep packet was generated with this command, without rebuilding:

```sh
dotnet .artifacts/vlm-benchmark/vlm-bench-1/a/review-packet-generator/bin/Release/net10.0/ReviewPacketGenerator.dll \
  --release .artifacts/vlm-benchmark/vlm-bench-1/b/VLM-B1-r1 \
  --r0 .artifacts/vlm-benchmark/vlm-bench-1/b/r0-replay/cli-handoff-v1/manifest.json \
  --out .artifacts/vlm-benchmark/vlm-bench-1/a/specialist-review-packet-v1 \
  --run qwen3-v1=.artifacts/vlm-benchmark/vlm-bench-1/root/qwen3-p46-v1 \
  --run qwen3-v2=.artifacts/vlm-benchmark/vlm-bench-1/root/qwen3-p46-v2 \
  --run qwen25-v1=.artifacts/vlm-benchmark/vlm-bench-1/root/qwen25-p46-v1 \
  --run qwen25-v2=.artifacts/vlm-benchmark/vlm-bench-1/root/qwen25-p46-v2 \
  --run mistral-v1=.artifacts/vlm-benchmark/vlm-bench-1/root/mistral-p46-v1 \
  --run mistral-v2=.artifacts/vlm-benchmark/vlm-bench-1/root/mistral-p46-v2
```

This output now exists and cannot be overwritten by the generator. Later corrected
snapshots need a new output directory and version. Synthetic misleading-OCR controls and
the separately registered repeatability sweep do not enter this first-sweep packet.

## Frozen first-sweep deliverables

- [Self-contained HTML](../../.artifacts/vlm-benchmark/vlm-bench-1/a/specialist-review-packet-v1/index.html)
- [Unfilled JSON worksheet](../../.artifacts/vlm-benchmark/vlm-bench-1/a/specialist-review-packet-v1/worksheet.json)
- [Generation receipt](../../.artifacts/vlm-benchmark/vlm-bench-1/a/specialist-review-packet-v1/generation.json)
- [Printable PDF](../../.artifacts/vlm-benchmark/vlm-bench-1/a/specialist-review-packet-v1-pdf/output/pdf/p46-first-sweep-specialist-review.pdf)

The packet contains 51 exact hashed line crops, nine source-page contexts (1440 through
1520), five proposed physical units and all 306 first-sweep outputs. No result is missing.
The status counts come from the frozen records:

| Condition | Completed | Truncated |
| --- | ---: | ---: |
| Qwen3 V1 | 51 | 0 |
| Qwen3 V2 | 51 | 0 |
| Qwen2.5 V1 | 49 | 2 |
| Qwen2.5 V2 | 49 | 2 |
| Mistral V1 | 51 | 0 |
| Mistral V2 | 50 | 1 |

An independent artifact audit verified all 86 copied/generated file hashes, all 51 crop
joins, 306 output entries, null reviewer/decision fields and the unchanged original
81-decision worksheet. It found no scripts, event handlers or external resources in the
HTML. Evidence: `a/review-packet-generator/final-audit.py` and `final-audit.json`.
An independent second-agent audit passed 2,886 mechanical checks, including all 306 exact
raw strings/statuses/transport receipts, all 86 generated asset hashes and 94 original
input hashes, with all reviewer/decision fields null and acceptance flags false. Evidence:
`b/specialist-packet-independent-audit.json`. Neither audit adjudicates Greek readings.

The HTML SHA-256 is
`10db14b91949e357254d0409233b1f4abedd7a4b49ab63323fba87eec8b34895`;
the worksheet SHA-256 is
`c721917f5dc6c3915d1d4a0c0ec642901debcc0f3c884270eee8e97b8a498651`.

## Printable companion and verification

The static PDF is 115 A4 pages: cover, approximately two pages per line, nine full-page
contexts shown once each, and retained attribution notes. It includes every provisional
source transcription, derived diagnostic reference, R0 text, raw VLM output and status,
including the complete truncated outputs, plus the specialist questions. Compact profile
and geometry notes point to the immutable HTML/worksheet for full wire receipts, Unicode
inventories and detailed geometry. There are no interactive form fields or scripts.

The PDF was rendered with local headless Chrome 156.0.8078.80 using the artifact-only
`a/specialist-review-packet-v1-pdf/create-pdf.mjs`. No model calls, source-image edits or
scholarly corrections were performed. The browser assertion verified that all 408 direct
source/R0/model text blocks were unchanged by the print transformation. The original HTML
and worksheet hashes remained unchanged.

Verification found all 51 sample IDs, 60 images, nine source contexts and all 306 statuses.
All 115 pages passed a character-bounds check. Latest renders of the cover, first line,
longest truncated responses, source contexts and final page were inspected without
clipping, overlap, missing images or missing-font tofu. Black diamonds with question marks
in the output are literal U+FFFD emitted by the models; they have not been substituted for
unrenderable Greek glyphs.

PDF extraction preserves 458 of 459 source/reference/R0/model blocks exactly after removing
layout whitespace and footers. The remaining Mistral V1 block for `vlm-s0021` canonically
composes epsilon plus psili during PDF shaping/extraction; it is NFC-equivalent, with no
noncanonical mismatch. **Use the unchanged HTML/worksheet for exact Unicode sequences and
spacing.** The PDF is a viewing/printing companion, not a replacement data source.

The PDF SHA-256 is
`c55739aa96c00f3b693b5d3b34a62d08bf53888ff825f86d7eb070a5c2186ef3`.
Exact authoring/verification commands, source hashes, checks and retained render evidence
are in `a/specialist-review-packet-v1-pdf/pdf-generation.json`, `pdf-qa.json`,
`verify-pdf.py` and `qa/`. These are separate from the frozen HTML packet.

## Packet use

`index.html` embeds all line crops and full-page context as data URIs; it uses no JavaScript,
external images, fonts or styles. Text, provenance and raw receipts are HTML escaped.
`crops/` contains the exact line PNGs; `inputs/` preserves the source/run snapshots;
`worksheet.json` provides the unfilled decision fields; `generation.json` lists file hashes,
source joins, missing conditions and preservation of the original decision worksheet.

For each line, a specialist should inspect the image before comparing suggestions and
record which letters have visible support, which accents/breathings and spaces are written,
the actual majuscule/lunate forms, visible abbreviations/overbars, and any crop clipping,
neighboring-line contamination or source-alignment error. Geometry retains its limitations:
an initial quadrilateral is not a verified final polygon, and an absent per-pixel map is
not repaired by this packet. Keep proposed expansions/restorations separate from visible
transcription and leave unresolved spans unresolved.

The existing `GreekTextProfile.Inspect` report accompanies each raw VLM text. Scalar offsets
and `U+....` code points expose Latin lookalikes and otherwise similar characters without
rewriting them. Its warnings are **profile observations, not semantic failures or proposed
corrections**. In particular, U+FFFD—the current prompt's uncertainty marker—is classified
as `Other`; that flag alone says nothing about whether an uncertainty mark is appropriate.

Every reviewer/date/decision field begins as null; acceptance and training flags remain
false. Save specialist work as a new attributed copy, then arrange independent review and
adjudication under the corpus policy. The original 81 decisions remain pending until their
own review workflow records actual specialist decisions. Model agreement, this packet, and
automatic profile inspection cannot close them.

Image attribution and retained PDM1.0 evidence identify the University of Michigan Library
Digital Collections; source-transcription attribution and CC BY 4.0 evidence identify the
Institut für Neutestamentliche Textforschung. These asset-specific records accompany the
packet and are not replaced by the software license.
