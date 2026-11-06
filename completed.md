# Completed work archive

Items checked off in `todo.md`, moved here at the end of each work cycle. `todo.md` keeps open
and in-progress checklist items; plan narrative and reference records (contracts, source
register, decisions, estimates) live in [project-plan.md](project-plan.md). Rule recorded in
`lessons.md`.

Archive build: 2026-11-06 (midday/overnight verified additions; current additions uncommitted).
Entries preserve their original commit references and links. 191 items.

**Current scholarly qualification (November5):** the historical profile entry below
records an implemented pilot engineering draft. Its wording about a specialist and
“ratified for pilot” is not evidence of named Greek-specialist ratification. All81
P46 specialist decisions and the current scholarly acceptance gates remain pending.
The dated entry is preserved rather than rewritten as a later approval.

**Current UI qualification (November 5 follow-up):** historical UI entries retain
their original evidence and wording. The local-path HTTP404 and default-hosting
failure were subsequently fixed under UI-01, with actual API-hosted Chromium
validation. See [the bounded implementation and limits](docs/evaluation/ui01-capture-delivery-2026-11-05.md);
this does not turn earlier tests into retrospective end-to-end evidence.


## 1. Request and boundaries
- [x] Receive a separate instruction to begin implementation after review; this review does not authorize coding. *(satisfied 2026-10-18; implementation began.)*
- [x] Read the workspace `Agent.md` and reference checkout `Agent.md`.
- [x] Inspect Kraken's documented product capabilities and command surface.
- [x] Design an independent C# replacement with a TypeScript/Angular web interface.
- [x] Record architecture, delivery tasks, validation gates, and unresolved decisions here.
- [x] Incorporate the user's Greek emphasis and explicit priority for manuscripts, papyri, and inscriptions.
- [x] Research concrete datasets and published licenses, distinguishing images, transcriptions, code and weights (Section 5).
- [x] Define parallel work packages, ownership, dependencies and integration rules for two or more AI instances (Section 6).
- [x] Create separate no-code task briefs for Instance A (contracts/integration) and Instance B (corpus/evaluation planning), linked in Section 6.

## 3. Reference capability inventory and release scope

### Greek transcription contract — completed 2026-11-02

- [x] Define versioned transcription profiles with a Greek specialist. Cover case, lunate/final/medial sigma, historical letters, ligatures, punctuation, numerals, abbreviations, nomina sacra, overbars, supralinear letters and nonalphabetic signs. Record visible glyph forms separately when plain Unicode text cannot express the distinction. *(greek-diplomatic-1.0-draft ratified for pilot; executable policy/sign inventory and profile tests.)*
- [x] Use Unicode scalar-aware processing and grapheme-aware editing/alignment; define revision-scoped scalar offsets in persisted annotations, with explicit conversion to C#/browser UTF-16 offsets and grapheme boundaries. Changing normalization must not detach annotations or diacritics from their source spans. *(NFC remapping and revision offsets already implemented; shared JSON fixture now asserts identical C#/Angular scalar/UTF-16 boundaries.)*
- [x] Represent unclear traces, alternative readings, lost versus illegible text, known/estimated/unknown gap extent, erasures, additions, deletions, different hands and correction sequences as annotations. Keep “not yet transcribed” distinct from “illegible.” Editorial underdots/brackets must not be confused with visible marks on the object. *(annotation vocabulary + editor tools + TEI mapping; supplied text is L2-only.)*
- [x] Keep diplomatic text, editorial reading, scholarly annotations, and application revision history distinct. Historical corrections in the witness are not application edits. Do not claim to identify a hand or reconstruct a correction sequence automatically without evidence. *(L0/L1/L2 + L3 annotation ledger; hands/sequences require explicit labels; no automatic attribution.)*
- [x] Preserve imported text and original model output; derive canonical NFC text for interchange/scoring with explicit offset mappings. Do not apply compatibility folding, case folding, sigma substitution, spelling modernization, or accent stripping to the diplomatic layer by default. Training label normalization is a model-versioned decision. *(raw imports retained as attachments where bounded; L0 kept separate; NFC remapping already tested.)*

- [x] Preserve scriptio continua where present. Word boundaries, expanded abbreviations and added diacritics belong to derived readings, with reversible links to the source spans. Do not infer missing accents from the fact that the language is Greek. *(L1 spacing policy adopted; zero-width WordBoundary annotations, editor insertion, dual-pane derived boundary preview and `export word-boundaries` CSV are implemented; witness text is never rewritten.)*

### Scholarly interchange — completed 2026-11-02

- [x] Preserve unsupported imported markup in the original attachment and report it; do not silently flatten scholarly distinctions or claim universal TEI compatibility. *(Checksummed bounded `NativeAttachment` + aggregated name/count report for TEI and EpiDoc imports.)*
- [x] Preserve stable object, surface, fragment and line identifiers and provenance. Export readable diplomatic text plus a structured version that retains uncertainty and editorial interventions; explain any information lost in plain text or OCR-only formats. *(Line `@xml:id`/`@n`, `idno`, licence/title/responsibility and image links round-trip; export loss reports are machine-readable. Surface/line/fragment crosswalks join native ids to external refs, image hashes, revision and source ids; native fragments persist external refs/metadata and surface membership; project integrity/overlap audits are read-only.)*

## 5. Independent model and data strategy

### Intake backlog, ownership and unresolved gaps
- [x] Identify concrete repositories/DOIs and inspect published terms; record the results above.

## 6. Delivery backlog and acceptance gates

