# LeviathanText project state — 2026-11-05

**Final handoff: 2026-11-05 13:00 CST.**
The authorized work period is finished. Changes remain uncommitted; no push or release publication occurred. Packaged documentation preserves its earlier build-time snapshot; this repository report includes the late local-capture HTTP delivery finding.

**Later UI-01 follow-up:** the local-path delivery and default-/ui boot failures
reported in this13:00 snapshot are now fixed for bounded local PNG/JPEG previews.
The later gate passes1,756 C# cases plus one skip,169 frontend cases and an actual
API-hosted16-case Chromium flow. This historical snapshot is retained; see the
[current UI-01 implementation, evidence and limits](../evaluation/ui01-capture-delivery-2026-11-05.md).
Greek quality, licensing and other release gates remain unchanged.

## Overall assessment

LeviathanText is an independent C#/.NET 10 OCR/HTR and scholarly transcription
workspace with an Angular 20/TypeScript interface. Its CLI, API, workers, native
persistence and bounded interchange paths run on the macOS arm64 pilot machine
without a Kraken or Python application runtime. It has substantial tested workflow
functionality. It is **a development pilot, not an accepted accurate Greek OCR
release or a demonstrated complete replacement for eScriptorium**.

Engineering completeness and recognition quality are different gates. The project
has no qualifying P46 model, no specialist-adjudicated Greek acceptance corpus,
no accepted inscription recognizer and no demonstrated VLM accuracy improvement.
Dependencies and supported deployment platforms also need release clearance.

The web workspace has a confirmed local-capture delivery gap: native filesystem
paths are passed directly to image URLs, and a published-API check returns HTTP 404
for that location. Full-surface viewing/editing of local captures is therefore an
open UI-01 requirement. Passing frontend units, served bundles and other API
operations do not establish that missing end-to-end workflow.

## Working code and architecture

- Native project storage separates physical objects, surfaces, captures and frames
  from line geometry and revisioned text. L0 is machine output, L1 diplomatic text
  and L2 editorial material; source attachments, annotations and review decisions
  retain provenance. SQLite provides optimistic project writes and durable jobs.
- Import/export covers bounded PAGE, ALTO, hOCR, ABBYY and TEI/EpiDoc, images,
  IIIF and rasterized PDF. Unsupported constructs and unreconstructable scholarly
  layers are reported as loss. This is not full schema-version or metadata parity.
- Recognition/training mechanics, variable-width preprocessing, checkpoint reload,
  optional learned layout and evaluation tooling exist. The default layout remains
  projection-based; learned models are opt-in and material dependent.
- The editor supports source inspection, diplomatic edits, editorial proposals,
  geometry/order changes, review and advisory annotations. New race controls
  preserve later typing and recovery drafts through delayed requests.
- Local and hosted provider adapters support explicit AI proposals. Native proposal
  issuance and acceptance now use authoritative source revisions, persisted issued
  records, L2 provenance, account attribution and transactional permission checks.
  Actual provider quality remains unproven by offline transport tests.
- Workers process persisted jobs independently. Event streams observe standalone
  worker state, and admitted project jobs recheck current authorization. Optional
  completed-attempt budgets enable supervisor-managed process retirement.

## This development period

The authorized work window is November 5, 06:39–13:00 CST. Work is based on
pushed baseline commit `413812654cbc2829a5016e78133fa40a8d4ee8fb`. New changes remain
uncommitted. All 42 demonstrated checklist slices from this period are archived
in `completed.md` (173 total entries); broader capability/release gates stay open.
Parallel ownership, generated fail-before regressions, independent
review and preserved benchmark snapshots are recorded in the dated reports.

Completed or verified slices include:

1. Byte-verified capture relocation after restore, read-only dry-run behavior and
   checksummed receipts; a P46 three-page rehearsal preserves native metadata and
   exact images through backup/restore, relocation and export.
2. Current-revision review summaries, stable authenticated attribution, role/path
   enforcement and refusal of structural operations that would orphan evidence.
3. Draft-safe editor saves and stale-order guards; accepted L2/advisory results
   refresh immediately without losing unsaved diplomatic text.
