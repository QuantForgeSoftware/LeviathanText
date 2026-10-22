# LeviathanText — actionable work list

Open and in-progress checklist items, grouped by the plan sections/phases. Plan narrative, requirements, source register, architecture decisions, gates and estimates live in [project-plan.md](project-plan.md); completed items are archived in [completed.md](completed.md).

Markers: `[ ]` open, `[~]` in progress; completed items are archived at cycle end.

## 3. Reference capability inventory and release scope

### Greek transcription contract — required before labeling data

- [ ] Define versioned transcription profiles with a Greek specialist. Cover case, lunate/final/medial sigma, historical letters, ligatures, punctuation, numerals, abbreviations, nomina sacra, overbars, supralinear letters and nonalphabetic signs. Record visible glyph forms separately when plain Unicode text cannot express the distinction.
- [ ] Preserve scriptio continua where present. Word boundaries, expanded abbreviations and added diacritics belong to derived readings, with reversible links to the source spans. Do not infer missing accents from the fact that the language is Greek.
- [ ] Support polytonic accents, smooth/rough breathings, diaeresis, iota subscript/adscript and combining sequences wherever present, including later manuscript hands and editorial readings. Preserve their presence and absence.
- [ ] Represent unclear traces, alternative readings, lost versus illegible text, known/estimated/unknown gap extent, erasures, additions, deletions, different hands and correction sequences as annotations. Keep “not yet transcribed” distinct from “illegible.” Editorial underdots/brackets must not be confused with visible marks on the object.
- [ ] Keep diplomatic text, editorial reading, scholarly annotations, and application revision history distinct. Historical corrections in the witness are not application edits. Do not claim to identify a hand or reconstruct a correction sequence automatically without evidence.
- [ ] Preserve imported text and original model output; derive canonical NFC text for interchange/scoring with explicit offset mappings. Do not apply compatibility folding, case folding, sigma substitution, spelling modernization, or accent stripping to the diplomatic layer by default. Training label normalization is a model-versioned decision.
- [ ] Use Unicode scalar-aware processing and grapheme-aware editing/alignment; define revision-scoped scalar offsets in persisted annotations, with explicit conversion to C#/browser UTF-16 offsets and grapheme boundaries. Changing normalization must not detach annotations or diacritics from their source spans.

### Scholarly interchange

- [ ] Define a bounded TEI P5/EpiDoc profile for surfaces/image links, lines, original/regularized readings, abbreviations/expansions, unclear/gap/supplied text, additions/deletions and responsibility. Pin schema versions and specify supported import/export round trips before implementation.
- [ ] Preserve stable object, surface, fragment and line identifiers and provenance. Export readable diplomatic text plus a structured version that retains uncertainty and editorial interventions; explain any information lost in plain text or OCR-only formats.
- [ ] Preserve unsupported imported markup in the original attachment and report it; do not silently flatten scholarly distinctions or claim universal TEI compatibility.
- [~] Support local high-resolution image sets first and a bounded IIIF import workflow in the Greek workspace, preserving source/canvas identifiers, attribution and available views. Keep cached projects usable offline. *(IIIF P3 import with per-view captures/attribution and checksummed on-demand image cache; offline after fetch. Large-image tiling and TIFF remain unsupported; high-res sets work when decodable.)*

## 5. Independent model and data strategy

