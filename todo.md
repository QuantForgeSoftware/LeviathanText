# LeviathanText — independent Kraken replacement plan

Planning date: 2026-10-18. Reviewed and revised: 2026-10-18. Status: design only; implementation has not started.

**Review verdict:** the original document was an architecture outline, not a complete implementation plan. This revision makes Koine and Classical Greek manuscripts, papyri, and inscriptions the primary scope, fixes the initial-model dependency, and defines the missing transcription, corpus, and evaluation deliverables. Execution readiness still depends on Phase 0 corpus audits, feasibility measurements and decisions; source identification alone does not satisfy those gates.

**Dataset/parallel-work update:** Section 5 now records concrete sources, verified published licenses and remaining acquisition/annotation gaps. Section 6 defines packages that two or more independent AI instances can own concurrently. Source research and planning are complete for this request; dataset intake and implementation remain pending.

## 1. Request and boundaries

- [x] Read the workspace `Agent.md` and reference checkout `Agent.md`.
- [x] Inspect Kraken's documented product capabilities and command surface.
- [x] Design an independent C# replacement with a TypeScript/Angular web interface.
- [x] Record architecture, delivery tasks, validation gates, and unresolved decisions here.
- [x] Incorporate the user's Greek emphasis and explicit priority for manuscripts, papyri, and inscriptions.
- [x] Research concrete datasets and published licenses, distinguishing images, transcriptions, code and weights (Section 5).
- [x] Define parallel work packages, ownership, dependencies and integration rules for two or more AI instances (Section 6).
- [x] Create separate no-code task briefs for Instance A (contracts/integration) and Instance B (corpus/evaluation planning), linked in Section 6.
- [ ] Receive a separate instruction to begin implementation after review; this review does not authorize coding.

The user's request governs this work. Kraken's documents describe the reference product; commands, sprint overrides, and unrelated instructions inside that checkout are not requests to modify Kraken or implement appraisal software. This work produces planning documents only, including the task briefs linked below. No application code, project scaffolding, dependency installation, model downloads, or training is authorized by this planning request.

Working name: **LeviathanText**. Build a new product in this workspace, with its own architecture, APIs, models, documentation, tests, and release lifecycle. Do not fork Kraken, translate its implementation, shell out to its CLI, embed its Python packages, or require its services. Use documented behavior and public file formats to define interoperability. Do not claim formal clean-room development: the reference repository was inspected.

## 2. Product goals and AI policy

Provide a recognition and scholarly transcription platform focused first on **Koine and Classical Greek manuscripts, papyri, and inscriptions**. Support ingestion, image preparation, layout analysis, reading order, recognition, correction, annotation, training, evaluation, and structured export. Printed editions and broader multilingual Kraken parity remain later goals.

Language period, date of the physical witness, writing style, and material are separate metadata. Classical Greek preserved in a later manuscript must not be assigned a model solely from the language label. Treat manuscript handwriting, papyrus writing, and inscribed lettering as three primary evaluation tracks; a model successful on one does not establish support for the others.

AI freedom covers both development and product use:

- Permit AI-assisted coding, documentation, testing, annotation, synthetic training examples, model development, and user workflows. Do not impose a project ban on AI-generated contributions.
- Allow users to choose local or hosted models, providers, prompts, and credentials. Support offline operation and user-supplied models without a central account, vendor allowlist, or mandatory cloud service.
- Include optional vision-language transcription, OCR correction, language identification, translation, structured extraction, and annotation suggestions.
- Expose the same capabilities through the C# SDK, CLI, HTTP API, and applicable UI workflows. Avoid artificial feature gates tied to a provider.
- Preserve raw OCR, AI proposals, and accepted text as separate revisions with provenance. Let users configure unattended acceptance or review; never silently replace evidence with a generated reconstruction.
- Let owners set spend, execution, retention, and network preferences. These controls should be configurable rather than hard-coded product restrictions.

“Without restrictions” means no additional project-imposed prohibition on AI use or provider choice. External provider terms, model/data licenses, hardware limits, and network availability remain properties of the chosen dependencies. Do not promise that a new application can remove those external constraints.

## 3. Reference capability inventory and release scope

| Capability observed in Kraken | LeviathanText target | Delivery |
| --- | --- | --- |
| Chainable preprocessing, segmentation, recognition; batch input | Typed, composable, resumable pipeline through CLI and SDK | First usable release |
| Baseline/region detection and reading order | Trainable layout and order modules; editable geometry/order | Basic inference first; training before parity |
| Trainable recognition, configurable networks | C# training and fine-tuning; versioned model definitions | Before parity |
| RTL, bidirectional, vertical and mixed-script text | Explicit logical text order, orientation and script per region/line | Before parity; fixtures from first release |
| Line, word and character geometry | Polygons, baselines, alignment and confidence with precision metadata | Line/grapheme first; word geometry only for an explicit segmentation layer |
| ALTO, PAGE XML, hOCR, ABBYY XML, native output | Native JSON and text; standards-based adapters | PAGE plus scholarly TEI/EpiDoc subset first; remaining adapters in later parity work |
| Public model repository and local models | Local registry, explicit downloads, hashes and model cards | Local first; remote catalogs later |
| Dataset compilation, pretraining, training, evaluation | Dataset lifecycle and repeatable C# experiments | Supervised training before parity; pretraining later |
| Ground-truth import and correction workflows | Angular annotation workspace integrated with training | Minimal annotation before first models; full editor in Greek release |
| Optional PDF and multipage image handling | Raster/image and document ingestion | High-resolution photos/TIFF first; PDF in later ingestion expansion |
| Independent extension beyond reference | Local/hosted AI assistance, revision history and provider plugins | Initial Greek assistance alongside editor; wider workflows later |

A first usable Greek release is not full Kraken parity. Broad vertical/RTL support, unrelated scripts, all legacy output formats, and printed-book optimization do not gate the Greek release. Preserve extensible contracts and track their later delivery. Within Greek, preserve the actual writing direction and allow manual ordering, including unusual inscription arrangements found in the selected corpus. Kraken's separate eScriptorium integration is context, not a dependency to reproduce or embed.

### Greek transcription contract — required before labeling data

The default output is a **diplomatic transcription**: a documented representation of the visible witness. A separate editorial reading may supply word divisions, accents, abbreviation expansions, spelling normalization, or restorations. Both remain linked to the image and their responsible human/model; an interpretation must not become a training label for visible lettering merely because it forms plausible Greek.

- [ ] Define versioned transcription profiles with a Greek specialist. Cover case, lunate/final/medial sigma, historical letters, ligatures, punctuation, numerals, abbreviations, nomina sacra, overbars, supralinear letters and nonalphabetic signs. Record visible glyph forms separately when plain Unicode text cannot express the distinction.
- [ ] Preserve scriptio continua where present. Word boundaries, expanded abbreviations and added diacritics belong to derived readings, with reversible links to the source spans. Do not infer missing accents from the fact that the language is Greek.
- [ ] Support polytonic accents, smooth/rough breathings, diaeresis, iota subscript/adscript and combining sequences wherever present, including later manuscript hands and editorial readings. Preserve their presence and absence.
- [ ] Represent unclear traces, alternative readings, lost versus illegible text, known/estimated/unknown gap extent, erasures, additions, deletions, different hands and correction sequences as annotations. Keep “not yet transcribed” distinct from “illegible.” Editorial underdots/brackets must not be confused with visible marks on the object.
- [ ] Keep diplomatic text, editorial reading, scholarly annotations, and application revision history distinct. Historical corrections in the witness are not application edits. Do not claim to identify a hand or reconstruct a correction sequence automatically without evidence.
- [ ] Preserve imported text and original model output; derive canonical NFC text for interchange/scoring with explicit offset mappings. Do not apply compatibility folding, case folding, sigma substitution, spelling modernization, or accent stripping to the diplomatic layer by default. Training label normalization is a model-versioned decision.
- [ ] Use Unicode scalar-aware processing and grapheme-aware editing/alignment; define revision-scoped scalar offsets in persisted annotations, with explicit conversion to C#/browser UTF-16 offsets and grapheme boundaries. Changing normalization must not detach annotations or diacritics from their source spans.

