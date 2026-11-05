# Evaluator acceptance sheet for Greek recognition

**Template status: draft and unfrozen.** Use one copy of the per-track table for each material, supported profile and claim. Project owner and Greek reviewer set scholarly thresholds after a development pilot. The engineering owner sets resource budgets for the actual target. The evaluator freezes the plan before selection, and a separate custodian controls confirmation labels. [Project verification plan](../../project-plan.md#7-verification-plan) · [Three-track acceptance plan](acceptance-plan.md).

## 1. Release registration and custody

| Record to freeze | Evaluator entry |
| --- | --- |
| Sheet ID/version, UTC freeze, decision scope, source commit and patch/assembly hashes | Pending |
| Claimed material and language variety, supported writing hands, captures and conditions, explicit exclusions | Pending |
| Model/checkpoint and dependency SHA-256, preprocessing and crop versions, decoder and full configuration | Pending |
| Dataset/release/reference and split hashes; evidence paths; explicit intended-use rights and credits for images, annotations and weights | Pending |
| Project owner, independent Greek specialist, adjudicator, engineering owner, evaluator, confirmation custodian and permitted-access list | Pending |
| Disjoint setup, selection and confirmation unit lists and hashes; repeated passages/editions, physical-object lineage and prior exposure | Pending |
| Frozen reference profile, normalization and scoring versions, uncertainty-marker and exclusion policy, eligible denominators and minimum independent-unit/reviewer coverage | Pending |
| Execution scope, admissible candidates, fixed seeds, run/repetition counts, fixed confirmation budget, evidence location and stop rules | Pending |

The corpus owner provides an asset ledger with source and holding identifiers, terms, source raster hash, source-frame geometry or reproducible transform, crop hash and dimensions, layer/revision identity and review status for each line. Preserve the source edition as its own reference. An independent second reading and adjudicator resolve conflicts or retain alternatives with explicit scoring treatment. Track exclusions by reason and unit. Freeze the exact scoring cohort and denominator before candidate outputs. Keep test material out of prompts, tuning, thresholds and model choice. A confirmation result used to change a candidate spends that test material. Record the reuse and obtain fresh held-out units for another claim.

## 2. Current evidence and open gates

| Track | Verified preparation and historical evidence | Outstanding acceptance work |
| --- | --- | --- |
| Manuscript | P46 has 51 provisional crop-to-target proposals across five proposed leaves of one Koine witness. Forty-four crops have training/development exposure and seven have development exposure. The R0 replay audit verifies 153 joins across three repetitions. M01 and M02a archive joins and earlier transfer evidence exist. M02b and M02c have PAGE-only inventories, with no verified raster payload in that intake. | Zero reviewed P46 selection or confirmation lines. The separate original 81-decision specialist worksheet is pending. Resolve crop boundaries, diplomatic readings and leaf identity, and freeze fresh physical units. Provide independent Classical material and its own appropriate comparator before making a Classical improvement claim. [P46 evidence](../data/vlm-benchmark-corpus.md) · [Local manuscript intake](../data/local-greek-page-intake-2026-11-04.md). |
| Papyrus | P01 has 27 exact local image/PAGE joins, 321 source lines and 6,318 raw line scalars. Five photographs are image-only. The historical split of 176 train, 50 development and 95 test lines is reproducible. Its test set is spent. The metadata ledger identifies P02 and P03 restrictions and P03/P04 derivative overlap. | Verify exact photograph terms, visible wording, geometry, lineage and suitable fresh object/hand coverage. No independent acceptance result from these inventories. [P01 intake](../data/local-greek-page-intake-2026-11-04.md#p01-zenon-papyri) · [Test-set reuse](locked-set-reuse.md) · [Rights and overlap](../data/papyrus-rights-overlap-2026-11-04.md). |
| Inscription | I.Sicily has 438 primary-edition metadata candidates under the conservative selector. Four checked manifests provide credited photographs. The Petrie catalogue has 24 entries, 18 with embedded images and Greek letters in the PDF text layer. | Zero rights-cleared, image-aligned reviewed lines in this intake. Resolve exact capture and edition terms, obtain source-aligned geometry and independently adjudicated readings, and group by object/site. A photographed entry with an edition is a sourcing lead. [Inscription audit](../data/inscription-intake-2026-11-04.md). |

The existing VLM blank, contrast and word studies used generated controls. The word-screen retry recorded 240 completed requests and verified worker cleanup. JSON responses passed the eight generated blank images across all three models, but there were wrong two-model agreements on text. The compact study also contains stable wrong unanimous readings. These findings support the proposed controls and failure accounting. They do not establish natural Greek accuracy or an acceptance rule based on agreement. [Word-screen report](vlm-word-screen-retry-2026-11-04.md) · [Compact agreement evidence](../handoffs/vlm-four-hour-2026-11-04.md#evidence).

## 3. Per-track score and decision worksheet

Duplicate this table for manuscript, papyrus and inscription. Record separate rows for each claim and adequately covered material, hand, capture or condition stratum. Attach a row ledger with all intended samples, failed/missing attempts and exclusions. Leave numeric thresholds blank until the owner and reviewer freeze them.

| Check | Proposed method and evidence fields | Frozen threshold / measured result / decision |
| --- | --- | --- |
| Dataset adequacy and reference quality | Count independent units, surfaces, eligible lines, reference scalars, marked-sign examples, controls and excluded spans by stratum. Join the exact source frame, crop and target revision. Freeze unresolved-span and alternate-reading treatment. | Pending / pending / pending |
| Diplomatic text | Report case-sensitive NFC scalar I/D/S and uncapped micro CER, grapheme error rate, macro unit CER, exact line fraction, length ratio and raw differences. Keep spacing, sigma, marks and punctuation. Record minimum unit coverage and confidence interval method. | Pending / pending / pending |
| Marks, signs and coverage | Report omissions, substitutions and insertions for visible accents, breathings, diaeresis, iota, overbars and abbreviations. Include supplied marks on unmarked lines, review exclusions, abstention and coverage. Use WER only with a separately annotated word layer. | Pending / pending / pending |
| Layout and order | Compare recognition with reviewed geometry to the complete automatic pipeline. Report overlap, one-to-one baseline matching and tolerance, precision/recall, line assignment, reading order, missing text, blank additions and clipped marks. Include difficult captures and distinguish layout from recognition errors. | Pending / pending / pending |
| Raw and assisted paths | Score frozen raw recognizer, language-assisted output and visual assistance in separate rows. Keep accepted editorial output separate. Record model/protocol status, grounding failures, unsupported restorations, overwritten witness variants, acceptance/rejection and residual quality. | Pending / pending / pending |
| Correction effort | Counterbalance route/editor/difficulty, preserve identical crops and interface, blind model and reference, collect active edit and elapsed seconds, and independently review residual text. Retain all assignments including timeouts and manual fallback; predeclare coverage and paired analysis. | Pending / pending / pending |
| Performance and lifecycle | Record target OS/CPU/GPU/backend, physical RAM, image dimensions, model size, preprocessing, cold load, warm p50/p95, lines/pages per minute, peak process and native/GPU memory, queue throughput, failures, sustained swap and cancellation/retry recovery. Separate resource budgets by workload. | Pending / pending / pending |
| Training/reload and regression | Record authorized budget, exact seed, data version, training-only alphabet, leakage checks, checkpoint/resume/reload evidence and reproducibility tolerance. Compare every previously supported profile with frozen baseline and registered regression margin. | Pending / pending / pending |
| Format and scholarly invariants | Pin schema versions and test fixture round trips for gap/unclear/supplied, abbreviations, hand and correction layers. Preserve originals, revisions and offsets, report unsupported fields, disable external XML entities, and block restorations from diplomatic training labels. | Pending / pending / pending |
| Workspace, security and independence | Test source-coordinate overlays, transforms and capture changes, polytonic input, grapheme-safe editing, undo, permissions and concurrency. Verify file/archive bounds, traversal and credential redaction, cancellation/recovery and the target release's independent CLI/API/worker package inventory. | Pending / pending / pending |

An accepted claim names the exact scope and evidence. Keep a failed gate and an underpowered or blocked gate distinct. Do not substitute a pooled average for any track's result. In the combined initial scope, manuscript, papyrus and inscription gates each require their own decision.

## 4. Separate study gates

**VLM-BENCH-1 research proposal.** Freeze the currently proposed intake minima, independent-unit precision assessment and budgets before natural requests. Compare R0, V1 image-only and V2 image plus fallible R0 on identical source crops, retaining three repetitions and separating text-only/mismatched diagnostic controls. Hide references from requests and pin package, projector, prompt, endpoint, decoding, response format and preparation. The protocol proposes at least 10% relative micro-CER reduction with the paired physical-unit 95% interval wholly below zero, 10,000 resamples, no covered-stratum regression or known mark loss, and at least 99% valid natural completions. It proposes 40 successful valid empty no-trace controls, separate unreadable and altered-image checks, at least 20% median end-to-end correction savings with no residual-quality loss, and warm p95 at most 30 seconds per line with combined memory at most 70% of physical RAM and no OOM or sustained swap growth. These are proposed research gates, pending evaluator freeze. Select at most one condition for independent confirmation. [Protocol](vlm-lmstudio-benchmark.md#evaluation-and-decision-criteria) · [Editor and custody preflight](vlm-reviewed-run-preflight.md).

Freeze how each score view handles failures. Preserve raw responses and status. Plain text includes the full captured model text even when malformed or truncated; a natural slot without output counts as a deletion of the whole reference. The strict JSON view separately accounts for decoded text, rejected responses and raw-representation scores. Apply the declared view consistently without repairing malformed data or switching views after seeing results. HTTP error bodies remain transport diagnostics. Grounding controls retain failure and insertion counts in separate denominators. Keep repetitions clustered by physical unit. An unavailable suitable R0, zero R0 error for a relative-gain claim, or insufficient independent coverage prevents the corresponding claim. [Scoring contract](vlm-benchmark-scoring.md).

**P46 NT-15/17/18 research sequence.** The training brief requires a named reviewed dataset, rights, source-linked geometry, training-only alphabet and disjoint splits before fitting. Its study-specific control proposes 4–8 reviewed training lines, at most 4,000 updates, at most 2% training CER, nonempty output and finite loss. Pass that control before considering the registered matched T0/T1/T2 matrix and its separate five-point median development screen, paired-seed and per-leaf rules. The closed provisional studies do not pass the advancement criteria and do not supply reviewed references. NT-08 product acceptance and NT-20 resource validation remain separate. [Next training brief](../tasks/p46-next-training.md) · [Reviewed corpus brief](../tasks/p46-reviewed-corpus.md) · [Closed expansion study](p46-expanded-training-study.md).

## 5. Offline no-fit checks

Run from the repository root with the recorded local artifact directories and a built CLI. These commands only verify receipts or rescore frozen outputs. They use no live provider and make no training calls.

```sh
audit=.artifacts/vlm-benchmark/p46-comparator-audit-v1
intake=.artifacts/vlm-benchmark/worksheet-intake-v1
retry=.artifacts/vlm-benchmark/consensus-word-retry.Dw2XS8
cli=src/LeviathanText.Cli/bin/Release/net10.0/LeviathanText.Cli.dll
out=$(mktemp -d "${TMPDIR:-/tmp}/leviathan-acceptance-XXXXXX")

PYTHONDONTWRITEBYTECODE=1 python3 "$audit/test_audit.py" > "$out/comparator-check.json"
python3 "$intake/verify.py" > "$out/intake-check.json"
dotnet "$cli" benchmark vlm-validate "$retry/fixtures/block-1/manifest.json" > "$out/validation.json"
dotnet "$cli" benchmark vlm-score "$retry/visit-1/qwen3-vl-8b-instruct/transcription-json-v1" \
  --references "$retry/fixtures/block-1/references.json" --out "$out/score.json"
python3 - "$out" "$retry" <<'PY'
import json
import sys
from pathlib import Path

out, retry = map(Path, sys.argv[1:])
new = json.loads((out / "score.json").read_bytes())
old = json.loads((retry / "visit-1/qwen3-vl-8b-instruct/transcription-json-v1.score.json").read_bytes())
if new["plannedRequests"] != 10 or new["reports"] != old["reports"]:
    raise SystemExit("Frozen score replay differs")
PY
```

The comparator verifier exercises nine negative cases and the intake verifier exercises 18 checks. The latter tests mechanical worksheet integrity and attribution fields. It cannot authenticate a person or approve a reading. For the 51-row worksheet, run the [return checker](vlm-reviewed-run-preflight.md#mechanical-intake-of-a-returned-p46-worksheet) against the separately saved attributed copy. The timing-assignment [dry-run replay](vlm-reviewed-run-preflight.md#offline-dry-run-evidence) and [paired-bootstrap invented fixture](../../.artifacts/vlm-benchmark/paired-bootstrap-v1/README.md) are also available locally. They establish accounting and arithmetic only.

For a future engineering comparison, use `runs regress-suite` with the evaluator's frozen criteria and already recorded baseline and candidate IDs. The `evaluate lines` command performs packaged-model inference on training/development rows and records an evaluation. It does not perform fitting and does not access a locked split. Its sample thresholds in the operations guide are illustrations. [CLI operations](../operations.md#evaluation-and-error-drilldown).

## 6. Sign-off and result

| Role | Name | Evidence receipt | Decision and exact scope | UTC date |
| --- | --- | --- | --- | --- |
| Project owner | | | | |
| Greek specialist / adjudicator | | | | |
| Engineering owner | | | | |
| Evaluator | | | | |
| Confirmation custodian | | | | |

Retain the frozen sheet and append results separately with exact run and review hashes. Record each track as pass, fail, blocked, or inconclusive, with the eligible scope and disposition. Keep reviewer identities and confirmation artifacts under the agreed access controls.
