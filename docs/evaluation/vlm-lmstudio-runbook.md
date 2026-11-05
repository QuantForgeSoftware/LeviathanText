# Running the local VLM benchmark through LM Studio

Checked against the CLI and LM Studio documentation on 2026-11-04. This runbook produces
exploratory inference evidence. It performs no training, opens no project database, and
does not create or change L1 transcriptions, L2 interpretations or accepted proposals.
`Completed` describes a valid transport completion, not correct Greek or visual grounding.

Read the [benchmark specification](vlm-lmstudio-benchmark.md) for study design and gates,
the [transport contract](vlm-lmstudio-transport.md), [scoring policy](vlm-benchmark-scoring.md),
[corpus inventory](../data/vlm-benchmark-corpus.md), and the
[CLI](../handoffs/VLM-CLI.md), [transport](../handoffs/VLM-A1.md) and
[evaluation](../handoffs/VLM-B1.md) handoffs. A fresh clone does not include local `.artifacts`
receipts, downloaded models or the owner's image files. Obtain an authorized frozen corpus
separately; do not assume the historical paths in those handoffs exist on your machine.

## Build the CLI

Use a .NET 10 SDK, as required by the projects. From your checkout root:

```sh
dotnet --info
dotnet restore src/LeviathanText.Cli/LeviathanText.Cli.csproj
dotnet build src/LeviathanText.Cli/LeviathanText.Cli.csproj -c Release --no-restore -m:1
```

The commands below use `src/LeviathanText.Cli/bin/Release/net10.0/LeviathanText.Cli.dll`.
Keep the complete output directory together with its dependencies. Record the checkout
commit, uncommitted diff if any, SDK/runtime and build output. Freeze a copy of the compiled
directory for a comparison; do not rebuild it while that comparison is running. No Angular
server, project import, `--store` option or training command is required.

## Prepare one LM Studio candidate

