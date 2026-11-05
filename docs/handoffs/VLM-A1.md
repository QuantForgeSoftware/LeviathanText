# VLM-A1-r2 handoff — standalone image transport

Prepared 2026-11-04 for `VLM-BENCH-1`, activated during the user-authorized session ending
10:29:19 CST. Base commit: `6c9265f6fb82f3472c9d7faa013e79c35fe6e155`, plus the pre-existing
uncommitted Nov4 review fixes. Those fixes and old P46 artifacts were preserved. The
implementation activation supersedes the task brief's original planning-only scope for
these owned files; it does not authorize this agent to train or run real models.

## Owned changes

- `src/LeviathanText.AI/VisionInference.cs`: standalone request and per-attempt receipt.
- `src/LeviathanText.AI/VisionImage.cs`: immutable, pinned, bounded PNG snapshot.
- `src/LeviathanText.AI/VisionHttpProvider.cs`: opt-in loopback transport with one attempt,
  strict bounds, whole-response timeout and explicit failure records.
- `src/LeviathanText.AI/VisionResponseParser.cs`: SSE/JSON extraction and retained partial
  text without scholarly normalization or repair.
- `src/LeviathanText.AI/LeviathanText.AI.csproj`: existing repository ImageSharp 3.1.11 version.
- `tests/LeviathanText.AI.Tests/VisionHttpProviderTests.cs`: synthetic/owned-loopback regressions.
- `docs/evaluation/vlm-lmstudio-transport.md` and this handoff.

No existing text-provider implementation, proposal contract or model-loading path changed.
No CLI/shared-plan/corpus/scorer files were edited by A. No commits or DCO certifications
were made. Root owns CLI integration, real runtime/model work and shared plans. B owns the
manifest, references and scoring. Its scorer was read mechanically, without opening its
reference labels; no concrete scoring defect was found, and exact-text/valid-completion
semantics were clarified in B's documentation.

## Integration contract

Construct `VisionImage.FromPng(bytes, expectedSha256)`, then `VisionInferenceRequest` with
`SampleId`, `Image`, `SystemPrompt`, `UserPrompt`, `PromptVersion` and optional `Seed`.
Use `VisionHttpProvider(AiProviderSettings, HttpMessageHandler?, IAiCredentialSource?)`
and dispose it when the run ends. Its default handler disables redirects; an injected
handler must also do so. The initial route is loopback only, OpenAI-compatible and Local,
with `MaxAttempts=1`, timeout <=120 seconds and response cap <=1 MiB. No transcription
revision or prior hypothesis is needed. The caller owns the V1/V2 prompt distinction.

`InferAsync` returns `VisionInferenceResult`, including Cancelled, Timeout, Truncated,
InvalidOutput, HttpError, ResponseTooLarge, Busy, InputRejected and ConfigurationError.
The scorer must receive every attempt's captured `Text`, including nonempty failed text;
never pass the HTTP `RawResponse` as model text. Preserve the detailed transport status
even when mapping to B's coarser vocabulary. Record image-construction exceptions as
InputRejected in the runner. A completed blank is `Completed` with `Text=""`.

The exact image/prompt/request hashes, request byte count, raw-response byte-prefix hash,
finish reason, optional token usage and timings are available in each receipt. A response
that echoes a configured credential is explicitly marked and its wire representation is
redacted; do not claim that redacted wire text is an exact raw-byte reproduction. Metadata
and reference targets do not belong in the model prompts; the transport transmits the
caller-provided prompt and exact PNG metadata unchanged.

## Evidence

Artifacts are under `.artifacts/vlm-benchmark/vlm-bench-1/a/`.

