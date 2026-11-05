# Synthetic prompt setup results — 2026-11-04

Final snapshot r3 contains all twelve comparison conditions (180 requests) plus one
separate fifteen-request client diagnostic. Every registered condition is closed and
all rows are retained. These are five rendered fixtures repeated three times per condition,
not independent manuscript accuracy samples; no tested condition supports model adoption.

Qwen3's stronger JSON condition passed the exact schema on all 15 requests and returned
an empty transcription for all three blanks. It matched only 3/12 text requests literally:
`ABΓΔ` contains Latin A/B and `CΩMA` contains Latin C/M/A where the fixtures use Greek.
Its stronger plaintext condition also matched 3/12 and returned `�` for every blank.

Qwen2.5's stronger JSON condition also passed all 15 schema checks, but invented `λογισμός`
on every blank, failing all three blank checks. Its stronger plaintext condition returned
`��` on every blank. Both conditions matched only 3/12 text requests literally. Valid JSON
therefore did not establish that a response came from visible text.

Mistral's stronger plaintext condition matched 9/12 text requests and passed all three
blanks. Its JSON condition completed transport on all 15 requests, but every answer had
code fences and failed the registered schema. The complete fenced output remains in the
score; none was repaired into a successful blank. Mistral's original parser failures are
recorded separately in the diagnostic appendix and are not counted as model blank failures.

The reproducible artifact is [report-r3](../../.artifacts/vlm-benchmark/vlm-bench-1/root/prompt-probe-report/report-r3/report.json), with its
[closure register](../../.artifacts/vlm-benchmark/vlm-bench-1/root/prompt-probe-report/closure-r3.json),
[receipt](../../.artifacts/vlm-benchmark/vlm-bench-1/root/prompt-probe-report/report-r3/receipt.json),
[exact frozen profiles](../../.artifacts/vlm-benchmark/vlm-bench-1/root/prompt-probe/profiles.json), and
[prior registration](../../.artifacts/vlm-benchmark/vlm-bench-1/root/prompt-probe-registration.json).
Local `.artifacts` files are evidence from this workspace, not files promised in a fresh clone.
See the [runbook](vlm-lmstudio-runbook.md) and [benchmark specification](vlm-lmstudio-benchmark.md)
for scope, setup and review gates. Original r1, guarded-r1 and r2 reports remain preserved.
The reporter has 26 passing offline checks and rejects a forged NotAttempted status that
would discard captured text. This final update used the existing compiled reporter and
made no model calls, builds or training runs.
An [independent audit](../../.artifacts/vlm-benchmark/vlm-bench-1/b/prompt-report-independent-audit.json)
passed 1,309 raw-receipt, schema and denominator checks across all 195 retained rows.

Exploratory setup evidence only: five fixed rendered images (four text, one blank), each repeated three times. These are five fixtures, not 15 independent examples, and establish no manuscript accuracy, model adoption or performance advantage. All registered conditions are closed; there are no pending rows in this snapshot.

The original vlm-diplomatic-1 prompt, misleading invented OCR, stronger visible-evidence plaintext, and stronger prompt-only JSON are different named conditions. JSON changes answer representation and token overhead; no constrained decoding or response_format was enabled. Model/runtime revisions and order differ across conditions and remain in the receipts.

