# VLM extension through 13:00 CST — 2026-11-04

This is the historical extension handoff. Later work completed the synthetic Debug
trace and tested the shipped CLI JSON prompt.
[Later benchmark handoff](vlm-two-hour-2026-11-04.md).

The owner extended the ongoing assignment to 13:00 CST. This handoff supplements the
[closed first phase](vlm-session-2026-11-04.md), preserving its reports and evidence. The
extension evaluates explicit structured output through LM Studio and makes that format
available as an experimental CLI condition. It does not select a recognizer, change a
preprocessing default, adjudicate Greek readings or authorize model promotion.

## Paired experiment

The [registered schema study](../evaluation/vlm-json-schema-benchmark.md) completed all
**276 planned requests**: 270 matrix requests and six separate format challenges, with
no failed or missing transport results. Three existing local models each processed the
same 15 generated fixtures, in two adjacent response-format conditions and three repeats.
There are only 13 distinct text fixtures and two blanks; repetitions are not new samples.
This is a synthetic format diagnostic, not a new manuscript or held-out Greek benchmark.

The common window was registered at 11:01:55 CST with a 12:01:55 deadline. Actual requests
ran from 11:12:01 to 11:18:39. Model loading and log setup consumed part of the window.
All session-owned models were unloaded, and the original empty loaded-model list was
verified at 11:19:11. The pre-existing LM Studio server remains running. No further live
model calls belong to the CLI follow-up.

The [full results](../evaluation/vlm-json-schema-results-2026-11-04.md) show:

- Qwen3 and Qwen2.5 each returned valid transcription JSON on all 45 matrix requests in
  either route. Their paired API content strings were identical. Qwen2.5 invented
  `λογισμός` on the setup blank in every repetition in both routes.
- Mistral returned valid JSON on 3/45 prompt-only responses and 45/45 schema-requested
  responses. Three paired strings were identical; the other 42 differed only by exact
  code-fence wrappers. Its Greek characters were not corrected by the schema option.
- Valid JSON, exact characters, blank success and transport completion remain separate
  measures. Stable repeated errors and improved formatting do not establish grounding or
  suitability for manuscript recognition.

The frozen artifact producer passed 280 mocked checks. An independent final-report audit
passed 6,165 checks; all failed-before evidence and the partial first report remain intact.
Artifacts are under `.artifacts/vlm-benchmark/vlm-bench-2/`; `b/closure-r2.json` and
`b/report-r2/receipt.json` bind the final inputs and results. Root's
`closed-live-receipt.json` pins 336 live/run/log/resource files, and
`post-run-model-hashes.json` verifies that all six VLM weight/projector files and both R0
model/metadata files still match their earlier hashes.

## Runtime observations and limits

The [runtime investigation](../evaluation/vlm-runtime-observations-2026-11-04.md) retained
276 model-input events, 276 model-output events and 792 runtime events. Event associations
use model identity and serial request intervals; they are not unique request-ID proofs.
Forty-four backend output diagnostics contain JSON fences absent from the HTTP completion
content. Pinned backend source permits fenced or bare schema JSON and exposes its tagged
content subtree. This is a source explanation consistent with the observed difference,
not independent proof that a particular grammar sampler was active for every request.

Each model logged 47/92 one-token prompt evaluations with cache reuse. Paired timings
therefore cannot establish a clean schema speed effect. Effective image grids, resizing,
dedicated vision-token totals and effective temperature/top-p/seed fields were not emitted
at the current settings. Requested settings are verified from the exact request bodies;
embedded GGUF metadata and saved chat preferences do not substitute for effective values.
No global runtime settings were changed to obtain these logs.

Resource sampling retained 40, 35 and 101 snapshots for Qwen3, Qwen2.5 and Mistral. Sampled
summed process RSS maxima were approximately 5.66, 7.05 and 14.49 GiB respectively. These
include the selected application/backend/CLI processes, may double-count shared pages and
exclude unselected processes; they are neither GPU allocation nor exact memory peaks.
Host swap/cache state and model loading remain confounds. No resource acceptance or
performance improvement is claimed.

## Optional CLI follow-up

