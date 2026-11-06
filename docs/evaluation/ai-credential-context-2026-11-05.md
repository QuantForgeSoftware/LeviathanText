# Per-call credential redaction

Date: 2026-11-05. Synthetic credentials and in-process/loopback HTTP fixtures only; no hosted calls or live model requests.

## Problem and bounded correction

Text transport/error parsing previously resolved the configured credential again when sanitizing a response. Rotating that source while a request was in flight could leave the actual authorization key in a transport error, malformed response, Ollama truncation, or model-catalog parse failure. Shared cached provider instances also require isolation between concurrent calls.

`AiHttpTransport.SendJsonWithContextAsync` now returns a disposable `JsonResponse` containing the bounded body and its per-logical-call redaction context. Each attempt captures its actual resolved authorization key; there are at most five distinct values, matching the existing attempt limit. There is no shared/global credential history. Transport failures sanitize before disposing the context; text and model-catalog adapters keep it alive through post-response parsing. Disposal clears the key list and owned body reference. The existing public `SendJsonAsync` signature still returns the raw success body and disposes the temporary context itself.

Literal replacements use the longest exact credential first, including short credentials and rotations where one key prefixes another. After literal replacements, the existing bounded JSON string-prefix scanner suppresses the entire excerpt if it detects a remaining encoded credential. It does not return decoded source text. Transport excerpts inspect at most 513 bytes, retain at most a 512-byte prefix, and record at most 240 characters.

To avoid exposing a credential fragment, error-body reads now fill that bounded buffer until EOF or the limit rather than treating one short read as a complete body. If a call used a credential and the declared or observed body exceeds 512 bytes, the whole excerpt is suppressed. A complete body of at most 512 bytes is sanitized normally. Keyless requests retain bounded diagnostic prefixes. This deliberately avoids inventing partial-secret matching heuristics.

Before returning model content to the generic proposal validator, the HTTP text adapter rejects exact or bounded JSON-decoded credential echoes with `MalformedResponse` and a suppressed excerpt. An echoed key cannot become proposed text or be reintroduced by a later schema-validation failure. Ordinary Greek content is unchanged; scripted providers are unchanged. This does not introduce arbitrary encoding detection or retain a historical credential set across calls.

Vision inference captures its one authorization secret in a call-local variable and uses it through completion/error parsing. Two controls verify rotation during both completed and malformed responses preserves that redaction behavior. A further safeguard suppresses nonempty authenticated captures when HTTP EOF has not been observed: `RawResponse` becomes a redaction marker, `Text` is empty, and arbitrary captured `ResponseModelId`/`FinishReason` strings become null. This covers timeout, cancellation, disconnect, response limit, and decoder failure before EOF. It sets `CredentialsRedacted` without guessing which captured fragments resemble a key.

That vision safeguard preserves status/detail/HTTP status, timing and numeric token/byte counters, hashes, and the incomplete-capture flag. Keyless partial output remains unchanged for local benchmarks. Fully read authenticated responses keep existing handling, including nonsecret model text stopped at its token limit; model truncation is distinct from incomplete HTTP capture. Empty/pre-dispatch captures are unchanged.

## Verification

- **Six regressions failed before the context correction**: rotated transport error, text parse failure, model catalog parse, Ollama truncation, retry keys, and concurrent calls on one cached adapter.
- A separate isolated candidate snapshot with only the new content-echo rejection disabled reproduced **three failures**: a literal key became a successful candidate proposal; an invalid structured response reached the generic failure excerpt; a nested JSON-escaped key became a successful structured proposal. This is a controlled guard ablation, distinct from the original pre-context baseline. Snapshot: `.artifacts/credential-redaction-stage-baseline` (disposable, untracked).
- Final focused selection: **16 passed**, covering those cases plus five rotating retry keys, overlapping prefixes, typed-response disposal, legacy convenience return value, encoded excerpts, short exact keys/control-character/length bounds, and two vision controls.
- The follow-up excerpt-boundary selection reproduced **five failures before correction**: a short read through a key, a long key crossing the limit, a declared oversized credential body, a complete exact-limit body, and a keyless chunked diagnostic. All **five passed after correction**. Generated streams verify no more than 513 bytes are read, and known declared oversize bodies need not be read at all.
- The authenticated-incomplete vision selection reproduced **10 failures and 1 passing keyless control before correction**, using literal/escaped fragments across five failure paths. All 11 pass after correction, alongside a new complete authenticated token-limit control. Tests verify unchanged captured-byte hashes/counters/timing/usage and failure classification.
- Complete final AI suite: **194 passed, 1 existing live-Ollama test skipped**, no compiler/analyzer warnings. `git diff --check` passed.

```sh
dotnet test tests/LeviathanText.AI.Tests/LeviathanText.AI.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:ArtifactsPath=/private/tmp/leviathan-streams-artifacts --filter 'FullyQualifiedName~CredentialRotationRedactionTests|FullyQualifiedName~Rotation_during_vision_response'
dotnet test tests/LeviathanText.AI.Tests/LeviathanText.AI.Tests.csproj --no-restore --no-build -p:ArtifactsPath=/private/tmp/leviathan-streams-artifacts
```

The compatibility HTTP helper intentionally returns an unsanitized success body to its caller, as before. Callers using that low-level API remain responsible for how they record it; the text/catalog adapters use the typed context. Immutable managed strings cannot be erased in place; this change bounds ownership and removes references on disposal rather than claiming secure memory erasure.