Unicode supports both precomposed and combining representations of polytonic Greek. Canonical equivalence is distinct from editorial normalization; the contract above is a project design choice grounded in the [Unicode Greek FAQ](https://unicode.org/faq/greek.html), [normalization specification](https://www.unicode.org/reports/tr15/) and [text segmentation specification](https://www.unicode.org/reports/tr29/).

### Material-specific workflows and scope

| Primary track | Required first-release workflow | Separate research/expansion work |
| --- | --- | --- |
| Manuscripts | Folios, recto/verso, columns, curved lines, marginalia, selected majuscule/minuscule hands, abbreviations and scribal corrections | Generalization across unrepresented hands; automatic hand attribution; overlapping palimpsest layers |
| Papyri | Fragment boundaries, irregular baselines, fibers/background noise, lacunae, partial letters, selected literary/documentary hands | Automatic fragment joining and unrestricted cursive-hand coverage |
| Inscriptions | Object/surface/face identity, high-resolution photographs, orientation, uneven lighting, worn strokes, line/glyph annotation and actual text direction | 3D reconstruction, RTI/multispectral processing and recognition on arbitrary curved surfaces |

All three tracks belong in the initial corpus and product workflow. Release automatic recognition support per documented material/hand/imaging profile only after its gate passes; manual annotation alone must not be advertised as automatic recognition. If one track remains research-only, disclose that explicitly rather than calling the overall Greek scope complete.

Keep original photographs immutable. Crop/deskew/dewarp and lighting adjustments produce traceable derivatives. Line crops must retain diacritics, overbars and interlinear additions. Preserve alternative photographs/illumination views of the same surface, but do not merge their coordinates without registration. Generative image enhancement, if offered, is a labeled aid and never substitutes for the original visual evidence.

### Scholarly interchange

- [ ] Define a bounded TEI P5/EpiDoc profile for surfaces/image links, lines, original/regularized readings, abbreviations/expansions, unclear/gap/supplied text, additions/deletions and responsibility. Pin schema versions and specify supported import/export round trips before implementation.
- [ ] Preserve stable object, surface, fragment and line identifiers and provenance. Export readable diplomatic text plus a structured version that retains uncertainty and editorial interventions; explain any information lost in plain text or OCR-only formats.
- [ ] Preserve unsupported imported markup in the original attachment and report it; do not silently flatten scholarly distinctions or claim universal TEI compatibility.
- [ ] Support local high-resolution image sets first and a bounded IIIF import workflow in the Greek workspace, preserving source/canvas identifiers, attribution and available views. Keep cached projects usable offline.

The proposed profile uses the distinctions documented by [TEI primary-source transcription](https://www.tei-c.org/release/doc/tei-p5-doc/en/html/PH.html) and [EpiDoc transcription guidelines](https://epidoc.stoa.org/gl/latest/app-alltrans.html). Image interchange should follow the [IIIF Presentation API](https://iiif.io/api/presentation/3.0/); full IIIF hosting is outside the first release.

## 4. Architecture decisions

### Application stack and deployment

- All first-party backend, command line, training orchestration, preprocessing and ML workflow code is C#. The optional browser application is TypeScript with Angular. No first-party Python service, conversion script, or training pipeline.
- Select a supported .NET LTS and compatible supported Angular release at implementation kickoff; pin SDKs, packages and native runtimes after compatibility checks. Do not guess future version numbers in this plan.
- Begin as a modular application with an ASP.NET Core host, independently launchable C# worker, CLI, and reusable domain/application libraries. Avoid initial microservices and Kubernetes requirements.
- Support a local CLI without a web server. Offer a local web workspace and a self-hosted server deployment using the same application services.
- CPU inference is the portable baseline on Windows, Linux and macOS. Treat GPU acceleration and training support as explicit OS/architecture/backend capabilities verified in a matrix, not universal promises.
- Native ML and imaging libraries may be dependencies behind C# interfaces. “Written in C#” does not imply rewriting tensor kernels or PDF rasterizers in managed code.

### Planned module boundaries (names are design placeholders)

| Module | Responsibility |
| --- | --- |
| Core | Objects/surfaces/captures, fragments, geometry, transcription layers, scholarly annotations, revisions and model metadata |
| Application | Use cases, pipeline execution contracts, validation and permissions |
| Imaging | Decode, orientation, tiling, deskew, optional binarization and coordinate transforms |
| Layout | Regions, baselines, line extraction, reading order and script routing |
| Recognition | Tensor preparation, model execution, unsegmented grapheme sequences, decoding, confidence and alignment |
| Training | Datasets, augmentation, losses, optimizers, checkpoints and evaluation |
| Formats | Native persistence, TEI/EpiDoc profile, PAGE and later OCR-format adapters; IIIF ingestion |
| AI | Provider-neutral requests, model discovery, streaming and structured proposals |
| Infrastructure | Persistence, files, model registry, job leases and telemetry |
| CLI / API / Worker | Thin entry points using shared application services |
| Angular workspace | Collection management, annotation, correction, jobs and model experiments |

### ML runtime decision and feasibility gate

Use ONNX Runtime's C# API as the candidate portable inference adapter. Use TorchSharp as the candidate for C#-authored neural networks, training, and initial inference of newly trained models. These are candidates requiring a feasibility spike, not a claim of complete OCR architecture support.

Do not make ONNX export a prerequisite for the first training release: demonstrate a C#-trained checkpoint reload and inference through the TorchSharp adapter first. Add an ONNX export path only after a C#-accessible exporter and supported operators have been proven. Keep compatible third-party ONNX inference separate from that export question.

ONNX Runtime's documented training artifacts may require Python tooling; it is therefore not the default training plan. If a required operator, export path, or accelerator is unavailable, record the result and change the model/backend choice within C# before committing to the architecture. A Python fallback would require an explicit change to the user's language requirement.

### Pipeline and domain model

Pipeline: ingest object/surface/captures → prepare traceable image derivatives → detect or annotate layout → establish reading order → extract lines/regions → recognize visible text → align → optional editorial/AI proposals → review/accept → export a selected transcription layer.

Persist stable identifiers for collections, physical objects/documents, surfaces/sides, fragments, captures, regions, lines, grapheme spans and revisions. A book page is one surface type, not the mandatory shape of every source. Record collection/shelfmark identifiers, provenance, language period, witness date/range, material, hand/style labels and bibliographic links when known; permit unknown values.

Store source-image coordinates, polygons, baselines, reading-order edges, logical Unicode text, display direction, alternatives, confidence semantics and model/run provenance. Each annotation identifies its capture and coordinate frame. Preserve the mapping from processed images to the chosen original, including nonlinear dewarping where used. Record crops and information loss; do not assume every preprocessing operation is invertible. Multiple captures need explicit registration before geometry transfers between them. Represent unavailable character geometry explicitly rather than inventing exact cuts.

Represent words only where a source or editorial segmentation layer supplies them. Model reading order as a validated graph with a serializable ordered view; detect cycles, preserve uncertain alternatives, and permit human corrections. Empty regions, unreadable traces and unprocessed regions must have distinct statuses. The Greek transcription contract governs Unicode and editorial transformations across every adapter.

Each stage declares input/output contracts and versions. Persist stage status, model/config hashes and artifacts. Resume from valid checkpoints; invalidate dependent stages when inputs change. Use page/tile streaming and bounded queues to avoid loading an entire collection into memory.

### Storage and jobs

Use SQLite plus managed artifact directories for local installations. Plan PostgreSQL and an object-storage adapter for shared deployments without requiring both for local use. Store document metadata and revisions transactionally; keep images, tensors and checkpoints as hashed files with recorded ownership and retention.

Use durable jobs with leases, heartbeats, cancellation, retries, progress and resource limits. Assume at-least-once execution: use idempotent output publication and atomic artifact finalization. A restarted worker must not duplicate accepted revisions or overwrite completed exports. Back up metadata and files as a consistent set; test restore.

### Web workspace

Use Angular and TypeScript for an object/surface viewer with capture selection, zoom, image/geometry overlays, region/baseline editing, reading-order manipulation and synchronized Greek transcription. Include a polytonic input palette, font/combining-mark rendering checks, keyboard annotation, uncertainty/gap/abbreviation tools, historical correction layers, undo/revision history and raw-versus-proposed comparisons. Display diplomatic and editorial readings side by side with links to the original image. Permit manual line/region annotation whenever automatic layout fails. Add dataset splits, jobs, model cards and evaluation reports. Maintain coordinate accuracy while zooming and rotating; support accessibility and large-collection navigation.

Generate the TypeScript API client from the HTTP contract during implementation. Use versioned REST endpoints and server-sent events for job progress initially. Keep heavy image processing and training on C# workers; preserve a usable CLI for automation.

### AI integration

Create provider adapters for user-selected hosted endpoints and local model endpoints; support in-process local inference where technically feasible. Define capabilities rather than assuming every endpoint supports images, tools or structured output. Credentials belong in OS/server secret storage, never project exports or browser bundles.

Every proposal records source lines/regions, model identity, prompt/config version and acceptance status. Validate provider responses against schemas, keep document content separate from executable instructions, and offer dry-run previews for bulk changes. Tools receive bounded access to explicit project resources. User-configurable automation may accept proposals; irreversible document deletion remains a separate application operation.

Separate visual recognition, candidate-reading assistance and restoration tasks in prompts, outputs and evaluation. Accepted restorations remain marked as supplied text. Lexicons, morphology and parallel passages can inform optional editorial suggestions, but must not silently replace variant spellings, dialect forms, names or an unusual witness reading with a familiar text. Provide source-image excerpts and alternative readings where available; model confidence is not scholarly certainty. Evaluate local and hosted vision models on the same held-out witnesses before selecting adapters or claiming accuracy gains.

## 5. Independent model and data strategy

- [ ] Assemble independently licensed initial corpora and consented/owned project samples. Record provenance, allowed uses and redistribution terms separately for images, annotations and weights.
- [ ] Split by physical witness/object and, for generalization tests, by hand/collection before augmentation. Keep fragments, alternate lighting captures, crops and derived images of one object in the same split. Track repeated passages and editions separately when evaluating AI or language-assisted reading.
- [ ] Build an initial C# sequence recognizer with a tractable CTC architecture, without mandatory word segmentation or a dictionary. Compare NFC scalar, decomposed scalar or grapheme label strategies on real Greek lines; verify combining-mark coverage, unknown labels, time-step capacity and repeated-symbol decoding. Select the simplest measured approach; retain variable/irregular region extraction as a feasibility question.
- [ ] Build region/baseline detection and reading-order training modules; specify their annotations, loss functions and evaluation contracts before implementation.
- [ ] Support from-scratch training and fine-tuning, deterministic seeds where possible, resumable optimizer state, early stopping and best-checkpoint selection.
- [ ] Add augmentation suited to scans and handwriting; keep synthetic/AI-generated examples labeled and evaluate on held-out real documents.
- [ ] Support configurable alphabets/tokenizers, unknown-character reporting and mixed-script routing. Avoid a Latin-only core assumption.
- [ ] Bundle or distribute an initial model only after its rights and measured capabilities are recorded. A working engine without usable weights is not a usable OCR release.
- [ ] Define a native model package: architecture/version, weights, tokenizer, preprocessing, directions/scripts, runtime requirements, provenance, metrics, hashes and license metadata.
- [ ] Reject executable model payloads; enforce archive size/path limits and compatibility checks.
- [ ] Treat Kraken `.mlmodel`/`.safetensors` weight compatibility as optional research, not guaranteed migration. Weight files alone do not define a compatible architecture. Never require Kraken to load models.

### Corpus acquisition and annotation deliverable

Phase 0 must produce a versioned corpus manifest, not just a list of websites. For each of the three tracks, record actual object/surface/line counts, images available, capture quality, transcription profile, usable label counts, unresolved readings, language/writing style coverage, rights status, train/dev/test assignment and remaining annotation effort. The project owner selects representative collections; a Greek specialist owns annotation rules and adjudication; the implementation team owns manifests, splits and repeatable import. Assign these roles before the corresponding work starts; one person may hold multiple roles. Do not invent a corpus size or accuracy target before seeing the images.

- [ ] Select at least one concrete pilot collection per track and prepare real image-to-line/transcription pairs. A published normalized text or isolated-character dataset is not automatically a line-recognition training set.
- [ ] Audit every proposed test transcription against its exact image, with independent second review and adjudication. Retain unresolved alternatives instead of forcing agreement; specify which spans receive exact scoring and which receive separate uncertainty scoring.
- [ ] Inventory uncommon signs, ligatures, marks and damage states with their counts. Expand training data based on coverage and learning curves rather than a single total-line target.
- [ ] Provide a bootstrap path using imported or manually labeled lines before the complete Angular editor exists. Minimal annotation and C# training must precede any release that depends on new models.
- [ ] Choose seed weights only after verifying Greek/material coverage, C# loading and distribution terms. Otherwise train the initial model in C# from authorized data; public Greek weights are not an assumed dependency.

### Concrete source and license register — checked 2026-10-18

**Recommended starting route:** M01/M02 provide manuscript image/transcription seeds; P01 provides a small real diplomatic papyrus seed; I01/I02 supply material from which to construct an ancient inscription corpus. These are source selections for intake, not a claim that the corpus has been downloaded, fully audited or made large enough for release. This review inspected primary descriptions, license files, institutional policies and rendered Zenodo Rights fields; archive contents and trained-model quality remain untested. Counts below are publisher-reported except the P01 file-list count.

Status vocabulary: **priority intake** = useful content with a verified published open license; **construct** = open source assets still need pairing/annotation; **conditional** = useful but requires per-item selection or an unresolved rights check; **auxiliary** = a different task/material that cannot establish primary-track accuracy; **exclude default** = not part of the planned broadly reusable training distribution under its present terms. A dataset being online is not itself a reuse license.

The default bundled training-data path should prefer public-domain/CC0/CC BY material. [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) permits commercial reuse/adaptation with attribution and change notices. [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/) additionally conditions sharing adapted material on ShareAlike; it is not an NC license. Preserve these obligations at the asset level. Do not infer model-weight or application-code licensing solely from a dataset's license; resolve the intended artifact's terms separately. Restricted third-party sources do not create an AI ban in LeviathanText itself.

#### Manuscripts

| ID / concrete source | Content and fit | Published license evidence | Decision and remaining gap |
| --- | --- | --- | --- |
| **M01 EPARCHOS v1.0** — [DOI 10.5281/zenodo.4095301](https://zenodo.org/records/4095301) | 120 scanned pages of Add. 6791, c.1500–1530, two hands; PAGE XML region/line/word polygons and transcriptions. Useful for later Greek minuscule and abbreviations. | Rendered Zenodo Rights explicitly **CC BY 4.0**, attached to the dataset. | **Priority intake.** Audit image/label pairing, archive notices and treatment of abbreviations. Zenodo reports 9,285 lines while the [HTR-United catalog](https://htr-united.github.io/catalog.html) reports 2,272; count actual PAGE lines before budgeting. Later witnesses cannot stand in for early majuscule or damaged papyri. |
| **M02a Stavronikita 53 v1.0** — [DOI 10.5281/zenodo.5595669](https://zenodo.org/records/5595669) | Gospel manuscript dated 1301–1350; 54 pages, 1,038 lines. Direct Koine-text relevance through a later minuscule witness. | Rendered Zenodo Rights: **CC BY 4.0**. | **Priority intake.** Inspect the archive's transcription/geometry layout and how tachygraphic signs, nomina sacra and expanded words are labeled; keep the whole witness grouped in split design. |
| **M02b Stavronikita 79 v1.0** — [DOI 10.5281/zenodo.5578136](https://zenodo.org/records/5578136) | 16th-century manuscript; 40 pages, 803 lines; PAGE region/line/word polygons with text. | Rendered Zenodo Rights: **CC BY 4.0**. | **Priority intake.** Additional hand/style coverage; verify visible versus expanded labels and exact texts before claiming Classical/Koine genre coverage. |
| **M02c Stavronikita 114 v1.0** — [DOI 10.5281/zenodo.5578251](https://zenodo.org/records/5578251) | Late-15th-century manuscript; 44 pages, 1,051 lines; PAGE region/line/word polygons with text. | Rendered Zenodo Rights: **CC BY 4.0**. | **Priority intake.** Validate page/crop geometry and decorative initials; compare held-out witness performance rather than randomizing lines across the same book. |
| **M03 Vienna Byzantine Greek school corpus** — [repository](https://github.com/HTR-School-Vienna/2023--byzantine-greek), [license](https://github.com/HTR-School-Vienna/2023--byzantine-greek/blob/main/LICENSE.md) | Dresden SLUB A.151, 15th-century liturgical material; transcriptions of 66 images marked ground truth, with other automatic text distinguished. | Repository license **CC BY-SA 4.0**; README says SLUB images carry **Public Domain Mark**, but an earlier sentence says the digitization is not copyright-free. | **Conditional auxiliary.** Resolve the contradictory image wording against the SLUB item notice; confirm image/text alignment and retain only verified ground truth. Public Domain Mark asserts status rather than granting a license. Any Transkribus model has separate terms. |

M01–M02c provide **258 published pages across four datasets**, not four independent populations of ancient handwriting. Annotations must be reconciled with this project's diplomatic policy before training. Printed Greek sets such as [Pogretra](https://zenodo.org/records/4774201) belong to later printed-text work or labeled transfer experiments, not manuscript acceptance.

#### Papyri

| ID / concrete source | Content and fit | Published license evidence | Decision and remaining gap |
| --- | --- | --- | --- |
| **P01 Zenon Papyri HTR v0.9** — [DOI 10.5281/zenodo.6565706](https://zenodo.org/records/6565706) | Diplomatic PAGE XML prepared in Transkribus. Live record lists **32 JPGs and 27 XMLs**, with 27 matching-name pairs and five extra images. A small Hellenistic documentary papyrus seed. | Rendered Zenodo Rights explicitly **CC BY 4.0** for the dataset. | **Priority intake.** Inspect XML contents, geometry, image credits and unresolved readings; only count actual annotated pairs. Do not treat 32 photographs as 32 ground-truthed samples or the small archive as full papyrus coverage. |
| **P02 ICDAR2023 Greek Letters on Papyri, 2024 answer release** — [Zenodo 13825619](https://zenodo.org/records/13825619) | Iliad literary book hands; character boxes/classes and preservation tags. Release describes 150 training images and 34 test sides. | **Mixed image rights**, documented in `CompetitionOverview.xlsx` and JSON; institutional terms remain relevant. No uniform image license verified. | **Conditional glyph supplement.** Select a documented reusable subset. Its introduction totals 185 surfaces while release sections total 184 images; reconcile actual files/TM identifiers. Labels remove accents/editorial marks and sometimes use edition-assisted readings, so they are not automatically visible diplomatic line labels. |
| **P03 Hell-Date** — [DOI 10.5281/zenodo.15083589](https://doi.org/10.5281/zenodo.15083589), [creator's dataset page](https://d-scribes.philhist.unibas.ch/en/hell-date/dataset/) | 194 images of 157 Hellenistic papyri, 310–3 BCE; dating metadata and partial character annotations. Documentary/cursive diversity. | **Per-image mixture** of PD/CC0/CC BY, NC, institution-specific and unknown terms; creator's [rights discussion](https://d-scribes.philhist.unibas.ch/en/hell-date/dataset/dataset-summer-2024-version/) warns against assuming uniform reuse. | **Conditional subset.** Verify current release metadata and each image's rights; not a complete line dataset. Missing character boxes may mean unannotated, not background. |
| **P04 Hell-Char / PaLit-Char / Med-Char** — [repository](https://github.com/ipavlopoulos/diachronic-greek-letterforms), [explicit license](https://raw.githubusercontent.com/ipavlopoulos/diachronic-greek-letterforms/main/LICENSE) | Individual Greek letter crops across Hellenistic, Roman and medieval hands; useful for character pretraining/transfer experiments, not line recognition. | Repository explicitly declares **CC BY 4.0** for code, datasets and accompanying material. Hell-Char derives from Hell-Date, whose source rights are mixed. | **Conditional auxiliary.** Reconcile the downstream grant with source-image rights/permissions before including crops in the default pool; do not assume a proven conflict or blanket clearance. Track parent-image IDs and overlaps with P03. |
| **P05 Papyri.info IDP** — [data repository and license](https://github.com/papyri/idp.data) | DDbDP documentary/DCLP literary EpiDoc text, APIS/HGV metadata and identifiers. Text side of a future image/transcription join. | Repository data **CC BY 3.0**, attribution to respective projects. | **Construct.** This grant does not cover externally linked images. Match the exact object/image, remove editorial supplies from visual labels, and add line coordinates. |
| **P06 Turin Papyrus Online Platform** — [institutional access/reuse policy](https://collezionepapiri.museoegizio.it/en-GB/section/Papyrus-Database/Policy-on-access-and-publication-of-papyri/) | Institution-hosted papyrus images; potential source for an original Greek subset. Greek object count and paired transcription availability not yet verified. | Published policy states **CC0 images**; partner PDFs are **CC BY-NC**. Some access requires registration. | **Construct.** Inventory only the Greek subset and its asset-level notices; create diplomatic line labels. Do not transfer image rights to partner publications or share personal credentials. |

#### Inscriptions

| ID / concrete source | Content and fit | Published license evidence | Decision and remaining gap |
| --- | --- | --- | --- |
| **I01 I.Sicily / Crossreads** — [EpiDoc repository](https://github.com/ISicily/ISicily), [archive DOI 10.5281/zenodo.2556743](https://doi.org/10.5281/zenodo.2556743) | Ancient Sicilian inscriptions, EpiDoc editions and IIIF photography. The 2024 project paper reports 4,694 records across all languages, including 3,153 stone inscriptions; these are not Greek image/line-pair totals. | XML repository **CC BY 4.0**; project lead's [2024 paper, pp.74–76](https://revistas.ucm.es/index.php/GERI/article/download/95520/4564456571142) states all material, including IIIF imagery, is published **CC BY 4.0**. | **Priority construct.** Filter Greek, relevant dates and available images; pin a version-specific release/commit (the DOI follows versions); retain image credits/notices. Derive and audit visible transcriptions, polygons, baselines and damage labels. EpiDoc line breaks alone are not pixel alignment. |
| **I02 Petrie Museum Greek Inscriptions (2022)** — [DOI 10.48420/20466087](https://doi.org/10.48420/20466087), [Manchester institutional record](https://research.manchester.ac.uk/en/datasets/petrie-museum-greek-inscriptions/) | Actual photographs of objects with Greek inscriptions in UCL's Petrie Museum. | Institutional record explicitly states photographs are shared with museum permission under **CC BY 4.0**. | **Priority image seed / construct.** File count, date/material distribution and any supplied transcriptions remain unverified; no aligned line ground truth was confirmed. Inventory and annotate with an epigraphist. |
| **I03 Bessarion** — [dataset repository](https://github.com/Archaeocomputers/Bessarion), [license](https://github.com/Archaeocomputers/Bessarion/blob/master/LICENSE.md) | Images and PAGE XML for Byzantine/post-Byzantine-style Greek painted inscriptions: repository totals 122 images, 504 outlined lines, 2,776 words and 10,414 characters from 37 locations. | Repository **CC BY-SA 4.0**. | **Auxiliary with ShareAlike conditions.** Actual text-recognition subsets are smaller than total geometry counts; audit paired usable samples. Useful for imaging/adapter experiments, but cannot establish accuracy on Classical/Koine carved stone. Split by monument/site. |

No broad, already aligned and audited Classical/Koine inscription photo-to-line corpus was verified. I01/I02 make construction concrete; I03 supplies an auxiliary annotated testbed.

#### Restricted, uncertain or mismatched sources

| Source | Verified restriction or limitation | Treatment |
| --- | --- | --- |
| [Codex Sinaiticus XML](https://codexsinaiticus.org/en/project/transcription_download.aspx) | **CC BY-NC-SA 3.0** transcription terms; images have separate terms | **Exclude default.** Valuable scholarly reference or separately permitted research; obtain appropriate rights before placing it in a broadly reusable distributed corpus. |
| [GRK-Papyri](https://d-scribes.philhist.unibas.ch/en/gkr-papyri/) | Creator offers noncommercial research use; 50 images/10 scribes for writer identification, not transcribed lines | **Exclude default.** Does not fill the line-recognition gap. |
| [AL-PUB v2](https://www.kaggle.com/datasets/miswindall/al-pub-v2), [publisher entry](https://data.cs.mtsu.edu/al-pub/) | Public character crops; exact current dataset/image license not verified. AL-ALL includes unpublished material | **Hold.** Resolve actual terms before use; even a cleared release would be glyph-level auxiliary data. |
| [PHI](https://inscriptions.packhum.org/), [I.PHI processing code](https://github.com/sommerschield/iphi) | PHI site terms limit personal study/copying; Apache-2.0 processing code does not license the corpus. Text only | **Exclude default pending rights resolution.** Do not confuse text restoration/attribution resources with visual recognition pairs. |
| [IGCyr/GVCyr v0.2](https://amsacta.unibo.it/id/eprint/5698/) | Institutional EpiDoc dataset **CC BY-NC 4.0**; linked photography needs its own review | **Exclude default.** Recheck a different release independently if proposed. |
| [Cornell Eleusis photographs](https://digital.library.cornell.edu/collections/eleusis) | Collection terms state private study, scholarship and research use | **Permission-dependent lead**, not the default open pool. |
| [Pandektis metadata](https://data-admin.ekt.gr/dataset/pandektis-epigrafes) | **CC0 metadata explicitly excludes linked digital images**; item `edm:rights` controls image statements | **Discovery only** until each photograph's rights are resolved. |

### Intake backlog, ownership and unresolved gaps

The data workers D-M, D-P and D-I in Section 6 can execute these independent packages after the relevant acquisition work is authorized. License review does not require adopting source code or pretrained models.

- [x] Identify concrete repositories/DOIs and inspect published terms; record the results above.
- [ ] **DATA-01 (shared convention; integrator):** define the ledger fields: source ID/version/commit, URL/DOI, retrieval date, checksum, object/surface/hand/TM or inventory IDs, parent-image lineage, label granularity, language/period/material, transcription profile, separate image/text/annotation/code/weight license identifiers, exact evidence URL/notice, attribution, exceptions, allowed intended use and unresolved questions. Record a source as pending if its evidence is missing.
- [ ] **MAN-01 (D-M):** acquire and inventory M01/M02, count valid image/PAGE pairs and lines, reconcile count discrepancies, sample-check image provenance and abbreviation/diacritic treatment. Add M03 only with its distinct obligations retained. Propose witness/hand holdouts, not a random line split.
- [ ] **PAP-01 (D-P):** inspect P01's 27 named pairs; convert its annotations into the agreed diplomatic profile and label the five extra images as unannotated. Audit line/character counts and source-image notices before setting volume targets.
- [ ] **PAP-02 (D-P):** produce an include/exclude ledger for reusable P02/P03 images; resolve P04's source-rights mapping. Match P05 texts to authorized photographs, including a Greek-only P06 inventory, and estimate missing line-label work.
- [ ] **INS-01 (D-I):** count the intersection of Greek text + photograph + usable terms in I01 and inventory I02. Create source-linked visible readings, line geometry and uncertainty annotations; preserve the published edition separately. Keep I03 as its own auxiliary benchmark.
- [ ] **DATA-02 (integrator + data workers):** deduplicate across sources using object/TM/inventory IDs and image lineage. All views, recto/verso, fragments, crops, alternate editions and derived glyph sets from the same object stay in one split; audit P01/P02/P03/P04 overlap explicitly.
- [ ] **DATA-03 (Greek reviewer + workers):** adjudicate a development pilot and lock separate acceptance objects; record ambiguity and annotation completeness. Publish actual counts and annotation effort before committing to a release accuracy or date.

Remaining coverage gaps: early Greek majuscule manuscript hands and more independent witnesses; literary versus documentary papyrus variation beyond the small Zenon archive and Homeric glyph sets; Koine Christian papyri, difficult cursive, damaged lines, correction layers and visible diacritics; ancient carved inscriptions across sites/materials/lighting, with image-aligned diplomatic labels. Corpus size, source accessibility and license declarations do not establish recognition quality. No dataset/model archive was downloaded and no training or code was produced during this source review.

## 6. Delivery backlog and acceptance gates

The original week estimates are withdrawn: they were not based on the selected materials, annotation volume, available specialists or training hardware. Estimate calendar time and compute/storage costs after Phase 0, with named engineering and annotation owners. Keep discovery/data acquisition, labeling, model research and product engineering visible as separate work streams.

Dependency order: **transcription contract and corpus → representative C# feasibility → minimal training/models and CLI → scholarly editor plus initial AI → broader training/generalization → wider Kraken parity**. Release hardening applies to each shipped milestone. Initial AI needs stable source/revision contracts; it does not wait for all-script parity.

### Parallel execution by two or more AI instances

The phases below are acceptance milestones, not a requirement that one instance perform all tasks sequentially. Independent packages may run concurrently once their declared inputs are available. This section is a future execution plan; the current instruction authorizes research/document changes only, not implementation.

**Shared contract checkpoint C0:** before parallel implementation, one designated integrator owns the object/surface/capture IDs, coordinate frames, revision-scoped text offsets, diplomatic/editorial distinction, corpus manifest fields, split identifiers, normalization/scoring/uncertainty rules, model input/output contract, error vocabulary and HTTP event/response shapes. Publish a versioned prose specification and small representative fixtures, including gaps, combining marks and unspaced text. Numeric acceptance thresholds remain measured Phase 0 outputs. Once agreed, each worker builds against that version. A proposed contract change is a handoff to the integrator, not permission to alter another worker's files. Research and transcription-profile proposals may proceed while C0 is being prepared.

All paths in this table are **planned ownership boundaries** for eventual execution. The no-code briefs below permit only their explicitly listed Markdown outputs; implementation and data-artifact paths remain deferred. The integrator may combine packages for a two-instance team but preserves one writer per path.

| ID | Work package and exclusive write scope | Required inputs | Handoff and completion evidence |
| --- | --- | --- | --- |
| C0 | Integrator: `docs/contracts/`, `src/LeviathanText.Core/`, `src/LeviathanText.Contracts/`, their tests, solution/project setup, central dependency versions, `todo.md` | This plan; sample records supplied by data workers | Versioned contracts/fixtures; decisions and remaining blockers; baseline commit once implementation is authorized |
| D-M | Manuscript data: `docs/data/manuscripts.md`, `data/manifests/manuscripts/`, `data/annotations/manuscripts/`, artifact-store prefix `datasets/manuscripts/` | Section 5 source IDs; manifest convention from C0 before finalizing manifests | Pinned sources, asset rights, actual paired counts, label-policy audit, object/hand split proposal; no unsupported “ready” claim |
| D-P | Papyrus data: `docs/data/papyri.md`, `data/manifests/papyri/`, `data/annotations/papyri/`, artifact-store prefix `datasets/papyri/` | Same convention; papyrus source IDs | Line-versus-character inventory, permitted image subset, diplomatic-label audit, overlap groups and explicit missing annotation work |
| D-I | Inscription data: `docs/data/inscriptions.md`, `data/manifests/inscriptions/`, `data/annotations/inscriptions/`, artifact-store prefix `datasets/inscriptions/` | Same convention; inscription source IDs | Greek image/text intersections, per-image credits, missing geometry/labels and object/site holdout proposal |
| IMG | Imaging/layout: `src/LeviathanText.Imaging/`, `src/LeviathanText.Layout/`, matching tests | C0 geometry; a small authorized sample from any data package | Crop/transform contract tests, preserved marks, original-image mappings, manual-layout adapter and measured failure cases |
| ML | Recognition/training: `src/LeviathanText.Recognition/`, `src/LeviathanText.Training/`, matching tests | C0 model/label contract; approved development pairs; imported crops can precede IMG | C# train/resume/reload/infer demonstration, model package and reproducible metrics; no Python/Kraken runtime requirement |
| FMT | Formats/ingestion: `src/LeviathanText.Formats/`, matching tests | C0 annotation contract and public-format fixtures | PAGE and bounded TEI/EpiDoc import/export, explicit unsupported-field report and round-trip evidence; IIIF adapter later in same scope |
| APP | `src/LeviathanText.Application/`, `src/LeviathanText.Infrastructure/`, `src/LeviathanText.Api/`, `src/LeviathanText.Worker/`, `src/LeviathanText.Cli/`, their tests and database migrations | C0 contracts; fake recognition results initially, real adapters at integration | Durable jobs, revisions, persistence and HTTP/CLI behavior; restart/retry evidence; shared schemas changed only through C0 |
| UI | Angular workspace: `web/`, its tests and UI docs | C0 HTTP contract and representative fixture responses | Source-image/Greek editor, uncertainty/editorial layers, input/rendering evidence; operates against mocks before API availability |
| AI | Provider adapters: `src/LeviathanText.AI/`, matching tests | C0 evidence/proposal contracts; provider-independent test doubles | Source-linked proposals, local/hosted adapter evidence, malformed-response handling; credentials never included in handoff |
| EVAL | Evaluation: `src/LeviathanText.Evaluation/`, `docs/evaluation/`, evaluation tests | C0 scoring rules; adjudicated manifests; frozen candidate outputs | Reproducible per-track scoring, leakage checks, benchmark report and pass/fail against fixed thresholds |

Dataset workers publish immutable, versioned development images/crops and annotations under their assigned artifact-store prefix; ML/IMG/FMT consume them read-only and write run-specific derivatives under their own job IDs. The integrator records the configured store location and retention policy. This assigns ownership to data artifacts as well as documentation without putting large corpora in Git.

**With exactly two instances:** A is integrator and owns C0/APP; B begins D-M → D-P → D-I while A writes C0 and the transcription-contract proposal. After C0 and an initial approved development sample, A proceeds with FMT/IMG/APP and B with ML. B can use supplied line crops while IMG is pending; A can use fixed recognizer outputs while ML is pending. After the core contracts stabilize, A takes UI and B takes AI, with EVAL assigned explicitly and peer-reviewed. Merge only at the checkpoints below, not by editing each other's working files.

**With three or more instances:** run D-M, D-P and D-I independently; keep one integrator. After C0, run ML, FMT/IMG, APP, and UI/AI in parallel as slots permit. At four instances a practical allocation is integrator/APP, ML, IMG/FMT, and UI/AI; each owns its tests. EVAL may be a separate instance where available, or a bounded cross-review task after candidate outputs are frozen. Separate role assignments do not require an additional person for each role.

**Implementation session record (2026-10-19 morning, two-instance mode):** A (contracts + C0/APP/FMT/IMG/UI + integration) and B (AI/EVAL/ML). Records in both handoffs; collaboration state: clean separation verified, no conflicts.

**Two-instance mode restored (2026-10-19 06:54):** Instance A resumes as **integrator** and owns `docs/contracts/`, `docs/handoffs/A-PLAN.md`, `todo.md`, and the implementation packages **C0/APP/FMT/IMG/UI**. ML/AI/EVAL code exists from the single-instance session; A holds it until B claims it (B's natural packages: ML/AI/EVAL refinement or D-M/D-P/D-I data work — B records its claim in its own handoff; A will reflect it on this board). Ready inputs for B: contract set `C0-0.4-draft`, per-track audited manifests (`data/manifests/*`), pilot reports, and the trained-model/checkpoint conventions.

**Starting assignment:** C0 and D-M/D-P/D-I are ready for further documentation/intake planning; every implementation package is unstarted and awaits a user instruction permitting code. Data/license research is complete only to the extent recorded in Section 5; acquisition, pairing and expert annotation remain pending. Give each new instance a package brief containing its ID, allowed action scope, base revision, owned paths, contract version, dependency inputs, acceptance criteria and handoff path `docs/handoffs/<package-id>.md`. A brief must carry the no-code constraint whenever it is still in effect. This lets a fresh instance resume without relying on another chat's memory.

### Issued two-instance planning briefs

Baseline ID: **PLAN-2026-10-18-two-instance-v1**, issued 2026-10-18. These briefs are ready to assign; creating them has not started either assignment or created separate chats. Both instances can begin documentation concurrently. Each records its starting input hashes because there is no Git baseline yet.

| Package | Owner / brief | Current status | Dependency before acceptance | Handoff |
| --- | --- | --- | --- | --- |
| A-PLAN | Instance A — [contracts and planning integration](docs/tasks/instance-a-contracts-and-integration.md) | **review** (documentation complete 2026-10-18; handoff: [docs/handoffs/A-PLAN.md](docs/handoffs/A-PLAN.md); blocked on B's peer review — see execution record below) | B's material-fit review of the named C0 draft; recorded dispositions | `docs/handoffs/A-PLAN.md` |
| B-PLAN | Instance B — [corpus intake and evaluation planning](docs/tasks/instance-b-corpus-and-evaluation.md) | **done → absorbed** (B's planning docs completed by B; B-CODE increment merged into single-instance mode) | — | `docs/handoffs/B-PLAN.md`, `docs/handoffs/B-CODE.md` |

**Implementation package board (single-instance mode, night session 2026-10-18/19; all owned by Instance A):**

| ID | Scope | Status | Evidence |
| --- | --- | --- | --- |
| C0 | Contracts/Core + solution setup | **done** (increments 1–3 + C0-0.4-draft dispositions) | commits `5f6ac24`, `da56646`, `44d4a9a`, `15fa2c7` |
| FMT | Formats/ingestion | **done** (native JSON, diplomatic+loss reports, PAGE 2013/2019, TEI/EpiDoc, IIIF P3) | commit `95b7bd3`, `1a6d09c` |
| IMG | Imaging/layout adapters | **done (adapters)**; automatic layout detection unstarted | commit `1f3a4f3` |
| APP | Persistence/jobs/CLI/Worker | **done (local-first core)**; HTTP editor workflows partial | commits `b7bf85e`, `1bc025d` |
| ML | Recognition/training | **done (feasibility gate on real + synthetic data)** | commits `72690ea`, `a3e48b7`, `0534ac1` |
| EVAL | Evaluation/scoring | **pilot reports live** (EVAL-02/04 pilot form); frozen acceptance sheet pending specialists | commit `9a3efd7`, `a0c43fc` |
| AI | Provider adapters | **scaffold done** (schema validation, scripted double, acceptance→L2); real hosted/local adapters untested | commit `72690ea` |
| UI | Angular workspace | **scaffold + surface view live**; annotation editor partial | commits `108d45e`, `4903a80` |
| DATA-01..03 | Corpus ledger/manifests/splits | **ledgers committed** (p01, m01, m02a, i01); expert adjudication pending | commits `3bc2131`, `4d16e1b`, `3b75bf0` |

**Morning-session state (2026-10-19, session close 10:10):** two-instance mode operating cleanly — A (integrator/APP/FMT/IMG/UI) and B (AI/EVAL/ML) with disjoint paths verified. Session increments: transcription write endpoints + service tests (`e9684c8`, `1bbddd8`), line editor UI (`bb8d50f`), Layout package + honest negative finding (`56bac39`, `d01495c`), extraction fix for the measured CTC starvation (`0c6c768`), AI services wired into the API (`51eb654`), streaming pipeline (`fcee469`), jobs panel + dual-pane UI (`0a1f9af`, `78201e7`), API write-surface hardening from live smoke (`3314d02`), packaged-model CLI loading (`934c6f9`), handbook (`6ea4f56`). B's locked-set disclosure reconciled (multiple runs — see docs/evaluation/locked-set-reuse.md). **Suite state: 340/340 tests green across 13 suites; Release build 0 warnings/0 errors.**

A alone edits this board and the contract documents. B alone edits the three material plans and evaluation plan. Each owns its own handoff. The briefs are read-only inputs during execution. A publishes a draft manifest/transcription contract early; B publishes source cards independently. They then exchange reviews without requiring either package to be accepted first. A integrates accepted decisions; unresolved scholarly, acquisition and feasibility work stays pending. Completing these documentation packages does not complete I0 or Phase 0, or authorize code. The briefs specify acceptance criteria, exact owned paths and the evidence required in each handoff.

**A-PLAN execution record (2026-10-18, by Instance A):** the user authorized executing the A-PLAN brief. Instance A claimed ownership, recorded baseline hashes (handoff rev 1), and published contract proposal **`C0-0.1-draft`** ([corpus-manifest](docs/contracts/corpus-manifest.md), [greek-transcription](docs/contracts/greek-transcription.md)) followed by **`C0-0.2-draft`** ([domain-and-pipeline](docs/contracts/domain-and-pipeline.md), [interchange-and-api](docs/contracts/interchange-and-api.md), [examples](docs/contracts/examples.md)). All five documents are **proposals pending review**; none is accepted, and the scholarly conventions await Greek-specialist adjudication. B never started (no B handoff or B-owned documents exist), so A-PLAN status is **`review`**, not `done`, and A's review of B is pending. The complete evidence trail, per-file SHA-256 hashes, open reviewer questions and blockers (BLK-A-1…3) are in [`docs/handoffs/A-PLAN.md`](docs/handoffs/A-PLAN.md). All Phase 0/I0/implementation gates below remain unchecked; no code, downloads, installs or benchmarks were performed, and the contracts define no schemas or file formats.

**Implementation status — C0 (Instance A as integrator; user authorization to code received 2026-10-18):**

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
- [ ] B's review of the contract set (I0 gate) — moot while single-instance; substitute with a self-review of all five contract documents against all packages at the next integration checkpoint (I1).

**Ownership collision note (2026-10-18, Instance A):** a concurrent worker created `src/LeviathanText.{AI,Evaluation,Recognition,Training}` and their test projects (disjoint packages — acceptable) and also rewrote `tests/LeviathanText.Core.Tests/LeviathanText.Core.Tests.csproj` and `src/LeviathanText.Contracts/LeviathanText.Contracts.csproj` into a minimal style while introducing `Directory.Build.props`. Those are C0-owned paths (solution/project setup). The centralization itself is sound; A (integrator) adopted it, restored the missing xunit global using and fixture content in the Core.Tests project, and will keep the central files consistent. Concurrent workers must confine edits to their own packages and route central-file changes through the integrator per todo §6 rules.

### Coordination and integration rules

- [x] Before implementation, establish the repository and one branch/worktree per active package; the current workspace is not yet a Git repository. This task does not initialize it. All workers start from the same recorded baseline. If worktrees are unavailable, enforce disjoint write paths and a single integrator; never run concurrent edits to one file. *(established on `main` at `728ec9c`; single-instance mode makes worktrees moot — disjoint paths superseded by full ownership)*
- [x] Maintain a package board with ID, owner instance, status (`ready`, `active`, `blocked`, `review`, `done`), dependencies, base revision, owned paths, contract version and handoff location. Only the integrator edits the shared board/`todo.md`; other instances submit status and contract proposals to it. *(board maintained; integration checkpoints recorded in handoffs)*
- [ ] A worker claims one package before editing. Ownership persists across context resets; reassignment requires a recorded handoff. Each worker reads this plan, `Agent.md`, its package brief and current contract version. Do not infer authorization from another instance's unreviewed output.
- [ ] Workers may read shared code/contracts but change only owned paths. Shared project files, lockfiles, generated API contracts, schema migrations and central package versions have one named owner. The integrator resolves requested cross-boundary changes before dependent work resumes.
- [ ] Each handoff includes changed paths/commit, assumptions, input/data hashes, license evidence, tests or document checks performed, failure results, unresolved questions and the exact next dependency. Dataset/model artifacts are separately versioned; do not copy large corpora or secrets into Git or chat.
- [ ] A different instance reviews each handoff. The integrator merges one package at a time, runs relevant contract/integration checks, and records the accepted revision. A worker's “done” means its scoped acceptance passed, not that the whole phase passed.
- [ ] Reserve GPU, download, storage and benchmark resources explicitly to avoid contention. Keep authoritative locked test labels with the evaluator; training and prompt tuning use development sets. Parallel instances must not leak held-out labels through handoff examples.
- [ ] If a dependency fails, record the blocker and continue an independent package or use the agreed fixtures. Do not silently replace an ML/backend choice, loosen a quality threshold, adopt a restricted dataset or change a scholarly convention to unblock another instance.

Integration checkpoints: **I0** accepts C0 plus data conventions; **I1** combines native persistence, imported/manual geometry and C# recognition; **I2** combines API, Angular editor and scholarly exports; **I3** adds AI proposals and per-track acceptance reports; **I4** applies the release checklist. New data tracks can advance through I1 independently, but the combined initial scope is not complete until all three material gates pass.

### Phase 0 — Greek evidence, contracts and feasibility

- [~] Select pilot manuscript, papyrus and inscription collections, representative hands/styles, target development/deployment hardware and expected collection scale. *(partially: pilot candidates pinned from the license register — P01 (papyri), M01+M02a (manuscripts), I01 (inscriptions); final selection is the project owner's; hardware = macOS arm64 CPU pilot machine only)*
- [~] Deliver the transcription handbook, corpus manifest and expert-adjudicated pilot/test sets from Section 5. Label the source type and unsupported conditions explicitly. *(contract set = the handbook's core; per-track audit records live; expert adjudication pending — nothing is adjudicated yet)*
- [x] Define the object/surface/capture contract, annotation offsets, native format and bounded TEI/EpiDoc mapping before building adapters or datasets. *(C0-0.4-draft, implemented as compiled contracts)*
- [~] In a later implementation task, prove C# autograd, CTC loss, variable-width batching, normalization/tokenization, training, optimizer-state resume, checkpoint reload, decoding and inference on real Greek lines from the selected tracks. A toy tensor example does not pass this gate. *(papyri track: proven end-to-end incl. locked split; manuscript track: trained + page-holdout scored; cross-witness M02a→M01 gap measured; inscription track has no labels yet — mechanics only proven via TEI import path)*
- [~] Compare automatic and manually supplied layout for all three tracks. Record extraction failures and preserve marks around line boundaries. Test enough real images to expose material-specific failures; a single image per track is only a smoke test. *(imported/manual layout is the implemented path; automatic layout unstarted; per-track smoke only so far)*
- [~] Package and run the candidate C# path on the actual target machine(s), recording native dependencies, train/inference duration, peak RAM/VRAM, pilot/development-set accuracy and unsupported accelerators. Prove high-resolution image/TIFF handling and select the imaging backend. Reserve locked test sets for acceptance after model/configuration selection. *(pilot-machine measurements recorded: 0.105 s/line training, 1614 MB allocations, CPU backend; TIFF handling unverified — PNG/JPG only so far)*
- [~] Choose and prove the initial-model route per track: compatible seed weights or a minimal C# training run from authorized line data. If no route works for a track, retain it as an unresolved release blocker, not assumed model availability. *(papyri + manuscripts: C#-from-scratch route proven mechanically; inscription route: unresolved blocker — no labels exist)*
- [ ] Compare appropriate Kraken configurations externally where compatible models exist; record any missing comparator. Keep those dependencies out of LeviathanText. Establish expert/manual correction time as another baseline. *(untouched tonight: requires external comparator execution)*
- [ ] Deliver the acceptance sheet in Section 7, select runtime versions, and assign owners/budgets. Decide whether ONNX export is feasible; native TorchSharp inference remains a valid first route. *(pilot report is the template candidate; freeze awaits pilot baselines + owners)*

Gate: all three tracks have actual source data, an annotation contract and a documented model route; the C# demonstration and measured acceptance sheet support proceeding. Generic printed Greek accuracy does not satisfy this gate.

### Phase 1 — Greek engine, minimal training and CLI

- [x] Implement native object/surface/capture persistence, scholarly annotation layers and coordinate mappings. *(NativeProject + LocalStore + Annotation/TextRevision contracts + frame registration)*
- [~] Implement image preparation, tiling, manual/imported line annotations, baseline/layout inference, extraction, recognition and alignment, with bounded resource use. *(decode/crop/deskew + extraction with bounded queues contract; automatic baseline/layout inference unstarted)*
- [x] Deliver minimal C# dataset preparation, training/fine-tuning, checkpoint resume and model packaging needed to produce the initial track-specific models. The first release must not depend on Phase 3 to obtain its models. *(CtcTrainer: train/evaluate/best-checkpoint/early-stop/resume; checkpoints now complete model packages with persisted labels)*
- [~] Support unspaced Greek recognition, visible diacritics and signs, uncertain spans and explicit model/profile selection per material/hand. Keep model outputs separate from expert annotations. *(label set supports spaces/diacritics; profile selection is per-run; model-vs-expert separation is contractual; unspaced accuracy unmeasured at scale)*
- [x] Import/export native JSON and diplomatic UTF-8 text, the bounded TEI/EpiDoc profile, and a supported PAGE subset for annotation interchange; report losses in less expressive formats. *(all four formats + severity-graded loss reporting)*
- [x] Provide batch processing, cancellation, durable stage checkpoints, run manifests and actionable errors. *(RecognitionPipeline batch path + cooperative cancellation + checkpoint validity classification + closed error vocabulary)*

Gate: a fresh offline installation processes the held-out sets for all three tracks, reloads its own trained checkpoints and meets the agreed per-track pilot thresholds. Manual layout assistance is recorded and evaluated separately from fully automatic processing. Unmet tracks remain visible blockers to claiming the full initial scope.

### Phase 2 — scholarly workspace and initial AI assistance

- [~] Add ASP.NET Core API and Angular collection/object/surface/job workflows, local capture import and bounded IIIF import. *(API live with projects/import-page/exports/jobs+SSE; IIIF P3 import proven against a real manifest; Angular list + surface view live; editor workflows partial)*
- [ ] Implement Greek input, geometry/order correction, diplomatic/editorial views, source-linked alternatives, lacuna/uncertainty markup, abbreviations and historical correction layers.
- [ ] Export corrected training data using visible-text labels and explicit annotation status; do not train visual recognition on supplied restorations.
- [ ] Add local and hosted AI adapters with user-selected models/prompts, credentials, capability discovery, retries and resource/spend settings.
- [ ] Deliver candidate readings and optional word division, expansion, accentuation and restoration proposals, with opt-in acceptance and persistent editorial labels. Support evidence inspection and alternative readings in the editor.
- [ ] Validate keyboard input, grapheme selection, mark rendering, undo, crash recovery and source-coordinate links. Support safe local hosting; shared-server authentication/authorization must be complete before shared deployment.

Gate: users can transcribe, review, train from corrections and export selected examples from all three materials without losing scholarly distinctions. Initial AI demonstrates local offline and hosted operation, preserved evidence, provider switching and measured effects on correction effort/unsupported additions. Claims of improved recognition require the benchmark results, not merely a functioning adapter.

**First usable Greek release:** Phase 1 + Phase 2 + the release checklist below, with the selected material/hand/capture profiles and remaining limitations published. Broader handwritten Greek coverage is not implied by this release.

### Phase 3 — expanded Greek training and generalization

- [ ] Expand C# training into a complete experiment workflow: dataset versions, comparable runs, learning curves, error analysis and reproducible reports.
- [ ] Deliver trainable segmentation and hierarchical reading order, class/taxonomy changes and recovery. Keep region-based alternatives available where line extraction is unsuitable.
- [ ] Expand recognition to new hands, fragment conditions and inscription styles using additional real data; evaluate unseen-object and unseen-hand generalization separately.
- [ ] Add annotation prioritization/active learning and optional advanced pretraining; quantify regression on previously supported profiles when models change.
- [ ] Add authenticated shared workspaces, concurrent edit handling, model catalog adapters and collaboration workflows if required for the deployment.

Gate: users can train recognition/layout/order models in C#, reload them and evaluate independently on the per-track acceptance sets. Expanded support is demonstrated per profile; uncertainty or damage is not hidden by a pooled accuracy number.

### Phase 4 — wider replacement scope

- [ ] Add printed Greek editions/critical apparatus layouts and other scripts according to user priorities.
- [ ] Complete broader RTL/BiDi/vertical support, ALTO/hOCR/ABBYY adapters and PDF/document ingestion needed for Kraken parity.
- [ ] Extend AI to translation, structured extraction, optional lexical/morphological annotations and bulk workflows with source references.
- [ ] Reconcile every reference capability row against acceptance evidence before claiming full parity.
- [ ] Consider searchable PDF, distributed execution, automatic fragment joining and advanced imaging as separately estimated extensions.

Gate: each claimed added capability has independent acceptance evidence; none is required merely to ship the agreed initial Greek workflow.

### Release checklist — applies before every usable release

- [ ] Package CLI, server, workers, Angular assets and required model artifacts for the supported target matrix; verify operation without Kraken or Python.
- [ ] Complete throughput/resource profiling, backup/restore, schema migrations, install/update/rollback checks and dependency inventory.
- [ ] Publish supported materials/hands/captures, annotation conventions, model cards, benchmark results and known failure conditions.
- [ ] Publish installation, transcription, training, provider-configuration and migration instructions; verify a fresh offline installation with installed models.

Gate: reproducible installation and benchmark reports, tested recovery and honest per-track capability claims. A failed quality gate cannot be relabeled as successful full-scope delivery.

## 7. Verification plan

Define numeric release thresholds in Phase 0 from pilot/development baselines rather than promising unsupported accuracy or speed. The project owner and Greek reviewer set acceptable transcription/correction quality; the engineering owner records hardware/performance budgets. Freeze the acceptance sheet after the pilot and before production tuning; keep locked test sets out of training, prompt examples, threshold setting and model selection. If acceptance failures are used to guide a subsequent model, add fresh held-out witnesses and disclose the reused set's status. Report manuscript, papyrus and inscription results separately, including per-hand/capture strata where samples support them.

The acceptance sheet must contain:

| Required entry | How it will be used |
| --- | --- |
| Corpus/version, object counts, evaluated lines/signs, unresolved spans, split hashes | Show what was actually evaluated and prevent alternate-capture leakage |
| Transcription profile, Unicode/scoring version, whitespace and uncertainty rules | Make reported errors reproducible and distinguish diplomatic from editorial accuracy |
| Raw-model, language-assisted and AI-assisted results; manual versus automatic layout | Prevent an assisted workflow from being represented as standalone visual recognition |
| Numeric accuracy/coverage thresholds and correction-time target for each material | Define pass/fail separately; thresholds are pending measurements, not invented here |
| Hardware, image sizes, throughput, peak RAM/VRAM and training budget | Establish usable operating limits on the actual deployment |
| Expert-review process, named owners and evidence links | Make failures actionable and establish who adjudicates ambiguous labels |

- Recognition: primary canonical-equivalent, case-sensitive scalar CER and grapheme error rate on agreed visible-text spans; retain raw-sequence diagnostics. Normalize both prediction and truth consistently without stripping marks. Report mark-specific omissions/substitutions/insertions separately, including incorrectly supplied accents or breathings on unmarked text. WER applies only to an explicitly annotated word layer; score proposed word division separately for scriptio continua.
- Uncertainty: retain unresolved readings and annotation status; report excluded-span counts, abstention/coverage and accuracy-versus-coverage. Prevent empty output or broad uncertainty masks from producing an apparently good score. Score additions on uninscribed/blank regions and wholly missed lines as failures, not omitted samples.
- Layout: region overlap, baseline detection precision/recall, line assignment, reading order and text-region coverage; disclose matching tolerances. Include disconnected fragments, rotated inscriptions, marginal/interlinear text and crop loss of marks. Compare transcription on ground-truth layout with complete automatic-pipeline output to localize errors.
- AI: compare raw recognition, candidate readings and accepted editorial output separately; measure unsupported restorations, overwritten witness variants and expert correction time. Include unfamiliar passages, variant readings and damaged/blank areas so reproducing a memorized standard edition does not count as visual recognition. Model score calibration and scholarly certainty remain separate.
- Training: train/reload/infer parity, reproducibility tolerance, interrupted checkpoint recovery, split leakage detection and real-data generalization.
- Formats: schema validation using pinned schema versions, independent fixtures and supported-field round trips, especially gap/unclear/supplied, abbreviation/expansion, hands and correction layers; external XML entities disabled.
- UI: coordinate-overlay accuracy across captures/transforms, polytonic input, grapheme-safe selection, combining-mark rendering, keyboard accessibility, undo and concurrent revision conflicts where supported.
- Operations: pages/minute, startup latency, peak RAM/VRAM, large pages, bounded queues, cancellation, retry and worker-crash recovery.
- Security/data handling: file-size limits, malformed documents, path traversal, model archive handling, secret redaction, project access and optional network isolation.
- Independence: release dependency/file inventory proves no Kraken package, copied implementation, subprocess or Python bootstrap is required.

During implementation, choose focused tests that exercise behavior and failures; run broader regression checks at integration/release gates. For this planning-only task, verification consists of reviewing this document and the changed-file set; no build or runtime claim is made.

Acceptance invariants that can be specified now: original assets/revisions remain retrievable; unsupported scholarly fields are reported; all supported round-trip fixtures preserve semantics; canonical equivalents compare equally while distinct visible readings remain distinct; editorial expansions/restorations cannot silently enter diplomatic training labels. Numeric performance/accuracy targets remain an explicit Phase 0 deliverable.

## 8. Migration and rollout

- [ ] Inventory representative existing images, annotations, exports, model metadata and scripts without modifying the reference checkout.
- [ ] Import documents through public formats into a new project, keeping original files and reporting unsupported metadata. Preserve scholarly layers from TEI/EpiDoc imports; existing OCR exports may lack them and must not be presented as complete witness records.
- [ ] Run both products independently on an authorized sample; compare text, geometry, reading order, throughput and correction effort.
- [ ] Let users review discrepancies and choose models/configuration before a collection cutover.
- [ ] Keep exports and project backups sufficient to roll back a migration. Replacing the product does not require modifying or deleting Kraken.
- [ ] Document unsupported proprietary checkpoints or architecture-specific weights and offer retraining from authorized annotations.

## 9. Decisions and risks to resolve

| Decision/risk | Initial direction | Resolution point |
| --- | --- | --- |
| Primary language/material scope | **Decided by user:** Koine and Classical Greek; manuscripts, papyri and inscriptions first | Reflected throughout this revision |
| Which witnesses, hands and imaging conditions are supported initially? | Select a real pilot collection and capability profile for each of the three tracks | Phase 0; still unresolved |
| Scholarly transcription conventions | Diplomatic evidence plus separate editorial readings; expert-adjudicated profile | Before dataset labeling |
| C# model/export support | TorchSharp training/native inference; ONNX inference adapter | Phase 0 operator/export spike |
| GPU differences across platforms | CPU baseline; publish measured backend matrix | Phase 0, then release packaging |
| Training data, annotators and initial weights | Actual image/label pairs, named reviewers and a proven seed-or-training route per track | Phase 0; required before Phase 1 commitments |
| Quality expectations and annotation effort | Per-material acceptance sheet; unresolved-span policy and correction-time baseline | Phase 0 targets; Phase 1–2 release gates |
| Inscription/fragment recognition difficulty | Separate models/benchmarks; manual layout permitted but disclosed | Phase 0 evidence; research risk remains |
| TEI/EpiDoc interoperability scope | Bounded versioned profile with explicit loss reporting | Phase 0 contract, Phase 1–2 verification |
| AI restriction claim | Treat absence of project-imposed AI bans as a requirement; source of claimed Kraken restriction is unverified | Clarify only if policy comparison becomes necessary |
| Project license and contribution policy | **Approved and documented 2026-10-19:** Apache-2.0, maintainer-led governance, DCO sign-off, reproducible improvement evidence and regression gates | Local policy files complete; hosting enforcement remains pending repository configuration |
| Standalone vs shared service priority | CLI/local first; shared server follows | Before Phase 1 implementation |
| Staffing, dates and compute costs | Re-estimate from actual annotation/model work; earlier week estimates withdrawn | Phase 0 completion |

## 10. Sources and planning review

### Licensing and contribution policy — 2026-10-19

- [x] Install the unmodified [Apache-2.0 license](LICENSE) and [DCO 1.1 text](DCO), plus project attribution and license-scope notices in [NOTICE](NOTICE).
- [x] Write [governance](GOVERNANCE.md), [contribution requirements](CONTRIBUTING.md) and a [pull-request evidence template](.github/pull_request_template.md) reflecting the owner's approval.
- [x] Link the policy documents from the README and verify text integrity, links and consistency without changing application code.
- [ ] Configure required maintainer review, trusted status checks, DCO validation and protected release/default branches on the hosting service. No Git remote is currently configured; writing local policy files does not enable these controls.

Review result: LICENSE matches the official Apache text byte for byte; DCO matches the official published text; 29 local links in the policy/README/template documents resolve; whitespace checks pass. Independent review clarified that an earlier contributor's sign-off does not certify later edits. No application code changed, no runtime tests were needed for these document changes, and the pre-existing edit to `docs/evaluation/layout-comparison.md` was left untouched. Hosting controls are not yet enabled.

Local reference material inspected:

- `/Users/johnbaima/Code/LeviathanText/Agent.md`: planning/checklist/review workflow and delegation guidance.
- `/Users/johnbaima/Code/kraken/Agent.md`: reference checkout instructions; unrelated project-specific overrides are outside this request.
- `/Users/johnbaima/Code/kraken/README.rst`: core feature inventory, training, scripts, exports and related interface context.
- `/Users/johnbaima/Code/kraken/pyproject.toml`: CLI capabilities, runtime dependency stack and declared license identifier.
- `/Users/johnbaima/Code/kraken/docs/user_guide/inference.rst`: pipeline, batch input, models and serialization.
- `/Users/johnbaima/Code/kraken/docs/user_guide/`: training, model and migration reference documents.

Technical feasibility references checked during planning:

- [ONNX Runtime C# API and supported packages](https://onnxruntime.ai/docs/get-started/with-csharp.html).
- [ONNX Runtime C# training artifacts](https://onnxruntime.ai/docs/api/csharp/api/Microsoft.ML.OnnxRuntime.TrainingSession.html): documented artifact generation uses Python utilities, motivating a separate C# training decision.
- [TorchSharp](https://github.com/dotnet/TorchSharp/) and [C# training examples](https://github.com/dotnet/TorchSharpExamples): candidate C# training/runtime foundation; OCR-specific feasibility remains to be demonstrated.

Review results:

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
- [ ] Execution readiness: select real collections and hardware, secure usable image/label pairs and reviewers, prove the C# pipeline, and freeze measured acceptance thresholds.
- [ ] Implementation remains unstarted pending a separate user instruction.