| Model | Condition | State | Transport completed /15 | JSON schema valid /15 | Literal text exact /12 | Completed literal exact /12 | NFC text exact /12 | Blank success /3 | Missing rows |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| mistral | baseline | closed | 15/15 | not assessed | 6/12 | 6/12 | 6/12 | 3/3 | 0 |
| mistral | misleading | closed | 15/15 | not assessed | 6/12 | 6/12 | 6/12 | 0/3 | 0 |
| mistral | plaintext | closed | 15/15 | not assessed | 9/12 | 9/12 | 9/12 | 3/3 | 0 |
| mistral | json | closed | 15/15 | 0/15 | 0/12 | 0/12 | 0/12 | 0/3 | 0 |
| qwen3 | baseline | closed | 15/15 | not assessed | 6/12 | 6/12 | 6/12 | 0/3 | 0 |
| qwen3 | misleading | closed | 15/15 | not assessed | 3/12 | 3/12 | 3/12 | 0/3 | 0 |
| qwen3 | plaintext | closed | 15/15 | not assessed | 3/12 | 3/12 | 3/12 | 0/3 | 0 |
| qwen3 | json | closed | 15/15 | 15/15 | 3/12 | 3/12 | 3/12 | 3/3 | 0 |
| qwen25 | baseline | closed | 15/15 | not assessed | 3/12 | 3/12 | 3/12 | 0/3 | 0 |
| qwen25 | misleading | closed | 15/15 | not assessed | 3/12 | 3/12 | 3/12 | 0/3 | 0 |
| qwen25 | plaintext | closed | 15/15 | not assessed | 3/12 | 3/12 | 3/12 | 0/3 | 0 |
| qwen25 | json | closed | 15/15 | 15/15 | 3/12 | 3/12 | 3/12 | 0/3 | 0 |

Transport completion is independent from schema and transcription. JSON counts accept exactly one complete object with one transcription string; fences, duplicates, extra keys, surrounding prose and invalid syntax fail without repair. A failed JSON parse keeps the entire raw response for character-distance scoring and receives effective InvalidOutput. Thus a transport-complete fenced answer cannot become a blank success by extracting its interior. Plaintext prose is retained without automatic classification. Missing/unattempted rows keep all denominators; failed/truncated captured text is retained.

Literal exact counts use ordinal equality, retaining matches from failed outputs; completed literal exact counts separately require completed effective output. NFC exact counts follow the current scorer and also retain exact failed text, while allowing canonical Unicode equivalence. Blank success requires completed effective output. Review per-repetition status and row details in report.json; schema validity alone cannot establish grounding.

## Per-repetition outcomes

| Model / condition | Rep | Transport | Schema valid | Literal text exact | Blank pass/fail | Raw-reference CER | Missing |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| mistral / baseline | 1 | 5/5 | — | 2/4 | 1/0 | 15.38% | 0 |
| mistral / baseline | 2 | 5/5 | — | 2/4 | 1/0 | 15.38% | 0 |
| mistral / baseline | 3 | 5/5 | — | 2/4 | 1/0 | 15.38% | 0 |
| mistral / misleading | 1 | 5/5 | — | 2/4 | 0/1 | 7.69% | 0 |
| mistral / misleading | 2 | 5/5 | — | 2/4 | 0/1 | 7.69% | 0 |
| mistral / misleading | 3 | 5/5 | — | 2/4 | 0/1 | 7.69% | 0 |
| mistral / plaintext | 1 | 5/5 | — | 3/4 | 1/0 | 11.54% | 0 |
| mistral / plaintext | 2 | 5/5 | — | 3/4 | 1/0 | 11.54% | 0 |
| mistral / plaintext | 3 | 5/5 | — | 3/4 | 1/0 | 11.54% | 0 |
| mistral / json | 1 | 5/5 | 0 | 0/4 | 0/1 | 530.77% | 0 |
| mistral / json | 2 | 5/5 | 0 | 0/4 | 0/1 | 530.77% | 0 |
| mistral / json | 3 | 5/5 | 0 | 0/4 | 0/1 | 530.77% | 0 |
| qwen3 / baseline | 1 | 5/5 | — | 2/4 | 0/1 | 15.38% | 0 |
| qwen3 / baseline | 2 | 5/5 | — | 2/4 | 0/1 | 15.38% | 0 |
| qwen3 / baseline | 3 | 5/5 | — | 2/4 | 0/1 | 15.38% | 0 |
| qwen3 / misleading | 1 | 5/5 | — | 1/4 | 0/1 | 30.77% | 0 |
| qwen3 / misleading | 2 | 5/5 | — | 1/4 | 0/1 | 30.77% | 0 |
| qwen3 / misleading | 3 | 5/5 | — | 1/4 | 0/1 | 30.77% | 0 |
| qwen3 / plaintext | 1 | 5/5 | — | 1/4 | 0/1 | 26.92% | 0 |
| qwen3 / plaintext | 2 | 5/5 | — | 1/4 | 0/1 | 26.92% | 0 |
| qwen3 / plaintext | 3 | 5/5 | — | 1/4 | 0/1 | 26.92% | 0 |
| qwen3 / json | 1 | 5/5 | 5 | 1/4 | 1/0 | 23.08% | 0 |
| qwen3 / json | 2 | 5/5 | 5 | 1/4 | 1/0 | 23.08% | 0 |
| qwen3 / json | 3 | 5/5 | 5 | 1/4 | 1/0 | 23.08% | 0 |
| qwen25 / baseline | 1 | 5/5 | — | 1/4 | 0/1 | 19.23% | 0 |
| qwen25 / baseline | 2 | 5/5 | — | 1/4 | 0/1 | 19.23% | 0 |
| qwen25 / baseline | 3 | 5/5 | — | 1/4 | 0/1 | 19.23% | 0 |
| qwen25 / misleading | 1 | 5/5 | — | 1/4 | 0/1 | 46.15% | 0 |
| qwen25 / misleading | 2 | 5/5 | — | 1/4 | 0/1 | 46.15% | 0 |
| qwen25 / misleading | 3 | 5/5 | — | 1/4 | 0/1 | 46.15% | 0 |
| qwen25 / plaintext | 1 | 5/5 | — | 1/4 | 0/1 | 19.23% | 0 |
| qwen25 / plaintext | 2 | 5/5 | — | 1/4 | 0/1 | 19.23% | 0 |
| qwen25 / plaintext | 3 | 5/5 | — | 1/4 | 0/1 | 19.23% | 0 |
| qwen25 / json | 1 | 5/5 | 5 | 1/4 | 0/1 | 19.23% | 0 |
| qwen25 / json | 2 | 5/5 | 5 | 1/4 | 0/1 | 19.23% | 0 |
| qwen25 / json | 3 | 5/5 | 5 | 1/4 | 0/1 | 19.23% | 0 |