- [ ] Assemble independently licensed initial corpora and consented/owned project samples. Record provenance, allowed uses and redistribution terms separately for images, annotations and weights.
- [ ] Split by physical witness/object and, for generalization tests, by hand/collection before augmentation. Keep fragments, alternate lighting captures, crops and derived images of one object in the same split. Track repeated passages and editions separately when evaluating AI or language-assisted reading.
- [~] Build region/baseline detection and reading-order training modules; specify their annotations, loss functions and evaluation contracts before implementation. *(line detection: patch/row CNN trained on PAGE boxes with page-level splits, opt-in per material, architecture/package validation, dev-tuned decoding; measured M02a P 0.965/R 0.987 vs projection 0.523/0.545 and page CER 0.971→0.942. Reading-order learning and region (column) segmentation remain; P01 model not adopted).*
- [ ] Support configurable alphabets/tokenizers, unknown-character reporting and mixed-script routing. Avoid a Latin-only core assumption.
- [ ] Bundle or distribute an initial model only after its rights and measured capabilities are recorded. A working engine without usable weights is not a usable OCR release.
- [ ] Define a native model package: architecture/version, weights, tokenizer, preprocessing, directions/scripts, runtime requirements, provenance, metrics, hashes and license metadata.
- [ ] Reject executable model payloads; enforce archive size/path limits and compatibility checks.
- [ ] Treat Kraken `.mlmodel`/`.safetensors` weight compatibility as optional research, not guaranteed migration. Weight files alone do not define a compatible architecture. Never require Kraken to load models.

### Corpus acquisition and annotation deliverable

- [ ] Select at least one concrete pilot collection per track and prepare real image-to-line/transcription pairs. A published normalized text or isolated-character dataset is not automatically a line-recognition training set.
- [ ] Audit every proposed test transcription against its exact image, with independent second review and adjudication. Retain unresolved alternatives instead of forcing agreement; specify which spans receive exact scoring and which receive separate uncertainty scoring.
- [ ] Inventory uncommon signs, ligatures, marks and damage states with their counts. Expand training data based on coverage and learning curves rather than a single total-line target.
- [ ] Choose seed weights only after verifying Greek/material coverage, C# loading and distribution terms. Otherwise train the initial model in C# from authorized data; public Greek weights are not an assumed dependency.

### Intake backlog, ownership and unresolved gaps

- [ ] **DATA-01 (shared convention; integrator):** define the ledger fields: source ID/version/commit, URL/DOI, retrieval date, checksum, object/surface/hand/TM or inventory IDs, parent-image lineage, label granularity, language/period/material, transcription profile, separate image/text/annotation/code/weight license identifiers, exact evidence URL/notice, attribution, exceptions, allowed intended use and unresolved questions. Record a source as pending if its evidence is missing.
- [ ] **MAN-01 (D-M):** acquire and inventory M01/M02, count valid image/PAGE pairs and lines, reconcile count discrepancies, sample-check image provenance and abbreviation/diacritic treatment. Add M03 only with its distinct obligations retained. Propose witness/hand holdouts, not a random line split.
- [ ] **PAP-01 (D-P):** inspect P01's 27 named pairs; convert its annotations into the agreed diplomatic profile and label the five extra images as unannotated. Audit line/character counts and source-image notices before setting volume targets.
- [ ] **PAP-02 (D-P):** produce an include/exclude ledger for reusable P02/P03 images; resolve P04's source-rights mapping. Match P05 texts to authorized photographs, including a Greek-only P06 inventory, and estimate missing line-label work.
- [~] **INS-01 (D-I):** count the intersection of Greek text + photograph + usable terms in I01 and inventory I02. Create source-linked visible readings, line geometry and uncertainty annotations; preserve the published edition separately. Keep I03 as its own auxiliary benchmark. *(tooling delivered: EpiDoc text-first import as L2 reference, manual unplaced-line box placement, namespaced ids, and static review packets with crops/current text/decision CSV + review apply; ISic001151 smoke green. Actual Greek-specialist transcription/geometry labels remain required.)*
- [ ] **DATA-02 (integrator + data workers):** deduplicate across sources using object/TM/inventory IDs and image lineage. All views, recto/verso, fragments, crops, alternate editions and derived glyph sets from the same object stay in one split; audit P01/P02/P03/P04 overlap explicitly.
- [ ] **DATA-03 (Greek reviewer + workers):** adjudicate a development pilot and lock separate acceptance objects; record ambiguity and annotation completeness. Publish actual counts and annotation effort before committing to a release accuracy or date.