### Issued two-instance planning briefs
- [x] Repository established on `main` (commit `728ec9c`: planning documents + A-PLAN contract drafts as baseline).
- [x] Solution (`LeviathanText.slnx`) with `src/LeviathanText.Contracts/` and `src/LeviathanText.Core/` plus test projects; central `Directory.Build.props` pins **net10.0** (SDK 10.0.301, current LTS), Nullable + ImplicitUsings enabled.
- [x] Contract types implementing `C0-0.2-draft`: `EntityId`, `ScalarRange`, `TextLayer`/`TextRevision`/`AgentRef`, `AnnotationKind`/`Annotation` (uncertainty vocabulary), `ErrorCode`/`LeviathanError`, `StageRun`/`StageName`/`ModelRef`/`Checksum`/`LayoutSource`, `Confidence` (with `None` semantics), manifest vocabularies (`MaterialTrack`, `LabelGranularity`, `PairingStatus`, `AnnotationCompleteness`, `TranscriptionProfileRef`). `ContractVersion.Current = "C0-0.2-draft"` compiled in.
- [x] Core Unicode machinery: `ScalarText` (scalar ↔ UTF-16 conversion, surrogate-safe), `Graphemes` (UAX #29 extended clusters via `StringInfo`), `NfcMapper` (per-cluster NFC with a prefix-based source→target scalar-offset map; annotations that would split a composed cluster fail loudly with `StaleAnnotationAnchor`), `GreekMarkOrder` (versioned `greek-diplomatic-1.0-draft` profile mark ordering; explicitly *not* canonical equivalence).
- [x] `RevisionChain`: stale-parent writes fail `RevisionConflict`; superseded revisions remain retrievable; `NormalizeToNfc` creates a new revision and remaps annotations or fails loudly; per-layer chains independent.
- [x] Representative Greek fixtures (`tests/LeviathanText.Core.Tests/Fixtures/greek-lines.json`, labeled INVENTED, excluded from corpus counts) covering combining marks, scriptio continua, lunate sigma, overbar abbreviations, gaps and unclear spans.
- [x] Verification: `dotnet build` succeeds; **72/72 tests pass** (28 Contracts + 44 Core). Committed as `5f6ac24`.
- [x] C0 increment 2 (commit `da56646`): `LeviathanError`/contract-record JSON serialization and round-trip fixtures (Formats-facing) — `LeviathanText.Contracts.Serialization.ContractJson` with camelCase/string-enum/strict conventions (interchange-and-api.md §2.0); `StageRun` tuples replaced with serializable `StageArtifact` records; `LeviathanError` equality made content-based; round-trip tests over revisions, annotations, stages and the Greek fixtures.
- [x] **Single-instance mode (user decision, 2026-10-18 evening):** all packages now owned by Instance A. B's increment (commit `72690ea`) merged after independent verification (Release build clean; 136/136 tests). B's emergency repairs to Core (`RequireCurrentAnchor` CS0165, `MapRange` end-clamp) reviewed and accepted — equivalent to A's own fixes. B's contract requests dispositioned: **B-CCR-6 accepted** (`catalog/metadata-text` asset kind + `preservation_tags`), **B-CCR-7 accepted** (`EntityKind.Proposal`, prefix `prop`; `AiProposal.Id` now `EntityId`) → contract set **`C0-0.4-draft`** (commit `15fa2c7`).
- [x] **FMT package** (commit `95b7bd3`): native project persistence (`NativeProject` + `NativeJson` with contract-version refusal); diplomatic plain-text export with **mandatory loss report**; bounded **PAGE XML** import (pinned 2013+2019 namespaces, ReadingOrder consumed, severity Loss|Note issues) + export round-trip; bounded **TEI/EpiDoc** import/export (unclear/gap/supplied/app+rdg as annotations, never literal brackets in L1; DOCTYPE/external entities refused). 13 Formats tests.
- [x] **APP package** (commit `b7bf85e`): `Infrastructure.LocalStore` (SQLite via Microsoft.Data.Sqlite 10.0.13; schema versioning, newer-schema refusal, transactional project/job persistence); `Application` services (`ProjectService` over `IProjectStore`, `InterchangeService`); local **CLI** (`project new/list`, `import page`, `export diplomatic/page/tei`, `--store`/`LEVIATHAN_STORE`, closed-vocabulary errors, loss report on stderr). CLI smoke-tested end to end on real fixtures.
- [x] **IMG package** (commit `1f3a4f3`): SkiaSharp decode (size/dimension guards, sha256), `ImageTransform` records (Crop/Deskew mappable incl. center-pivot matrix; NonlinearDewarp recorded-but-refused, explicitly unmappable), `DerivativeFactory`, `LineCropper` with mandatory vertical padding (diacritics/overbars/interlinear retention) + edge-loss reporting + explicit refusal on absent geometry. 10 imaging tests.
- [x] **PAP-01 executed** (commit `3bc2131`): P01 v0.9 acquired from Zenodo — **59/59 files md5-verified**; audited **27 pairs / 321 source TextLines / 6,318 raw Unicode scalars** (corrected by the later [recount](docs/data/local-greek-page-intake-2026-11-04.md)); 5 unannotated images recorded image-only; DATA-01 ledger at [data/manifests/papyri/p01.json](data/manifests/papyri/p01.json); execution record in [docs/data/papyri.md §10](docs/data/papyri.md). Real-image smoke (869×539 decode + real line crop) passing.
- [x] **Real-data training gate increment** (commit `a3e48b7`): CRNN/CTC trained on real Greek papyrus lines — witness-level split train 176 / dev 50 / **test 95 locked**; 300 epochs; train loss 0.083 → 0; **best dev CER 0.833** (beats the empty-output baseline of 1.0, so real letters are read); **reload parity exact** (fresh model reproduces 0.833). Per-crop Otsu binarization recorded in preprocessing. **Pilot measurement only**: generalization is data-limited (176 training lines memorized; annotation volume stays the gate); no release claim, and the locked test set was never used for tuning.

### Coordination and integration rules
- [x] Before implementation, establish the repository and one branch/worktree per active package; the current workspace is not yet a Git repository. This task does not initialize it. All workers start from the same recorded baseline. If worktrees are unavailable, enforce disjoint write paths and a single integrator; never run concurrent edits to one file. *(established on `main` at `728ec9c`; single-instance mode makes worktrees moot — disjoint paths superseded by full ownership)*
- [x] Maintain a package board with ID, owner instance, status (`ready`, `active`, `blocked`, `review`, `done`), dependencies, base revision, owned paths, contract version and handoff location. Only the integrator edits the shared board/`todo.md`; other instances submit status and contract proposals to it. *(board maintained; integration checkpoints recorded in handoffs)*

### Phase 0 — Greek evidence, contracts and feasibility
- [x] Define the object/surface/capture contract, annotation offsets, native format and bounded TEI/EpiDoc mapping before building adapters or datasets. *(C0-0.4-draft, implemented as compiled contracts)*

### Phase 1 — Greek engine, minimal training and CLI
- [x] Implement native object/surface/capture persistence, scholarly annotation layers and coordinate mappings. *(NativeProject + LocalStore + Annotation/TextRevision contracts + frame registration)*
- [x] Deliver minimal C# dataset preparation, training/fine-tuning, checkpoint resume and model packaging needed to produce the initial track-specific models. The first release must not depend on Phase 3 to obtain its models. *(CtcTrainer: train/evaluate/best-checkpoint/early-stop/resume; checkpoints now complete model packages with persisted labels)*
- [x] Import/export native JSON and diplomatic UTF-8 text, the bounded TEI/EpiDoc profile, and a supported PAGE subset for annotation interchange; report losses in less expressive formats. *(all four formats + severity-graded loss reporting)*
- [x] Provide batch processing, cancellation, durable stage checkpoints, run manifests and actionable errors. *(RecognitionPipeline batch path + cooperative cancellation + checkpoint validity classification + closed error vocabulary)*

## 10. Sources and planning review

### Licensing and contribution policy — 2026-10-19
- [x] Install the unmodified [Apache-2.0 license](LICENSE) and [DCO 1.1 text](DCO), plus project attribution and license-scope notices in [NOTICE](NOTICE).
- [x] Write [governance](GOVERNANCE.md), [contribution requirements](CONTRIBUTING.md) and a [pull-request evidence template](.github/pull_request_template.md) reflecting the owner's approval.
- [x] Link the policy documents from the README and verify text integrity, links and consistency without changing application code.
- [x] User constraints are explicit: independent product, C# backend/training, Angular/TypeScript UI, AI-friendly policy, plan only.
- [x] Scope prioritizes Koine/Classical Greek manuscripts, papyri and inscriptions, with printed editions later.
- [x] Initial-model production and minimal annotation/training precede the first model-dependent release.
- [x] Greek transcription, scholarly interchange, corpus ownership and acceptance-sheet deliverables are explicit.
- [x] Object/surface/capture modeling and unspaced recognition replace assumptions of rectangular pages and word boxes.
- [x] Initial AI assistance follows stable evidence/revision contracts rather than waiting for broad Kraken parity.
- [x] Architecture addresses C# training/export uncertainty, data availability, document fidelity, durable jobs and migration.
- [x] Future work is unchecked and has dependencies and measurable acceptance categories.
- [x] Concrete dataset sources, license evidence, restricted/uncertain alternatives and remaining material-specific gaps are recorded in Section 5.
- [x] Parallel package ownership, two-instance and larger-team assignments, versioned contracts and serial integration checkpoints are recorded in Section 6.
- [x] Two standalone planning briefs define disjoint write ownership, independent starting work, review dependencies, acceptance criteria and versioned handoffs; neither assignment has been executed by this briefing task.
- [x] Independent brief review found no ownership overlaps, circular acceptance dependency or unintended authorization of implementation/acquisition. The changed-file check contains only the two briefs and this plan.
- [x] Independent source/plan reviews checked manuscript, papyrus and inscription facts; identified ownership gaps were corrected.
- [x] No application code was written; this work changes only planning documents.
- [x] Implementation has started (superseded the original "unstarted" status; see Section 6 implementation records and the handoffs). This line is kept for history.

## 6. Delivery backlog and acceptance gates
### Phase 2 — scholarly workspace and initial AI assistance
- [x] Export corrected training data using visible-text labels and explicit annotation status; do not train visual recognition on supplied restorations. *(TrainingDataExport: L1-only labels, editorial-supply lines excluded outright, uncertainty spans + exact-span complements, CLI/API exports; 8 tests + live smoke. ML training consumption remains B's pipeline work.)*

## 6. Delivery backlog and acceptance gates
### Issued two-instance planning briefs
- [x] B's review of the contract set (I0 gate) — moot while single-instance; substitute with a self-review of all five contract documents against all packages at the next integration checkpoint (I1). *(done: docs/contracts/self-review-2026-10-19.md — conformance matrix, deviations, open actions)*

## 5. Independent model and data strategy
- [x] Build an initial C# sequence recognizer with a tractable CTC architecture, without mandatory word segmentation or a dictionary. Compare NFC scalar, decomposed scalar or grapheme label strategies on real Greek lines; verify combining-mark coverage, unknown labels, time-step capacity and repeated-symbol decoding. Select the simplest measured approach; retain variable/irregular region extraction as a feasibility question. *(C# CRNN+CTC with grapheme labels; label-strategy/decoding/time-step measurements in docs/evaluation/decoding-comparison.md; unknown graphemes fail loudly; layout extraction researched separately — see learned-layout records.)*
- [x] Support from-scratch training and fine-tuning, deterministic seeds where possible, resumable optimizer state, early stopping and best-checkpoint selection. *(CtcTrainer: resume from checkpoint, early stopping, best checkpoint, seeded; ML/pilot tests; model package CLI.)*
- [x] Add augmentation suited to scans and handwriting; keep synthetic/AI-generated examples labeled and evaluate on held-out real documents. *(LineAugmenter stretch/shift/salt-pepper + P01 A/B; layout CNN scale/shift/noise + augmentation A/B on real M02a/P01 pages; no synthetic training examples were introduced.)*
### Corpus acquisition and annotation deliverable
- [x] Provide a bootstrap path using imported or manually labeled lines before the complete Angular editor exists. Minimal annotation and C# training must precede any release that depends on new models. *(PAGE/EpiDoc/ALTO/hOCR import, manual line placement/geometry, CLI `train lines` and `layout train` produce verified packages without requiring the full review UI.)*

## 6. Delivery backlog and acceptance gates
### Phase 4 — wider replacement scope
- [x] Implement bounded ALTO v4 and hOCR import/export (line boxes + line text; unsupported elements reported; external entities refused; legacy import path). *(adapters + round-trip tests + CLI/API/bundle exports; no word-segmentation synthesis)*

## NTVMR / CSNTM pilot — 2026-11-02

- [x] **NT-12 (review preparation):** prepare [the P46 specialist packet](docs/data/p46-specialist-review-packet.md) with 81 proposed line/context crops, source TEI and apparatus, full pages, 18 adjudication questions, blank CSV worksheets and portable offline archive. All answers remain pending; this completes packet preparation only, not NT-05/NT-11 adjudication or training readiness.
- [x] **NT-13 / provisional experiment (training instance):** run the owner's newly authorized bounded P46 training experiment on a conservatively filtered, unadjudicated subset; keep physical leaves separate, record baseline/selected checkpoint metrics and reload verification, and publish a local research report. Specialist acceptance and release gates remain open. *(Completed: [two-run report](docs/evaluation/p46-provisional-training.md), 17 train / 7 development lines; selected v2 epoch 1,164, 73.97% development CER; package reload and independent metric audit passed. Research experiment only.)*

### P46 development-error investigation — 2026-11-02

- [x] **NT-14 / development-error investigation (training instance):** quantify v2 failures, audit preprocessing/labels/geometry and test the saved checkpoint without retraining; update the plan with evidence-ranked corrective experiments, ownership and acceptance checks. *(Completed: [evidence report](docs/evaluation/p46-development-error-investigation.md), preserved-checkpoint inference/beam test and [D1–D5 next-run protocol](project-plan.md#p46-next-run-protocol--2026-11-02); no retraining.)*

### Training diagnostics, modes and padding — 2026-11-02

- [x] **NT-16 / D2 (A; parallel with NT-15):** implement the authorized diagnostic telemetry, explicit train/eval modes and background/polarity-safe padding/augmentation contracts with regression tests; preserve existing model polarity and propagate shared-transform metadata. Record any broader preprocessing migration remaining. These inactive issues did not explain v2. *(Completed: [engineering record](docs/evaluation/training-engineering.md); 155 selected tests pass, solution build 0 warnings/errors, and saved P46 output matches 24/24. Broader preparation unification is NT-16b.)*

### Shared preprocessing contract — 2026-11-02

- [x] **NT-16b / remaining D2 preparation (A; before NT-18):** implement the authorized versioned shared C# preparation contract and propagate package profiles through training/evaluation/inference; preserve legacy route behavior, serialize profile/height/geometry, reject unsupported/conflicting contracts and prove old-package compatibility plus tensor equivalence with regression tests. P46 rectification remains external to generic crop preparation. *(Completed: [contract/verification](docs/evaluation/preprocessing-contract.md); 219 selected tests pass, solution build 0 warnings/errors, typed format-2 packages and legacy format-1 defaults verified; saved P46 output unchanged.)*

### Two-hour provisional study — 2026-11-02

- [x] **NT-19 / two-hour provisional study (training instance):** work 2026-11-02 15:37:39–17:37:39 CST on the newly authorized training session; preserve reviewed-gate status, implement/test T1 normalization and T2 gamma, inspect inputs, run the provisional fit control and bounded three-seed matrix if the control passes, then retain checkpoints/metrics/resource evidence and update the plan. Does not complete specialist-dependent NT-15/17/18.

Completed the authorized 15:37:39–17:37:39 CST window; closure recorded 17:38:10 CST. [Full results](docs/evaluation/p46-two-hour-study.md) and [A1.1 handoff](docs/handoffs/NT-PILOT-A.md): three controls plus nine accuracy comparisons (63,594 updates), six separate training-resource probes (6,000 updates), exact recovery checks, 475 passing regressions and zero-warning solution build. No qualifying development improvement or model promotion. Source packets, evidence and parallel next-work briefs are retained; reviewed NT-15/17/18 and remaining NT-20 gates stay open.

### Second two-hour provisional session — 2026-11-02

- [x] **NT-21 / second two-hour provisional session (A integrator + corpus/reviewer agents):** work 2026-11-02 18:07:20–20:07:20 CST; preserve the closed study and original splits, independently inspect conservative source-reference lines from staged Michigan1470–1520, freeze a separate provisional expansion and matched old/expanded-data experiment if source/crop QA and technical control pass, finish demonstrated trainer-lifecycle fixes, retain all failures/resource/reload evidence and report the result. No specialist acceptance, fresh blind result or automatic model promotion is inferred.

Authorized18:07:20–20:07:20 CST window complete; actual closure 2026-11-02 20:07:32 CST. [Results](docs/evaluation/p46-expanded-training-study.md):44 provisional training lines,10 registered accuracy fits/53,471updates, failed final-CER screen81.28→80.82%,493 passing regressions and851 exact saved-string replays. [Maintainer packet](docs/handoffs/p46-evening-maintainer-review.md) retains three demonstrated engineering corrections. Longer lifecycle and five-route resource probes preserve the unresolved RSS-growth finding; specialist-dependent NT-15/17/18 and resource NT-20 stay open. No model promotion, commit, submission or release.

## Now — doable without external inputs
- [x] **Codec resize/merge parity:** `fail`/`new`/`union` label-set resize with explicit NFD normalization and 4 xUnit tests (`test_merging.py`).
- [x] **RTL recognition records:** bbox/baseline geometry, region identity, logical/display order, slicing/cuts and 6 xUnit tests; full BiDi and RTL recognition remain blocked.
- [x] **Forced-alignment baseline-OCR records:** baseline/boundary, per-grapheme cuts, confidence, region identity and JSON round-trip tests.
- [x] **Tokenizer/script coverage:** unknown-grapheme reporting, bounded script detection and mixed-script routing checks + 4 xUnit tests.
- [x] **XML edge matrix:** duplicate-ID rejection, Rect/line geometry fallback, dimension/unit/source validation, reading-order groups and round-trip tests; official XSD validation remains dependent on pinned schemas.
- [x] **DATA-01 ledger validation:** opt-in extended audit for object IDs, parent-image lineage, granularity, language/period/material, transcription profile, evidence URL, unresolved questions and code/weight licences.
- [x] **PDF reading-order/column inference mechanics:** clean two-column gutter ordering with single-column fallback and synthetic tests; no real-document accuracy claim.
- [x] **Chained `segment → ocr` CLI convenience:** `image segment-ocr` forwards to the existing segment/OCR path; process test added.
- [x] **Publication docs:** image commands, model cards, AI provider configuration, PDF limits and known failures added to `docs/operations.md`; verified fresh-offline-with-installed-models remains blocked until weights exist.
- [x] **Session/presence UI:** the surface view creates/heartbeats/revokes its session and presence uses the authenticated principal; a session panel lists active sessions and revokes one, with a spec.
- [x] **Merged writes beyond text:** annotation writes now carry the revision they were drawn against (stale anchors fail), review decisions accept an optional `revisionId` (stale decisions fail 409), and geometry/order/retraction writes use the optimistic project version so a stale writer fails rather than silently overwriting; concurrent-writer tests cover geometry and retraction. No field-level merge is claimed for these routes.
- [x] **Object/surface/fragment provenance crosswalk:** export and re-import stable native ids and external references for objects, surfaces and fragments (line ids and `idno` already round-trip). *(Surface/per-line/fragment crosswalk CSVs include native ids, source ids, refs, revision and image hash; object/surface refs round-trip through TEI; native fragments persist external refs/metadata and surface membership with API create/list. A read-only `project audit` reports duplicate/dangling/out-of-range state. Source importers do not infer fragment boundaries.)*
- [x] **TEI schema pinning/validation:** pin and validate against the chosen TEI P5/EpiDoc schema (XSD or RelaxNG) without weakening the bounded profile; keep DOCTYPE/entity refusal. Extend the profile to the remaining EpiDoc constructs only where a round-trip test exists. *(Bundled `docs/contracts/tei-bounded-profile.xsd` pins/validates the bounded profile with a representative export test; SHA-256 pinning and DOCTYPE refusal are tested; `<space>`, `<damage>`, `<ex>` and `<certainty>` are bounded. Official EpiDoc interoperability remains an operator-supplied external schema pin.)*

November 5 midday verified additions; [state and evidence](docs/handoffs/project-state-2026-11-05.md) record the exact gates, source snapshots, incidents and limits. Current additions are uncommitted.

- [x] **MID-01 (integrator):** add explicit byte-verified capture relocation for restored projects, atomic provenance, dry-run and regression tests; keep accepted-record comparison gated.
- [x] **MID-02 (parallel):** reauthorize admitted project jobs and open project event streams against current membership; regress revocation and opt-out compatibility.
- [x] **MID-03 (parallel):** add opt-in worker job budgets, verify attempts/idle/shutdown semantics and measure bounded process recycling; retain native-leak investigation.
- [x] **MID-04 (parallel):** preserve ABBYY recognized character whitespace/baselines/block geometry and report omitted alternatives/metadata; record official schema limitations.
- [x] **MID-06 (parallel):** scope review summaries/adjudication to the current revision, retain historical decisions and separate correction provenance; add generated regressions.
- [x] **MID-07 (integrator):** retain later edits/drafts across delayed same-line saves and metadata refreshes; serialize saves and guard review/annotation against unsaved displayed text.
- [x] **MID-08 (parallel):** synchronize job-event publication/replay/subscription with ordered delivery and concurrency regressions.
- [x] **MID-09 (parallel):** integrate baseline-to-bbox fallback in hOCR export with precedence, direction and degenerate-geometry regressions.
- [x] **MID-10 (parallel):** refuse unsupported rotated PDF reference text explicitly before raster side effects; retain image-only import.
- [x] **MID-11 (integrator):** retain provider-session counters across registry resolution and reserve concurrent/retried spend estimates atomically with offline regressions.
- [x] **MID-12 (parallel):** preserve per-request redaction of the exact dispatched credentials across rotation/retries without global secret retention; regress text and vision failures.
- [x] **MID-13 (parallel):** reject stale region/line order snapshots across browser visits while preserving draft orders and legacy caller compatibility.
- [x] **MID-14 (integrator):** deliver persisted standalone-worker job progress/terminal snapshots to SSE clients, preserve reauthorization and validate replay cursors.
- [x] **MID-15 (parallel):** prevent structural line edits from orphaning any-layer transcription/review evidence and reject unusable split/merge geometry.
- [x] **MID-16 (parallel):** replace whole-user-table scalar authorization checks with indexed lookups, preserving behavior and measuring scale with generated accounts.
- [x] **MID-17 (parallel):** bind authenticated direct edit/review/acceptance attribution to stable account IDs, preserving local labels and imported provenance.
- [x] **MID-18 (A + B):** connect workspace AI requests/acceptance to native persisted projects, authoritative issued records and atomic L2 provenance; close missing route/JSON shape and cross-project authorization gaps with generated end-to-end tests. [Parallel brief](docs/tasks/native-ai-workspace-2026-11-05.md).
- [x] **MID-19 (integrator):** provide a reproducible non-fitting engineering verification command, safe result receipts and README/CI readiness guidance.
- [x] **MID-20 (integrator):** expose unreconstructable native L1/L2 differences as explicit TEI layer loss, with controls for exact linked alternatives/supplied spans.
- [x] **MID-21 (integrator):** enforce the reviewer role for equivalent trailing-slash review routes, with denial-before-mutation regressions.
- [x] **MID-22 (integrator):** include operational/evidence docs and bounded TEI schema in the package, repair root known-issues links and verify the actual archive.
- [x] **MID-23a (engineering + independent review):** replace the AI strict-PNG ImageSharp path with existing permissive Skia assets, preserving encoded bytes, hashes, bounds and decoding validation; retain native-RID and measured performance limits.
- [x] **MID-23b (engineering):** clear the frontend dependency advisories with compatible patches and scoped overrides, and verify the actual Angular consumers, tests and production build.

- [x] **MID-05 (integrator):** independently review changes, run non-fitting regression checks, verify disposable operations and archive only demonstrated completions before 13:00 CST.

## Review corrections — 2026-11-03
- [x] **REV-01 (interchange instance):** namespace imported TEI identities and remap references, preserve nested annotation text, inline whitespace and milestone positions, and correct L1-to-L2 span mapping. Reproduce all five defects in regression tests and verify ordinary import/export paths. *(Completed in `9ff4a94`; regression evidence in [review fixes](docs/handoffs/review-fixes-2026-11-03.md#dispositions).)*
- [x] **REV-02 (concurrency instance):** protect migrated persisted projects with version checks, detect concurrent first transcriptions and validate merge bases against line/layer/history. Retain failing baseline and passing candidate regression evidence. *(Completed in `9ff4a94`; migration/merge regression evidence in the same handoff.)*
- [x] **REV-03 (root):** confine registry destinations, validate every redirect before a request, support format-2 catalog entries and add regression tests through the registry/API paths. *(Completed in `9ff4a94`; API/CLI/application registry tests pass.)*
- [x] **REV-04 (root + independent reviewer):** review the combined fixes, run affected suites and the zero-warning solution build, document exact results and remaining limits, then archive completed review tasks. *(Root rerun: Release build 0/0; Application 201, Formats 87, Infrastructure 29, API 64, CLI 33, Worker 20 = 434/434; Angular 51/51. [Integration verification](docs/handoffs/review-integration-2026-11-03.md).)*


## Review corrections — 2026-11-04

- [x] **NOV4-01 (root):** prevent raw server-file disclosure through image bundles, enforce the shared-server filesystem authorization boundary and retain valid image exports, with regression tests.
- [x] **NOV4-02 (UI instance):** preserve advisory proposal revision/range and render only annotations belonging to the displayed revision/layer, with stale/current and L1/L2 controls.
- [x] **NOV4-03 (edition instance + root):** retain advisory payloads in edition HTML and make local capture images portable in standalone/API editions without breaking bundled relative links.
- [x] **NOV4-04 (CLI/script instance):** fix optional project HTML arguments and make worker-probe readiness identify the owned API before mutations, with regression evidence.
- [x] **NOV4-05 (root + peer reviewers):** review the combined patch, run affected suites and a zero-warning Release build, record limits and before/after evidence, then archive completed checklist items.
- [x] **NOV4-06 (CLI/script instance):** fix editor state and asynchronous responses when switching lines, and use the L2 revision chain when accepting alternatives; retain baseline failures and full Angular regression/build evidence.

## VLM benchmark through LM Studio — 2026-11-04

- [x] **VLM-01 (A + B; root integrates):** agree the VLM-BENCH-1 input/result manifest, exact ownership, source hashes and independent reviewer/custodian roles; preserve the existing uncommitted review fixes.
- [x] **VLM-02 (A; independent of Greek review):** prepare LM Studio transport and synthetic fixture specifications; pin the Qwen3-VL-8B and Qwen2.5-VL-7B GGUF/projector/license receipts, with Mistral Small 3.1 24B conditional on hardware. Verify installed runtime, actual image support and resource limits only in a later execution assignment.
- [x] **VLM-03 (B; parallel with VLM-02):** inventory rights, exact crop/target identities, exposure and per-track coverage; coordinate with NT-PILOT-B2 on outstanding P46 decisions and propose disjoint setup/selection/confirmation units without inventing accepted labels.
- [x] **VLM-05 (A; later implementation assignment, VLM-01/02):** implement bounded image requests and reproducible C# benchmark receipts through LM Studio; demonstrate real image consumption and regression coverage for text-only compatibility, Unicode, cancellation, source anchors and no L1 mutation.
- [x] **VLM-08 (root + A/B):** peer-review exact VLM-A1/VLM-B1 handoffs, record all remaining data/runtime/hardware gaps and final decision, and archive completed tasks without closing unrelated P46 training/specialist/resource gates.
- [x] **VLM-10 (A implementation, B/C independent review, root integration):** deliver an explicit experimental CLI transcription-JSON option with a distinct prompt version, bound format provenance, strict schema assessment and separately labeled raw-representation/decoded-text scores. Preserve default plain-text request bytes, legacy scoring, all failed/missing slots and unmodified transport receipts. Old readers must reject the new condition. Verify through mocked regression tests and replay of closed legacy runs; no further model calls or training are needed. See the [implementation plan](project-plan.md#optional-structured-cli-follow-up).
- [x] **VLM-11 (A implementation, B/C review, root integration):** support authenticated local LM Studio endpoints through an explicit `--credential-env NAME` reference using the existing bearer/redaction transport. Preserve keyless defaults, request-body hashes, failed/missing denominators and secret-free artifacts; disclose redacted output in scoring. Prove missing-token/no-send, header-only credential placement, echo redaction, 401 handling and invalid-reference rejection with mocked tests. Do not inspect real credentials or change the local service; live authenticated-server compatibility remains untested.

### Completed benchmark diagnostics

- [x] **VLM-09 synthetic portion:** measured effective image grids and vision-token counts for all nine requests, documented available runtime internals and gaps, and verified worker cleanup. Natural geometry comparisons remain open in [todo.md](todo.md#vlm-benchmark-through-lm-studio--2026-11-04). [Debug trace](docs/evaluation/vlm-09-debug-trace-2026-11-04.md) · [Input metadata](docs/evaluation/vlm-model-input-metadata-2026-11-04.md).
- [x] **Extension studies:** completed the paired schema-request comparison and runtime audit. [Extension handoff](docs/handoffs/vlm-extension-2026-11-04.md).
- [x] **Two-hour follow-up:** completed crossed and masked grounding controls, compact glyph contrasts, native reasoning and CLI format comparisons, fresh-worker repeatability, and the mechanical P46 worksheet intake check. [Two-hour handoff](docs/handoffs/vlm-two-hour-2026-11-04.md).
- [x] **Four-hour follow-up:** completed diverse blank and contrast studies, Greek script-profile triage, compact agreement analysis, and comparator readiness checks. Preserved the four original word-screen visits as unavailable after idle preflight. [Four-hour handoff](docs/handoffs/vlm-four-hour-2026-11-04.md).
- [x] **Word-screen retry:** completed all four frozen blocks consecutively, reconciled 240 requests and 80 image/profile triples, and verified that all benchmark workers were unloaded. LM Studio stayed at Debug. [Retry report](docs/evaluation/vlm-word-screen-retry-2026-11-04.md).

## Migration and rollout rehearsal — 2026-11-04

- [x] Inventory the reference checkout without changing it. Reviewed all 291 hashed inventory records against the post-import snapshot and reconciled the 22 PAGE/ALTO outcomes. [Rehearsal](docs/evaluation/reference-migration-rehearsal-2026-11-04.md).
- [x] Correct three rehearsal defects with focused regressions. Inventory leaves the store untouched, unavailable capture checksums do not imply duplicates, and restore validates the backup before creating the selected database and rejects directory components in manifest image names. [Rehearsal](docs/evaluation/reference-migration-rehearsal-2026-11-04.md).

## NTVMR source contract — 2026-11-04

- [x] **NT-02 / G1:** publish the [versioned source contract](docs/data/ntvmr-source-contract-v1.md) and [checksum inventory](docs/data/ntvmr-source-inventory-v1.json). Verified archived public response bytes for nine actual NTVMR TEI and Michigan IIIF image pairs, six metadata-only leads, and 34 preserved assets. [Source handoff](docs/handoffs/NT-PILOT-B.md).

## Resource and corpus evidence — 2026-11-04

- [x] **NT-20 native attribution:** reproduced the allocation growth in fresh processes and isolated a native TorchSharp module-lifecycle contribution with independent Darwin allocation stacks and tensor controls. [Native allocation report](docs/evaluation/nt20-native-allocation-attribution-2026-11-04.md).
- [x] **PAP-01 mechanical intake:** verified 27 image/PAGE pairs and five image-only records, replayed the prior split, audited geometry, and generated reversible source-only NFC projections for 321 lines. Specialist adjudication and image-rights clearance remain tracked separately. [Local PAGE intake](docs/data/local-greek-page-intake-2026-11-04.md).
- [x] **NT-04/06 mechanical pilot:** import all nine archived pairs, preserve original attachments, reject repeated records, replay both layer exports after restore, and preserve the P46 license URL and nonbreaking page markers. [Nine-page rehearsal](docs/evaluation/ntvmr-nine-page-rehearsal-2026-11-04.md).
- [x] **Capture rollback rehearsal:** verify the separate roles of the database snapshot, adjacent image cache, and pinned external capture files. Recover exact bytes by re-establishing recorded paths on disposable fixtures. [Runbook](docs/evaluation/migration-capture-portability-2026-11-04.md).
- [x] **Model migration documentation:** record unsupported weight formats, preserve provenance, and specify the path to retrain from authorized reviewed annotations. [Model migration boundary](docs/evaluation/model-migration-boundary-2026-11-04.md).
- [x] **Greek PAGE sign inventory:** audited 5,440 source lines in 285 PAGE files across P01 and the manuscript deposits. Counted script forms and Unicode separately from editorial delimiters and identified specific examples for scholarly review. [Sign inventory](docs/data/greek-page-sign-inventory-2026-11-04.md).
- [x] **NT-20 project recognition mitigation:** verify a package once per project, share the model sequentially across surfaces, and deterministically release model and tensor scope. The prior wrapper leak reproduced. All 144 generated-fixture text/confidence joins matched the baseline. [Overnight handoff](docs/handoffs/overnight-todo-2026-11-05.md).
- [x] **Evaluator acceptance-sheet draft:** record candidate per-material gates, custody fields and replayable offline checks. The evaluator still owns threshold adoption and release sign-off. [Acceptance sheet](docs/evaluation/evaluator-acceptance-sheet.md).
- [x] **Reference parity scope decision:** classify Apache Arrow datasets, PyTorch layers, Kraken VGSL, legacy Kraken weights/polygons, and Python entry points as outside the native replacement's portable scenario set. [Parity inventory](docs/quality/kraken-test-parity.md).
- [x] **Offline PAGE layout comparison:** expose vertical IoU and center-distance metrics for a checked declared frame. Record input hashes and fail on omitted nested lines, unplaced lines, or malformed geometry. [Operations](docs/operations.md#compare-page-line-geometry-offline).
- [x] **Bounded migration refinements:** import FineReader 10 ABBYY records through the existing native adapter and mark detached PAGE raster references in the ledger. [Operations](docs/operations.md#migration-inventory-read-only).
- [x] **Durable worker transitions:** repair redirected-stdin service exit, atomically claim and finalize attempts, preserve cancellation, deduplicate concurrent same-key creation, and fail an unsupported stored job without stopping the daemon. [Worker evidence](docs/evaluation/durable-worker-transitions-2026-11-04.md).
- [x] **API SQLite isolation and stream disposal:** use independent request connections, preserve shared in-memory stores per host, and cancel idle SSE subscriptions on disconnect. Correctness and latency were measured separately. [API evidence](docs/evaluation/api-sqlite-connection-isolation-2026-11-04.md).
- [x] **Bundle capture export boundary:** preserve the explicit image option across raw entries and PDF backgrounds, verify declared capture digests, and isolate concurrent response ZIPs. [Portable capture and concurrent bundle evidence](docs/evaluation/migration-capture-portability-2026-11-04.md).
- [x] **Maintainer enforcement runbook:** document trusted check sources, approval policy, DCO verification, and fork isolation. Actual host configuration remains with the owner. [GitHub enforcement](docs/governance/github-enforcement.md).
- [x] **Local release package rehearsal:** verify macOS ARM64 payload/tar identity, packaged UI/API/worker execution, and isolated install/update/rollback. Prevent hidden archive members and verification against the operator's store. [Packaging evidence](docs/release/packaging.md#overnight-macos-package-rehearsal-2026-11-04).
- [x] **Guarded line geometry edits and resize:** compare the submitted prior geometry, preserve drafts across conflicts and late replies, and constrain all four resize handles. Legacy API bodies remain accepted. [Geometry verification](docs/evaluation/geometry-stale-save-2026-11-04.md).
- [x] **Gray8 and row-stride layout:** repair single-column, gutter and feature scans on valid grayscale and padded rasters. Matched packed-color geometry and feature outputs, with measured throughput and allocation improvements on the registered fixtures. [Layout evidence](docs/evaluation/layout-gray8-stride-2026-11-04.md).
- [x] **ONNX feasibility decision:** retain native TorchSharp. Record the pinned export gap, dynamic shape and gate-order requirements, and separate parity and deployment acceptance for a future synthetic prototype. [Feasibility note](docs/evaluation/onnx-crnn-feasibility-2026-11-04.md).
- [x] **Shared job project authorization:** enforce membership on actual queued targets, bind surface jobs to the authorized project, and reject ambiguous unqualified IDs and case-based scope bypasses. The matched replay verifies denied outsider jobs and unchanged target data. [Authorization evidence](docs/evaluation/job-project-membership-2026-11-04.md).
- [x] **Overnight two-worker soak:** complete and reconcile all 68 jobs, 1,632 generated imports and original attachments over 34 waves across eight hours and fifteen minutes. The final packaged candidate separately passed 16 jobs and 768 imports. All owned processes exited zero. [Soak report](docs/evaluation/overnight-worker-soak-2026-11-05.md).

## Overnight change review — 2026-11-05

- [x] Reproduce and fix queue quota races, geometry response and order acknowledgements, and any further concrete defects from the overnight diff. Add focused regressions, review the combined changes, and archive verified corrections. [Review record](docs/handoffs/overnight-review-2026-11-05.md).

## Midday follow-up gates — 2026-11-05

November 5 midday verified additions; [state and evidence](docs/handoffs/project-state-2026-11-05.md) record the exact gates, source snapshots, incidents and limits. Current additions are uncommitted.

- [x] **MID-24 (A + integrator review):** define and implement safe shared ownership/disposal for PatchLayoutModel threshold views and actual API/worker/CLI owners, with untrained-fixture regressions; retain upstream native retention as open.
- [x] **MID-25 (B + integrator):** preserve FIFO event delivery through reentrant callbacks, subscription changes and callback faults; verify existing SSE behavior.
- [x] **MID-26 (integrator):** verify native AI in actual packaged API processes with a generated loopback provider double, including durable acceptance, authorization, caps and restart.
- [x] **MID-27 (A + independent review):** measure and remove repeated project/attachment parsing in bounded native AI bulk target resolution without changing authorization, ambiguity or transaction safety.
- [x] **MAN-01a (integrator):** verify complete M02b/c publisher archive and PNG integrity, exact PAGE joins/dimensions and byte/pixel duplication; preserve upstream rights and scholarly-review gates.
- [x] **MID-28 (R + integrator):** correct new TEI physical-line ingestion across nested/break=no milestones and verse blocks, preserving native round trips and refusing ambiguous cross-line alternatives; verify structural P46 counts without accepting labels.
- [x] **MID-29 (A + integrator):** prevent stale same-line annotation-list callbacks from resurrecting retracted alternatives or erasing newer state; retain fail-before controls and full browser checks.
- [x] **MID-30 (integrator):** finish the registered1,000-cycle generated packaged-native-AI stability check, including restart/persistence/source preservation and complete owned-process cleanup.
- [x] **MID-31 (integrator):** reject oversized Skia image headers before full pixel decode and dispose any rejected bitmap; verify invalid-payload dimension failures and valid boundary/checksum controls.
- [x] **MID-32 (B + independent review):** complete and harden the optional generic LabelCodec's scalar/mapping/merge invariants with pure non-fitting regressions; do not wire it into existing model/tokenizer packages.
- [x] **MID-33 (integrator):** package exact upstream license/notice files and a deterministic runtime NuGet inventory, with safe offline collection tests and explicit missing/native-notice review gaps.
- [x] **GOV-01a (integrator):** audit existing repository visibility/default branch/protection/rulesets/workflows read-only and record concrete enforcement gaps without changing hosting settings.
- [x] **MID-34 (B + independent review):** preserve explicit inherited XML language and declared header language metadata through new TEI native imports/exports, respecting empty reset and scope, with no reading inference or old-project migration.
- [x] **MID-35 (A + integrator):** default-skip every case in the six audited fitting classes before constructor execution unless explicitly opted in; prove Fact/Theory behavior with harmless sentinels, retain engineering exclusions and disclose the six accidental generated-fixture fits. Do not claim this guards unfiltered mixed-ML/Pilot projects.
- [x] **INS-01a (A metadata + integrator):** expand the15 existing Classical-period I01 metadata candidates into an exact item/date/edition/graphic/rights handoff; inspect bounded manifest metadata only and retain image permissions, language identification, geometry and scholarly review as open.
- [x] **TEST-01a (R static audit + integrator):** inventory remaining ML/Pilot fitting and corpus/locked-label access paths from source only, retain exact source hashes and recommend explicit independent opt-in boundaries without executing tests or reading dataset labels.
- [x] **NOTICE-01a (B metadata + integrator):** investigate exact-version SQLitePCLRaw source/license provenance using immutable maintainer metadata and package assets; preserve candidate evidence without clearing unproven binary/native catalog gaps.
- [x] **MID-36 (A + integrator):** remove unused revision-keyed native proposal publications from the singleton job replay bus, retaining durable native issue/failure behavior and actual job event contracts; prove the retained-log bug before/after with generated HTTP regressions. Do not claim this explains total observed RSS or fixes general job-history retention.

UI-01 follow-up: the task below records the starting defect. The [completed implementation](docs/evaluation/ui01-capture-delivery-2026-11-05.md) proves bounded PNG/JPEG delivery, raw-frame geometry, current authorization and actual default-/ui browser integration; TIFF/remote-frame and secondary-new-geometry boundaries remain explicit.

- [x] **UI-01 (high priority; API/frame + client/integration):** implement authorized full-capture image delivery and client URL mapping for local captures. Source inspection and a published-API generated-PNG proof confirm the serialized filesystem imageLocation becomes an origin-root URL returning HTTP404; no full-capture route exists. Preserve original bytes/checksums/native geometry; define bounded preview/EXIF/TIFF/frame and authentication/membership contracts before implementation. Unit/build/bundle serving and line-crop endpoints do not establish this workflow. Then verify the actual browser proposal/acceptance/visible-L2 flow while preserving an L1 draft; the earlier computer-use attempt was blocked by the locked Mac. [Published-API proof](docs/evaluation/local-capture-browser-delivery-gap-2026-11-05.md), [parallel API/client brief](docs/tasks/local-capture-browser-delivery-2026-11-05.md).

GEO-01 and TEST-01 follow-up: [capture creation/order](docs/evaluation/capture-geometry-2026-11-05.md) and [test/corpus boundaries](docs/evaluation/fitting-and-corpus-test-boundaries-2026-11-05.md) passed isolated, independent and full no-fitting verification. Original starting-state wording is preserved below; current behavior and limits are in those reports.

- [x] **TEST-01 (engineering):** audit remaining mixed-ML and real-corpus Pilot fitting entry points and add explicit discovery-time opt-in gates before constructors, preserving the pure codec selection and separate locked-evaluation policy. The six-class guard is verified, but unfiltered whole-solution testing can still fit other models or use local corpus artifacts. The source audit traces57 fitting-API paths and a full-snapshot loader that opens locked contents before returning partitions. Implement separate fitting/corpus/locked gates and safe partition loading from the [two-instance brief](docs/tasks/fitting-and-corpus-test-boundaries-2026-11-05.md); [source audit](docs/evaluation/remaining-fitting-test-audit-2026-11-05.md). Verify new guards with harmless sentinels and disabled discovery; no actual fitting is authorized by this task.
- [x] **GEO-01 (API/frame + client):** add explicit capture/frame selection for new regions/lines on secondary captures and safe mixed-frame ordering. [Capture geometry contract](docs/tasks/capture-geometry-2026-11-05.md) frozen before implementation. Current UI deliberately limits new-line drawing to explicitly frame-compatible primary API cases while displaying/editing compatible existing secondary-frame geometry.

GEO-02 follow-up: the [scoped edit implementation](docs/evaluation/capture-existing-geometry-2026-11-05.md) and [independent integration review](docs/evaluation/capture-existing-geometry-independent-review-2026-11-05.md) passed the current central no-fitting gate (1935 passed/18 intentional skips), with 198 frontend passes and the generated actual-browser14-check proof. Original task wording is preserved below; legacy endpoint and draft-recovery limits remain explicit.

- [x] **GEO-02 (API/frame + client):** bind existing-line geometry edits to explicit project/surface/capture, current display derivative, exact frame/source bounds and expected geometry. Preserve unsaved move/resize/placement drafts across authorization refresh; block a changed source identity until explicit reconciliation. Existing line-only PUT remains a compatibility path and is not covered by GEO-01 source-binding claims. [Completed creation/order evidence](docs/evaluation/capture-geometry-2026-11-05.md), [scoped edit brief](docs/tasks/capture-existing-geometry-2026-11-05.md).

## Overnight verified follow-up — November6,2026

Main engineering-v5 passes2024 tests with18 intentional skips, zero failures and a zero-warning/error build. Frontend363 and production build pass. The linked isolated before/after proofs retain compilation/harness negatives separately. Current additions remain uncommitted. These closures cover their stated engineering boundaries; Greek adjudication, natural recognition quality, full workspace/draft reauthorization, ImageSharp replacement and native lifetime/whole-platform release gates remain open.

- [x] **RESTORE-02 (data-loss prevention; independent evaluator + root):** validate native JSON/project identity and required current-schema query shapes before publishing a checksum-valid backup over a healthy forced-restore target. Preserve read-only validation, older-schema compatibility and duplicate-ID native rescue; do not migrate or rewrite backup payloads. Verified read-only Infra/CLI before/after cases include ten checksum-valid corrupt forced restores that previously replaced healthy DB/cache bytes; all candidate checks preserve them. Old-schema/zero-version/duplicate-ID controls pass. Main engineering-v5 passes. [Evidence](docs/evaluation/data-ownership-before-after-2026-11-06.md). This is a bounded verification gate, not detection of every logical corruption.

- [x] **DERIV-01 (coordinate/resource correctness; source owner + root):** prove/fix unchecked crop-end overflow using tiny actual public-API controls; separately verify disposal of owned transform intermediates with unchanged caller-source/final-output semantics. [Crop proposal](docs/tasks/derivative-crop-overflow-proposal-2026-11-06.md), [ownership proposal](docs/tasks/derivative-intermediate-ownership-proposal-2026-11-06.md). Integrated and verified: actual original crop13 has4 failures/9 passing controls, candidate13 passes; labeled ownership extraction has4 failures/4 controls, candidate8 passes; original public2 controls pass. Main engineering-v5 passes. [Evidence](docs/evaluation/derivative-ownership-and-crop-proof-2026-11-06.md). ownership's pure-extraction baseline must not be mislabeled as the original compiled binary. Neither closes native decoder/canvas/OOM or TorchSharp lifetime attribution.

- [x] **BUNDLE-01 (integrity correction; independent evaluator + root):** prevent first-match image/derived-export misassociation for legacy projects with duplicate surface/capture IDs. Preserve native rescue: explicit project.json/integrity/omissions/manifest only for those ambiguous projects; valid-project exports remain unchanged. Four ambiguity cases and a normal full-bundle control pass; old evidence records first-image reuse and duplicate ZIP names. Exact native rescue and main engineering-v5 pass. [Evidence](docs/evaluation/data-ownership-before-after-2026-11-06.md). No ZIP importer or automatic restore capability is implied. [Rescue contract](docs/tasks/bundle-native-rescue-2026-11-06.md).

- [x] **PDF-01 (data-loss correction; root + independent evaluator):** prevent PDF rasterization from overwriting an earlier capture when an explicit output directory and safe filename stem are reused. Preflight every requested target and publish only with exclusive creation; never delete existing files as rollback. Generated public renderer and actual-host HTTP cases demonstrate old capture overwrites and corrected refusal/preservation. Exclusive publication controls and main engineering-v5 pass. [Evidence](docs/evaluation/data-ownership-before-after-2026-11-06.md). No rollback deletion is attempted; partial newly created derivatives can remain on later failure. [Publication contract](docs/tasks/pdf-derivative-publication-2026-11-06.md).

- [x] **UI-03 (authenticated exports; root + independent evaluator):** replace bare protected adjudication/bundle browser links with authenticated HTTP downloads, retaining exact bytes and project scope. Guard originating selection/surface, credential/authority and component lifetime before download side effects; keep secrets out of URLs. Source inspection confirms bare anchors cannot use localStorage credentials. The coherent intent/export76-case baseline has69 failures/7 controls; full current frontend363 passes and production build succeeds. Actual default-UI/Kestrel Chrome proof passes16 checks, including same-response download bytes,403 refusals and late authorizedA200 after B switch. [Browser proof](docs/evaluation/authenticated-export-browser-proof-2026-11-06.md). [Contract](docs/tasks/authenticated-export-downloads-2026-11-06.md).


## Retained-parser transfer — November6

- [x] **PERF-03 (retained-parser transfer; engineering + independent evaluator):** current ImageSharp-to-Skia fallback copy uses one bounded native pixel view and exact native premultiplication, retaining existing parser/frame/checksum/pixel/budget contracts and dependency versions. Actual original/candidate36 controls pass; own-main32 ABBA Decode visits preserve all256 outputs/source identities and close all processes without caps. At6MP on this host, measured medians improve from2.654–2.739s to20.262–27.904ms and allocations from1099–1116MiB to0.221–17.178MiB. Selective main integration passes engineering-v6:2050 tests/18 intentional skips, zero build warnings/errors; frontend371 and production build pass. [Evidence and limitations](docs/evaluation/main-fallback-pixel-transfer-proposal-2026-11-06.md). Full frame buffers and ImageSharp3.1.11's six advisories remain; this is not dependency migration, a native-memory guarantee or all-platform qualification. Changes remain uncommitted.


- [x] **NATIVE-REF01 (root + independent persistence reviewer):** native Save→Load now preserves explicit unplaced line/region references through a property-only null/default converter and exact legacy `col-` rescue on three fields. Required IDs, malformed near misses and required property presence remain strict. Original61 cases:9FAIL52PASS; isolated61PASS. Independent LocalStore/read-only/reopen/backup verification/fresh-file restore14PASS versus9 original failures/5 controls, with raw source bytes unchanged. Integrated full Formats284PASS and safe engineering-v7:2125PASS/18 intentional skips/build0warnings0errors. Null browser projections preserve frame/region absence and source coordinates:23 relevant controls pass. No ID minting, database rewrite or CLI rollback claim. [Evidence](docs/evaluation/native-unplaced-reference-2026-11-06.md).
- [x] **UI-SCALAR-01 (root + independent source reviewer):** correct two invented rendering-example scalar expectations (precomposed `ἄνθρωπος`:8; `εα\u0342ντι`:6). Preserve the exact example strings and combining sequences. Five rendered-DOM controls have2 original failures/3 controls and5 corrected passes. No normalization, manuscript reading or Greek-specialist decision changes.


- [x] **AUTH-SSE-01 (transport + independent evaluator):** browser jobs streams now use a captured header credential and same-origin fetch with no key query, preserving dispatched cursor/gap replay, bounded history, authoritative terminal status, capacity, rotation and revocation. Healthy quiet bodies stay open; header admission, parsing/byte/error-retry and cleanup bounds remain. Independent tests expose and correct URL leakage, close-during-cancel hang, delayed/shared authorization fencing and callback/unread-body ownership faults. Current48 independent cases and624 whole frontend cases pass. Actual default-/ui API/browser proof passes29 functional assertions plus1118 source/runtime closure checks, with no backend outbound attempts and all owned processes gone/temp removed. Deliberate first API restart required recorded SIGKILL escalation; final host/browser exit0, not an all-graceful claim. [Implementation](docs/evaluation/header-authenticated-job-stream-2026-11-06.md), [independent actual proof](docs/evaluation/header-job-stream-independent-review-2026-11-06.md). Server query-key compatibility and other legacy URL helpers are unchanged. The current506679-byte initial bundle still exceeds its unchanged500000-byte warning budget; PERF-UI-01 stays open.


## November6 final bounded follow-ups

- [x] **DERIV-02a:** deterministic disposal across six bitmap destination initialization paths plus nested fitted scaling is integrated without changing public pixels/coordinates or fallback COPY. Independent instrumented old-control-flow extraction30 failures/8 controls becomes38 pass;29 original/extracted/repaired public outputs are byte-identical;39 permanent regressions and engineering-v9 pass. This closes the bounded implementation, not native allocation/OOM/platform/resource qualification; DERIV-02 remains open. [Independent evidence](docs/evaluation/exceptional-bitmap-ownership-independent-2026-11-06.md).
- [x] **AUTH-JOBS-RAW-01:** jobs list/snapshot/enqueue requests now pin captured credentials through delayed interceptors and refuse stale dispatch. Current read401/403 invalidates shared authority synchronously; stale failures do not. Enqueue401 invalidates authority, while a Reader's expected POST403 remains local. Five independent component regressions and10 actual-transport cases pass after reproduced failures; full639 frontend cases pass. Current API/browser38 checks plus1118 source-closure checks pass, owned processes are reaped and temporary stores removed. First controlled restart required recorded SIGKILL escalation. [Read boundary](docs/evaluation/jobs-raw-authority-boundary-2026-11-06.md), [current browser evidence](docs/evaluation/header-job-stream-current-browser-2026-11-06.md).

- [x] **TEST-LIVE-01:** engineering entry point overrides inherited `LEVIATHAN_OLLAMA_LIVE=1` to0 alongside fitting/corpus/locked capabilities. An actual-shell recording-dotnet regression reproduces the old leak (1 failure/1 control) and passes both cases after correction without a provider/model invocation. Main focused TestPolicy passes18 with17 intentional skips; Python runner tests pass7/7, including all four flags on build/test calls. Historical full engineering-v9 remains2204/18; this is a separate focused follow-up. [Scope audit](docs/evaluation/engineering-v9-scope-audit-2026-11-06.md).

- [x] **FORM-ACK-01:** successful older manual annotation/abbreviation acknowledgements now clear only fields whose raw current values still equal their captured submitted values. Real DOM/recording-subject regressions reproduce2 data-loss failures with2 ordinary-clear controls; identical4 cases pass after the exact bounded patch. Full643 frontend tests and production build pass (507074 initial bytes, unchanged500000 warning). Fresh same38 API/browser checks pass against final assets with1119 unchanged inputs. This is value-equality preservation, not form-generation/ABA identity, uncertain-write locking, receipts, replay or atomic expansion. [Before/after evidence](docs/evaluation/manual-form-ack-preservation-2026-11-06.md), [final browser replay](docs/evaluation/header-job-stream-current-browser-2026-11-06.md).


## November6 uncommitted-change review

- [x] **REVIEW-NOV06:** parallel review corrected late/revoked AI authorization and transactional credential/policy guards; ambiguous geometry/review targets and invalid ancestry; checksum-valid null-container forced restores; recognition ambiguity preflight; unknown geometry replay/read-discard; manual editor/proposal interleaving and partial workflow recovery; divergent/unknown L2 shortcut overwrites and first-write races; and stale orientation field acknowledgements. Regression receipts distinguish actual writes/overwrites from sentinel-only preflight and scripted callback cases. Final audited C# gate2288 passed/18 intentional skips, build0 warnings/errors; whole frontend680 passed; Python27 passed, npm consumer controls and production AOT build passed. Initial assets517251B retain the unchanged500000B warning budget. Broader recovery/receipt, native integrity, dependency/platform and scholarly gates remain open. No training, real providers, commits or pushes. [Full review and limitations](docs/handoffs/uncommitted-review-2026-11-06.md).