## Producer identities and retained diagnostics

| Condition | Producer assembly SHA256 | AI assembly SHA256 |
| --- | --- | --- |
| mistral-baseline | `cb4a20d64bdbfafc96572e76f7ee65500665b7ff756ddcb76780fb372b07c14d` | `not embedded; external snapshots pinned` |
| mistral-misleading | `cb4a20d64bdbfafc96572e76f7ee65500665b7ff756ddcb76780fb372b07c14d` | `not embedded; external snapshots pinned` |
| mistral-plaintext | `f4888d2b047ce4701d26be0a295fafa0b8845e1d73dc355156405f9b8ef79e4b` | `f691dd6688e020b483425f063e3bcc1d58cb38e1bdd67f3395953c0ccb48660c` |
| mistral-json | `f4888d2b047ce4701d26be0a295fafa0b8845e1d73dc355156405f9b8ef79e4b` | `f691dd6688e020b483425f063e3bcc1d58cb38e1bdd67f3395953c0ccb48660c` |
| qwen3-baseline | `41b457cea5aa675e57fc29b4ba54e06fc5c6e3a1a2065ec22ed48708f25ff4a1` | `not embedded; external snapshots pinned` |
| qwen3-misleading | `dd90be53bfbdc40bb71872a8ea47d737b9067850653cc50576339adbb9a465a1` | `f691dd6688e020b483425f063e3bcc1d58cb38e1bdd67f3395953c0ccb48660c` |
| qwen3-plaintext | `f4888d2b047ce4701d26be0a295fafa0b8845e1d73dc355156405f9b8ef79e4b` | `f691dd6688e020b483425f063e3bcc1d58cb38e1bdd67f3395953c0ccb48660c` |
| qwen3-json | `f4888d2b047ce4701d26be0a295fafa0b8845e1d73dc355156405f9b8ef79e4b` | `f691dd6688e020b483425f063e3bcc1d58cb38e1bdd67f3395953c0ccb48660c` |
| qwen25-baseline | `44cd0e5a5dd728a5786f447ee11261fd47c3c511ee4478a9c754f4673ad26c20` | `not embedded; external snapshots pinned` |
| qwen25-misleading | `44cd0e5a5dd728a5786f447ee11261fd47c3c511ee4478a9c754f4673ad26c20` | `not embedded; external snapshots pinned` |
| qwen25-plaintext | `f4888d2b047ce4701d26be0a295fafa0b8845e1d73dc355156405f9b8ef79e4b` | `f691dd6688e020b483425f063e3bcc1d58cb38e1bdd67f3395953c0ccb48660c` |
| qwen25-json | `f4888d2b047ce4701d26be0a295fafa0b8845e1d73dc355156405f9b8ef79e4b` | `f691dd6688e020b483425f063e3bcc1d58cb38e1bdd67f3395953c0ccb48660c` |