4. Native persisted AI issuance/acceptance and bounded batches; source-linked
   editorial provenance, idempotent durable receipts, tamper rejection and
   permission checks at commit. Ambiguous editorial alignment is refused.
5. Provider-session cap retention, concurrent/retry spend reservations and
   request-scoped rotating-credential redaction for text and vision failures.
6. Ordered event publication, standalone-worker SSE observations, replay cursors
   and reauthorization on disablement, membership changes and local auth activation.
7. ABBYY whitespace/geometry preservation, hOCR baseline fallback, explicit rotated
   PDF text refusal and explicit TEI layer-loss reporting.
8. Indexed scalar user queries and scoped SQLite commands, with measured generated
   before/after comparisons rather than inferred performance claims.
9. Shared learned-layout ownership and deterministic API/worker/CLI cleanup,
   preserving threshold aliases until their own disposal. This does not fix the
   separately traced upstream TorchSharp/libtorch retention.
10. A non-fitting engineering verification entry point, an inactive CI example,
    safer evidence receipts, packaged operational/schema documentation and a
    tested frontend dependency-security update.
11. Correct physical TEI splitting across nested milestones and verse blocks,
    original source contexts, explicit language inheritance/resets and separate
    declared languages. New P46 imports have 81 physical rows; old native records
    and specialist packets are unchanged. Language lists are unions across retained
    L1/L2 content, with explicit loss where that union cannot be serialized exactly;
    per-span language reconstruction is not implemented.
12. Generic LabelCodec scalar boundaries, immutable mappings and safe compatible
    merges, independently checked without integrating it into existing checkpoints.
13. Skia dimension admission before raster allocation, plus generated functional
    and peak-RSS evidence. The TIFF fallback remains a separate full-frame path.
14. AI static-PNG decoding through existing Skia assets, preserving exact transmitted
    bytes/hashes, bounds and validation; full Imaging replacement remains open.
15. Offline runtime-package notice collection and exact upstream supplements,
    retaining unresolved source-version/native notice flags.
16. Mechanical M02b/c archive/image/PAGE audit and a read-only hosting-enforcement
    audit. These neither accept labels nor enable repository protection.
17. Discovery-time fitting opt-in for six audited test classes, with harmless
    constructor/Fact/Theory evidence after the disclosed verification mistake.
18. Removal of unused native proposal payloads from the singleton job replay bus.
    Two HTTP regressions fail before and pass after; actual job replay, native
    records, responses and authorization remain intact.

Detailed evidence is linked from `todo.md`, `completed.md` and the evaluation
reports. Final package and sustained-run receipts are reconciled below.

## Verification and operations

The final integrated Release gate passes **1,684 C# cases**,
with one existing live-Ollama skip and zero build warnings/errors. Earlier 1,512,
1,529, pre-language 1,643 and interim language 1,672/1,682 results are historical integration checkpoints. The selection excludes
six audited fitting classes and all pilot workflows. Two individually receipted
pure LabelCodec classes from the mixed ML project contribute 22 of those passes;
no model/fitting classes from that project execute. The build compiles them.
Fitting and natural accuracy were not verified by this gate. The current layout
training-owner cleanup compiles and its untrained construction/lifetime helper is
tested. Receipts are in `.artifacts/midday-final-retention-engineering/`.

**Verification incident:** earlier parallel Application/API commands omitted the
required exclusion filter and unintentionally ran synthetic layout-training
fixtures. The Application fixture uses four generated pages and 40 epochs; the API
fixture uses three generated pages and 30 epochs, then generated inference. The
retained logs confirm three Application and three API invocations: three 40-epoch
and three 30-epoch generated fits, respectively. These were outside the assigned
non-fitting scope. No P46/source-corpus training, accuracy
search, accepted-label creation, model promotion or provider/VLM call resulted.
The filtered gate above was rerun separately; the earlier full-suite totals are
not the accepted non-fitting receipts. The incident and prevention follow-up are
recorded explicitly rather than calling the entire period training-free.
Fixture image/model roots have test cleanup; the API fixture's randomly named
temporary SQLite stores do not have explicit cleanup, so complete fixture-store
removal is not claimed. A discovery-time opt-in guard now protects all six audited classes before test
constructors run. Actual default checks produce 21 skips and zero fitting bodies;
a harmless sentinel verifies explicit opt-in and constructor behavior. Mixed
ML/Pilot suites still require the documented exclusions.

