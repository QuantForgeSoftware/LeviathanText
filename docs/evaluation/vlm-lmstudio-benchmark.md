# VLM benchmark through LM Studio — VLM-BENCH-1

Planned 2026-11-04; the original planning assignment performed no model operations.
The owner's subsequent three-hour assignment activated implementation and bounded local
inference diagnostics from 07:29:19 to 10:29:19 CST on 2026-11-04. This is a new inference
comparison, separate from the closed P46 training studies. Training, fine-tuning,
distillation and model promotion remain out of scope. The two
[A engineering](../tasks/vlm-lmstudio-engineering.md) and
[B evaluation](../tasks/vlm-corpus-evaluation.md) briefs can proceed in parallel.

The first phase is now closed in the [execution handoff](../handoffs/vlm-session-2026-11-04.md).
The owner's later extension to 13:00 CST activates the separately registered
[paired JSON-schema diagnostic](vlm-json-schema-benchmark.md). Its synthetic inputs, requests,
deadline and results do not revise the closed VLM-BENCH-1 studies below.

### Initial execution scope (registered before collection-image inference)

The first execution is an exploratory engineering pilot, not the reviewed selection or
confirmation study below. Its frozen P46 release has 51 provisional crops across five
physical units, with 44 lines exposed to R0 training and seven repeatedly used for
development. All specialist decisions remain pending. The original source references
use lowercase normalized spelling and σ/ς, whereas a diplomatic VLM output may preserve
visible capitals, lunate sigma and marks. Raw distance to these source references must
not be presented as diplomatic transcription accuracy or a model-selection result.

Use five synthetic controls (contrasting Greek strings, polytonic text, lunate sigma,
and a blank image) with three repetitions per admitted candidate before natural-image
inference. These establish the transport/image-consumption gate, not manuscript support.
Run the 51 natural crops in image-only and frozen-R0-assisted conditions, initially one
repetition each, in the registered Qwen3, Qwen2.5, Mistral order. This first sweep is 306
natural requests plus 45 synthetic requests if all three candidates pass admission.
Here diagnostic admission means demonstrated image conditioning and workable resource
bounds. A failed blank control is retained and blocks adoption; it does not prevent an
explicitly exploratory natural-image diagnostic. No failed control is reclassified as a pass.
One loaded model and one request at a time; retain every failure and missing planned row.
Use seed42 ordering, a 512-output-token cap and the frozen `vlm-diplomatic-1` prompt.
Any additional repetitions or geometry
diagnostics must be separately named and registered before their outputs are inspected.

Allow at most 15 minutes per natural condition and no new natural run after 09:45 CST;
reserve the remaining session for verification, scoring and handoff. Incomplete matrices
remain incomplete evidence; do not trim samples or promote a model from partial results.
The larger reviewed study and its 4,000-request cap below remain future work. Record
download/build overlap as a timing confound; collect isolated timing checks before any
performance claim. Existing system swap is baseline state, so resource receipts must
also retain changes in swap activity during each condition.

Execution receipts are under `.artifacts/vlm-benchmark/vlm-bench-1/`. Frozen input and
rights/geometry evidence belong to B; transport to A; CLI integration, model lifecycle,
live inference and shared plan changes to the integrator. The command supports exploratory
runs only and cannot certify selection/confirmation eligibility.

Secondary setup registration (2026-11-04, before these requests):
`synthetic-misleading-ocr-v1` reuses the five exact rendered images with deliberately
incorrect OCR strings, including a nonempty Greek hint on the blank image. These hints
are invented controls, **not R0 predictions**. Run the unchanged OCR-assisted prompt
three times per model (45 requests), in Qwen2.5/Mistral/Qwen3 order to reuse loaded
models. Keep this condition separate from natural V2 and score against the original
rendered text/empty control. Its purpose is to expose image-versus-text grounding failures;
it cannot establish natural-text accuracy. The immutable registration and hashes are in
`root/synthetic-misleading-ocr-v1/registration.json` under the artifact root above.