Install and launch LM Studio once, then check `lms --help` and `lms get --help`.
`lms` ships with LM Studio; command availability can vary by installed version.
See the [official CLI setup](https://lmstudio.ai/docs/cli).

The registered GGUF candidates are listed below. These are exact file selections, not
claims that the models meet the Greek recognition or hardware gates. The two files in each
row form a pair; the similarly named Qwen2.5 and Mistral projector files are different files.

| Candidate/package source | Main model file | Matching vision projector |
| --- | --- | --- |
| [Qwen3-VL-8B-Instruct](https://huggingface.co/lmstudio-community/Qwen3-VL-8B-Instruct-GGUF/tree/main) | `Qwen3-VL-8B-Instruct-Q4_K_M.gguf` | `mmproj-Qwen3-VL-8B-Instruct-F16.gguf` |
| [Qwen2.5-VL-7B-Instruct](https://huggingface.co/lmstudio-community/Qwen2.5-VL-7B-Instruct-GGUF/tree/main) | `Qwen2.5-VL-7B-Instruct-Q4_K_M.gguf` | `mmproj-model-f16.gguf` |
| [Mistral-Small-3.1-24B-Instruct-2503](https://huggingface.co/lmstudio-community/Mistral-Small-3.1-24B-Instruct-2503-GGUF/tree/main) | `Mistral-Small-3.1-24B-Instruct-2503-Q4_K_M.gguf` | `mmproj-model-f16.gguf` |

Download the candidate you intend to run, using its full repository URL. Select the exact
Q4_K_M file above and verify the resolved publisher/repository before accepting the download:

```sh
lms get https://huggingface.co/lmstudio-community/Qwen3-VL-8B-Instruct-GGUF --gguf
lms get https://huggingface.co/lmstudio-community/Qwen2.5-VL-7B-Instruct-GGUF --gguf
lms get https://huggingface.co/lmstudio-community/Mistral-Small-3.1-24B-Instruct-2503-GGUF --gguf
```

These are three alternative acquisitions, not a requirement to download all models for a
smoke check. If the installed CLI auto-selects a different variant, use its variant selector
(`--select` in the checked installation; older documentation lists
`--always-show-download-options`) or select the file in LM Studio's download UI. Avoid an
unqualified search or automatic approval that silently picks another package. The CLI also
supports a model-name `@q4_k_m` qualifier, but still verify the resolved repository and file.
[LM Studio download reference](https://lmstudio.ai/docs/cli/local-models/get),
[Hugging Face's LM Studio integration](https://huggingface.co/docs/hub/lmstudio).

After acquisition, locate the package in the model directory configured in LM Studio and
verify that **both** named files are present. If the projector is absent, stop and obtain
that exact file from the same pinned package revision before loading the pair. Keep it
beside its model in that package directory; never substitute a projector from another row.
Record repository revision, file sizes and SHA-256 values, and retain model/license/template
metadata. `lms get` alone is not a pinned-revision reproducibility receipt. Confirm projector
loading in the LM Studio logs and retain that evidence; a successful text response cannot
establish image support. The [image-input documentation](https://lmstudio.ai/docs/typescript/llm-prediction/image-input)
describes the VLM requirement and supported image inputs; this runner deliberately accepts PNG only.

List installed models and use the exact returned **model key**, not an assumed filename:

```sh
lms ls --json
```

For the registered packages, the pilot observed `qwen3-vl-8b-instruct`,
`qwen2.5-vl-7b-instruct` and `mistral-small-3.1-24b-instruct-2503`. Treat those as lookup hints;
the key returned on your machine is authoritative. Check memory admission first, especially
for Mistral. The following is a template: replace `MODEL_KEY_FROM_LMS_LS` and choose/freeze
GPU settings appropriate to your machine. The 8192-token context is the pilot setting, not
a universal optimum.

```sh
lms load MODEL_KEY_FROM_LMS_LS --context-length 8192 --gpu max --estimate-only
lms load MODEL_KEY_FROM_LMS_LS --context-length 8192 --gpu max --identifier lt-vlm-candidate
lms ps --json
lms server start --bind 127.0.0.1 --port 1234
lms server status
```

Use `lt-vlm-candidate`, the assigned loaded identifier, as the benchmark `--model` value.
Unload your previously loaded candidate before switching; do not disturb someone else's
service. Run one loaded benchmark model and one request stream at a time. Record actual
context/offload/runtime settings, not just the estimate. The documented commands cover
[model keys, identifiers and load estimates](https://lmstudio.ai/docs/cli/local-models/load)
and [loopback server binding](https://lmstudio.ai/docs/cli/serve/server-start).

The runner defaults to `http://127.0.0.1:1234/v1` and permits only loopback HTTP `/v1`
endpoints, with no embedded credentials, query or fragment. It supports no remote/cloud
fallback. For an authenticated local server, explicitly add `--credential-env LM_API_TOKEN`
to the inference command, where `LM_API_TOKEN` names an environment variable you populated
through your existing secure environment setup. Pass the **variable name**, never the
token value. The name must contain 1–128 ASCII letters/digits/underscores and start with a
letter or underscore. No credential is discovered implicitly when the option is absent.
LM Studio documents API-token authentication and bearer headers for version 0.4.0 onward:
[authentication reference](https://lmstudio.ai/docs/developer/core/authentication).

The CLI uses the existing credential transport and saves only the nonsecret reference.
Missing/blank credentials produce recorded configuration failures without HTTP; 401/403
responses remain failed attempts with no keyless retry. Request-body hashes and structured
format audits exclude authorization headers. If a response echoes the configured token,
the provider redacts it and the score discloses the redaction; those retained strings are
not exact original server output. Redacted and failed rows stay in the denominator.
Offline validation/scoring neither needs nor accepts the credential option. This client
wiring is verified with dummy-token mocks; a live authenticated-server test remains
unperformed. Do not disable server authentication to work around a client configuration.

Before collection-image inference, run the specification's contrasting synthetic images,
polytonic/lunate controls and blank image for three repetitions. Retain every output.
Different visible inputs must produce grounded differences; an HTTP success or `vision`
flag is insufficient. A failed blank control remains a failed gate even when a separately
authorized exploratory natural-image diagnostic continues. Do not alter a prompt, image,
model or interpretation of a control inside a frozen condition.

## Prepare the request manifest and separate references

All paths and placeholder values in the examples below are **templates, not existing
files**. Create your own `example-corpus/manifest.json`, authorized static PNGs and evidence
files. Resolve image paths relative to the containing manifest directory. Calculate the
SHA-256 of the exact PNG bytes, for example with `shasum -a 256 /ABSOLUTE/PATH/TO/example-corpus/crops/line-001.png`
on macOS, and replace `REPLACE_WITH_64_HEX_SHA256` below. Do not resize or rewrite a crop
after freezing its hash.

```json
{
  "protocol": "VLM-BENCH-1",
  "datasetId": "example-greek-pilot-v1",
  "datasetVersion": 1,
  "releaseId": "example-r1",
  "runPurpose": "exploratory",
  "pathBase": "containing-manifest-directory",
  "sourceEvidenceBase": "/ABSOLUTE/PATH/TO/example-corpus",
  "samples": [
    {
      "sampleId": "line-001",
      "imagePath": "crops/line-001.png",
      "imageSha256": "REPLACE_WITH_64_HEX_SHA256",
      "physicalUnit": "leaf-001",
      "material": "papyrus",
      "reviewState": "provisional",
      "exposure": "unknown",
      "rightsEvidenceRef": "provenance.json#/samples/0/rights",
      "sourceGeometryRef": "provenance.json#/samples/0/geometry"
    }
  ]
}
```

Keep recto/verso and lines from the same physical unit grouped together. Record actual
training/development/source-inspection exposure; `unknown` is not a held-out claim. Point
rights evidence to the item-specific permission record, and geometry evidence to the source
image hash, crop coordinates/transform and provenance needed to reproduce the crop.
`sourceEvidenceBase` is optional but, when supplied, must be a bounded absolute filesystem
path. It preserves the resolution context of relative evidence references and their JSON
Pointer fragments. The CLI records these references; it does **not** open them, verify
permissions or adjudicate geometry. That review belongs to the corpus owner.

Place offline targets in a separate `references.json` wrapper:

```json
{
  "protocol": "VLM-BENCH-1",
  "datasetId": "example-greek-pilot-v1",
  "datasetVersion": 1,
  "references": [
    {
      "sampleId": "line-001",
      "physicalUnit": "leaf-001",
      "material": "papyrus",
      "referenceText": "αβ",
      "controlKind": "NaturalLine",
      "reviewed": false,
      "reviewer": null,
      "scholarlyAccuracyEligible": false
    }
  ]
}
```

`αβ` is an invented schema example, not a transcription of any manuscript. Replace it with
the independently managed target and preserve its source/review record. The wrapper's
dataset identity and the complete sample/unit/material cohort must exactly match the run.
For a verified blank fixture use `NoCharacterTraces` and an empty reference; the other
supported control kinds are `UnreadableTraces` and `MaskedCounterfactual`, with the
constraints in the scoring policy. Do not relabel unreadable traces as a blank.

Reference text is never accepted in the request manifest; unknown fields are rejected.
The reference wrapper's review metadata documents provenance but the scorer does not
independently establish its truth. Current CLI runs are always exploratory with
`selectionEligible=false`. Every non-synthetic sample requires `--allow-provisional`,
including a sample whose local label says reviewed; that flag permits the exploratory
operation and grants no scholarly acceptance. The existing P46 release has **zero reviewed
selection/confirmation lines**, and its normalized source-reference distance is not
diplomatic accuracy. Other manuscript, papyrus and inscription strata require their own
reviewed targets and material-specific evidence.

## Validate, run and score

The following template commands assume you replaced the example paths and prepared the
files above. Validation decodes and hashes images without contacting a model or opening a
project store:

```sh
dotnet src/LeviathanText.Cli/bin/Release/net10.0/LeviathanText.Cli.dll benchmark vlm-validate /ABSOLUTE/PATH/TO/example-corpus/manifest.json --allow-provisional
```

Use a **new, nonexistent output directory** for each condition. Create its parent if
necessary, but do not pre-create the run directory itself. This bounded image-only example
uses one repetition, matching the initial natural-image diagnostic; use the preregistered
repetition count for your actual condition and three for the synthetic gate:

```sh
dotnet src/LeviathanText.Cli/bin/Release/net10.0/LeviathanText.Cli.dll benchmark vlm /ABSOLUTE/PATH/TO/example-corpus/manifest.json --allow-provisional --model lt-vlm-candidate --endpoint http://127.0.0.1:1234/v1 --mode image-only --repetitions 1 --max-requests 1000 --time-limit-minutes 15 --max-output-tokens 512 --out /ABSOLUTE/PATH/TO/results/new-image-only-run
```

For R0 assistance, first replay the frozen existing recognizer checkpoint on these same
hashed images, without training. Preserve checkpoint, alphabet, preprocessing and producer
hashes, plus raw per-image outputs. Make a separately hashed derived manifest with the
**actual unedited R0 output** in every sample's `baselineText`, including `""` when R0
really returned empty. Keep the same dataset identity/cohort for a paired comparison;
record the derived release and R0 linkage externally. Do not fill this field with source
transcriptions, specialist answers, reconstructed text or invented OCR. `baselineText`
alone is not proof that R0 produced it. The frozen R0 replay pointer for the historical
pilot is in the evaluation handoff; it is not bundled with a fresh checkout.

```sh
dotnet src/LeviathanText.Cli/bin/Release/net10.0/LeviathanText.Cli.dll benchmark vlm /ABSOLUTE/PATH/TO/example-corpus/manifest-with-frozen-r0.json --allow-provisional --model lt-vlm-candidate --mode ocr-assisted --repetitions 1 --max-requests 1000 --time-limit-minutes 15 --max-output-tokens 512 --out /ABSOLUTE/PATH/TO/results/new-ocr-assisted-run
```

Score each closed run offline, with a **new, nonexistent report filename** and an existing
parent directory:

```sh
dotnet src/LeviathanText.Cli/bin/Release/net10.0/LeviathanText.Cli.dll benchmark vlm-score /ABSOLUTE/PATH/TO/results/new-image-only-run --references /ABSOLUTE/PATH/TO/example-corpus/references.json --out /ABSOLUTE/PATH/TO/results/new-image-only-score.json
```

Scoring reads frozen receipts and separate references, not the model service or image
files. It verifies dataset/cohort, plan, prompt/image joins, duplicates and retained
transport receipts. It keeps every planned row in each repetition's denominator; absent
output becomes missing/`NotAttempted`, and nonempty failed or truncated output is scored
in full. Never filter to completed, short or apparently plausible lines before scoring.
NFC scalar distance can exceed 100%; it does not silently fold case, sigma or spaces.
Blank controls have their own outcomes rather than a fabricated zero-length CER.

## Optional structured responses

The default response format is `plain-text`. To explicitly request the fixed transcription
object schema, add `--response-format transcription-json-v1` to a newly registered run:

```sh
dotnet src/LeviathanText.Cli/bin/Release/net10.0/LeviathanText.Cli.dll benchmark vlm /ABSOLUTE/PATH/TO/example-corpus/manifest.json --allow-provisional --model lt-vlm-candidate --response-format transcription-json-v1 --mode image-only --repetitions 1 --max-requests 1000 --time-limit-minutes 15 --max-output-tokens 512 --out /ABSOLUTE/PATH/TO/results/new-json-run
```

This uses the named `vlm-diplomatic-json-1` prompt and
`VLM-TRANSCRIPTION-JSON-1` output contract. The run records the requested format and exact
schema hash; compact per-request audits bind that format to the provider's outgoing request
digest. These are producer-attested receipts, not independently reconstructible full request
bodies or proof of server receipt. Identical repeated requests may have identical digests.
Older scorers reject the new prompt version. Existing plain-text runs keep their prior
scoring semantics, and explicitly selecting `plain-text` uses the same request as omitting
the option.

Use the same `vlm-score` command on the closed JSON run. Its report separates full raw
representation distance from strict-derived transcription distance and schema validity.
Only a complete object with the sole string property `transcription` supplies a decoded
view. Fences, duplicate/extra keys, invalid Unicode and incomplete JSON fail without repair;
the complete raw text still contributes to the failure score. Original transport status
and raw receipts remain untouched. A valid object from a truncated or failed response
does not become a successful response. Blank success requires completed transport, valid
schema and an explicitly empty transcription string. Missing or uncaptured results cannot
pass a blank control. Raw plus decoded retained text share the cumulative scoring budget.

The [paired synthetic study](vlm-json-schema-results-2026-11-04.md) supports format
interoperability, not better Greek recognition or prevention of hallucinated text. HTTP
completion content can also differ from backend diagnostic output after server parsing;
requesting a schema is not proof of a particular sampler mechanism. This CLI prompt and
its 120-second request limit differ from that study's artifact-only prompt and 30-second
limit. The example above is a new condition, not a reproduction of the closed experiment.

## Bounds, interruption and replay

| Setting | Current CLI behavior |
| --- | --- |
| Cohort/repetitions | 1–1000 samples; 1–3 complete planned repetitions. |
| Request budget | `--max-requests` 1–4000, default 4000; an oversized complete plan is rejected before execution, not trimmed. |
| Manifest/receipt | Input, resolved replay and serialized run receipt each <=4 MiB. The expanded plan is checked before creating output or making requests; fitting the input alone does not guarantee the run receipt fits. |
| Sampling/order | Temperature 0, top-p 1, sampling seed 42; repetition order seeds 42/1729/2026. Runtime reproducibility still needs measurement. |
| Output tokens | `--max-output-tokens` 1–4096, default 1024; this protocol's example explicitly uses 512. |
| Run deadline | `--time-limit-minutes` 1–480, default 30; examples use 15. Registration/image validation precede the run timer. |
| Request deadline | 120 seconds, one attempt, one request in flight. No automatic retries. |
| Image/wire | Static PNG <=8 MiB, <=4,000,000 pixels; request <=12 MiB; captured response <=1 MiB. |
| Prompt/baseline | Combined prompt <=20,000 characters; manifest baseline <=10,000 characters. JSON escaping can make a baseline exceed the rendered prompt bound; that remains an input-rejected failure. |

Managed deadline expiry records cancellation and remaining `NotAttempted` rows. Ctrl-C is
also connected to managed cancellation for benchmark commands: press it once and allow the
process to finish writing the planned rows and summary. The actual Unix child-process
regression verifies this behavior; Windows console signalling has not been exercised by
that platform-specific test. This graceful handler applies only to `vlm` inference;
offline `vlm-validate` and `vlm-score` keep normal console interruption. An interrupted
offline score may leave an incomplete output file; preserve the original run and use a
fresh score path for a new attempt. Exit 0 means all planned transport calls completed, not correct
transcription; exit 3 means some planned work failed or was not attempted.
Validation/configuration errors are reported separately. A forced kill, crash or power loss
can still leave a partial last JSONL record or no summary. Preserve the original evidence.
Missing complete rows can be scored against the frozen plan, but
malformed/truncated JSONL is rejected; any recovery requires a separately identified,
audited derivative, never an in-place deletion. The command does not resume into an
existing run directory. Use a fresh, separately registered run for a retry.

`input-manifest.json` stores the exact submitted bytes; `run-manifest.json` binds its hash,
plan, model, settings and prompts. `results.jsonl` retains each raw transport receipt, and
`summary.json` records closure. `resolved-input-manifest.json` is a separately hashed
replay manifest with absolute image paths. Its `sourceEvidenceBase` preserves relative
rights/geometry context; the original `inputManifestPath` and `imagePathBase` are also
recorded. Reuse that resolved manifest with `vlm-validate` and then `vlm` into a fresh
directory. It references existing images/evidence rather than embedding or copying them;
moving to another machine requires an explicit new path-resolved manifest and receipt.

New run manifests include `producerAssemblies` entries with name, version and SHA-256 for
the loaded CLI, AI, Evaluation and ImageSharp assembly files. `codeAssemblySha256` remains
the CLI-only hash for older readers. Older runs lacking dependency entries need their
external build map: an unchanged CLI hash cannot identify a changed AI parser. Score
reports bind the input/run/result/reference bytes they read. Preserve the scoring build
separately too; the producer entries identify the inference producer, not a later scorer.

Offline scoring caps cumulatively retained hypothesis text at **64 Mi UTF-16 code units**
(128 MiB of character storage before object/parser overhead). The check occurs before each
row is retained, across all repetitions; supplementary Unicode characters occupy two code
units. No CLI option can raise this cap. Only canonical plan identity and status/text are
retained, so unused diagnostics, raw wire and repeated source metadata do not accumulate.
This is not a claim that total process memory stays below 128 MiB. Oversized cumulative
text, individual references/hypotheses or edit-work budgets reject the entire score with an
explicit error and no report, rather than trimming text or dropping rows. Keep the error,
full raw evidence and planned cohort; use a separately documented evaluation procedure if
those bounds prevent a complete score.

## Keep the evidence the CLI cannot collect

Alongside each registered comparison, retain:

- Model/projector full repository revisions, selected filenames, sizes and SHA-256 values;
  model/license/tokenizer/chat-template metadata; exact loaded identifier and `lms ls`/`lms ps` snapshots.
- LM Studio application and inference-runtime/build versions; CPU/GPU, RAM/VRAM and OS;
  context, offload, cache/tiling/preprocessing settings and projector-load logs.
- Before/during/after process and host resource samples, their sampling method and coverage,
  swap activity, load time, concurrent workloads, run order and actual start/end times.
- Synthetic input hashes and independently known targets, every control output, rights and
  geometry records, exposure/review state, frozen R0 evidence, and unresolved specialist decisions.
- Exact application/scoring builds and study registration, all incomplete conditions and
  failures, and any explicit client diagnostic or recovery derivative without rewriting the original.

Provider timings exclude some preprocessing and model loading; sampled process RSS is
not complete GPU memory or a guaranteed peak. Concurrent downloads/builds and cache state
can confound comparisons. Neither these receipts nor a lower source-reference distance
automatically establish Greek accuracy, speed, memory improvement or saved correction time.
Keep development diagnostics separate from independently reviewed selection/confirmation
work, and keep all inference outputs as raw evidence until an authorized human review.
