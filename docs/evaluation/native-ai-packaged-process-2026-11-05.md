# Native AI in packaged processes — 2026-11-05

A disposable macOS arm64 package completed20 HTTP checks against the real API
executable and an owned loopback OpenAI-compatible provider double. This extends
the in-process HTTP fixtures to packaged binaries, configuration binding, real
HTTP serialization, SQLite persistence and process restart. No actual model,
LM Studio request, hosted service, corpus image, fitting or Greek decision was used.

Evidence: `.artifacts/midday-native-ai-process-v4/registration.json`, `run.py`,
`results.json`, `requests.json`, `http-calls.json`, `cleanup.json` and owned API logs.
Package manifest SHA256:
`8539cb96fef5e4e25e3df81a9889033bb7703541a4965e9e8e203bf091bd8312`.
This frozen package predates shared-layout ownership and the final npm override;
it retains ImageSharp3.1.11 and is **internal verification, not a secure release**.

The generated source has one invented Greek line `αβγδε` and a generated PNG.
The server selects `βγ` from authoritative L1 instead of the forged client source.
Explicit acceptance creates `αΩδε` as L2 with the stable authenticated account ID,
retains L1 and the exact original TEI attachment, and writes durable issued and
acceptance records. Repeating that acceptance is a version-preserving no-op.
Tampering with the supplied proposal fails422 without changing native state.

Removing membership prevents acceptance and subsequent provider dispatch; adding
membership back restores access. A fresh second proposal replacing the same
two-character span is conservatively refused409 because the existing editorial
alignment is not unique. This expected refusal preserves native state; it is not
claimed as a successful rebase or silently overridden.

The provider cap of four logical calls persists across separate HTTP requests:
a fifth call returns a failure without reaching the double. After a genuine API
restart, prior issued records and idempotent acceptance survive while the
process-session cap resets, as documented. In total the double receives five
requests over two API processes. Diplomatic export still contains only the source
reading. The final project version is8. Both API processes exit0, the server
thread stops and no owned process remains running.

Three exploratory attempts are retained separately: v1 had a verifier-only Python
HTTP logging error; v2 expected200 instead of a correct successful DELETE204;
v3 expected acceptance where the documented uniqueness guard correctly returned409.
All three cleanup receipts confirm owned-process shutdown. They are not counted
as product failures or passing checks. The complete corrected v4 run passes.

The separate package verification checks all365 manifest file hashes and tar
members, twelve arm64 native-library identities, browser asset serving, CLI startup
with no external tools on PATH, and a persisted standalone-worker job completing
successfully. Its receipt is `.artifacts/midday-package-verification/report.json`.
This remains one local target and generated workflow, not a distributed deployment,
hosted-provider integration, performance benchmark or scholarly acceptance.
