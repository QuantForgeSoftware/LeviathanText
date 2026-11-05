# First-sweep output diagnostics - 2026-11-04

This is a nonmutating inventory of the **306 frozen first-sweep outputs** for 51 provisional
P46 lines. It identifies specialist review priorities; it does not adjudicate Greek,
change labels or scores, select a model, or close the original 81 pending decisions.
`Completed` describes transport protocol, not transcription correctness. V1 is image-only;
V2 is OCR-assisted. Synthetic controls and later repeatability runs are outside this inventory.

The authoritative artifact is
[run-r2/inventory.json](../../.artifacts/vlm-benchmark/vlm-bench-1/a/output-diagnostics/run-r2/inventory.json).
It retains every exact raw string, SHA-256, condition/status, sample/source ID, frozen profile
annotation and scalar inventory, plus all flagged examples. The original
[specialist packet](../../.artifacts/vlm-benchmark/vlm-bench-1/a/specialist-review-packet-v1/index.html)
provides crops, full-page context and the unfilled decision worksheet.

## Observed Unicode features

Counts are scalar occurrences; parentheses give the number of affected outputs out of 51.
The lookalike column is a deliberately limited **candidate heuristic**: uppercase ASCII
`ABCEHIKMNOPTXYZ`. It is not a complete confusables analysis or an error count.

| Condition | ASCII Latin letters (outputs) | Lookalike candidates | U+FFFD markers (outputs) | Prose candidates | Length outliers |
| --- | ---: | ---: | ---: | ---: | ---: |
| Qwen3 V1 | 1 (1) | 1 | 5 (5) | 0 | 0 |
| Qwen3 V2 | 358 (1) | 9 | 2 (1) | 1 | 1 |
| Qwen2.5 V1 | 134 (6) | 126 | 1,179 (14) | 1 | 2 |
| Qwen2.5 V2 | 106 (2) | 0 | 1,030 (5) | 0 | 2 |
| Mistral V1 | 476 (3) | 7 | 35 (22) | 3 | 2 |
| Mistral V2 | 70 (1) | 2 | 1,046 (18) | 1 | 1 |

Under this bounded profile, all observed letters outside its allowed categories are ASCII
Latin: 1,145 occurrences across 14 outputs. No additional non-ASCII outside-profile letters
were observed. This is not full Unicode Script classification: the existing profile broadly
covers the Greek/Coptic blocks. Recognized numeral signs are excluded even when Unicode
classifies them as modifier letters; for example, the two U+02B9 signs in Qwen3 V1,
`vlm-s0022` / `P46-1460-L10`, remain numeral signs.

U+FFFD is the prompt's permitted uncertainty marker, not inherently an error. Its 3,297
occurrences across 65 outputs need image-based interpretation. Seven outputs meet the
separate replacement-dominance heuristic; counts are 0, 0, 4, 2, 0 and 1 in table order.

| Condition | Greek uppercase / lowercase | Raw combining marks | Marks in derived NFD inventory | Raw spaces / line feeds |
| --- | ---: | ---: | ---: | ---: |
| Qwen3 V1 | 278 / 1,514 | 0 | 224 | 195 / 0 |
| Qwen3 V2 | 147 / 1,676 | 0 | 236 | 302 / 2 |
| Qwen2.5 V1 | 301 / 969 | 0 | 103 | 1,192 / 0 |
| Qwen2.5 V2 | 92 / 1,320 | 0 | 120 | 1,125 / 0 |
| Mistral V1 | 801 / 634 | 4 | 156 | 191 / 12 |
| Mistral V2 | 819 / 474 | 0 | 68 | 45 / 8 |

Raw whitespace consists only of U+0020 and U+000A in this cohort. No unpaired combining
mark was flagged by the existing profile. The four raw combining marks occur in Mistral V1:
U+0313 twice, U+0305 once and U+0342 once. Most diacritics use precomposed letters, so zero
raw combining marks does **not** mean no diacritics. NFD is used only to inventory canonical
mark components; stored outputs and benchmark scores are untouched. Full codepoint, case,
sigma-form, whitespace and mark counts remain in the JSON.

## Concrete review priorities

1. **Letter identity and script mixing.** Qwen3 V1, `vlm-s0041` / `P46-1500-L05`, ends with
   the exact token `ΘΗΛΕΚΤΗC`: its final character is Latin C, U+0043. Qwen2.5 V1,
   `vlm-s0037` / `P46-1490-L01`, ends with `ΧΡI`, using Latin I, U+0049. Inspect the crop's
   actual traces and record the intended transcription convention; do not automatically
   replace either scalar with a Greek counterpart. The inventory retains all 14 affected outputs.
