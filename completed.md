# Completed work archive

Items checked off in `todo.md`, moved here at the end of each work cycle. `todo.md` keeps
open and in-progress work plus reference records (contracts, source register, decisions,
estimates); completed checklist items live here. Rule recorded in `lessons.md`.

Archive build: 2026-10-19 (archive run from `85488f5`).
`9810e57`.
Entries are verbatim, including commit references and links. 49 items.


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
- [x] **PAP-01 executed** (commit `3bc2131`): P01 v0.9 acquired from Zenodo — **59/59 files md5-verified**; audited **27 pairs / 321 real diplomatic TextLines / 14,394 chars**; 5 unannotated images recorded image-only; DATA-01 ledger at [data/manifests/papyri/p01.json](data/manifests/papyri/p01.json); execution record in [docs/data/papyri.md §10](docs/data/papyri.md). Real-image smoke (869×539 decode + real line crop) passing.
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
