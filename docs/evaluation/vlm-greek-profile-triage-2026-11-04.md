# Greek profile flags on closed compact glyph output

The existing Greek diplomatic profile found mechanical issues on 30 of 126 incorrect generated-glyph responses and on none of the 66 exact responses. Among the 162 glyph responses without an issue, 96 were incorrect. This is a retrospective check of a closed synthetic study. The output flags describe the captured strings and leave their readings intact. [Compact study](vlm-cli-format-compact-2026-11-04.md).

## All planned rows

The four batches each contribute 48 generated glyph responses and two blank responses. The eight blank responses repeat the same blank raster across conditions. The glyph rows reuse fixtures across conditions, so counts describe responses and do not represent independent source samples. Every row completed in this study. Exact means NFC exact match in the original score receipt with valid completion.

| Flag | Glyph incorrect flagged / 126 | Glyph exact flagged / 66 | Glyph incorrect without flag | Blank incorrect flagged / 4 | Blank exact flagged / 4 |
| --- | ---: | ---: | ---: | ---: | ---: |
| ASCII Latin | 14 | 0 | 112 | 0 | 0 |
| Both ASCII Latin and Greek profile categories | 9 | 0 | 117 | 0 | 0 |
| Other profile issue excluding U+FFFD | 0 | 0 | 126 | 0 | 0 |
| U+FFFD replacement character | 16 | 0 | 110 | 2 | 0 |
| Unpaired combining mark | 0 | 0 | 126 | 0 | 0 |
| Any profile issue | 30 | 0 | 96 | 2 | 0 |

The mixed category is a subset of ASCII Latin and the separate flags can overlap. U+FFFD is an explicit scalar check and the profile also categorizes it as Other. Its presence in captured text does not identify where it originated. The mixed category uses this profile's range classifications and does not implement general Unicode script detection.

| Model and profile | Exact glyphs / 48 | Incorrect glyphs with any issue | Incorrect glyphs without any issue | Incorrect blanks with any issue / 2 planned | Incorrect blanks without any issue / 2 planned |
| --- | ---: | ---: | ---: | ---: | ---: |
| Qwen3-VL 8B plain text | 23 | 6 | 19 | 2 | 0 |
| Qwen3-VL 8B transcription JSON | 24 | 6 | 18 | 0 | 0 |
| Mistral Small 3.1 24B plain text | 10 | 12 | 26 | 0 | 2 |
| Mistral Small 3.1 24B transcription JSON | 9 | 6 | 33 | 0 | 0 |

For example, Mistral plain text returned `κονάμεχ` for both `κονἀμεχ` and `κονἁμεχ` in the Times smooth/rough pair. Both incorrect responses were profile-clear. The same batch returned the profile-clear Greek word `ἀνθρώπων` on both blank slots. A future held-out comparison can use the same flags per route and report all four exact-by-flag cells, including incorrect outputs without an issue. This study gives no basis for automatically accepting, rejecting or correcting output.

## Reproduction and receipts

The local C# diagnostic references the existing `GreekTextProfile.Inspect` implementation at profile `greek-diplomatic-1.0-draft`. It verifies each results and reference SHA-256 against the score receipt and joins the score, results and references by sample ID. For plain text it inspects the captured string. For transcription JSON it inspects the exact decoded transcription from the receipt after checking the raw JSON round trip. The report preserves reference text, raw model string, inspected text, completion, control outcome, NFC exact match, edit distance, category counts and scalar-offset issues for every row. No normalization or trim is applied before inspection.

The diagnostic built with zero warnings or errors. A repeated run produced byte-identical report JSON. A separate recount verified all four 50-row batches, the flag denominators and U+FFFD detection. Report SHA-256 is `6778d39e887f7516f3f9dc305bd9598d5a740be3f931d380641adbc2c77c0ac3`. No model calls were made. [C# diagnostic](../../.artifacts/vlm-benchmark/greek-profile-triage-v1/Program.cs) · [Full report](../../.artifacts/vlm-benchmark/greek-profile-triage-v1/report.json) · [Hash receipt](../../.artifacts/vlm-benchmark/greek-profile-triage-v1/receipt.sha256) · [Input score receipts](../../.artifacts/vlm-benchmark/cli-format-compact-v1/live.mH3gv5).
