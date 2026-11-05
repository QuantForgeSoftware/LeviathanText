# Greek PAGE sign and damage encoding inventory

This inventory covers 5,440 direct line strings from 285 previously verified PAGE XML files. The sources are P01 Zenon and four manuscript sets, M01 and M02a–c. M02b and M02c have PAGE and filename receipts only. Inscription geometry and image evidence are outside this inventory. The counts describe encoded source text. No new photographs, accepted labels or specialist readings were produced.

## Diacritics and normalization

The literal raw and NFC combining counts agree. Most diacritics are precomposed in the source. The table therefore also gives canonical NFD counts **only where the mark attaches to a Greek letter**. This excludes marks decomposed from a Latin letter and standalone marks.

| Source | Lines | Literal combining marks, raw/NFC | Acute / grave / circumflex | Smooth / rough | Diaeresis | Iota subscript | Overline | NFC-changed lines |
| --- | ---: | ---: | --- | --- | ---: | ---: | ---: | ---: |
| P01 Zenon | 321 | 3 / 3 | 1 / 0 / 1 | 0 / 0 | 0 | 0 | 2 | 2 |
| M01 EPARCHOS | 2,272 | 0 / 0 | 7,218 / 5,770 / 3,383 | 4,467 / 2,330 | 27 | 387 | 0 | 14 |
| M02a Stavronikita 53 | 1,038 | 0 / 0 | 2,112 / 1,397 / 1,306 | 1,885 / 805 | 402 | 15 | 0 | 0 |
| M02b Stavronikita 79 | 803 | 0 / 0 | 1,836 / 1,205 / 774 | 1,237 / 568 | 153 | 152 | 0 | 0 |
| M02c Stavronikita 114 | 1,006 | 0 / 0 | 2,397 / 967 / 1,162 | 1,183 / 602 | 12 | 2 | 0 | 1 |

Canonical decomposition was identical before and after NFC on every line. NFC replaced P01's two U+2329 angle characters with U+3008. It also mapped 13 M01 U+1FFD spacing oxias and one M02c U+1FFD to U+00B4, and one M01 U+1FEF to U+0060. These 17 changes preserve scalar length and should stay traceable to the source glyph encoding. P01 also has three Latin precomposed A-with-ring-and-acute letters. Their decomposed marks are counted separately. Its literal underdot has no preceding base in the source string. Treat that as a review flag. Iota subscript is counted by its encoding. Adscript cannot be inferred from neighboring letters.

## Sigma variants, numeral candidates and abbreviation indicators

| Source | Lunate sigma, lower / upper | Standard sigma, medial / final / upper | Spacing acute, raw → NFC | Greek koronis | Private-use scalars |
| --- | --- | --- | --- | ---: | ---: |
| P01 | 404 / 0 | 2 / 0 / 0 | 0 → 0 | 0 | 15 |
| M01 | 0 / 0 | 3,194 / 3,412 / 9 | 145 → 158 | 214 | 0 |
| M02a | 0 / 0 | 1,049 / 1,029 / 0 | 0 → 0 | 33 | 0 |
| M02b | 0 / 0 | 591 / 877 / 0 | 0 → 0 | 11 | 0 |
| M02c | 0 / 0 | 1,394 / 1,373 / 20 | 13 → 14 | 28 | 0 |

Sigma, koronis and private-use counts are identical in raw and NFC. None of the counted dotted or reversed lunate variants occurs. P01 includes 13 uppercase heta and two lowercase stigma, and four integral-like and four almost-equal symbols. The 15 private-use scalars span three code points and eight lines. The source strings do not establish whether these encode letterforms, numerals, abbreviations or transcription shorthand. The same holds for the M02c half-triangular colon (48 occurrences). Preserve these encodings when preparing the review packet.

No Greek upper/lower numeral sign or encoded prime was found in these direct strings. All ASCII decimal digits in the manuscripts are within delimited tokens. These include source-specific labels that may index signs or interventions. Do not count them as manuscript numerals. P01's two encoded overlines are useful review candidates. No M01 or M02a–c direct line encodes a combining macron or overline. This does not establish the absence of nomina sacra, ligatures or bars on the pages.

## Delimiters and possible damage or editorial markup

| Source | Lines with balanced tokens | Angle-plus tokens | Angle-digit tokens | Square-digit tokens | Brace-digit / other tokens | Unpaired delimiter lines |
| --- | ---: | ---: | ---: | ---: | --- | ---: |
| P01 | 0 | 0 | 0 | 0 | 0 / 0 | 2 |
| M01 | 0 | 0 | 0 | 0 | 0 / 0 | 0 |
| M02a | 158 | 137 | 0 | 42 | 2 / 0 | 0 |
| M02b | 283 | 215 | 0 | 117 | 0 / 0 | 0 |
| M02c | 104 | 13 | 42 | 55 | 0 / 1 | 0 |

A line may contain more than one token. These are literal delimiters in the Unicode source text, including escaped angle brackets. None of the checked PAGE XML contains the surveyed explicit semantic nodes for damage, unclear text, gaps, supply, additions, deletions, choice, abbreviation, expansion or glyph identity. The only custom keys encountered are P01 reading order and one structure field. Damage state is unknown on the lines in this inventory. Assigning a plus or numeric token to a damage state requires the publisher's convention and a matching source image.

## Coverage gaps and review packet priorities

1. Start with P01's two overlines, isolated underdot, heta, stigma and private-use lines. Verify exact glyph rendering and distinguish visible ink from transcription notation. Retain the source code point alongside any adjudicated replacement or annotation.
2. Review a small set of Greek base letters with clustered marks in later manuscripts. Examples in the inventory include 280 M01 circumflex-plus-iota-subscript combinations and 60 M02b diaeresis-plus-acute combinations. Include similar-looking unmarked lines so the review records presence and absence.
3. Resolve one example from each delimiter family before selecting additional samples. Record whether each token represents an abbreviation, decorative sign, uncertain trace, supplied content, missing annotation or another convention. Derive no default deletion or expansion rule from frequency.
4. Fill gaps with authorized and independently reviewed examples. This inventory supplies no verified visible uppercase lunate, adscript, Greek numeral, ligature, worn inscription or distinct damage-state examples. Add examples by object, mark type and actual condition, with separate exposure and physical-unit grouping before learning-curve comparisons.

The local `.artifacts/corpus-intake-2026-11-04/` directory retains `sign-inventory.py`, `sign-inventory.json` and `sign-inventory-receipt.sha256`. The report contains all counted code points, separate raw and NFC totals, NFD combinations, and per-line review flags. Its input SHA-256 receipts come from the earlier [PAGE audits](local-greek-page-intake-2026-11-04.md). It records the Unicode database version used for decomposition and name checks.
