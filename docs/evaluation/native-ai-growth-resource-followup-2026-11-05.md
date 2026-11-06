# Native-AI growing-history resource follow-up

November 5, 2026. Read-only lifetime analysis followed by one approved narrow
managed-retention correction (MID-36). No new resource workload, real provider call, fitting or
heap-profiling process was started. The regression tests below use
only generated native records and an in-process HTTP-handler double.

## Observation and interpretation boundary

The completed [registered soak](native-ai-process-soak-2026-11-05.md) passes all
1,000 issue/accept cycles, restart/persistence/source checks, and clean shutdown.
Its two API lifetimes have 50 sparse RSS samples each: cycles 1–491 rise from
152.20 to 437.28 MiB (sampled maximum 479.02); cycles 501–991 rise from 255.47 to
677.31 MiB (maximum 708.42). Samples also fall substantially within each lifetime.
These are not monotonic live-heap traces or demonstrated process high-water marks.
The final normalized checkpoint JSON is 4,082,138 bytes; root's separate read-only
store check records 4,936,362 UTF-8 bytes for the persisted wrapper representation.
The source has one L1, 1,000 L2 revisions,1,000 annotations,1,000 issued attachments
and 1,000 acceptance receipts. Those different representations/counts are not
interchangeable memory measurements.

No managed live/committed heap, generation/LOH size, allocation rate, GC pauses or
native allocation trace was captured. There is no root attribution, fixed-history
control or matched post-change resource run. Neither the observed growth nor the
correction below proves a native leak, explains hundreds of MiB, establishes
memory acceptance or fixes upstream TorchSharp retention. No model was loaded by
this generated native-AI workflow.

## Concrete source paths and lifetimes

| Path | Evidence and lifetime | Implication |
| --- | --- | --- |
| `Program.cs:59`, `JobEventBus.cs:16–28`, original `ProposalsController.cs:42/48` | The bus is singleton. Every proposal success appends serialized proposal text, or a failure reason, to a revision-keyed `JobStream.Log`. Replay history is never pruned; unsubscribe removes a subscriber only. | A concrete managed root grows with proposal requests even without listeners. Approximately 500 successful entries accrue in each registered API lifetime. Their retained byte size was not measured; they cannot be equated with the RSS delta. |
| `NativeAiProposalService.Issue` and `.Accept` | Issue appends an authoritative record containing source revision/proposal; acceptance appends a receipt, L2 revision and annotation. These are deliberately persisted append-only evidence. | The serialized project legitimately grows. Removing or truncating this evidence to improve a memory number would change required semantics. |
| `LocalStore.LoadProject` / `NativeJson.Load`; `SaveProjectCore` / `NativeJson.Save` | A load obtains the whole JSON string and deserializes the whole project. Each issue/accept save serializes the whole project to another string before its optimistic transaction. Commands/readers are scoped and disposed. | Growing multi-MB strings, nested attachment strings and materialized graphs recur on every operation. Full serialization and round trips are concrete allocation work, not proof that old graphs remain live. |
| `ProposalsController.Propose` | Resolves a project, awaits the provider, reloads current authorized state, validates/issues and saves. Request-local project graphs/results are not placed in a project cache. | At least two native-project loads for the selected one-project propose path, plus one whole-project save. Request overlap and async liveness require measurement; the source does not prove an exact simultaneous-live graph count. |
| `NativeAiProjectResolver.ForProposals` | Scans one project at a time, parses issued attachments for candidate IDs, retains at most two matching project IDs per requested ID, then reloads the selected project. The soak supplies an explicit project for acceptance. | One selected-project discovery load plus one reload for acceptance; no persistent project cache. Candidate JSON parsing duplicates some work before authoritative ledger parsing. The prior 128-ID optimization does not eliminate per-operation growing-history work. |
| `NativeAiProposalService.ReadLedger` | Each issue and acceptance reparses/checksums every issued attachment and receipt. Each issued source lookup scans revisions; each receipt scans revisions/annotations and reserializes matched evidence for digests. Temporary dictionaries/records are local. | A concrete growing traversal/allocation opportunity: with issued, receipt and revision/annotation counts growing together, repeated per-record scans can produce quadratic traversal work. This is source-level complexity, not a measured attribution of time or retained heap. Integrity checks must survive any future indexing. |
| `Program.cs:39–43/66–75`, `ApiStoreLifetime`, `RequestProjectStore` | One lifetime store anchor; scoped request connections/stores/services/resolver; `LocalStore.Dispose` disposes its connection. RequestProjectStore retains only binding IDs/authorization. | No native project-history root was found in these service fields. SQLite pooling/page caches and native buffers still need actual telemetry; disposal alone does not establish RSS return. |
| `AiProviderRegistry`, `AiHttpProvider`, `AiHttpTransport` | Registry caches one raw adapter per configured provider, retaining settings/client and scalar budgets, not proposal/chat history. Each generated schema wrapper is ephemeral. Request/response/timeouts/budgets are disposed; per-call response body is cleared and at most five remembered credentials are cleared after parsing. | No growing request-history field was found in the inspected provider path. A session-long client/counter is not one leaked client per operation. This does not rule out runtime/native caches or uninspected roots. |