The [remaining-suite source audit](../evaluation/remaining-fitting-test-audit-2026-11-05.md)
indexes 74 ML/Pilot declarations, including 57 paths reaching fitting APIs (three
negative pre-update controls and 54 capable of optimization). It executes none.
Some apparently metadata-only tests fit in their setup. Dataset-presence skips
also do not protect corpus or locked-label access: the current full-snapshot
loader materializes locked contents before returning partitions. Separate fitting,
corpus-access and locked-evaluation gates plus a loader change remain TEST-01 work;
the spent historical P01 split does not become fresh through new guards.

The current frontend passes 151 tests and production build on actual Node 22; the
earlier 149-case dependency/consumer gate also passed on Node 26. Script regressions pass 26/26 when allowed to start their
owned loopback fixtures. Tests which aborted under sandbox socket restrictions
are not counted as passing or as product failures.

An optional actual Angular-to-API browser smoke stopped before setup: computer-use
inventory reported the Mac locked and exposed no browsers/apps. No API, provider
or tab was started for that attempt. The 151 frontend cases/build and actual HTTP
process checks remain valid within their own scope; they do not establish that
unperformed live-browser workflow. UI-01 records the remaining integration check.
Later source inspection and an owned published-API proof locate a concrete cause:
importing a generated local PNG returns an absolute filesystem imageLocation;
requesting that origin-root URL returns 404, while the UI HTML returns 200. No
browser visual result is inferred, but full-capture HTTP delivery is demonstrably
absent. No late product patch or package replacement was made; UI-01 requires an
authorized, bounded capture-ID route/client mapping and explicit frame semantics.
The [published-API proof](../evaluation/local-capture-browser-delivery-gap-2026-11-05.md)
and [parallel API/client brief](../tasks/local-capture-browser-delivery-2026-11-05.md)
record exact ownership, security/frame/resource gates and actual browser acceptance.
The final generated HTTP check exits its owned API cleanly and preserves all 67
registered inputs; it does not call a model or fit one.

The final local macOS arm64 package is
`.artifacts/releases/midday-final-language-reviewed/`: **451 manifest files**,
859,089,487 payload bytes, with a 216,134,465-byte tar archive. Root independently
verified tar SHA-256
`153ceae7f48764ba1d3580fb4710dd31d797b964c527f449cc7d0769e361c91d`.
This is an internal engineering artifact; release clearance remains false.

Actual published CLI/API/worker execution, twelve native architecture checks,
served UI/two scripts, job completion, archive closure and all 320 registered
product/configuration/script/browser/catalog hashes pass. The package runs without
Kraken or Python as application runtimes; it requires its declared .NET/native
runtime environment and does not bundle recognition weights.

The actual published API passes 20 native-AI HTTP checks over two cleanly exited
processes and five loopback-double requests, including account attribution,
authoritative source selection, durable receipts/idempotence, revocation before
dispatch, tampered-graph refusal and a request cap that resets on process restart.
Provider cleanup passes; no real language model is used. Published API and CLI
PNG probes each pass 20 cases (seven valid exact-byte cases and 13 rejections).

The published CLI imports the three P46 TEIs into 81 new physical rows, all with
explicit `grc` and separate declared header language. A copied 85-row legacy
project remains byte-identical, as do 104 specialist-packet files. Four generated
physical/logical/inline/reset/ignored-note language cases preserve exact expected
arrays and text. No accepted label or source map changes. An initial harness used
a nonexistent folio filter and safely received InputMissing; a fresh registered
corrected harness passes, with the failed receipt preserved.