| Check | Result |
| --- | --- |
| Equivalent old-provider image-support reproduction | Built successfully; intended failure: string-only user content, no image field; `baseline/result.log`. |
| Initial focused transport tests | 23 passed; `candidate/focused.trx`. |
| Independent-review edge regressions before corrections | 5 failed for intended partial-string, same-chunk UTF-8 and escaped-credential behavior; `candidate/edge-baseline.trx`. |
| Edge-corrected focused candidate | 31 passed; `candidate/focused-edge-candidate.trx`. |
| Edge-corrected existing+new AI suite | 119 passed, 1 opt-in live test skipped; `candidate/full-ai-edge-candidate.trx`. |
| Malformed-JSON Unicode regressions before correction | 3 failed with unhandled invalid UTF-8/unpaired-surrogate exceptions; `candidate/unicode-baseline.trx`. |
| Initial full AI suite, including 34 vision cases | 122 passed, 1 opt-in live test skipped, zero build warnings; `candidate/full-ai-final.trx`. |
| Observed LM Studio Mistral blank response before correction | Exact retained SSE fixture failed; 5 invalid-empty controls passed; `candidate/mistral-empty-baseline.trx`. |
| Current full AI suite, including all 40 vision cases | **128 passed, 1 opt-in live test skipped**, zero build warnings; `candidate/full-ai-mistral-blank.trx`. |

The baseline support probe uses the existing Release DLL and captures a real provider
request into an in-process handler. It does not invent a missing-type compilation failure
as a baseline. The revised public API did not exist on the baseline, so the equivalent
behavioral reproduction and new implementation tests are different harnesses. Baseline
source and DLL hashes are retained in `baseline/source-receipt.json`; edge-failure source
hashes are in `candidate/edge-baseline-hashes.json`.
The final candidate hashes, environment, commands and result counts are in
`verification.json` (r2; the previous receipt is preserved as `verification-r1.json`). The final sources preserve valid prefixes of invalid Unicode output
while recording `InvalidOutput`; those failures no longer escape the provider as exceptions.

Exact final verification command, from the repository root:

```sh
LEVIATHAN_OLLAMA_LIVE=0 dotnet test tests/LeviathanText.AI.Tests/LeviathanText.AI.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false --logger 'trx;LogFileName=full-ai-mistral-blank.trx' --results-directory .artifacts/vlm-benchmark/vlm-bench-1/a/candidate
git diff --check
```

The first sandboxed test attempt compiled cleanly but the test runner could not bind its
local socket. That environment failure is retained in `candidate/build-sandbox-test-abort.log`.
Tests then ran with local socket permission. The live Ollama test was explicitly disabled
with `LEVIATHAN_OLLAMA_LIVE=0`; no real model was called by A. An actual disposable loopback
redirect fixture verifies that the default HTTP handler does not follow a redirect.
Instance B independently identified the partial-string, same-chunk UTF-8 and escaped-key
cases, then re-reviewed their corrections and found no remaining concrete blocker in that
pass. The separately added Unicode guard cases preserve the same interface and evidence
contract. No speed or memory improvement is claimed by these transport tests.

Root's first Mistral synthetic run exposed a valid blank-response shape with an empty SSE
delta, `finish_reason=stop` and `[DONE]`, but no content/role field. The original parser
incorrectly returned `InvalidOutput`. The r2 correction permits this exact blank completion
while preserving invalid missing-stop/missing-DONE/tool-call/malformed streams and missing
JSON content. Root retains the original raw run and reruns synthetic controls into a new
artifact; no historical result was rewritten. This is transport compatibility evidence,
not an automatic claim that the model's abstention was appropriate for every image.

## Remaining gates and limits

Root's live contrasting-image/blank capability checks are required before any claim that
LM Studio consumes the image. Runtime, model/projector hashes, ignored parameters and
server resource limits remain root's receipt. The candidate package register and exact
proposed filenames remain in the transport note and benchmark specification.

Passing transport tests does not make provisional source labels accepted ground truth.
Specialist decisions, natural grounding controls, material-specific selection/confirmation,
R0 before/after performance, correction effort and adoption gates remain separate.
Neither training, natural-text accuracy, a speed improvement nor model promotion is
claimed. Only the new optional image transport and the documented synthetic failures
are covered by this handoff.
