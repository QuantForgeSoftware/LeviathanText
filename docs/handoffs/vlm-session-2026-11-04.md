# LM Studio VLM execution handoff — 2026-11-04

The three-hour assignment runs from 07:29:19 to 10:29:19 CST. Live model execution is
closed; all session-owned models were unloaded, leaving the pre-existing server running.
The closing verification receipt records the final repository and test state.
Base commit: `6c9265f6fb82f3472c9d7faa013e79c35fe6e155`. Earlier uncommitted November 4
review fixes were preserved. No release, default recognizer replacement, specialist
adjudication or model promotion is part of this work.

## Outcome and decision

LeviathanText now has a separate, bounded C# image-input path to LM Studio, an exploratory
CLI benchmark runner and a strict offline scorer. Local synthetic controls demonstrated
image-conditioned responses from the pinned models. Their outputs also demonstrate invented text on blank controls,
Latin lookalikes, unwanted prose, added marks and susceptibility to misleading OCR hints.
**Keep VLM output advisory. None of the tested conditions is approved to replace the
recognizer or become training labels.**

All 34 registered run directories are closed, with all 1,509 planned slots retained:
1,478 transport completions, 21 truncations, one deadline cancellation, six unattempted
deadline slots and three original client-parser failures. The latter remain a historical
diagnostic alongside their corrected rerun. Thus 1,503 slots were attempted; repeated
requests and synthetic controls are not additional independent manuscript examples.

The natural pilot has only 51 provisional P46 lines from five proposed physical-leaf/unit
groups (folio/source-caption mapping still needs review): 44 were
used to train R0 and seven were repeatedly inspected during development. Its lowercase,
space-stripped source references do not encode literal visible case, sigma forms and
marks. Raw edit distances are diagnostic source-reference distances, not established
diplomatic accuracy. Classical Greek, other manuscripts and inscriptions remain untested.

## Delivered implementation

- [Vision transport](../evaluation/vlm-lmstudio-transport.md): explicit exact PNG bytes,
  bounded local HTTP, no fallback or retries, timeout/cancellation, preserved raw wire/text,
  Unicode handling and model/image/prompt/request hashes. Existing text-only APIs remain
  separate. [A handoff](VLM-A1.md).
- [CLI and operator runbook](../evaluation/vlm-lmstudio-runbook.md): validate, run and score
  without opening a project database; frozen requests, original and resolved manifests,
  producer assembly identities, no-clobber outputs and complete planned denominators.
  Unix Ctrl-C closes receipts with cancelled/unattempted rows; actual Windows console
  signaling remains untested. [CLI handoff](VLM-CLI.md).
- [Offline scoring](../evaluation/vlm-benchmark-scoring.md): NFC Unicode-scalar I/D/S,
  uncapped CER, separate physical-unit/material/control results, failed text retained,
  strict joins and bounded edit work. JSONL scoring also bounds cumulative retained text
  before retaining rows. [B handoff](VLM-B1.md).
- [Corpus inventory](../data/vlm-benchmark-corpus.md): exact crop/reference/rights/exposure
  records, existing recognizer replay and genuine frozen R0 hypotheses for V2.
- [Specialist packet](../evaluation/vlm-review-packet.md): 51 exact crops, nine page sides,
  source/reference/R0 text and all 306 first-sweep outputs. HTML and the JSON worksheet
  preserve exact strings; the 115-page PDF is the printable companion. The 81 original
  pending decisions remain unchanged, and all new reviewer decisions are unfilled.

Live testing exposed a valid empty SSE response that the client wrongly classified as
invalid. A captured synthetic regression failed before the correction and passes afterward.
The original failed client run and corrected rerun are separately retained. Independent
reviews also drove receipt binding, interrupted-run accounting, replay provenance, actual
console cancellation and cumulative scoring-memory fixes. An oversized run receipt is now
rejected before creating output or making a request, so the runner cannot produce a receipt
beyond its scorer's read limit. Offline validation/scoring retain ordinary console
interruption; inference uses graceful closure. Handoffs retain the failing-before/passing-
after evidence.

## Evidence and limits

Benchmark receipts, generated reports and review packets remain under
`.artifacts/vlm-benchmark/vlm-bench-1/`. Downloaded weights/projectors remain in the local
LM Studio model directory; original corpus sources retain their frozen locations. Models,
images and outputs are not bundled into the source contribution. Model weights and matching
projectors have local SHA-256 receipts, pinned upstream/package cards and license declarations.
The three candidate package cards declare Apache 2.0; the pinned repositories did not
contain standalone license files. This records local-evaluation provenance, not an asset
redistribution decision. P46 image and transcription rights are recorded separately.

LM Studio 0.4.26+4 used llama.cpp Metal runtime 2.65.1 on an Apple M5 Max with 64 GiB RAM.
The models were Q4_K_M with their matching F16 vision projectors, context 8192, GPU offload,
one loaded model and one request at a time. Sampling was temperature 0, top-p 1, seed 42;
repetition order seeds differ. Runtime seed honoring, internal image tiling and effective
chat-template rendering were not independently verified. Full configs and package hashes
are retained; sampling determinism is measured rather than assumed.