The actual install/update/rollback script passes with all three nested logs
retained. The owned application tree is removed, and default-store existence and
checksums remain unchanged. Runtime notice/catalog checks retain their explicit
14 review flags and four exact-version gaps. Full evidence is in the
[operations report](../evaluation/midday-package-operations-2026-11-05.md) and
`.artifacts/midday-final-package/final-language-reviewed/final-results.json`.

Earlier package receipts are historical snapshots; they were not overwritten or
relabelled as final-source evidence. Documentation in the final tar is a build-time
snapshot, while this repository report and the final archive reconciliation may be
updated afterward. Compiled/browser/catalog inputs remain frozen.

## Greek data, models and scholarly readiness

P46 Michigan is the selected Koine manuscript pilot. Pages 1440/1450/1460 have
archived item evidence, photographs, source TEI and a specialist packet. All 81
specialist decisions remain pending. The provisional line study has 51 crop/target
rows: 44 training-exposed and seven repeatedly development-exposed. They cannot
serve as fresh independent confirmation. No new accepted labels were created.

The historical 32-pixel-input-height run records 73.97% selected development CER. Matched
16-pixel-input-height and expansion studies record roughly 77–81% development CER and fail their
registered improvement gates. No model has been promoted. More training on these
same unresolved labels would not establish readiness.

The historical portability project contains 85 imported rows. The corrected
physical importer creates 27/26/28 rows (81 total) in **new projects**; actual
CLI compatibility checks show the old 85-row project is not migrated. This fixes
structural ingestion and does not establish crop/reading alignment or the cause of
the historical CER. Original source IDs are absent for these milestones; generated
IDs are distinct from source identities. The packet generator remains unlocated.
Page 1440 row 10 has source context V13/V14 but packet association V14, retained as
a question in the structural specialist addendum. Existing explicit scholarly
layer/export loss remains visible; native JSON and original attachments survive.
The latest real-source replay also exposed inherited grc language being retained
only in the source attachment. The correction now preserves explicit effective
language and separate header declarations, respecting empty resets and original
inline scope, including bare physical rows. Independent 13/13 generated controls
pass, and real P46 imports retain grc on all 81 lines. Older native language arrays
are not migrated. This preserves supplied metadata rather than classifying text.

Manuscript and papyrus deposits have mechanical intake and exposure audits;
upstream photographs, hand/folio mappings, exact physical-unit lineage and visible
readings still need confirmation. Greek inscription metadata candidates and
text-first editorial imports exist, but image-aligned readings and geometry remain
unreviewed. Later manuscript dates do not themselves establish Classical Greek
text coverage. Ancient inscriptions and documentary/literary papyri require their
own source and review evidence.

There is **no accepted, image-aligned Classical Greek evaluation corpus or
recognizer**. P46 and M02a Gospel evidence is Koine in differing witness dates;
other manuscript language/style coverage needs source review. The newly verified
M02b/c archives contribute 84 mechanical image/PAGE pairs and 1,809 source lines,
with zero accepted labels and unresolved upstream photo evidence. Across the 258
local M01/M02a/b/c images, exact RGB pixel hashes find no duplicates; that does not
exclude crops, reused photos or related physical units. I01 image grants and I02
deposit-versus-embedded-photo terms remain item specific and unresolved.

The new [15-object Classical-period inscription handoff](../data/classical-inscription-shortlist-2026-11-05.md)
adds an exact chronological metadata/permissions queue. All 15 editions are draft
and CC BY 4.0; their 57 source line markers are not accepted visible lines. Fifteen
official manifest requests identify 19 object views and 60 petrographic auxiliary
canvases. One image attribution explicitly carries British Museum CC BY-NC-SA 4.0
terms, distinct from its edition license; none is cleared here for general open
corpus reuse. Date/material discrepancies and missing IDs/views remain recorded.
ISic001432 is the first metadata/permission follow-up recommendation, with
ISic001439 second. Neither is a training-ready or language-adjudicated selection.

The VLM benchmark through LM Studio has transport, cleanup, scoring and synthetic
geometry/blank diagnostics. Reviewed natural references, frozen criteria and fresh
confirmation custody remain open under VLM-04/06/07/09. No VLM call or LM Studio
setting/model change occurred during this development period.

