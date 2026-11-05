# Synthetic line geometry diagnostic - 2026-11-04

This registered follow-up compares three representations of the **same ten generated
font fixtures**. It does not measure manuscript quality or select a preprocessing default.
The registration is
`.artifacts/vlm-benchmark/vlm-bench-1/root/geometry-registration.json`, study
`synthetic-line-geometry-v1`: three models, three variants, ten fixtures, one repetition,
90 planned requests. The original `vlm-diplomatic-1` image-only prompt, sampling seed 42,
order seed 42 and 512-output-token limit are retained. Reference labels are scoring-only.

## Fixtures and independent source verification

Eight fixtures contain 50 seeded random Greek uppercase letters, including lunate sigma;
one contains polytonic lowercase text; one is blank. Times New Roman, size 40, is rendered
on a flat beige background. These are generated typography, not handwriting or P46 crops.
All three variants retain the same sample IDs, reference strings and text/blank roles.

| Variant | Dimensions | Declared transformation |
| --- | --- | --- |
| raw | 1695 × 64 | Original generated strip |
| pad | 1695 × 512 | Exact strip translated down 224 pixels; white margins |
| scale2 | 3390 × 128 | Each original pixel replicated into an exact 2 × 2 block |

The artifact-only generator is `root/geometry-generator/`; the immutable fixtures and
generation receipt are in `root/geometry-v1/`. The reporter independently decoded the PNGs
with ImageSharp, while the generator used SkiaSharp. Before opening any inference receipt,
it verified:

- Registration, generator binary, installed font, manifests, references and all 30 PNG hashes.
- All 30 actual image dimensions and all 30 paired reference identities, including the
  seeded generated strings and blank role.
- All 5,424,000 translated/replicated source-pixel comparisons and 7,593,600 white-margin
  pixel comparisons; the original blank has no character traces.

The first independent audit completed at `2026-11-04T15:24:08.521964Z`. Evidence:
`a/geometry-report/pending-r1/geometry-audit.json`. Twelve artifact self-tests passed,
including deliberate changed source pixels, incorrect white margins, altered scaling,
invalid dimensions, changed references and missing paired outputs. Independent review
found that two absent captures could be labelled identical; the new test failed before
correction, then passed with an unavailable (`null`) comparison when either capture is
absent. Evidence: `a/geometry-report/missing-pair-baseline.log` and `self-tests.log`.
No model call or P46-data change was made by the reporter.

## Offline reporting and current state

The C# reporter is `a/geometry-report/GeometryReport.csproj`. It admits only conditions
explicitly marked closed by the integrator, pins their input/run/result/terminal-summary
files, invokes the current strict CLI `benchmark vlm-score`, and deserializes the current
Evaluation scorer's typed result. Inputs are checked again after scoring. Every pending
condition remains unscored; its inference receipts are not opened.

The initial `a/geometry-report/pending-r1/` snapshot and corrected-reporter
`a/geometry-report/pending-r2/` snapshot deliberately score **zero of nine conditions**.
They verify source-only auditing and pending-state handling without reading in-progress
inference results. The integrator subsequently froze **all nine conditions and all 90
requests**. The final snapshot is [final-r4/results.md](../../.artifacts/vlm-benchmark/vlm-bench-1/a/geometry-report/final-r4/results.md),
with exact per-ID outputs and metrics in `final-r4/comparison.json` and input/output pins
in `final-r4/receipt.json`. Earlier partial snapshots are preserved. No condition remains
pending, and no further live calls are part of this report.

| Closed condition | Text CER (9 fixtures) | Exact text | Blank pass | Transport statuses | Latency P50 / P95 |
| --- | ---: | ---: | ---: | --- | ---: |
| Qwen3/raw | 15.58% | 0/9 | 0/1 | Completed 10/10 | 6288.9 / 7557.3 ms |
| Qwen3/pad | 9.30% | 0/9 | 0/1 | Completed 10/10 | 1344.4 / 1942.1 ms |
| Qwen3/scale2 | 8.37% | 0/9 | 0/1 | Completed 10/10 | 879.7 / 1076.4 ms |
| Qwen2.5/raw | 21.16% | 0/9 | 0/1 | Completed 10/10 | 560.1 / 763.9 ms |
| Qwen2.5/pad | 30.47% | 0/9 | 0/1 | Completed 10/10 | 5927.2 / 6938.5 ms |
| Qwen2.5/scale2 | 32.79% | 0/9 | 0/1 | Completed 10/10 | 4788.9 / 5688.3 ms |
| Mistral/raw | 30.70% | 0/9 | 1/1 | Completed 10/10 | 1488.3 / 2433.1 ms |
| Mistral/pad | 33.26% | 0/9 | 1/1 | Completed 10/10 | 10002.4 / 12422.0 ms |
| Mistral/scale2 | 82.79% | 0/9 | 0/1 | Completed 10/10 | 1711.7 / 2565.4 ms |