Read the reports separately because they answer different questions:

- [First natural sweep](../evaluation/vlm-pilot-results-2026-11-04.md): six conditions,
  306 requests, all rows retained; 301 transport completions and five truncations.
- [Repeatability](../evaluation/vlm-repeatability-results-2026-11-04.md): three repetitions
  of each frozen natural condition; repeated views do not increase independent sample size.
- [Synthetic prompt and misleading-OCR controls](../evaluation/vlm-synthetic-prompt-results-2026-11-04.md):
  distinguish transport, exact characters, blank output and strict JSON compliance.
- [Synthetic geometry](../evaluation/vlm-geometry-results-2026-11-04.md): paired raw,
  white-padded and exact 2x enlarged printed Greek strips. This tests a preprocessing
  hypothesis; it does not establish handwritten-text accuracy or authorize a default change.
- [Output diagnostics](../evaluation/vlm-output-diagnostics-2026-11-04.md): exact Unicode,
  case/mark/spacing inventories and explicitly heuristic review priorities across all 306
  first-sweep outputs, without changing any text or declaring scholarly errors.
- [Resources](../evaluation/vlm-resource-observations-2026-11-04.md) and
  [embedded input metadata](../evaluation/vlm-model-input-metadata-2026-11-04.md): retain
  observed counters, runtime/package/template identities and the remaining measurement gaps.

Latency changed substantially after model reloads and download completion. Host/cache/I/O
state confounds these comparisons; they are not evidence of a code speedup. Sampled summed
RSS can double-count shared pages and is neither exact peak memory nor GPU allocation.
Keep long-tail failures and maximum latency visible alongside medians and percentiles.

## Verification

The final Release solution build passed with zero warnings/errors. Applicable suites pass
**289 tests**: AI 128, Evaluation 78 and CLI 83. One opt-in live Ollama test is skipped;
both fitting CLI tests are explicitly excluded. These counts cover this change, not a new
claim that every repository suite ran. Earlier unrelated review-fix evidence remains in its
own handoff. Final CLI rescoring of all 28 standard-protocol run directories also passed,
including older producer versions and the deadline tail; previously saved score reports
match where available, and no project database was created. The six prompt-only runs use
their separately tested frozen reporter.

Evidence: `root/final-solution-build-closed.log`,
`a/candidate/full-ai-mistral-blank.trx`, `root/integration-evaluation.trx`,
`root/cli-offline-cancellation/cli-no-training.trx`,
`root/final-offline-score-replay/receipt.json`, `root/session-audit-closed.json` and
the per-scope handoffs. The unchanged R0 model and metadata hashes are rechecked in
`root/r0-final-hashes.txt`. The session audit verifies preservation of the pre-existing
source changes; the intended CLI routing/help additions retain the earlier HTML-export fix.
All paths in this paragraph are relative to the benchmark artifact root.

## Verification deviation

Two early CLI regression invocations each unintentionally executed an existing one-epoch
synthetic alphabet fixture test. Temporary black-rectangle fixtures were disposed. No P46
or VLM weights were trained or modified. This was a deviation from the no-training scope;
the exact tests, timestamps and retained TRX hashes are in
`root/synthetic-test-training-deviation.json`. Later CLI verification excludes both fitting
tests explicitly; do not describe the whole session as having performed no training.

## Next work

1. Obtain Greek-specialist decisions using the packet; preserve source transcription,
   visible diplomatic reading and editorial interpretation as separate layers. Resolve
   case, lunate/final sigma, spacing, marks, damage and line-boundary questions before
   interpreting recognition scores.
2. Freeze a reviewed setup/development release with natural blank/damaged regions and
   witness variants. Add legally usable Classical/manuscript/inscription samples and
   disjoint physical objects; designate confirmation custody separately.
3. Register a small paired natural-image geometry experiment only after its reviewed
   references and mark-preserving crops exist. Keep the raw crop as control, retain every
   failed output and evaluate per-object regressions. First capture effective backend
   image transforms/token counts where available; embedded metadata alone does not establish
   them. Synthetic gains are a hypothesis, and their direction differs across these models.
4. Evaluate explicit structured decoding separately from prompt-only JSON. Fenced JSON
   currently fails the strict protocol; do not strip fences or repair responses and call
   that a successful model response. Retain Latin-lookalike and added-mark diagnostics.
5. Compare human correction effort with blinded, counterbalanced tasks and independent
   quality checks. The present diagnostic packet is unblinded and cannot supply that result.
6. Only after a condition passes the registered development gates, freeze it and evaluate
   fresh reviewed physical objects. Keep training, promotion and deployment as separate
   decisions. VLM-04/06/07 and unrelated P46 review/training gates remain open.

Changes are left uncommitted for maintainer review. No DCO certification was invented.