## 6. Delivery backlog and acceptance gates

### Phase 0 — Greek evidence, contracts and feasibility

- [~] Select pilot manuscript, papyrus and inscription collections, representative hands/styles, target development/deployment hardware and expected collection scale. *(partially: pilot candidates pinned from the license register — P01 (papyri), M01+M02a (manuscripts), I01 (inscriptions); final selection is the project owner's; hardware = macOS arm64 CPU pilot machine only)*
- [~] Deliver the transcription handbook, corpus manifest and expert-adjudicated pilot/test sets from [project-plan.md §5](project-plan.md#5-independent-model-and-data-strategy). Label the source type and unsupported conditions explicitly. *(contract set = the handbook's core; per-track audit records live; expert adjudication pending — nothing is adjudicated yet)*
- [~] In a later implementation task, prove C# autograd, CTC loss, variable-width batching, normalization/tokenization, training, optimizer-state resume, checkpoint reload, decoding and inference on real Greek lines from the selected tracks. A toy tensor example does not pass this gate. *(papyri track: proven end-to-end incl. locked split; manuscript track: trained + page-holdout scored; cross-witness M02a→M01 gap measured; inscription track has no labels yet — mechanics only proven via TEI import path)*
- [~] Compare automatic and manually supplied layout for all three tracks. Record extraction failures and preserve marks around line boundaries. Test enough real images to expose material-specific failures; a single image per track is only a smoke test. *(measured with one-to-one matching: projection P 0.403/R 0.473; learned row-logit underperforms (negative result); patch/row CNN opt-in per material reaches P 0.965/R 0.987 on M02a held-out and lowers page CER 0.971→0.942, while P01 out-of-domain degrades and the P01-trained variant is not adopted; multi-column/curved/rotated still unsupported; projection remains the default)*
- [~] Package and run the candidate C# path on the actual target machine(s), recording native dependencies, train/inference duration, peak RAM/VRAM, pilot/development-set accuracy and unsupported accelerators. Prove high-resolution image/TIFF handling and select the imaging backend. Reserve locked test sets for acceptance after model/configuration selection. *(pilot-machine measurements recorded: 0.105 s/line training, 1614 MB allocations, CPU backend; TIFF measured UNSUPPORTED by the current SkiaSharp codec — now rejected cleanly, backend selection remains open)*
- [~] Choose and prove the initial-model route per track: compatible seed weights or a minimal C# training run from authorized line data. If no route works for a track, retain it as an unresolved release blocker, not assumed model availability. *(papyri + manuscripts: C#-from-scratch route proven mechanically; inscription route: unresolved blocker — no labels exist)*
- [ ] Compare appropriate Kraken configurations externally where compatible models exist; record any missing comparator. Keep those dependencies out of LeviathanText. Establish expert/manual correction time as another baseline. *(untouched tonight: requires external comparator execution)*
- [ ] Deliver the acceptance sheet in [project-plan.md §7](project-plan.md#7-verification-plan), select runtime versions, and assign owners/budgets. Decide whether ONNX export is feasible; native TorchSharp inference remains a valid first route. *(pilot report is the template candidate; freeze awaits pilot baselines + owners)*

### Phase 1 — Greek engine, minimal training and CLI

- [~] Implement image preparation, tiling, manual/imported line annotations, baseline/layout inference, extraction, recognition and alignment, with bounded resource use. *(done: import/crop/extraction, auto layout (projection R=0.506 on real pages; learned attempt underperforms — recorded), recognition + alignment plumbing, bulk import via bounded pipeline; open: PNG/TIFF tiling and automatic layout for multi-column/curved pages; large JPEG line extraction now uses bounded scanline bands)*
- [~] Support unspaced Greek recognition, visible diacritics and signs, uncertain spans and explicit model/profile selection per material/hand. Keep model outputs separate from expert annotations. *(label set supports spaces/diacritics; profile selection is per-run; model-vs-expert separation is contractual; unspaced accuracy unmeasured at scale)*

### Phase 2 — scholarly workspace and initial AI assistance

- [~] Add ASP.NET Core API and Angular collection/object/surface/job workflows, local capture import and bounded IIIF import. *(API: projects/imports/layout/transcription/exports/search/QA/models/jobs/review/auth; Angular: collections, surface view/editor, geometry, review, search, jobs, exports. Remaining: concurrent workspaces/presence; project/surface/line deep links and previous/next surface navigation are live.)*
- [~] Implement Greek input, geometry/order correction, diplomatic/editorial views, source-linked alternatives, lacuna/uncertainty markup, abbreviations and historical correction layers. *(done: polytonic editor, geometry drag/resize/draw/delete/split/merge + region/line ordering with undo and a server-side geometry audit, dual-pane L1/L2 diff, all-kind annotations with retraction, alternatives accepted into L2, mark rendering, L0 confidence view; pending: abbreviation expansion editor; correction sequences/different-hand annotations now capture a hand/sequence label, but replay/ordering UI remains)*
- [~] Add local and hosted AI adapters with user-selected models/prompts, credentials, capability discovery, retries and resource/spend settings. *(B: provider schema/registry, scripted double, local Ollama transport smoke; workspace proposal panel. Hosted calls unproven; spend/resource settings not surfaced.)*
- [~] Deliver candidate readings and optional word division, expansion, accentuation and restoration proposals, with opt-in acceptance and persistent editorial labels. Support evidence inspection and alternative readings in the editor. *(propose/accept → L2 with provenance, the editor alternative-reading flow and a source-capture link are live; task-specific source-excerpt inspection and full task coverage remain.)*
- [~] Validate keyboard input, grapheme selection, mark rendering, undo, crash recovery and source-coordinate links. Support safe local hosting; shared-server authentication/authorization must be complete before shared deployment. *(validated: Cmd/Ctrl+S/Z, scalar↔UTF-16 conversions, rendering, undo, drafts, coordinate links. Shared server now has local accounts, roles, hashed keys, user management, auth audit and opt-in per-project membership; the workspace API-key panel is live. Remaining before shared deployment: SSO/session policy, invitation/presence UI and broader shared-workspace operations.)*

### Phase 3 — expanded Greek training and generalization

- [~] Expand C# training into a complete experiment workflow: dataset versions, comparable runs, learning curves, error analysis and reproducible reports. *(run registry + per-line `evaluate lines` error drilldown with CER/base/mark/confusion CSV/JSON, a Markdown report artifact, caller-supplied acceptance checks and recorded evaluation runs; missing: the specialist-owned frozen threshold sheet itself.)*
- [~] Deliver trainable segmentation and hierarchical reading order, class/taxonomy changes and recovery. Keep region-based alternatives available where line extraction is unsuitable. *(line-band segmentation delivered as an opt-in learned detector; hierarchical reading order, column/region segmentation and recovery remain)*
- [ ] Expand recognition to new hands, fragment conditions and inscription styles using additional real data; evaluate unseen-object and unseen-hand generalization separately.
- [~] Add annotation prioritization/active learning and optional advanced pretraining; quantify regression on previously supported profiles when models change. *(persisted confidence review queue and double-review dashboard exist; `runs regress` provides a run-to-run metric gate, but no automated prioritization model or per-profile regression suite.)*
- [~] Add authenticated shared workspaces, concurrent edit handling, model catalog adapters and collaboration workflows if required for the deployment. *(accounts/roles/hashed keys/auth audit + workspace key panel + opt-in per-project membership + admin panels + store-backed presence + hashed one-time project invitation tokens with UI done; distributed session registry/concurrent-edit merge and broader collaboration workflows remain. Revision conflicts are detected and surfaced with a reload/rebase action.)*

### Phase 4 — wider replacement scope

- [ ] Add printed Greek editions/critical apparatus layouts and other scripts according to user priorities.
- [~] Add explicit RTL/BiDi/vertical direction and script metadata per region/line, and use it in editing/display/interchange without guessing from language. *(native fields + PAGE readingDirection/primaryScript + hOCR dir/ocr_vertical + ALTO BASEDIRECTION + bounded TEI rend round-trip; API orientation endpoints; editor/display support. Remaining: full BiDi layout and vertical recognition.)*
- [~] Add ABBYY XML and PDF/document ingestion needed for Kraken parity. *(PDF page rasterization + optional coordinate-bearing PDF text as L2 reference with gap-based line segmentation are implemented; PDF reading-order/column inference and ABBYY XML remain.)*
- [ ] Extend AI to translation, structured extraction, optional lexical/morphological annotations and bulk workflows with source references.
- [ ] Reconcile every reference capability row against acceptance evidence before claiming full parity.
- [ ] Consider searchable PDF, distributed execution, automatic fragment joining and advanced imaging as separately estimated extensions.

### Release checklist — applies before every usable release

- [~] Package CLI, server, workers, Angular assets and required model artifacts for the supported target matrix; verify operation without Kraken or Python. *(osx-arm64 framework-dependent package + manifest + CLI smoke verified; web assets served at /ui when WebRoot is set. Other targets untested; model weights are intentionally not bundled.)*
- [~] Complete throughput/resource profiling, backup/restore, schema migrations, install/update/rollback checks and dependency inventory. *(backup/restore rehearsal PASS; schema v1–v7 migration tests and an old-binary migration/rollback rehearsal PASS; dependency inventory includes PDFium; throughput: single-page startup/import/layout/export/backup profile recorded, sustained collection load remains; installers/updaters and cross-platform rollback remain.)*
- [ ] Publish supported materials/hands/captures, annotation conventions, model cards, benchmark results and known failure conditions.
- [ ] Publish installation, transcription, training, provider-configuration and migration instructions; verify a fresh offline installation with installed models.

## 8. Migration and rollout

- [ ] Inventory representative existing images, annotations, exports, model metadata and scripts without modifying the reference checkout.
- [ ] Import documents through public formats into a new project, keeping original files and reporting unsupported metadata. Preserve scholarly layers from TEI/EpiDoc imports; existing OCR exports may lack them and must not be presented as complete witness records.
- [ ] Run both products independently on an authorized sample; compare text, geometry, reading order, throughput and correction effort.
- [ ] Let users review discrepancies and choose models/configuration before a collection cutover.
- [ ] Keep exports and project backups sufficient to roll back a migration. Replacing the product does not require modifying or deleting Kraken.
- [ ] Document unsupported proprietary checkpoints or architecture-specific weights and offer retraining from authorized annotations.

## 10. Sources and planning review

### Licensing and contribution policy — 2026-10-19

- [~] Configure required maintainer review, trusted status checks, DCO validation and protected release/default branches on the hosting service. *(git remote `origin` is configured and `main` is pushed; branch protection, required checks, DCO app and release environments are not configured on the host yet — local policy files alone do not enable them)*

### Execution readiness

- [ ] Execution readiness: select real collections and hardware, secure usable image/label pairs and reviewers, prove the C# pipeline, and freeze measured acceptance thresholds.

## 12. Kraken test parity — 2026-10-21

Full 305-test inventory and status: [docs/quality/kraken-test-parity.md](docs/quality/kraken-test-parity.md). Scenarios are reimplemented in C#/xUnit; Python/Torch/Kraken internals are explicitly marked non-applicable rather than claimed.

- [~] Maintain the 305-test parity matrix and map each test to a Leviathan test or capability item.
- [~] Reading order: region containment, over/under and touching lines, LTR/RTL horizontal order, real reversed rows and partial-order topological sort (`test_readingorder.py`, 15 scenarios). *(portable helper + 9 xUnit scenarios; integration into multi-column layout remains.)*
- [~] Geometry: baseline ↔ bbox conversion, boundary fallback, text-direction/topline placement, degenerate boxes and input immutability (`test_container.py`, 16 scenarios). *(helper + 5 xUnit scenarios; adapters/UI conversion remains.)*
- [~] Segmentation metrics: empty truth/prediction semantics, extra-prediction precision penalty, tolerance monotonicity and aggregate (`test_segmentation_metrics.py`, 5 scenarios). *(DetectionMetrics helper + 3 xUnit scenarios; metric selection/integration remains.)*
- [~] XML/format edges: PAGE/ALTO auto-detection, zero-dimension fallback, baseline direction, malformed documents and round trips (`test_xml.py`, 32 scenarios; overlapping `test_serialization.py`). *(legacy auto-detection, malformed docs, PAGE/ALTO language/tag/split inheritance, ALTO LANG/BASEDIRECTION/BASELINE export, PAGE explicit ReadingOrder export and group preservation (total vs partial unordered), declared page dimensions with image fallback and invalid-dimension rejection; remaining: full XML metadata/schema edge matrix.)*
- [~] ABBYY XML serializer/deserializer with a bounded profile and loss reporting (`test_serialization.py`). *(bounded FineReader 10 export/import, CLI/API/bundle + 3 xUnit scenarios; schema validation and richer metadata remain.)*
- [~] Label codec strictness: encode/decode, unknown labels, strict vs non-strict behavior and add-simple (`test_codec.py`, 11 scenarios). *(general LabelCodec + 7 xUnit scenarios; model integration remains optional.)*
- [~] Model loading: missing/invalid packages, version compatibility, task filters and safe weight round trips (`test_loaders.py`/`test_models.py`, 21 scenarios). *(task/package-format fields, task-filtered `LoadAll`, malformed/missing-package rejection + 5 xUnit scenarios; legacy/format migration and multi-model containers remain.)*
- [~] CLI contracts: output formats, segmentation modes, missing-model failures and XML input line types (`test_cli.py`, 11 scenarios). *(image-only `image binarize`, `image segment` and `image ocr` with JSON/PAGE/ALTO/text output, line/segment modes, direction flag, PAGE/ALTO XML input and auto/baselines/bbox line types, source-overwrite guard + 10 CLI process tests; a single chained segment→ocr command remains.)*
- [~] Training recovery: checkpoint-on-failure, resume and interruption behavior (`test_train_utils.py`/`test_ketos_training.py`, 5 scenarios). *(separate reloadable `.failure` checkpoint on exception/cancellation, original error preserved, resume already covered + 2 xUnit scenarios; segmentation/reading-order training smokes remain.)*
- [ ] Dataset/baseline operations: filters, merges, input transform validation and baseline set behavior (`test_dataset.py`/`test_merging.py`, 17 scenarios).
- [~] Imaging behavior: binarization/BW handling, page segmentation and dewarp failures (`test_binarization.py`/`test_pageseg.py`/`test_lineest.py`, 8 scenarios). *(Otsu/BW utility + 3 xUnit scenarios; bi-level page-segmentation contract + 3 tests; bounded line dewarper (grayscale/BW, color rejection, drift reduction) + 5 tests. Full center-normalizer parity remains.)*
- [ ] RTL/BiDi and recognition records: bbox/baseline records, logical/display ordering and RTL predictions (`test_rpred.py`/`test_tasks.py`, 71 scenarios; blocked until recognition/direction semantics are implemented).
- [ ] Forced alignment task: region assignment, record count and baseline OCR (`test_align.py`, 9 scenarios).
- [ ] Remote model registry/catalog behavior (`test_repo.py`, 3 scenarios).
- [ ] Not applicable by design: Apache Arrow dataset backend, PyTorch layers, Kraken VGSL, legacy Kraken weights/polygons and Python entry points (`test_arrow_dataset.py`, `test_layers.py`, `test_vgsl.py`, `test_newpolygons.py`, `test_plugins.py`).
