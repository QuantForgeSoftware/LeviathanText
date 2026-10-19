# Live local-provider smoke test (Ollama)

- Date: 2026-10-19 14:54 UTC (09:54 CDT); contract C0-0.4-draft.
- Endpoint: `http://127.0.0.1:11434` (local Ollama server); **no credentials** in the
  client. The configured model was `deepseek-v4.1-flash:cloud` (a cloud-backed model served
  through the local Ollama instance; its account authorization lives in the local server).
- Code: `tests/LeviathanText.AI.Tests/LiveOllamaTests.cs`, opt-in only.

## Result

| Step | Result |
| --- | --- |
| Capability discovery (`GET /api/tags`) | configured model present |
| Chat proposal (`POST /api/chat`), candidate-reading task | **succeeded**, 0.37 s |
| Response | non-empty, 6 characters; not adjudicated and not a quality score |

The test sends `think: false` (Ollama thinking models otherwise spend the token budget in
the reasoning field). Earlier, with the default thinking behavior and a 64-token cap, the
server returned empty `content`, non-empty `thinking`, and `done_reason: "length"`. That
case is now recorded as **`response-truncated`** instead of a misleading empty response.

## Reproduce

```bash
LEVIATHAN_OLLAMA_LIVE=1 \
dotnet test tests/LeviathanText.AI.Tests \
  --filter FullyQualifiedName~LiveOllamaTests
# optional: LEVIATHAN_OLLAMA_MODEL=<installed model>
```

Default test runs skip this test; CI never touches the network.

## Limits

- This is a transport/schema smoke test on **one** model and one short request.
- It is not a prompt/model comparison, not a Greek quality measurement, and not evidence
  for any hosted-provider claim. No external hosted API was exercised.
- The model is cloud-backed behind a local server, so results depend on that service's
  availability and authorization.
