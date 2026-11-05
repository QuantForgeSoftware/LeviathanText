# Three-model word screen retry

The four originally blocked visits were retried consecutively in one idle LM Studio window, finishing at 20:08:50 CST on November 4. The original time-spaced visits remain recorded as unavailable. The retry preserved all 40 frozen generated images, references, profile order, model order, and scoring rules. It measures agreement on generated words and blanks within one window.

All 240 requests completed and all 80 image/profile triples were evaluable. Agreement uses exact NFC strings across two or three models.

| Profile | Required agreement | Exact text / 32 | Wrong agreements on text | Text disagreements | Correct blank / 8 | Wrong blank agreements | Blank disagreements |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Plain text | 2 | 20 | 1 | 11 | 0 | 4 | 4 |
| Plain text | 3 | 10 | 0 | 22 | 0 | 0 | 8 |
| Transcription JSON | 2 | 23 | 3 | 6 | 8 | 0 | 0 |
| Transcription JSON | 3 | 9 | 0 | 23 | 8 | 0 | 0 |

JSON yielded correct empty strings on all eight blank images in all three models. The two-model rule produced three wrong text agreements under JSON and one under plain text. Unanimity yielded nine and ten exact text readings respectively, with disagreements on the other text fixtures. The profiles differ in both prompt and output format. Agreement on this generated set supplies review evidence. The earlier compact glyph study includes wrong unanimous readings, and natural validation still depends on reviewed references.

All visit, scoring, and load exits were zero. The server reported zero retry workers after cleanup. LM Studio stayed at Debug. [Local report](../../.artifacts/vlm-benchmark/consensus-word-retry.Dw2XS8/report.json).
