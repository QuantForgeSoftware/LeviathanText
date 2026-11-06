# Provider-session resource accounting — 2026-11-05

The API's singleton provider registry used to construct a fresh adapter on every
lookup, restarting its configured logical-call and spend counters. A spend check
also inspected settled cost without reserving in-flight allowances, so concurrent
calls and retried attempts could reuse the same available amount.

The registry now retains one lazily initialized adapter per configured provider.
Concurrent first lookups share that adapter. Its credentials are still resolved at
each dispatch; no credential is cached in the configuration or proposal record.
The allowance belongs to that registry/adapter's process lifetime and is shared by
requests using that configuration. It is not per-user, durable across restarts,
cross-process, or an account-wide provider billing limit. Discovery calls use their
separate catalog transport and do not consume generation allowances.

When `MaxSpendUsd` is configured, each actual HTTP attempt obtains an atomic local
allowance based on the configured token prices, an input estimate of UTF-8 prompt
bytes plus16 framing tokens, and the configured maximum output tokens. Held amounts
reduce `RemainingSpendUsd` before other requests can dispatch. The UTF-8 estimate is
more conservative for Greek than the former characters/4 heuristic; it is still an
estimate, not a universal tokenizer or proof of a provider's bill.

A reserved allowance is distinct from an actual dispatch. Invalid credentials,
pre-cancelled requests, a locally denied spend check and a logical-call-cap rejection
do not incur a spend charge. A spend-denied concurrent call does not consume a
logical-call slot. An admitted logical call counts once even if it retries; each
retry requires its own additional spend allowance. A cancellation racing actual
network dispatch can leave provider-side work unknown and keeps that attempt's
estimate.

For a received final response, valid reported usage replaces the final attempt's
estimate, including when its text is malformed or truncated and cannot be used.
Each missing or invalid usage component retains its configured estimate. Prior
retried attempts, transport failures, cancellations after dispatch and unreadable
responses retain their estimates because a failed response does not prove zero
billable work. Known actual cost is never clipped to the local cap: an overrun is
recorded and subsequent dispatches are blocked. In-flight work, inaccurate prices,
server behavior and custom tokenization can still exceed estimated allowances.
Use provider-side billing controls where a monetary guarantee is required.

These optional configuration controls do not impose new defaults on uncapped local
models. No hosted call, price lookup, credential disclosure or model fitting was
part of the change.

## Evidence

An isolated exact `4138126` source snapshot with the first five new generated HTTP
regressions produced **4 failures and1 control pass**: registry reset, parallel
oversubscription, repeated allowance reuse on retry, and malformed-response
accounting. The candidate passed all five, then added settlement/overrun,
credential failure, idempotent disposal, pre-cancellation, denied-call admission,
and known-usage/unusable-content tests after independent review.

Complete AI suite at this slice: **161 passed,1 existing opt-in live-Ollama test
skipped**, zero build warnings/errors. All executed network tests used in-process
or owned loopback doubles. The baseline and candidate logs are retained at
`/tmp/lt-midday-budget-before.log` and `/tmp/lt-midday-budget-full.log`; the isolated
baseline source is under `.artifacts/midday-budget/baseline-source/`.

Further credential-rotation redaction work has its own source changes and tests;
these counts describe the budget slice before that follow-up.