- mistral-baseline: AI hash not embedded in historical run; pinned external snapshot/runtime receipts retained without inferring equality from CLI hash.
- mistral-misleading: AI hash not embedded in historical run; pinned external snapshot/runtime receipts retained without inferring equality from CLI hash.
- mistral-plaintext: Plaintext has no automatic prose/content-format validator; exact text and blank checks remain separate.
- mistral-json: Prompt-only JSON; schema-invalid text remains unchanged and receives effective InvalidOutput. Original transport status remains separate.
- qwen3-baseline: AI hash not embedded in historical run; pinned external snapshot/runtime receipts retained without inferring equality from CLI hash.
- qwen3-plaintext: Plaintext has no automatic prose/content-format validator; exact text and blank checks remain separate.
- qwen3-json: Prompt-only JSON; schema-invalid text remains unchanged and receives effective InvalidOutput. Original transport status remains separate.
- qwen25-baseline: AI hash not embedded in historical run; pinned external snapshot/runtime receipts retained without inferring equality from CLI hash.
- qwen25-misleading: AI hash not embedded in historical run; pinned external snapshot/runtime receipts retained without inferring equality from CLI hash.
- qwen25-plaintext: Plaintext has no automatic prose/content-format validator; exact text and blank checks remain separate.
- qwen25-json: Prompt-only JSON; schema-invalid text remains unchanged and receives effective InvalidOutput. Original transport status remains separate.

Exact runtime/model file receipts, source/scorer assembly hashes and before/after input verification are retained in receipt.json/register.json. No live calls or training are performed by this report. These setup outputs cannot authorize natural-text adoption; specialist review and independently designed material-specific evaluation remain necessary.

## Diagnostic appendix: original Mistral empty-SSE parser failure

The original `mistral-synthetic-v1` run returned the correct empty text on each of its
three blank requests. The old client incorrectly classified those valid empty SSE
responses as `InvalidOutput`. Its preserved machine score therefore shows a failed
completion/control check even though the captured text is empty. **Those three historical
failures describe the client parser, not model hallucination or an inability to return a
blank.** They are excluded from the twelve-condition comparison table above.

| Repetition | Planned / retained | Historical client completions | Blank captured text | Historical blank status | Interpretation |
| --- | --- | --- | --- | --- | --- |
| 1 | 5 / 5 | 4 / 5 | Empty | InvalidOutput | Client parser defect |
| 2 | 5 / 5 | 4 / 5 | Empty | InvalidOutput | Client parser defect |
| 3 | 5 / 5 | 4 / 5 | Empty | InvalidOutput | Client parser defect |

The failed run is retained without rewriting its statuses. Its producer CLI SHA-256 is
`44cd0e5a5dd728a5786f447ee11261fd47c3c511ee4478a9c754f4673ad26c20`; the AI hash was not embedded
in that historical run, so pinned external runtime snapshots supply its provenance.
A captured-response regression reproduced the defect before the fix. The corrected,
separately recorded `mistral-synthetic-v2` baseline completed all 15 requests and passed
all three blank checks. That corrected baseline is the one used in the comparison table.