A normal single-accept cycle therefore performs at least four whole-project loads
(two around issuance and two around acceptance) and two full saves in this
one-project path, plus repeated attachment parsing. The bulk HTTP endpoint also
previews before accepting and returns a result containing the project; that is an
additional allocation/response concern, **not the single-accept soak workload**.
The driver keeps provider request bodies/call receipts in its own Python process;
those lists are separate from the sampled API PIDs and must not be assigned to API
RSS. The singleton demonstration RevisionChain is not the native issuance/acceptance
history store on this route.

## MID-36 bounded correction and regression evidence

Only `ProposalsController`'s unused JobEventBus constructor dependency and its two
proposal publications are removed. The successful proposal/failure HTTP shapes,
durable issued attachments, authorization checks and acceptance behavior remain.
No job-bus ordering, replay, retention or SSE behavior is changed. It does not
prune prior entries in an already-running process or bound ordinary job replay logs. Source search
finds only a public job-ID event route; the browser consumes `job.*` and obtains
proposal results from POST responses, not a revision proposal stream. Native JSON
remains authoritative; no persisted proposal/revision/receipt is removed.

New `NativeAiEventRetentionTests` exercise actual HTTP issuance with a generated
success or empty-response failure double. Both final baseline cases fail exactly
because revision-keyed replay contains `proposal.created` or `proposal.failed`.
Before asserting that absence, success verifies the persisted checksummed issued
record via `GetIssued`, authoritative source span and unchanged serialized L1;
failure verifies exact native JSON/version unchanged. An actual job publish/replay
control remains available after the fix. The corrected cases pass after the
change. The first baseline attempt had a contract-JSON fixture mistake; the second
had a collection-reference equality mistake on the success control. Both are
retained but superseded by `retention-before-final.trx`, whose two failures are
specifically retained-replay assertions. They are not reported as product defects.

Receipts are in `.artifacts/native-ai-growth-resource-followup-2026-11-05/`, including
an exact original controller copy, baseline/after TRX/logs, source hashes and the
full filtered API gate. No actual fitting is selected; fitting opt-in is absent.
The full filtered API run passes **220/220**, with zero build warnings/errors,
including existing authorization, attribution, native issuance/acceptance, ordering
and event-stream cases. Its valid isolated build uses byte-exact Formats fixtures
in a newly owned validation directory. The first full isolated run retained
187 passes/33 failures because older tests resolve sample fixtures by walking four
parents from the output directory; those files were absent in that initial layout.
No existing shared fixture was overwritten. The same source passes with the correct
owned fixture layout (`api-full-valid-layout.trx`); the wrong-layout receipt is kept,
not counted as a product regression or a passing gate.
Independent resource-agent review confirms the narrow diff, final baseline
failure positions, durable/no-write controls, absence of a proposal-stream consumer
and unchanged job replay. The root final **normal-output engineering gate passes
1,684 cases plus one existing live-Ollama skip**, API 220 included, with zero build
warnings/errors (`.artifacts/midday-final-retention-engineering/counts.json`).
That closes this functional/source gate; the resource investigation and memory
acceptance remain open. A newly published package is a separate later snapshot.
The change removes the demonstrated unused root; no post-change RSS improvement
has been measured or claimed.