A further synthetic-only setup probe is registered in
`root/prompt-probe-registration.json`: two fixed prompts emphasize visible case/lunate
forms and prohibit inferred spaces/marks; one returns plain text and one requests exactly
`{"transcription":"..."}`. Use the same five images, three repetitions, fixed sampling
seed42, order seeds42/1729/2026 and 512 tokens for each of the three models (90 requests).
Exact prompt texts/hashes are frozen in `root/prompt-probe/profiles.json`. Preserve raw
responses alongside strict JSON validation; reject extra/duplicate properties and never
repair malformed output. This tests whether an explicit empty-string representation helps
blank behavior. It is prompt-only JSON, distinct from LM Studio's documented
[schema-constrained output](https://lmstudio.ai/docs/developer/openai-compat/structured-output),
and cannot establish manuscript accuracy. No natural images enter this probe.

Repeatability extension, registered before its requests in
`root/repeatability-registration.json`: repeat the unchanged full 51-crop cohort for
all three original models and both natural conditions, three repetitions each (918
additional requests), in Mistral/Qwen3/Qwen2.5 order. Keep prompt `vlm-diplomatic-1`,
sampling seed42, order seeds42/1729/2026 and 512 tokens. This checks repeated outputs
and failure stability; it is not a new independent dataset or selection study. Retain
the first sweep separately. Cap each condition at20minutes and all natural inference
at09:45CST, reducing the per-run cap to the remaining time; explicitly record unstarted
or unfinished conditions. The original no-training and provisional-label limits remain.

Matched synthetic geometry diagnostic, registered in `root/geometry-registration.json`
before its requests: ten generated Greek font fixtures (nine text, one blank) have the
same 1,695×64 strip dimensions as the P46 crops. Compare the exact original pixels,
white vertical padding to1,695×512, and integer nearest-neighbor2× replication to3,390×128.
All5,424,000 translated/replicated pixel checks passed. The same text is expected for
each representation; no handwritten target or original P46 pixel is changed. Run one
repetition per variant/model with the unchanged original prompt and sampling seed42
(90requests), then report paired synthetic differences without a confidence interval or
handwriting claim. Runtime scaling/tiling may counteract these external transformations;
the diagnostic measures that behavior rather than assuming that more pixels help.

## Question and scope

Does a locally served VLM improve visible Greek transcription or reduce human correction
time, compared with the existing recognizer on the same images, without silently supplying
unwitnessed text? Evaluate Koine and Classical Greek across manuscripts, papyri and
inscriptions separately. P46 alone establishes neither Classical nor inscription coverage.
Printed-edition research motivates the experiment but supplies no handwriting accuracy
promise: [Greek edition recognition](https://arxiv.org/abs/2603.02803) and
[visually unsupported Greek output](https://arxiv.org/abs/2605.27750).

This first benchmark uses reviewed line crops; full-page detection, reading order and
automatic segmentation need a separate study. Keep model transcription as raw L0 evidence. Human-verified diplomatic text is L1;
expansion, restoration, supplied accents and interpretation remain explicit L2/L3 proposals.
Never turn model agreement, a self-reported confidence score or fluent Greek into accepted
ground truth. Existing text-only AI functionality and recognition defaults remain intact.

## Concrete candidate register

Source cards and package listings checked 2026-11-04. These are candidates, not measured
winners. Prefer the following Q4_K_M packages for the first bounded comparison; the matching
vision projector is mandatory. Approximate sizes below are downloads, not working-memory
requirements. Exact revisions and SHA-256 values must be pinned before execution.

| Order / role | Upstream model and terms | LM Studio package and required files |
| --- | --- | --- |
| 1 — primary | [Qwen/Qwen3-VL-8B-Instruct](https://huggingface.co/Qwen/Qwen3-VL-8B-Instruct), Apache 2.0 | [LM Studio community GGUF](https://huggingface.co/lmstudio-community/Qwen3-VL-8B-Instruct-GGUF/tree/main): `Qwen3-VL-8B-Instruct-Q4_K_M.gguf` (~5.03 GB) and `mmproj-Qwen3-VL-8B-Instruct-F16.gguf` (~1.16 GB). |
| 2 — smaller-generation comparator | [Qwen/Qwen2.5-VL-7B-Instruct](https://huggingface.co/Qwen/Qwen2.5-VL-7B-Instruct), Apache 2.0 | [LM Studio community GGUF](https://huggingface.co/lmstudio-community/Qwen2.5-VL-7B-Instruct-GGUF/tree/main): `Qwen2.5-VL-7B-Instruct-Q4_K_M.gguf` (~4.68 GB) and `mmproj-model-f16.gguf` (~1.35 GB). |
| 3 — different-family, hardware-gated comparator | [mistralai/Mistral-Small-3.1-24B-Instruct-2503](https://huggingface.co/mistralai/Mistral-Small-3.1-24B-Instruct-2503), Apache 2.0 | [LM Studio community GGUF](https://huggingface.co/lmstudio-community/Mistral-Small-3.1-24B-Instruct-2503-GGUF/tree/main): `Mistral-Small-3.1-24B-Instruct-2503-Q4_K_M.gguf` (~14.3 GB) and `mmproj-model-f16.gguf` (~878 MB). |

Start with Qwen3, then Qwen2.5; admit Mistral only after the hardware check. Similar Qwen
outputs are not independent corroboration. Do not silently substitute a text-only Qwen,
Thinking variant, different quantization, MLX conversion or remote service. Such a change
creates another named condition. A higher-precision quantization is a later sensitivity
study, not a best-of-many selection on the evaluation set.

For each package, retain upstream and conversion repository revisions, license text/hash,
tokenizer/chat-template identity, quantization, projector hash and LM Studio model ID.
Apache declarations cover these model repositories; they do not relicense manuscript
images, source transcriptions or the LM Studio application. Verify actual selected files
and notices before acquisition/distribution. No redistribution is planned here.

## LM Studio transport and compatibility gate

Use the owner's LM Studio server. Proposed local base is `http://127.0.0.1:1234/v1`, subject
to the actual configured address; it is not a claim that a server is installed or listening.
Reuse the C# HTTP-provider boundary, with `Host=Local` and OpenAI-compatible chat completions.
Current `AiProposalRequest` and HTTP messages carry text only: image evidence and bounded
image serialization require an explicit extension, not merely another model name.
See [LM Studio chat completions](https://lmstudio.ai/docs/developer/openai-compat/chat-completions).

Plan image-bearing content parts with an inline PNG data URI and accompanying text. Prove
that the installed LM Studio/runtime/model combination actually consumes the image using
two contrasting synthetic images and a blank control; HTTP 200 alone does not pass. LM
Studio documents image support and PNG/JPEG/WebP input, while its native chat API explicitly
documents image `data_url` input. If the compatible endpoint cannot handle the chosen
combination, stop that condition and specify a separately tested C# native-API adapter;
never fall back silently to text-only inference. No Python runtime is needed in the product.
[Image input](https://lmstudio.ai/docs/typescript/llm-prediction/image-input),
[native chat specification](https://lmstudio.ai/docs/developer/rest/chat).

Record application version, inference runtime/build, CPU/GPU, RAM/VRAM, OS, offload settings,
context size, image preprocessing/tiling, projector, prompt template and model identifier.
Use loopback by default; a different machine needs an explicit endpoint and appropriate
authentication. Keep credentials out of receipts. Do not send collection images to a cloud
fallback or use remote image URLs: transmit authorized, hashed crop bytes to LM Studio.

Initial engineering limits: one loaded model and one in-flight request; encoded PNG file
<=8 MiB and decoded image <=4 million pixels, HTTP request <=12 MiB, response <=1 MiB, 120-second timeout, one attempt
without automatic retries. These are proposed harness bounds, to be frozen before scores.
Oversize/unsupported input is a recorded failure, never silently downsampled or dropped.
Cancellation, unavailable model/projector, malformed output, timeout and token truncation
must produce explicit statuses. Any retry is a separately retained attempt.

## Data, review and exposure

The current 51 P46 proposed targets/maps are provisional and the original 81 specialist
decisions remain pending. Pages 1440/1450/1460 and other inspected/trained leaves are exposed
development material. Use them for labeled exploratory diagnostics only; they are not a
blind test set and may not be relabeled as one. B coordinates with NT-PILOT-B2 rather than
creating a competing accepted corpus. Training gates NT-15/17/18 remain unchanged.

Plan three disjoint releases, with actual available counts reported instead of invented:

1. **Setup:** up to 20 reviewed development lines plus synthetic fixtures, used to settle
   transport, prompts, crop scale and output parsing. Any inspected item stays exposed.
2. **Selection:** target 120 reviewed lines on at least five physical units, disjoint from
   setup units. For a three-track claim require at least 30 lines and three independent
   units per track; otherwise report only the available track. These are intake minima,
   not evidence that a clustered interval is reliable. B/evaluator must preregister a
   sample-size/precision assessment from setup evidence before selection; insufficient
   independent units for the planned effect means inconclusive evidence. Freeze strata for clear,
   faint/damaged, abbreviations/overbars, diacritics, uncommon letters and corrections.
3. **Confirmation:** target at least 60 fresh reviewed lines on disjoint physical units,
   controlled by an evaluator after a candidate/prompt is frozen. A wider generalization
   claim additionally requires an unseen witness/hand and Classical/non-biblical material.

For P46, the grouping unit is the physical leaf (recto/verso together); elsewhere use the
object/manuscript lineage, keeping fragments, photographs, crops and derivatives together.
Record repeated passages and known edition overlap. Strip shelfmarks, verse references,
book/chapter names and reference transcripts from requests; opaque IDs remain in the local
receipt. This reduces cues but cannot prove a foundation model never saw the text.
Do not tune using reserved acceptance labels or turn this benchmark into their exposure.

Each record needs source/holding ID, rights evidence, image and crop hashes, source-frame
geometry/transform, physical group, material/hand/profile, review state/reviewer, target
revision, exclusions/ambiguities and exposure history. References require independent Greek
review and adjudication; unresolved spans stay unresolved. AI instances can prepare these
records but cannot fill scholarly acceptance fields on a specialist's behalf.

## Frozen comparison and request policy

| Condition | Input and interpretation |
| --- | --- |
| R0 | Existing frozen recognizer/checkpoint and its declared preprocessing/decoder; raw output, no new fitting. |
| V1 | One image crop, diplomatic-only instruction, no OCR hypothesis or edition text. |
| V2 | The identical crop plus R0's raw hypothesis, clearly marked fallible; visual correction only. |

Run V1 and V2 for each admitted model; do not let V1 see V2 history. On a prespecified
diagnostic subset add image-free text correction and mismatched-image controls to measure
how much apparent benefit comes from language completion. These are diagnostics, not
candidate routes. Fix identical image bytes across VLMs; let R0 use its documented trained
transform from that same source crop and record both inputs. Do not feed VLMs the CRNN's
height-16/32 tensor as their only visual evidence. Freeze any higher-resolution recipe using
setup data; no per-failure crop or prompt rescue on selection/confirmation material.

Prompt requirements: transcribe visible letters/marks only; retain spelling, case, sigma
forms and written spacing; do not expand nomina sacra, supply lost letters, normalize
grammar or add unseen accents. Return plain text, or an empty string when there are no
identifiable character traces.
Represent a visibly present but unreadable character with the profile's predeclared marker;
it counts as an error against a known reference. No prose, markdown, translations or
alternative lists in the primary transcription field. Preserve the entire raw response.
Prompt compliance is measured, not assumed.

Initial run proposal: fresh stateless request per crop, temperature 0, top_p 1, seed 42
where supported, max output 1,024 tokens and context 8,192 after proving image+prompt fit.
Record unsupported/ignored parameters; temperature 0 does not guarantee identical outputs.
Repeat each condition three times in independently shuffled orders (order seeds
42/1729/2026), with all repetitions retained rather than selecting the best output.
Three warm-up setup requests per loaded model precede warm timing; report cold loading
separately. Use the same hardware/backend policy and no simultaneous training/profiling.
For all three models, 120 selection lines x two routes x three repetitions = 2,160 VLM
requests; 60 controls x two routes x three repetitions x three models = 1,080 more.
Reserve up to 216 image-free/mismatched-image diagnostic requests and nine warm-up requests.
This totals 3,465 requests. Eight hours would require a mean below roughly 8.3 seconds per
request even before cold loading; the p95 target alone does not prove feasibility. Use only
setup timing to admit a balanced two- or three-model matrix before any selection scores.
Cap the selection stage at 4,000 requests and eight wall-clock hours; if it cannot fit,
register a smaller balanced matrix before scores or report an incomplete experiment.
Confirmation needs its own frozen request/time budget.

Freeze R0 checkpoint/code hashes, eligible strata, dataset versions, prompts, decoding,
normalization, failure scoring and resource budgets before selection. A missing compatible
R0 for a material is a documented comparison gap, not a zero-accuracy baseline. Each
material requires its own appropriate comparator before a relative-improvement claim.

## Evaluation and decision criteria

These are proposed research decision rules for VLM-BENCH-1, not established product-release
thresholds. B/evaluator freezes them before outputs are inspected; any change creates a new
protocol version. Failure or insufficient data is a valid result.

| Measure | Definition / proposed gate |
| --- | --- |
| Primary recognition | Unicode-scalar CER = (substitutions + deletions + insertions) / reference scalars after the existing versioned NFC scoring policy. Preserve accents, sigma, case and real spacing; no compatibility folding. Retain raw output and report I/D/S, output/reference length ratio, exact-line accuracy, micro CER and macro physical-unit CER. Never cap CER at 100%; no primary WER on unsegmented scriptio continua. |
| Candidate selection | For each material claimed improved: at least 10% relative micro-CER reduction versus its R0, with a paired 95% bootstrap interval for candidate-minus-R0 CER entirely below zero (10,000 resamples by physical unit). Report all repetitions and variability; do not count repeats as independent samples or use pooled cross-track improvement to pass a failing track. If R0 CER is zero, no relative-gain claim is possible. Small/undercovered groups mean inconclusive evidence, not automatic acceptance. |
| No hidden quality regression | No observed CER increase in any prespecified adequately covered material/condition stratum; report low-count strata as unproven. Report visible diacritic/overbar/abbreviation fidelity separately and reject known losses even if aggregate CER improves. |
| Failures and abstention | Include every eligible line in the denominator. No model output means an empty hypothesis (all reference characters deleted). If invalid/truncated/failed generation emitted text, score the entire captured model text, including prose and inserted characters, under the frozen NFC policy; never replace nonempty output with empty to hide insertions. Keep status/failure rates separate from CER and require >=99% valid completed natural-line requests for adoption screening. HTTP error bodies are diagnostics, not model text. Report CER-versus-coverage; abstaining cannot remove difficult lines from the primary denominator. |
| Invented text | Add 20 blank crops, 20 reviewed damaged/background crops with no identifiable character traces, and 20 further controls (10 visible-but-unreadable trace cases plus 10 masked/counterfactual cases), separately scored from natural accuracy. All 40 no-trace controls must yield successful valid empty responses; errors/timeouts/truncations never pass. Visible unreadable traces follow the frozen uncertainty-marker contract, with zero invented letters; masked cases are audited against their altered evidence. These are finite controls, not proof of no hallucinations. A Greek reviewer audits every alleged variant correction and suspected completion. Synthetic edited images never become natural ground truth or training data. |
| Correction effort | Blind model identity and randomize/counterbalance matched line assignments across reviewers; avoid one reviewer correcting the same line repeatedly. Record time including model wait, residual errors, edits and rejected suggestions. Target >=20% median end-to-end correction-time reduction with no residual-quality loss; uncertainty/insufficient reviewer coverage blocks an effort claim. |
| Resources | Record cold start, image encoding, time to first token where measurable, full-response p50/p95, lines/minute, peak client+server RSS/GPU memory, errors and model switches. Proposed assisted-route budget: warm p95 <=30 seconds/line, peak combined memory <=70% of physical RAM, no OOM or sustained swap growth. Register actual host limits before running; a download size is not a RAM estimate. |
| Existing behavior | The optional VLM route must not alter R0 pixels, text-only provider payloads, L1 or existing model compatibility. Require focused fail-first tests for implementation changes, affected suite/build checks, and measured R0 route latency/RSS before/after; investigate any observed slowdown rather than absorbing it into a new VLM budget. |

Selection chooses at most one model/route for independently reviewed confirmation, using
quality gates first and correction time/resource use to break ties. Reapply the frozen
quality and grounding gates on fresh material. A slower VLM cannot be advertised as a
recognizer-speed improvement; a workflow claim requires the correction-time evidence.
If quality improves but effort/resources remain unproven, retain it as an experimental
optional reader. No automatic replacement, training-label acceptance or release follows.

## Shared handoff contract and parallel sequence

Contract label `VLM-BENCH-1` is a documentation contract, not a change to the product's
serialized ContractVersion. Root/integrator owns this specification and shared plans.
A owns the transport/harness proposal; B owns evidence/scoring/corpus proposals. Neither
edits the other's files, old P46 artifacts or existing uncommitted review fixes.

The exchange consists of: (1) A's capability receipt with the exact LM Studio/model/runtime
identity and supported fields; (2) B's versioned input manifest and separate label/scoring
receipt; (3) a frozen run manifest; (4) per-request outputs joining opaque sample ID, image
hash, model/condition/repetition, prompt hash, raw result, parsed result/status, timing and
resource measurements. Null means unavailable, never fabricated zero. No reference text
travels in V1/V2 request records. A does not receive confirmation labels.

Both instances may prepare documentation and synthetic fixture specifications independently.
Later A implements and proves transport; B assembles reviewed eligible material. Run
selection only after both exact handoffs are accepted by the integrator and an execution
assignment exists. Freeze the selected condition before evaluator-controlled confirmation.
Record failed compatibility, absent rights, missing reviewers and insufficient hardware as
named gaps. Current gaps: no installed-runtime capability receipt, no pinned local package
hashes, no verified target memory budget, unresolved P46 labels and no ready multi-track
reviewed/confirmation set. None is claimed resolved by this plan.