VLM-10 adds explicit `--response-format transcription-json-v1`, a distinct named prompt
and an output contract rejected by old scorers. Default plain-text requests and legacy
scores must remain unchanged. The new condition preserves raw API content and transport
receipts, records producer-attested format audits, and reports raw representation distance
separately from strict-derived transcription distance. Invalid schema output is retained
in full without removing fences or extracting fragments. Failed or missing responses
remain in every planned denominator. See the
[operator instructions](../evaluation/vlm-lmstudio-runbook.md#optional-structured-responses).

The verified CLI checkpoint passes **137 tests** (54 new structured-output cases and 83
earlier CLI cases), with both fitting tests excluded. The solution builds with zero
warnings/errors; all 28 historical plain-text reports replay byte-identically. Independent
score-only receipt mutations pass 76 checks, including missing-response, altered-audit,
duplicate-property and blank-control cases. A compiling baseline rejected the new option;
the old frozen scorer also rejects the new prompt version without writing a score.
Review-driven duplicate-field and auditless-response fixes retain failing-before evidence.

Evidence belongs under `.artifacts/vlm-benchmark/vlm-cli-json-1/`: `a/verification.json`,
`b/review-receipt.json`, `root/final-solution-build.log` and
`root/legacy-replay/receipt.json`. The frozen CLI SHA-256 is
`b2b287815ad8fd832ec880f2caca0e7b5589fa4ae932096a33ec0a4f07af55e2`.
The AI/Evaluation implementations remained unchanged during this CLI checkpoint.
The new CLI prompt and 120-second request limit have not been live-benchmarked; the closed
schema study used a different artifact-only prompt and a 30-second request limit.

## Authenticated endpoint follow-up

VLM-11 is complete as a separate checkpoint. `--credential-env NAME` explicitly selects
an environment reference for the existing bearer transport. Omission remains keyless.
Missing keys produce configuration failures without HTTP; authentication failures retain
their planned rows. Offline scoring validates the nonsecret authentication metadata and
discloses redacted results without resolving a credential. No actual credentials or server
authentication settings were inspected or changed.

Dummy-token tests reproduced four nested-content escape leaks: structured transcription
content could decode a credential after one-level wire redaction. The narrow provider
correction checks that additional bounded string layer and redacts the whole affected
content/wire, retaining the original captured-byte hash, count and status. It does not
repair JSON or recursively decode arbitrary encodings. The parser and Evaluation library
remain unchanged. A separate baseline reproduced eight invalid-authentication-metadata
cases that the scorer previously ignored; these now reject before report output.

Final applicable test evidence is **397 passes**: AI 148, CLI 171 and the unchanged
Evaluation suite's 78. One opt-in live Ollama test is skipped, and both fitting CLI tests
are excluded. The final solution builds with zero warnings/errors. All **30 compatibility
reports** replay byte-identically: 28 historical plain-text reports and both earlier
keyless structured modes. Independent checks cover 312 authenticated mock-receipt/scoring
assertions and a 278-case provider probe spanning byte/delta splits, incomplete JSON,
I/O failure, cancellation and unchanged Greek controls. The latter probe also passed an
independent 2,988-check review. These are engineering checks, not additional Greek samples.

Evidence is under `.artifacts/vlm-benchmark/vlm-cli-auth-1/`: `a/verification.json`,
`b/receipt-audit-r1/review-receipt.json`, `c/probe/run-r1/receipt.json`,
`root/final-solution-build.log` and `root/compatibility-replay/receipt.json`.
Final CLI SHA-256: `546024c17af3393284530283e5ac4be885ba1b75044c73fe1459c833eff52ff2`;
AI SHA-256: `3cb1dce39ae12cd873b994de6863a0b2017ec83e35390bc51bef13e2fa9cf632`.
Live authenticated-server compatibility and live execution of the new CLI JSON prompt
remain untested; neither was inferred from mocked transport checks.

## Remaining runtime trace

Read-only installed-source inspection verified the supported debug control:
**App Settings → Developer → Runtime Settings → Llama.cpp Engine Log Level → Debug (5)**.
It applies on the next model load and is global. The
[next-session procedure](../../.artifacts/vlm-benchmark/vlm-bench-2/c/verbosity-followup.md)
specifies a quiet synthetic window, bounded logging, allowlisted launch verification and
restoration to Info (3), followed by unloading owned debug workers.

The attempted read-only UI connection timed out before returning an app handle. No setting
mutation or additional inference was issued, and no new trace was registered. By then the
12:30 live-work cutoff had passed, so the UI attempt was not retried. The complete settings
file hash remained unchanged, saved verbosity remained 3 and the loaded-model list was
still empty at 12:33 CST. The original server remains running. See
`vlm-bench-2/root/verbosity-ui-timeout.json` and `final-model-state.json`. Effective grids,
image-token counts and native preprocessing therefore remain explicitly unmeasured.

## Remaining decisions

VLM-04/06/07 remain open. All 81 original specialist decisions and the additional proposed
line targets remain pending. The 51 P46 crops are exposed provisional material; they cannot
establish diplomatic accuracy or fresh-object generalization. Classical Greek, other
manuscripts and inscriptions still need reviewed, rights-cleared image/reference pairs.
Natural geometry comparisons need reviewed targets and mark-preserving crop definitions.
Correction-effort and workflow/resource gates also remain unproven.

No P46 or VLM weights were trained or changed during this extension. The earlier phase's
two unintended one-epoch synthetic fixture tests remain a disclosed scope deviation;
later verification explicitly excludes both fitting CLI tests. See the first-phase
handoff and its deviation receipt rather than describing the full assignment as having
performed no training.

Changes remain uncommitted for maintainer review. No DCO certification, release or default
model change was made.