## Prospective differential protocol — not executed

Before a future run, hash the exact runtime/source/toolchain and generated fixture
snapshots, pin the host/GC/server configuration and register all arms and process
order. Use the same generated L1/range/replacement, dummy account, explicit project,
provider-double response bytes and integrity assertions. No natural labels, model
or image decode is needed. Preserve every issued record/receipt and all inputs.

1. Prepare valid immutable snapshot prefixes with 1, 250 and 1,000 completed
   acceptances, all containing the same first proposal/receipt and unchanged L1.
   In three **fixed-history** arms, replay the exact same first accepted proposal
   1,000 times against a disposable copy of each snapshot. Assert native bytes and
   version never change. This isolates history-size-dependent retrieval/validation
   work under the same operation/body; it does not issue new proposals.
2. A **growing-history** arm starts from the same one-acceptance prefix and performs
   1,000 new issue/accept cycles with the same source range/replacement and frozen
   double response, preserving all new evidence. Its different operation mix,
   provider call, writes and event behavior must be reported: a fixed replay versus
   new issue/accept comparison alone cannot attribute causation to history size.
3. Use three fresh independent processes per arm, serially on the same host, with
   frozen order `[F1,F250,F1000,G]`, `[G,F1000,F250,F1]`,
   `[F250,G,F1,F1000]`. Start each timed operation at one-second cadence without
   overlapping requests; record lateness. Use a 30-minute per-process hard stop,
   a preregistered 1-GiB sampled-RSS safety stop and owned-process cleanup. These
   are experiment safety limits, not product memory acceptance thresholds. The
   shorter cadence differs from the historical nine-second soak; do not present
   the old/new traces as a matched performance comparison.
4. Capture request/provider/DB timings and bytes plus per-process OS RSS and
   managed allocation totals/rates, GC collections/pauses, managed live heap,
   committed heap, LOH/POH and fragmentation at registered checkpoints after the first 50
   operations (warmup, included in the 1,000-operation state-growth budget) and at
   100/250/500/750/1,000 operations. Exclude the first 50 from timed distributions,
   not from history counts. Distinguish provider-double and
   driver PIDs. Record current project bytes/history counts, bus event count and
   payload bytes, and SQLite/cache information. Instrument all arms equally and
   retain an instrumentation-overhead control. Collect root/type evidence if a
   fixed-size arm still grows; do not infer native bytes by simply subtracting
   managed heap from RSS.
5. An optional forced-GC/root-snapshot diagnostic must be separately registered,
   outside timed measurements and identical across arms; keep natural-GC traces.
   Report per-process trajectories and allocation/latency distributions, including
   negative results, without pooling restart boundaries or treating sampled maxima
   as exact peaks. No native-leak verdict or release acceptance follows without
   ownership/root evidence and an owner-approved resource threshold.

Potential later engineering candidates are request-local ledger/revision indexes
and reduced redundant full-project materialization/serialization, with complete
checksum, duplicate-ID, ownership, stale-source, idempotence, authorization and
optimistic-transaction regressions. Persistent validation caches, schema changes
or audit-history deletion are not proposed as an unreviewed shortcut.

## Source preservation

All 16 reviewed source files matched the frozen soak's source manifest before this
follow-up. The after-check changes only the approved ProposalsController file;
the other 15 remain byte-identical. Existing soak registration, driver, results,
analysis and source-manifest bytes are unchanged. New test/report files and
`source-hashes-before.json`/`source-hashes-after.json` identify the exact delta.
The source-only investigation did not open or modify the final SQLite store.