## Resource evidence

Worker attempt budgeting and failed-layout-load cleanup have regression proof.
The registered generated-input study completes **8,000/8,000 jobs/L0 outputs**;
the retained-P46 study completes **120 page attempts/3,240 L0 revisions**. All 54
workers exit 0 and exact text/confidence parity holds. Recycling does **not** show
a consistent sampled peak-RSS reduction and remains no upstream leak correction.

The natural replay uses 81 proposed bands on width 2200 review derivatives and an
existing legacy checkpoint. It reads no reference TEI, scores no CER, fits no model
and certifies no geometry. It does not cover original-resolution or >25 MP/tiled
TIFF. Training resource limits/platforms remain separately open.

Measured generated engineering improvements include:

| Bounded case | Before | After / finding |
| --- | --- | --- |
| Bulk proposal discovery, 128 IDs/six projects |172.7 ms;323.6 MB allocated |About 3.0 ms;3.05 MB allocated |
| Oversized 12001×4096 image rejection |About 254 MB peak process RSS |About 57 MB after header preflight |
| User identity lookup at 10,000 accounts |About 5,662 µs |About 5.3 µs |
| Isolated idle-only worker backoff, 100 noops |About 99.5 s |0.328 s, **research candidate only** |

The backoff candidate is not adopted: fairness, heartbeat/retry and representative
model/resource workloads need registered validation under the
[two-instance protocol](../tasks/worker-idle-backoff-validation-2026-11-05.md). Main's polling delay remains
unchanged. PNG replacement measurements show a 3.6% slower large-fixture median
with overlapping observed ranges, not proven performance equivalence. Its native
asset/RID contract is now explicit.

The registered frozen-runtime API run completed 1,000/1,000 issue/accept cycles
in 8,992.40 seconds, including restart after 500 and exact L1/source/receipt/account
checks. Both APIs exited 0 and the owned provider stopped. It used a loopback
double, no real model. Its runtime predates later UI/PNG/TEI corrections.
Sampled peak RSS reached 479.02/708.42 MiB as history grew; persistence stability
is proven for that snapshot, while memory acceptance remains open. See the
[complete run](../evaluation/native-ai-process-soak-2026-11-05.md).

The [growth follow-up](../evaluation/native-ai-growth-resource-followup-2026-11-05.md)
finds and removes an unused managed replay-history root in the native proposal
controller; the long run predates that correction. It does not establish that
this root caused the full RSS increase. Whole-project loads/saves and repeated
ledger scans still scale with retained history; a registered differential study
is planned, not executed. Real job logs also remain unbounded pending an explicit
retention/resynchronization contract (JOB-01).

## Release blockers and owner decisions

- Greek-specialist review, fresh physical-unit splits, quality/grounding/correction-
  effort criteria and usable independently licensed weights remain essential.
- ImageSharp 3.1.11 has six advisories. An earlier isolated 3.2.0 candidate audited
  clean but required issued vendor licenses; that route was not adopted.
  The owner selected permissively licensed replacements. AI's strict-PNG path now
  uses existing Skia assets; its direct package graph audits clean. Imaging still
  uses 3.1.11, and six advisories remain in 16 solution graphs. No vendor gate was
  bypassed. BitMiracle is a bounded TIFF candidate, not an approved security fix;
  the current fallback also supports PBM, TGA and QOI, so TIFF alone is insufficient.
  Standalone AI consumers now require matching Skia native assets; only the
  macOS arm64 pilot is demonstrated. Standalone AI Linux provisioning/execution
  remains unverified, and the native package's full notices still apply.
- npm now reports zero vulnerabilities after compatible patches and narrowly scoped
  CLI dependency overrides. This is a dated audit result, not a perpetual guarantee.
- Upstream native retention, original-resolution workloads, full complex geometry/
  reading order/BiDi and other deployment targets remain unverified or incomplete.
- Apache-2.0, DCO 1.1, evidence requirements and lead-maintainer approval govern the
  official project; independent forks remain permitted. Read-only GitHub evidence
  confirms public main is unprotected, with no rulesets or registered Actions
  workflows. Required hosting enforcement is **not configured**. Reviewer/DCO/
  trusted-check activation and negative enforcement tests remain owner work.
