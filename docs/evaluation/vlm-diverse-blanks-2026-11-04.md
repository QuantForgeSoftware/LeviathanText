# Diverse synthetic blanks and readable Greek controls

The JSON profiles passed all 24 blank slots for each model, covering 12 distinct
blank images twice. Plain text passed 0 of 24 for Qwen3 and 12 of 24 for Mistral.
Readable-word accuracy stayed flat for Qwen3 and fell from 14 of 24 to 12 of 24
for Mistral. All 192 requests completed transport.

| Model | CLI profile | Blank passes / 24 | Unique blanks passing both / 12 | Readable exact / 24 | Readable scalar edits | Joint pair passes / 24 | Schema-valid completions / 48 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Qwen3-VL 8B | Plain text | 0 | 0 | 12 | 12 | 0 | — |
| Qwen3-VL 8B | Transcription JSON | 24 | 12 | 12 | 12 | 12 | 48 |
| Mistral Small 3.1 24B | Plain text | 12 | 6 | 14 | 10 | 8 | — |
| Mistral Small 3.1 24B | Transcription JSON | 24 | 12 | 12 | 16 | 12 | 48 |

A joint pair passes when the blank returns empty text and its readable
counterpart matches the target under NFC in the same repetition. The
strict derived transcription determines JSON recognition and blank scores.
Raw JSON representation retained 516 and 526 scalar edits on readable
slots for Qwen3 and Mistral respectively, including syntax.

Qwen3 plain text returned a single replacement character on every blank.
Mistral plain text passed all grain, edge and band slots. The 12 failed
tone, gradient and stain slots returned six single replacement characters,
two triples and four instances of `ἀνθρώπων`. Both JSON profiles returned
empty transcription on all six background families.

The Mistral JSON profile changed both repetitions of the second gradient
word from `βίος` to `βί�ς`. It also changed `ἀρχῆ` to `᾽ρχῆ` on both
repetitions of two images whose target was `ἀρχή`. Qwen3 kept the same
exact-match and edit totals across profiles. Its recurring errors included
Latin `á` in `áρχή` and altered breathing or accent marks in other words.

The generator drew nonletter backgrounds in six families and added a
high-contrast Greek word to create each positive image. Independent PNG
decoding verified all 24 unique image hashes and exact agreement outside
the ink change in each pair. The six typed words repeat across backgrounds.
The CLI uses distinct prompt versions for its plain and JSON profiles, so
the comparison combines prompt and format. This evidence covers complete
absence of character traces and readable synthetic words. Faint fragments,
damaged lettering, and real holding images require separate reviewed tests.

[Registered design](../../.artifacts/vlm-benchmark/diverse-blank-v1/design.md) ·
[Fixture audit](../../.artifacts/vlm-benchmark/diverse-blank-v1/verification.json) ·
[Score receipts](../../.artifacts/vlm-benchmark/diverse-blank-v1/live.mqmAKj) ·
[Paired and family totals](../../.artifacts/vlm-benchmark/diverse-blank-v1/report.json).
