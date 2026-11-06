# Independent worker renewal verification

Status: original behavior reproduced; isolated candidate engine passes16 generated controls, and the separately registered real-clock SQLite host/observer proof completed. Main integration and representative resource qualification remain separate gates. No model, source corpus, label, provider or fitting operation was used.

## Actual original behavior

Original WorkerHost SHA-256 `7b56cfcb5d02cf4b0f4cd78eee7fa4038be421773307596c44788450962b7064` is retained with exact test sources under `.artifacts/experiments/worker-heartbeat-independent-v1/`.

Two original tests failed at the intended behavior assertions (`baseline-native.log`, `baseline-native-results/baseline-native.trx`):

- Actual RunNext ran a deliberately blocked synchronous generated handler. Its80ms lease expired while the handler remained active after a200ms observation interval, without any manual Heartbeat call. The second worker's ordinary pending-only poll correctly refused the Running job; the test did not reclaim a live publisher. The handler was released and joined in finally.
- A nonce-less manual Heartbeat renewed a replacement claim made by the same worker. Matching worker identity alone did not establish the originating attempt.

These are functional diagnostics, not scheduling or throughput acceptance thresholds. An earlier sandbox run aborted because VSTest's owned loopback socket was denied; it is preserved separately and is not counted as either behavioral failure.

## Isolated candidate controls

The candidate interface was obtained before writing mocks. Tests invoke production WorkerHost.RunNext and its automatic task/session lifetime; they do not manually call Heartbeat to simulate automatic renewal. A thread-safe generated store and controlled tick source make ownership/lifetime sequencing deterministic. No existing unsafe dictionary fixture is shared concurrently.

The16 cases cover captured job/worker/claim renewal; a background session opened on a different thread from the blocked handler; disposal before final CAS; an in-flight renewal joined before finalization; preserved API cancellation and externally replaced claim; contention followed by renewal; bounded observer errors; factory/tick/renew/dispose fault cleanup; caller/pre-cancellation; null, empty, forged and stale manual nonces; and monotonic explicit renewal timestamps. New manual HeartbeatAttempt positive/negative cases are distinct from the automatic-loop proof. The legacy nonce-less method intentionally refuses in this candidate; this is an explicit manual API behavior correction, not transparent compatibility.

`candidate-results-v2/candidate-v2.trx` records16/16 passing tests, with no compiler/analyzer warnings in `candidate-v2.log`. The first16-pass run had two xUnit1031 warnings from `.Result` after a guaranteed await; its source and log remain preserved. The clean version only changes those assertions to await syntax. Neither run uses a real-time performance SLO.

Candidate sources remain in R's isolated mirror and were not edited by this reviewer. The completed process proof below covers actual independently opened SQLite renewal connections and owned process shutdown. Provider-bounded native-wait qualification and current-published-worker rollout remain separate. These engine controls do not establish exactly-once side effects, safe reclamation of an active publisher, or immunity to process suspension/clock jumps. The ordinary worker entry point remains opt-in/default-disabled until root chooses rollout. Historical worker resource packages/results remain untouched.

## Registered actual SQLite process proof

The separately approved proof used original and candidate114-file runtime closures, each on its own fresh generated store. It ran eight owned mode processes: seed, host plus independent read-only observer, and final verifier for each version. The actual host calls production RunNext with a blocked synchronous handler; the candidate opts into the real `Program.LocalStoreLeaseRenewalSession` factory, which opens its own renewal connection. No manual Heartbeat or live-publisher reclamation occurs. Original entry point defaults remain disabled.

Fixed parameters were a1s lease,250ms candidate renewal interval and12 observer samples250ms apart. The original observer saw8 expired samples with unchanged heartbeat/expiry while the same handler remained Running. The candidate observed12 fresh samples with12 distinct heartbeat timestamps (the initial claim plus11 renewals); all retained the same captured worker and nonce. Its final summary recorded11 renewed,0 contended, no ownership loss, fault or observer fault. This is observed functional sequencing, not a scheduling/throughput SLO.

Both variants then completed exactly one handler attempt, final Succeeded state, zero retries and the exact generated output. The authoritative final job JSON was byte-identical across variants: SHA-256 `34d2bc985fd60aff5b4bd73ee09eaabff1bbbff0a487a7339389351578630696`. The read-only observer ran in a different process, and all raw lease timestamps, identities, DBs and JSON receipts are preserved.

Registration SHA-256 `72d22747b664848ebec1445dc357002f975e0d8b9dfa3a5790a528987bed1089`; actual binding `process-execution-v2.json`; evidence `process-results-v2/{proof.json,closure.json,original,candidate}`. Proof SHA-256 `57439e20c352a70261c7ee3409c8fd4c73c776457f0526aa5bd787296eb3de8d`. All241 registered runtime/source inputs remained unchanged. All eight children exited0 and were reaped; per-child watchdogs, observer and whole-driver watchdog joined; both private temporary stores were removed with no cleanup error or quarantined root. Caps were20s wall/10s CPU/512MiB sampled RSS per child,64KiB streams,120s global and bounded TERM/KILL cleanup. No cap triggered.

Before dispatch, independent review found that a cleanup/copy exception could skip remaining resources or delete uncopied evidence. Version2 collects each cleanup error and continues through every owned child, observer and guard. A failed copy or unconfirmed owner retains its original root with an explicit quarantine path. Two harmless fault-injected controls passed: first-child termination failure does not skip the second child/proof, and evidence-copy failure retains the first root while safely preserving/removing the second. The initial unexecuted proposal/driver remains preserved; no worker proof was rerun or adaptively relaxed.

R's17 real SQLite nonce/status/CAS/contention tests are separate evidence under the engine artifact, not additional tests from this reviewer. These results do not establish exactly-once publication, safe reclaim of an active process, native cancellation deadlines or representative inference resource performance. No production source was edited by the independent reviewer.