- Runtime notices now cover 16 packages with six embedded notice files and 23
  supplemental license/version files. Four exact SQLitePCLRaw source/license
  records and native-component review remain unresolved; 14 inventory flags stay
  conservative. The catalog's research anchor is not a final-artifact hash binding.
  PDFium package/build/native notice layers differ and are retained separately;
  no complete native SBOM or legal redistribution attestation is claimed.
  A [SQLite follow-up](../evaluation/sqlite-license-metadata-followup-2026-11-05.md)
  finds a maintainer commit declaring the matching version and one exact package
  source-asset match, but not full compiled-binary/source binding. The four catalog
  gaps and 14 review flags remain unchanged.
- Worker revocation checkpoints cannot undo an active surface unit, and attempt
  budgets do not bound bytes/time/surfaces. Native AI replay requires current source
  identity; ambiguous alignment/restoration is refused. Draft races are covered,
  while failed browser storage cannot guarantee durable recovery of new keystrokes.
- A matched external Kraken/eScriptorium comparison, accepted-record migration,
  measured correction effort and collection cutover acceptance remain open.

The next scholarly step is an adjudicated corpus handoff and frozen acceptance
protocol, followed by a registered matched study. The next release-engineering
steps are dependency clearance, representative resource/deployment verification
and hosted contribution enforcement. Neither can be replaced by a larger test count.

## Recommended next assignments

| Priority | Assignment | Owner / dependency | Acceptance evidence |
| --- | --- | --- | --- |
| 1 | Finish permissive Imaging replacement (SEC-01) | Decoder implementer and independent evaluator can work in parallel from the [two-instance brief](../tasks/permissive-imaging-replacement-2026-11-05.md) | Preserve TIFF/PBM/TGA/QOI and existing Skia behavior, preallocation/resource checks, exact notices and a clean final dependency graph; no silent format loss |
| 2 | Deliver local captures to the browser safely (UI-01) | Capture API/frame owner and client/integration owner in parallel | Authorized capture-ID delivery without filesystem exposure; bounded decode/derivative and EXIF/TIFF/frame contracts; actual no-line capture and overlay/browser checks with unchanged source bytes/geometry |
| 3 | Finish fitting/corpus/locked test boundaries (TEST-01) | Test-infrastructure engineer and independent static-audit reviewer | Default-off discovery before constructors/helpers, train/dev loads never open locked contents without explicit scope, harmless sentinel proof and fresh evidence destinations; retain the exact codec allowlist |
| 4 | Return the P46 specialist packet and structural addendum | Named Greek specialist; no engineering inference substitutes for decisions | Reviewed identities, visible readings, marks, crop geometry, ambiguities and exposure ledger; versioned accepted corpus |
| 5 | Establish a Classical Greek pilot distinct from the Koine evidence | Owner selects material; data worker verifies item terms; specialist confirms language/material coverage | Actual authorized image/line pairs on appropriate physical units, accepted labels and a separately held confirmation set |
| 6 | Register representative native-resource and polling studies (NT-20/PERF-01) | Engineering; frozen workload and resource limits before execution | Exact output parity, fairness/retry/heartbeat/shutdown checks, RSS/allocation/latency evidence and clean owned-process exits; retain negative findings |
| 7 | Enable supervised contribution enforcement | Repository owner chooses reviewer/DCO/check/bypass policy | Required checks and protected branches/rules actually configured, with negative tests showing unsuitable changes cannot merge |
| 8 | Close distribution/deployment gaps | Dependency/packaging owner; target-machine access and complete notice evidence | Exact-version SQLite/native notice mapping, per-platform install/update/rollback and declared resource limits |

The VLM natural comparison follows reviewed references and frozen criteria under
VLM-04/06/07/09; it can then use LM Studio without training. It is not a substitute
for the Classical Greek corpus assignment. An external Kraken/eScriptorium study
must compare authorized matched records, correction effort and preservation, rather
than relying on the number of implemented features.