2. **Literal escape text.** Qwen2.5 V2, `vlm-s0047` / `P46-1510-L09`, contains the six
   literal characters `\ufeff` repeated 21 times (126 scalars), not 21 U+FEFF characters.
   This explains 105 of that condition's 106 ASCII Latin letters. Retain this exact output
   as evidence; decoding those characters would create a different string.
3. **Commentary and long captures.** Qwen3 V2, `vlm-s0003` / `P46-1440-L04`, contains 525
   scalars, including an English note. Mistral V1, `vlm-s0002` / `P46-1440-L03` and
   `vlm-s0050` / `P46-1520-L02`, and both Mistral conditions at `vlm-s0032` /
   `P46-1470-L16`, also include English notes. Preserve full captures while reviewing
   visible transcription separately. The prose heuristic also flags the Latin-heavy
   Qwen2.5 V1 output at `vlm-s0047`; that flag is **not** a claim that the string is prose.
4. **Truncation and uncertainty runs.** All five `Truncated` outputs remain intact:
   Qwen2.5 V1 at `vlm-s0001` / `P46-1440-L02` and `vlm-s0050` / `P46-1520-L02`;
   Qwen2.5 V2 at `vlm-s0005` / `P46-1440-L11` and `vlm-s0045` / `P46-1500-L14`;
   Mistral V2 at `vlm-s0005`. Their lengths are 1,024, 1,024, 1,024, 1,023 and 1,021
   scalars respectively. Inspect the actual line independently; repeated U+FFFD does not
   establish that the corresponding number of unreadable characters exists.
5. **Marks, case and spacing.** Mistral V1 at `vlm-s0041` / `P46-1500-L05` contains
   `Γ̅` (U+0393, U+0305) and `Δ͂` (U+0394, U+0342). Its `vlm-s0018` / `P46-1460-L06`
   and `vlm-s0021` / `P46-1460-L09` contain combining U+0313. These make useful focused
   examples for comparing visible marks against model-supplied marks. Across the cohort,
   inspect capitalization, sigma/lunate forms and written gaps against the images; the
   provisional lowercase, space-stripped scoring references cannot adjudicate those features.

## Rules and verification

The artifact-only C# tool is `a/output-diagnostics/OutputDiagnostics.csproj`. It reuses and
verifies the packet's `GreekTextProfile` annotations and exact scalar offsets, checks all
86 packet assets before and after execution, and keeps the frozen HTML/worksheet hashes
unchanged. Twelve mock checks cover script mixtures, precomposed/decomposed marks,
unpaired marks, whitespace, replacement runs, heuristic boundaries, recognized numeral
signs and nonmutation. The numeral-sign case failed before correction; evidence is
`numeral-baseline.log` and `self-tests.log`. `run-r1/` is retained as the pre-correction
inventory; **use run-r2** for the figures above.
An independent second-agent audit passed 4,980 checks over all 306 exact raw outputs,
source IDs/hashes, per-row Unicode features, aggregates and the literal-escape/numeral
examples. Its receipt is `b/output-diagnostics-independent-audit.json`; it made no scholarly
adjudication. The final inventory SHA-256 is
`6991e46b8ef5c6d4a69b6e667cb762cf0180c30793e26abd469c16b1396dd308`.

Heuristics are explicit and not scholarly validation:

- Prose candidate: matches `note:`, `i have`, `transcrib`, `visible`, `translation`,
  `the image`, `cannot` or `unreadable`, case-insensitively; or at least 20 ASCII Latin
  letters with at least three whitespace scalars.
- Length outlier: raw scalar length exceeds `max(128, 4 × provisional-source length)`.
  The provisional source supplies only a length yardstick.
- Replacement-dominated: at least eight U+FFFD markers and at least half of non-whitespace
  scalars are U+FFFD.

Reproduction, from the repository root, requires a new output directory:

```sh
dotnet run --project .artifacts/vlm-benchmark/vlm-bench-1/a/output-diagnostics/OutputDiagnostics.csproj -c Release -p:UseSharedCompilation=false -- --self-test
dotnet .artifacts/vlm-benchmark/vlm-bench-1/a/output-diagnostics/bin/Release/net10.0/OutputDiagnostics.dll .artifacts/vlm-benchmark/vlm-bench-1/a/specialist-review-packet-v1 .artifacts/vlm-benchmark/vlm-bench-1/a/output-diagnostics/reproduction-new
```

No model was called, no training ran, and no specialist decision was inferred by this tool.