Each variant has the same 430 reference scalars across nine text fixtures. Their total edit
distances are 67, 40 and 36 for Qwen3; 91, 131 and 141 for Qwen2.5; and 132, 143 and 356
for Mistral, in raw/pad/scale2 order. Paired changes use destination minus source:

| Model and pair | Absolute CER change | Text IDs improved / worsened / tied |
| --- | ---: | ---: |
| Qwen3 raw to pad | −6.28 percentage points | 7 / 1 / 1 |
| Qwen3 raw to scale2 | −7.21 percentage points | 6 / 1 / 2 |
| Qwen3 pad to scale2 | −0.93 percentage points | 3 / 4 / 2 |
| Qwen2.5 raw to pad | +9.30 percentage points | 4 / 4 / 1 |
| Qwen2.5 raw to scale2 | +11.63 percentage points | 2 / 5 / 2 |
| Qwen2.5 pad to scale2 | +2.33 percentage points | 3 / 5 / 1 |
| Mistral raw to pad | +2.56 percentage points | 3 / 4 / 2 |
| Mistral raw to scale2 | +52.09 percentage points | 0 / 9 / 0 |
| Mistral pad to scale2 | +49.53 percentage points | 0 / 9 / 0 |

The direction of aggregate change differs between models on the same fixtures. Neither
transformation gives a uniform improvement across models or text lines; Mistral's scaled
variant has greater edit distance on all nine text fixtures and also fails the blank that
its raw/padded variants pass. This is a paired synthetic observation, not a manuscript
quality conclusion. An independent Python implementation verifies the final report's NFC scalar
distances, exact raw-output joins, blank handling, nearest-rank latencies, paired changes
and every pinned input/output hash; evidence is `a/geometry-report/audit-report.py` and
`final-r4-independent-audit.json` (958 mechanical checks). These synthetic-font results do not establish manuscript
quality or justify a geometry default. `Completed` only describes transport protocol, not
textual correctness.

A separate second-agent audit passed **1,996 checks** over all 82 source pins, 90 exact raw
strings/statuses/latencies, independently computed NFC distances and complete denominators,
all nine paired summaries, blank outcomes and nearest-rank timing. No concrete blocker was
found. Evidence: `b/geometry-final-independent-audit.json`. The frozen final comparison
SHA-256 is `06a8cfd60437a9529e004434ef996cc7ac1ba7472ba4f805766db79e5b848905`.

Reproducible commands, from the repository root:

```sh
dotnet run --project .artifacts/vlm-benchmark/vlm-bench-1/a/geometry-report/GeometryReport.csproj -c Release -p:UseSharedCompilation=false -- --self-test
dotnet .artifacts/vlm-benchmark/vlm-bench-1/a/geometry-report/bin/Release/net10.0/GeometryReport.dll /Users/johnbaima/Code/LeviathanText .artifacts/vlm-benchmark/vlm-bench-1/a/geometry-report/final-reproduction-new --closed qwen3/raw,qwen3/pad,qwen3/scale2,qwen25/raw,qwen25/pad,qwen25/scale2,mistral/raw,mistral/pad,mistral/scale2
```

Output directories must be new. The final command names all nine explicitly frozen
conditions; omitting `--closed` reproduces the source-audit/pending-only mode. The reporter
records its command, source/assembly hashes and every input/output hash. It does not
rebuild product projects or call a model.

## Interpretation limits

The nine text fixtures use the scorer's `NaturalLine` category solely to obtain NFC scalar
edit distance and exact-text counts. Full raw model output, including failed/truncated
output, is scored without case/sigma folding, trimming, accent removal, prose extraction or
a CER cap. The blank is a separate control and passes only with protocol-complete empty
output. Exact-text matching and transport completion remain separate measurements.

Reports retain all ten IDs, raw strings, statuses, blank outcomes and provider latencies.
Paired changes compare the same IDs for raw-to-pad, raw-to-scale2 and pad-to-scale2; these
are three representations, not three independent examples. Missing results in a closed
cohort remain in its denominator; pending cohorts receive no scores.

Provider elapsed times include encoding/HTTP and failed attempts with a receipt, but exclude
model loading and pre-request file validation. P50/P95 use nearest rank; the first recorded
request is shown separately without assuming it is cold. One repetition, sequential variant
blocks and concurrent host work limit timing comparisons. No confidence intervals,
manuscript-accuracy estimates, model-selection decision or default preprocessing change are
derived from this study.
