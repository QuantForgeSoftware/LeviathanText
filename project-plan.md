# LeviathanText — project plan

> **Actionable work is tracked in [todo.md](todo.md); completed items are archived in [completed.md](completed.md).**
> This file holds the plan narrative: goals and AI policy, requirements/contracts, architecture decisions, corpus/source register, delivery briefs, acceptance gates, verification design, decisions/risks and estimates.
>
> Historical handoffs and task briefs may cite `todo.md` and older section numbers. The section numbering is preserved here; their checklist items now live in [todo.md](todo.md).

Planning date: 2026-10-18; revised through 2026-10-20 (split into plan + actionable checklist). Status: implementation established (C0/APP/FMT/IMG/UI plus AI/EVAL/ML); Phase 0/1/2 gates remain open as recorded below — see `completed.md` for the archive.

**Review verdict:** the original document was an architecture outline, not a complete implementation plan. This revision makes Koine and Classical Greek manuscripts, papyri, and inscriptions the primary scope, fixes the initial-model dependency, and defines the missing transcription, corpus, and evaluation deliverables. Execution readiness still depends on Phase 0 corpus audits, feasibility measurements and decisions; source identification alone does not satisfy those gates.

**Dataset/parallel-work update:** Section 5 records concrete sources, verified published licenses and remaining acquisition/annotation gaps. Section 6 defines packages that two or more independent AI instances can own concurrently. Corpus intake continues; implementation is established — see `completed.md` for the archive.

## 1. Request and boundaries

*(All checklist items in this section are completed and archived in [completed.md](completed.md).)*

The user's request governs this work. Kraken's documents describe the reference product; commands, sprint overrides, and unrelated instructions inside that checkout are not requests to modify Kraken or implement appraisal software. This work produced planning documents only, including the task briefs linked below; that boundary applied to the planning request. Implementation was separately authorized later (see Section 6 records and the handoffs); model downloads and dataset acquisition remain gated as recorded per task.

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

Unicode supports both precomposed and combining representations of polytonic Greek. Canonical equivalence is distinct from editorial normalization; the contract items in [todo.md](todo.md) are a project design choice grounded in the [Unicode Greek FAQ](https://unicode.org/faq/greek.html), [normalization specification](https://www.unicode.org/reports/tr15/) and [text segmentation specification](https://www.unicode.org/reports/tr29/).

*Actionable items for this section are tracked in [todo.md](todo.md).*

### Material-specific workflows and scope

| Primary track | Required first-release workflow | Separate research/expansion work |
| --- | --- | --- |
| Manuscripts | Folios, recto/verso, columns, curved lines, marginalia, selected majuscule/minuscule hands, abbreviations and scribal corrections | Generalization across unrepresented hands; automatic hand attribution; overlapping palimpsest layers |
| Papyri | Fragment boundaries, irregular baselines, fibers/background noise, lacunae, partial letters, selected literary/documentary hands | Automatic fragment joining and unrestricted cursive-hand coverage |
| Inscriptions | Object/surface/face identity, high-resolution photographs, orientation, uneven lighting, worn strokes, line/glyph annotation and actual text direction | 3D reconstruction, RTI/multispectral processing and recognition on arbitrary curved surfaces |

All three tracks belong in the initial corpus and product workflow. Release automatic recognition support per documented material/hand/imaging profile only after its gate passes; manual annotation alone must not be advertised as automatic recognition. If one track remains research-only, disclose that explicitly rather than calling the overall Greek scope complete.

Keep original photographs immutable. Crop/deskew/dewarp and lighting adjustments produce traceable derivatives. Line crops must retain diacritics, overbars and interlinear additions. Preserve alternative photographs/illumination views of the same surface, but do not merge their coordinates without registration. Generative image enhancement, if offered, is a labeled aid and never substitutes for the original visual evidence.

### Scholarly interchange

The proposed profile uses the distinctions documented by [TEI primary-source transcription](https://www.tei-c.org/release/doc/tei-p5-doc/en/html/PH.html) and [EpiDoc transcription guidelines](https://epidoc.stoa.org/gl/latest/app-alltrans.html). Image interchange should follow the [IIIF Presentation API](https://iiif.io/api/presentation/3.0/); full IIIF hosting is outside the first release.

*Actionable items for this section are tracked in [todo.md](todo.md).*

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

*Actionable items for this section are tracked in [todo.md](todo.md).*

### Corpus acquisition and annotation deliverable

Phase 0 must produce a versioned corpus manifest, not just a list of websites. For each of the three tracks, record actual object/surface/line counts, images available, capture quality, transcription profile, usable label counts, unresolved readings, language/writing style coverage, rights status, train/dev/test assignment and remaining annotation effort. The project owner selects representative collections; a Greek specialist owns annotation rules and adjudication; the implementation team owns manifests, splits and repeatable import. Assign these roles before the corresponding work starts; one person may hold multiple roles. Do not invent a corpus size or accuracy target before seeing the images.

*Actionable items for this section are tracked in [todo.md](todo.md).*

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

Remaining coverage gaps: early Greek majuscule manuscript hands and more independent witnesses; literary versus documentary papyrus variation beyond the small Zenon archive and Homeric glyph sets; Koine Christian papyri, difficult cursive, damaged lines, correction layers and visible diacritics; ancient carved inscriptions across sites/materials/lighting, with image-aligned diplomatic labels. Corpus size, source accessibility and license declarations do not establish recognition quality. No dataset/model archive was downloaded and no training or code was produced during this source review.

*Actionable items for this section are tracked in [todo.md](todo.md).*

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

**Implementation session record (2026-10-19 midday, cycle 2 — A only, 4h):** geometry/reading-order correction (service + API + overlay editor), training-data export with annotation status (L1-only, restorations excluded), line-annotation listing, crash-recovery drafts + keyboard shortcuts + tested scalar↔UTF-16 utils, export actions and AI-assistance panel in the workspace, IIIF import via CLI/API, live-smoke-hardened API error mapping, packaging evidence, and a measured layout iteration (projection baseline 5× improvement; learned row-model negative result recorded). Suite grew to **359 tests + 12 web specs**; Release build clean.

**Implementation session record (2026-10-19 late afternoon, cycle 4 — A only, 4h):** ALTO/hOCR adapters and legacy import; imported-id scoping fix (cross-project collision); Greek profile QA CLI/API/bundle + strict training filter; label-hygiene measurement; layout content-bounds fix (precision 0.386→0.433) and rejected merge rule; multi-seed end-to-end page CER (0.944 vs 0.905 control, layout effect inseparable from training variance); image-only import; manual line add/delete + geometry undo; mark rendering and L1/L2 diff; alternatives→L2; project search; model discovery + inline recognition; confidence review queue; annotation/line CSV; durable layout.project/import.dir/recognize.project jobs with cooperative cancellation; backup/restore with image checksums; jobs listing; auth guard; operations/STATUS docs. Closing verification (cycle 4): 465 tests (464 passed + 1 opt-in skipped), 0 failed, Release 0 warnings/0 errors; 21 web specs. Contract self-review recorded with two integrity actions resolved (confidence persistence, retry idempotency) and geometry audit added.

**Implementation session record (2026-10-19 evening → 2026-10-20, cycles 7–19 — A only):** trainable layout module (patch/row CNN, augmentation, digests/model card, per-material opt-in, one-to-one metric fix); inscription EpiDoc bootstrap + manual placement + inventory scanner; release hardening (fresh-install smoke, dependency evidence, parity matrix); double-review/adjudication + retraction ledgers and dashboards; accounts/roles/auth audit (schema v3) + workspace key panel; per-grapheme confidence persistence. Closing verification: 522 tests (520 passed + 2 opt-in skipped), 0 failed, Release 0 warnings/0 errors; 35 web specs; fresh-install/demo smokes PASS.

**Implementation session record (2026-10-19 evening, cycle 6 — A only):** trainable layout module: patch/row CNN (`patch-row-cnn-v2`) with aspect-preserving preprocessing, weighted BCE training, dev-tuned decoding, package save/load and architecture checks; per-material opt-in integration (CLI `layout train`/`--model`, API `Layout:Models:<kind>`, Application detector parameter) with projection default; corrected layout/page matching to one-to-one greedy (previous reports regenerated); measured M02a P 0.965/R 0.987 vs projection 0.523/0.545, page CER 0.971→0.942; P01-trained variant measured but not adopted. Closing verification: 482 tests (481 passed + 1 opt-in skipped), 0 failed, Release 0 warnings/0 errors; 26 web specs; pilot demo PASS.

**Implementation session record (2026-10-19 continuation, cycle 5 — A only):** project deletion (store default + forced flag), split/merge of untranscribed lines with audit, persisted review queue from stored L0 confidence, geometry editor view-only page mode with click-to-open and zoom, model discovery + inline recognition already in cycle 4, corpus-manifest audit CLI + ledger fix, jobs-panel and diff specs, estimated-gap field matching server validation. Closing verification: 470 tests (469 passed + 1 opt-in skipped), 0 failed, Release 0 warnings/0 errors; 26 web specs; pilot demo PASS.

**Implementation session record (2026-10-19 afternoon, cycle 3 — A only, 4h):** line-level reading order (service/API/UI), generic annotation form (all kinds, extent, alternatives), automatic layout stage wired CLI/API/UI with overwrite refusal, remote-capture resolver (IIIF URL → checksummed cache; offline afterwards) used by layout and recognition, end-to-end workflow proofs on both real tracks (papyrus: image-only → 4 lines → 4 non-empty L0; manuscript: held-out M02a page → 25 lines → 19 non-empty L0), L0 revisions persisted with model provenance, self-service `train lines` CLI producing a verified model package, bulk PAGE import through the bounded pipeline (27/27 files in 0.6 s), PAGE import now resolves the referenced image path, multi-view capture selector, camelCase layer params, and measured TIFF-unsupported handling (clean rejection). Layout texture features retried — still below the corrected projection baseline (recorded). Durable layout jobs execute in the worker (API enqueue → DB pickup → 12 lines). Closing verification: 397 passed + 1 opt-in skipped across 13 suites, 0 failed, Release 0 warnings.

**Two-instance mode restored (2026-10-19 06:54):** Instance A resumes as **integrator** and owns `docs/contracts/`, `docs/handoffs/A-PLAN.md`, `todo.md`, and the implementation packages **C0/APP/FMT/IMG/UI**. ML/AI/EVAL code exists from the single-instance session; A holds it until B claims it (B's natural packages: ML/AI/EVAL refinement or D-M/D-P/D-I data work — B records its claim in its own handoff; A will reflect it on this board). Ready inputs for B: contract set `C0-0.4-draft`, per-track audited manifests (`data/manifests/*`), pilot reports, and the trained-model/checkpoint conventions.

**Starting assignment:** C0 and D-M/D-P/D-I are ready for further documentation/intake planning. Implementation is established and recorded on the board above; acquisition, pairing and expert annotation remain pending. Give each new instance a package brief containing its ID, allowed action scope, base revision, owned paths, contract version, dependency inputs, acceptance criteria and handoff path `docs/handoffs/<package-id>.md`. A brief must carry the no-code constraint whenever it is still in effect. This lets a fresh instance resume without relying on another chat's memory.

### Issued two-instance planning briefs

*(All checklist items in this section are completed and archived in [completed.md](completed.md).)*

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
| IMG | Imaging/layout adapters | **done (adapters)**; projection baseline improved 5×, learned row-model negative (recorded); automatic layout still research-only | commits `1f3a4f3`, `82fd004` |
| APP | Persistence/jobs/CLI/Worker | **local-first core complete**; transcription/geometry/order/annotation/export endpoints, worker, CLI, SSE jobs | commits `b7bf85e`, `1bc025d`, `13d88d5`, `7aadcd4` |
| ML | Recognition/training | **done (feasibility gate on real + synthetic data)** | commits `72690ea`, `a3e48b7`, `0534ac1` |
| EVAL | Evaluation/scoring | **pilot reports live** (EVAL-02/04 pilot form); frozen acceptance sheet pending specialists | commit `9a3efd7`, `a0c43fc` |
| AI | Provider adapters | **scaffold done** (schema validation, scripted double, acceptance→L2); local Ollama smoke passed (B) | commit `72690ea`, `8bffcf3` |
| UI | Angular workspace | **editor workflows live**: line editor, annotations, dual-pane, geometry overlay, jobs, exports, proposals | commits `108d45e`, `4903a80`, `d8892b3`, `14d059a` |
| DATA-01..03 | Corpus ledger/manifests/splits | **ledgers committed** (p01, m01, m02a, i01); expert adjudication pending | commits `3bc2131`, `4d16e1b`, `3b75bf0` |

**Morning-session state (2026-10-19, session close 10:10):** two-instance mode operating cleanly — A (integrator/APP/FMT/IMG/UI) and B (AI/EVAL/ML) with disjoint paths verified. Session increments: transcription write endpoints + service tests (`e9684c8`, `1bbddd8`), line editor UI (`bb8d50f`), Layout package + honest negative finding (`56bac39`, `d01495c`), extraction fix for the measured CTC starvation (`0c6c768`), AI services wired into the API (`51eb654`), streaming pipeline (`fcee469`), jobs panel + dual-pane UI (`0a1f9af`, `78201e7`), API write-surface hardening from live smoke (`3314d02`), packaged-model CLI loading (`934c6f9`), handbook (`6ea4f56`). B's locked-set disclosure reconciled (multiple runs — see docs/evaluation/locked-set-reuse.md). **Suite state: 340/340 tests green across 13 suites; Release build 0 warnings/0 errors.**

A alone edits this board and the contract documents. B alone edits the three material plans and evaluation plan. Each owns its own handoff. The briefs are read-only inputs during execution. A publishes a draft manifest/transcription contract early; B publishes source cards independently. They then exchange reviews without requiring either package to be accepted first. A integrates accepted decisions; unresolved scholarly, acquisition and feasibility work stays pending. Completing these documentation packages does not complete I0 or Phase 0, or authorize code. The briefs specify acceptance criteria, exact owned paths and the evidence required in each handoff.

**A-PLAN execution record (2026-10-18, by Instance A):** the user authorized executing the A-PLAN brief. Instance A claimed ownership, recorded baseline hashes (handoff rev 1), and published contract proposal **`C0-0.1-draft`** ([corpus-manifest](docs/contracts/corpus-manifest.md), [greek-transcription](docs/contracts/greek-transcription.md)) followed by **`C0-0.2-draft`** ([domain-and-pipeline](docs/contracts/domain-and-pipeline.md), [interchange-and-api](docs/contracts/interchange-and-api.md), [examples](docs/contracts/examples.md)). All five documents are **proposals pending review**; none is accepted, and the scholarly conventions await Greek-specialist adjudication. B never started (no B handoff or B-owned documents exist), so A-PLAN status is **`review`**, not `done`, and A's review of B is pending. The complete evidence trail, per-file SHA-256 hashes, open reviewer questions and blockers (BLK-A-1…3) are in [`docs/handoffs/A-PLAN.md`](docs/handoffs/A-PLAN.md). All Phase 0/I0/implementation gates below remain unchecked; no code, downloads, installs or benchmarks were performed, and the contracts define no schemas or file formats.

**Implementation status — C0 (Instance A as integrator; user authorization to code received 2026-10-18):**

**Ownership collision note (2026-10-18, Instance A):** a concurrent worker created `src/LeviathanText.{AI,Evaluation,Recognition,Training}` and their test projects (disjoint packages — acceptable) and also rewrote `tests/LeviathanText.Core.Tests/LeviathanText.Core.Tests.csproj` and `src/LeviathanText.Contracts/LeviathanText.Contracts.csproj` into a minimal style while introducing `Directory.Build.props`. Those are C0-owned paths (solution/project setup). The centralization itself is sound; A (integrator) adopted it, restored the missing xunit global using and fixture content in the Core.Tests project, and will keep the central files consistent. Concurrent workers must confine edits to their own packages and route central-file changes through the integrator per the coordination rules below.

### Coordination and integration rules

*Process rules, not tracked work items; the actionable checklist is in [todo.md](todo.md).*

- [ ] A worker claims one package before editing. Ownership persists across context resets; reassignment requires a recorded handoff. Each worker reads this plan, `Agent.md`, its package brief and current contract version. Do not infer authorization from another instance's unreviewed output.
- [ ] Workers may read shared code/contracts but change only owned paths. Shared project files, lockfiles, generated API contracts, schema migrations and central package versions have one named owner. The integrator resolves requested cross-boundary changes before dependent work resumes.
- [ ] Each handoff includes changed paths/commit, assumptions, input/data hashes, license evidence, tests or document checks performed, failure results, unresolved questions and the exact next dependency. Dataset/model artifacts are separately versioned; do not copy large corpora or secrets into Git or chat.
- [ ] A different instance reviews each handoff. The integrator merges one package at a time, runs relevant contract/integration checks, and records the accepted revision. A worker's “done” means its scoped acceptance passed, not that the whole phase passed.
- [ ] Reserve GPU, download, storage and benchmark resources explicitly to avoid contention. Keep authoritative locked test labels with the evaluator; training and prompt tuning use development sets. Parallel instances must not leak held-out labels through handoff examples.
- [ ] If a dependency fails, record the blocker and continue an independent package or use the agreed fixtures. Do not silently replace an ML/backend choice, loosen a quality threshold, adopt a restricted dataset or change a scholarly convention to unblock another instance.

Integration checkpoints: **I0** accepts C0 plus data conventions; **I1** combines native persistence, imported/manual geometry and C# recognition; **I2** combines API, Angular editor and scholarly exports; **I3** adds AI proposals and per-track acceptance reports; **I4** applies the release checklist. New data tracks can advance through I1 independently, but the combined initial scope is not complete until all three material gates pass.

### Phase 0 — Greek evidence, contracts and feasibility

Gate: all three tracks have actual source data, an annotation contract and a documented model route; the C# demonstration and measured acceptance sheet support proceeding. Generic printed Greek accuracy does not satisfy this gate.

*Actionable items for this section are tracked in [todo.md](todo.md).*

### Phase 1 — Greek engine, minimal training and CLI

Gate: a fresh offline installation processes the held-out sets for all three tracks, reloads its own trained checkpoints and meets the agreed per-track pilot thresholds. Manual layout assistance is recorded and evaluated separately from fully automatic processing. Unmet tracks remain visible blockers to claiming the full initial scope.

*Actionable items for this section are tracked in [todo.md](todo.md).*

### Phase 2 — scholarly workspace and initial AI assistance

Gate: users can transcribe, review, train from corrections and export selected examples from all three materials without losing scholarly distinctions. Initial AI demonstrates local offline and hosted operation, preserved evidence, provider switching and measured effects on correction effort/unsupported additions. Claims of improved recognition require the benchmark results, not merely a functioning adapter.

**First usable Greek release:** Phase 1 + Phase 2 + the release checklist below, with the selected material/hand/capture profiles and remaining limitations published. Broader handwritten Greek coverage is not implied by this release.

*Actionable items for this section are tracked in [todo.md](todo.md).*

### Phase 3 — expanded Greek training and generalization

Gate: users can train recognition/layout/order models in C#, reload them and evaluate independently on the per-track acceptance sets. Expanded support is demonstrated per profile; uncertainty or damage is not hidden by a pooled accuracy number.

*Actionable items for this section are tracked in [todo.md](todo.md).*

### Phase 4 — wider replacement scope

Gate: each claimed added capability has independent acceptance evidence; none is required merely to ship the agreed initial Greek workflow.

*Actionable items for this section are tracked in [todo.md](todo.md).*

### Release checklist — applies before every usable release

Gate: reproducible installation and benchmark reports, tested recovery and honest per-track capability claims. A failed quality gate cannot be relabeled as successful full-scope delivery.

*Actionable items for this section are tracked in [todo.md](todo.md).*

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

*Actionable items for this section are tracked in [todo.md](todo.md).*

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

Review result: LICENSE matches the official Apache text byte for byte; DCO matches the official published text; 29 local links in the policy/README/template documents resolve; whitespace checks pass. Independent review clarified that an earlier contributor's sign-off does not certify later edits. No application code changed, no runtime tests were needed for these document changes, and the pre-existing edit to `docs/evaluation/layout-comparison.md` was left untouched. Hosting controls are not yet enabled. The hosting-control action item is tracked in [todo.md](todo.md) §10.

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

Review results: execution readiness remains open; it is tracked in [todo.md](todo.md) §10.

### eScriptorium integration review — 2026-11-02

Reviewed [the integration assessment](docs/evaluation/escriptorium-integration.md) against its cited LeviathanText commit `fc96d5bb2f901f8610ad421d5f95e640c784ea67` (also current HEAD) and public eScriptorium sources. The direction is reasonable: investigate bounded interchange before a recognition-only service, and avoid a full engine replacement without evidence. The assessment needs corrections before its capability inventory or estimates can guide implementation. The original draft was left unchanged. This was a static source/document review, not an executed eScriptorium round trip or performance benchmark.

1. **P1 — The proposed import → recognize → PAGE workflow can export the previous text.** [RecognitionPipeline](src/LeviathanText.Application/RecognitionPipeline.cs) persists raw L0 revisions without changing the diplomatic pointer; [PageXml.Export](src/LeviathanText.Formats/PageXml.cs) selects that pointer. The pilot needs an explicit output-layer/run choice or an auditable review/acceptance step. It must verify that returned text is the selected new result and preserves its provenance, rather than claiming the unqualified workflow works already.
2. **P1 — Round-trip object identity is not preserved automatically.** [InterchangeService](src/LeviathanText.Application/ProjectService.cs) adds an import-specific suffix to region/line IDs, and PAGE exports those IDs. eScriptorium's [XML importer](https://gitlab.com/scripta/escriptorium/-/raw/develop/app/apps/imports/parsers.py) matches lines by external ID; override deletes existing lines/blocks. A safe pilot therefore needs reversible source-ID mappings, original image names and explicit non-destructive layer semantics, with checks for duplicate lines and preserved existing work.
3. **P2 — Existing reusable capabilities are omitted.** The cited commit already contains [recognition/forced-alignment task wrappers](src/LeviathanText.Application/TaskModels.cs), [CTC alignment and approximate cut serialization](src/LeviathanText.Application/ForcedAlignmentTask.cs), registry discovery/download commands in the CLI, a read-only model catalog API and TIFF decoding support. Correct remaining gaps are external adapter exposure, accurate source-pixel cuts, eScriptorium model lifecycle integration and measured deployment limits. Proportional boxes are not measured glyph geometry; existing TIFF support does not establish arbitrary large-image capacity.
4. **P2 — Narrow format and graph claims.** eScriptorium's importer supports PAGE/ALTO but does not establish TEI as a two-way format. Its [model definitions](https://gitlab.com/scripta/escriptorium/-/raw/develop/app/apps/core/models.py) permit absent character graphs. A recognition-only pilot can omit graphs with an explicit UI limitation; it should not invent evenly divided glyph boxes to imply alignment accuracy. Greek scalar/grapheme confidence mapping still requires a defined contract.
5. **P2 — Pin evidence and qualify conclusions.** Replace moving `develop` references with an eScriptorium commit and source permalinks. Upstream acceptance remains unknown; steering-committee membership is not evidence of rejection. The one-developer sidecar breakdown sums to 6–12 weeks, not the stated 6–10; neither range is validated by an integration pilot. Benchmark acceptance should reflect the intended workflow benefit and protect existing quality/performance, with fixed models, data, normalization and per-material reporting.

Before implementation, the sidecar proposal also needs a versioned request/result contract: original line IDs, image/model/config hashes, coordinate frames, text-offset/confidence semantics, result completeness and errors, retry/idempotency behavior, cancellation and stale-result rejection when source geometry/text changes. Keep Python/Kraken requirements confined to the optional external eScriptorium deployment. These are review findings, not approval or completed work on a new integration package.

## 11. Remaining-effort estimate — 2026-10-19

Estimate basis: measured output of the completed engineering (14 C# packages + Angular workspace, 359 tests), the three real-data pilots, and the pilot training runs. **The tables below are human-equivalent scope** (one engineer or one specialist). §11.D translates that to agent execution, which is a different number: my measured throughput is several human-equivalent days per active hour, but the calendar is still gated by humans and compute.

Units are **dev-days** (one human engineer working day) and **specialist-days** (Greek/papyrology/epigraphy expert time). Ranges are deliberately wide where the plan says "measure first"; no date claim is made.

### A. Engineering (C#/Angular), remaining

| Work | Estimate (dev-days) | Depends on |
| --- | --- | --- |
| Editor completion: geometry/reading-order editing UI, source-linked alternatives, correction layers, crash recovery, keyboard/accessibility pass | 8–14 | contract stability (done) |
| Trainable layout module (annotation source: PAGE boxes already audited for ~1,700 pages) | 5–9 | adjudicated box GT sample |
| Recognition scale-up experiments (capacity, augmentation, decoding, per-hand tuning) | 8–15 | adjudicated lines (volume is the limiter, not code) |
| ONNX export feasibility spike | 2–3 | — |
| Formats expansion: TIFF/PDF ingestion, ALTO/hOCR/ABBYY adapters | 10–18 | only for later parity |
| Auth + shared multi-user server | 5–9 | Phase 3 |
| Packaging/release engineering (installers, migrations, profiling, rollback) | 5–7 | release candidate |
| Evaluation tooling completion (acceptance sheets automation, leakage audit, correction-time capture) | 3–5 | frozen labels |
| **Engineering total** | **46–80** | |

### B. Specialist (scholarly) time, remaining

| Work | Estimate (specialist-days) |
| --- | --- |
| Adjudicate the transcription profile's open decisions (lunate sigma, epichoric table, numerals, underdots, word division) | 2–3 |
| M02a/M01 label-policy review (expansion-lite, diaeresis, spaces, nomina sacra/overbars) | 1–2 |
| Expert-audited pilot labels, dev + locked, per track (~300–500 lines) | 3–6 |
| Inscription line construction (~100–200 lines with geometry, from I01's 448-image subset) | 4–8 |
| Acceptance sheet, per-track thresholds and sign-off | 1–2 |
| Correction-time studies (manual + AI-assisted) | 1–2 |
| **Specialist total** | **12–23** |

### C. Data/rights (mostly parallel to A/B)

| Work | Estimate |
| --- | --- |
| Remaining acquisitions/audits: M02b/M02c, M03 decision, P02/P03 subset ledger, P05/P06 join, I02 inventory | 5–8 dev-days + 3–5 owner/legal days (elapsed, decisions) |
| Dedup, split assignment, manifest completion (DATA-02/03) | 2–3 dev-days |

### Critical path and phase gates (calendar, 1 engineer + ~10–20% specialist)

| Milestone | Remaining duration | Why |
| --- | --- | --- |
| **Phase 0 complete** (all three tracks: real data, annotation contract, adjudicated pilot, proven C# route, measured acceptance sheet) | **4–6 weeks** | dominated by specialist adjudication + inscription labels; engineering support is small |
| **First usable Greek release** (Phases 1–2 gates: engine, editor, AI, per-track thresholds) | **+6–10 weeks** → **10–16 weeks total** | editor completion + recognition scale-up + fresh held-out evaluation |
| **Phase 3** (generalization, active learning, shared workspaces) | +8–12 weeks | requires data volume first |
| **Phase 4** (wider Kraken parity: RTL/vertical, formats, PDF) | +10–16 weeks | independent of Greek release gates |

### Confidence and drivers

- **Engineering**: ±30% (high confidence in scope; low risk of hidden architectural work).
- **Specialist**: ±50% — the largest single uncertainty; scales directly with available expert hours.
- **Recognition quality**: **not estimable** — measured CER is 0.78–0.87 at 176–571 training lines; reaching release-quality needs adjudicated volume (thousands of lines per hand) and is a research outcome, not a schedule item.
- **What compresses the estimate**: (1) named specialist with regular hours; (2) a decision allowing NC corpora for research-only tracks (removes per-image rights friction); (3) accepting a narrow documented profile (e.g., two supported hands) instead of broad generalization; (4) combining the dev/acceptance label audits into one instrumented pass.
- **What expands it**: any track whose labels cannot be adjudicated; a requirement for fully automatic layout from day one; per-image institutional permissions for P02/P03.

### D. Agent (Instance A) timeline — the same scope, my execution

Measured velocity basis: this morning's ~3.5 hours of active session produced work equal to roughly 12–17 human dev-days (transcription service + 11 tests, API write surface + 13 HTTP tests + live smoke, editor/dual-pane/jobs UI, Layout package + harness, extraction fix, IIIF CLI/API + tests, streaming pipeline, handbook). That is ~4–5 human-days per active hour on **well-specified, contract-driven** work. I apply a **2× conservatism** for the remainder because it contains more experimentation and less greenfield code.

| Work package | My active time | Sessions (4–6 h cap) | Calendar if inputs are supplied |
| --- | --- | --- | --- |
| Engineering stream (46–80 human-days) | **20–50 h** | 5–10 | 1.5–4 weeks of working days |
| ML scale-up experiments | 8–20 h of my time + **30–80 h background training** | interleaved | gated by adjudicated data volume |
| Data acquisition/audit (5–8 human-days) | 2–4 h + download/convert wall time | 1–2 | 1–3 days (network/hardware) |
| Release engineering/packaging (5–7 human-days) | 2–4 h | 1–2 | 1 week |
| Formats/parity (10–18 human-days) | 4–8 h | 2–3 | 1–2 weeks |
| **My total for Phases 1–2 engineering** | **~35–80 h active** | **8–15** | **3–6 weeks** |

**What my speed does not change:**

1. **Phase 0's gate is human.** Adjudicated labels (12–23 specialist-days), rights decisions on P02/P03, and sign-off on the transcription profile cannot be compressed by me. With a specialist on regular hours: **4–6 weeks calendar** for Phase 0, of which my share is ~1 week.
2. **Research outcomes.** Recognition quality at 176–571 training lines is CER 0.78–0.87. Whether adjudicated volume gets a model to release quality is measured, not scheduled — my ability to run experiments fast does not make the outcome predictable.
3. **Context limits.** I work in bounded sessions; a multi-week calendar means re-briefing per the handoff protocol (`docs/handoffs/A-PLAN.md`), not one continuous run. Long sessions degrade my reliability (observed 2026-10-19: garbled edits after ~4 h without a break), so 4–6 h sessions with test gates are the sane unit.
4. **Compute wall clock.** Training/eval runs are minutes-to-hours each; suites already take ~20 min (ML alone ~14). Background runs don't need my attention but they extend the calendar.

**Bottom line:** if specialists and rights decisions arrived at a steady pace, I could deliver the **Phase 1–2 engineering** in about **3–6 weeks of calendar with ~8–15 working sessions (~35–80 active hours)**, and the **first usable Greek release** in roughly **6–10 weeks calendar** — the delta being human adjudication and fresh held-out evaluation. The 10–16 week figure in §11.C is the realistic plan when specialist time is part-time; it is not my coding speed.

## 12. NTVMR / CSNTM manuscript-pairing pilot — 2026-11-02

### Objective, scope and authority

Demonstrate that LeviathanText can help a manuscript project assemble and review a reliable image/transcription dataset. Start with images and transcriptions associated with the same manuscript pages in the New Testament Virtual Manuscript Room (NTVMR). After that workflow is verified, investigate mapping CSNTM photographs to the same pages despite differing image enumeration. The deliverable is a small, inspectable dataset and repeatable workflow; recognition training is a later, separately gated experiment.

Initial planning target: **10–20 pages from one Greek New Testament witness**, with a first inspection of three pages and a deliberately small line-annotation subset, provisionally **50–100 lines**. These are workload limits for a feasibility demonstration, not sufficient training volume or evidence of general Greek support. Prefer a straightforward primary text hand; include an abbreviation, visible correction or damaged span if available. Record the limitations of an easy sample. Add a second witness only after the initial workflow passes, and before any unseen-witness recognition claim.

Success means a Greek reviewer can inspect the exact image/text correspondence, correct proposed links or labels, and export the accepted records with provenance. A finished eScriptorium replacement, a CSNTM-hosted service, a public dataset and an accurate general recognition model are outside this pilot's initial acceptance scope. The standalone C# application and Angular interface remain the product; no Kraken/eScriptorium dependency is introduced.

This request authorizes this **plan and checklist only**. No acquisition, implementation, institutional contact, training, deployment or publication is performed by writing it. Future execution must follow the owner's instruction and source-specific rights. The supplied email is a lead about available resources, not a permission grant or a commitment to collaborate.

### Existing foundation and specific gaps

Reuse the image/PAGE/IIIF ingestion, source-linked editor, geometry placement, revision/annotation services, review packets, training export and evaluation infrastructure already implemented. Existing bounded TEI/EpiDoc handling is a starting point; it is not a verified NTVMR importer. No dedicated NTVMR/CSNTM connector was found in the source at planning time.

The [NTVMR transcription API](https://ntvmr.uni-muenster.de/community/api/transcript/get/) documents manuscript/page identifiers, TEI/raw-TEI output, version selection and copyright metadata. Before acquisition, verify the actual service route, availability, transcript ownership and image access for the chosen witness. Preserve the downloaded original XML and its header; do not request accent/mark removal or other filtering for visual labels. A page-level transcript may lack line-to-pixel correspondence.

CSNTM's [AI project description](https://new.csntm.org/projects/project-ai-for-manuscript-transcription/) establishes a related pairing/transcription objective, not an available cleared corpus or adoption agreement. Its [collaboration policy](https://new.csntm.org/collaboration/) distinguishes viewing from downloading and describes holding-institution rights. Reuse [the permission-request draft](docs/data/csntm-permission-request.md) once the owner selects exact witnesses and intended uses; do not send it automatically.

### Roles and parallel ownership

| Role | Responsibility / exclusive scope for this pilot |
| --- | --- |
| Project owner — John Baima | Select witness and intended use, obtain/confirm permissions, appoint reviewers and decide whether to expand or approach a partner. |
| Instance A — engineering/integrator | Shared identity/transcription/export contracts; C# adapters under proposed `src/LeviathanText.Application/Ntvmr/`; required existing Formats/API changes; Angular workflow; focused engineering tests. Owns updates to `project-plan.md`, `todo.md` and `docs/handoffs/NT-PILOT-A.md`. |
| Instance B — corpus/evidence | Source inventory and mapping proposals in `docs/data/ntvmr-pilot.md`; authorized manifests under proposed `data/manifests/ntvmr/`; artifact prefix `datasets/ntvmr-pilot/`; pilot-only evaluation fixtures/tests and reports; `docs/handoffs/NT-PILOT-B.md`. Does not edit A's contracts, application code or shared plans. |
| Greek reviewer — appoint before label acceptance | Confirms page matches and visible readings, resolves transcription-policy questions, reviews geometry and records uncertainties. A second qualified reviewer checks the scored label subset; disagreements require recorded adjudication. |
| Acceptance evaluator — appoint before freezing evaluation data | Controls locked labels and evaluates frozen candidate outputs. May also be a Greek reviewer, but must keep acceptance labels out of engineering, training and prompt-tuning handoffs. |

All proposed paths are future ownership boundaries, not files/assets created by this planning task. A publishes a minimal versioned field/transcription contract early; B can inventory sources and propose mappings independently. B provides source edge cases while A tests against permitted development examples or clearly labeled invented fixtures. One instance writes each path; shared-file or central-build changes go through A. Reassignments and access to test labels are explicit. If B later performs model tuning, B cannot also treat previously exposed labels as a fresh blind acceptance set.

Each handoff records baseline commit, owned paths, contract and manifest versions, source/evidence hashes, completed checks, known losses, blockers and exact next inputs. The other instance reviews each named revision before integration. Neither needs the other's package to be accepted before publishing an initial draft.

### Milestones and acceptance gates

| Gate | Work and deliverables | Acceptance / stop condition |
| --- | --- | --- |
| **NT-G0 — scope and access** | Candidate shortlist, named owner/reviewers, one selected witness, three-page inspection, image/transcript terms and exact intended uses. | Usable image/text access and rights evidence exist for local copying, processing and annotation. Record training, publication and weight/data redistribution separately. Unclear rights or unavailable assets block acquisition; select another candidate rather than assume permission. |
| **NT-G1 — faithful import and page matching** | Pin original images/XML, source IDs, versions, checksums and page identity evidence. Import as reference material with explicit mapping losses. Produce a side-by-side page view and a manifest with accepted, proposed, rejected and unresolved links. | All 10–20 included page links are human-confirmed. Ambiguous pages remain outside the accepted set. Counts reconcile; no duplicates or silent missing pages. A repeated import does not create duplicate objects or detach previous reviews. No claim of line-level training readiness yet. |
| **NT-G2 — visible line labels and review** | Select the line subset, add geometry and reading order, compare the source transcript to visible text, document corrections and review decisions. Keep the imported edition separately from accepted diplomatic labels. | Every included scored line has a confirmed image location, defined text/uncertainty policy and independent second review with adjudication. Unplaced, unreviewed and editorial-only spans are explicitly excluded. Verify crops preserve marks, overbars and additions. Record actual line/character counts and reviewer time. |
| **NT-G3 — portable, non-destructive demonstration** | A locally repeatable import → match → review → export demonstration; matching manifest, selected text-layer export, original attachments, loss report and restoration instructions. | A reviewer can reconstruct each accepted pair from source IDs/hashes. Export contains the chosen revision, not an old imported transcript. Backup/restore retains links and decisions; unrelated existing data is preserved. If eScriptorium round-trip is tested, use a disposable copy and prove stable original IDs, original image names, preserved layers and no duplicate lines. |
| **NT-G4 — CSNTM crosswalk extension** | With permissions in place, map a small CSNTM subset to the already verified NTVMR pages. Show evidence and manual confirmation, plus unresolved cases and measured matching effort. | Each accepted cross-collection link is individually confirmed. No assumption of equal page numbers or a constant offset; discrepant captures are accounted for. Page identity does not establish line geometry: register the new photograph and review transferred coordinates/readings, or annotate it independently, before training or scoring. If matching or access is impractical, retain the NTVMR-only deliverable as a valid bounded result. |
| **NT-G5 — optional recognition experiment** | Freeze development/evaluation membership, model/configuration and scoring rules; measure existing model output and manual correction effort. Train/tune only if approved rights, reviewed volume and compute justify it. | Report raw vs assisted results, manual vs automatic layout, failures, CER/grapheme/mark errors, throughput/resources and correction time. No improvement claim without a comparable baseline and no-regression checks. One-witness results establish only that scope. Poor results are reported; they do not invalidate a useful pairing/review tool. |
| **NT-G6 — collaboration decision** | Owner reviews a demonstration package with a short summary of verified capabilities, evidence, limitations, rights and proposed next contribution. | Decide among data-preparation-only collaboration, a larger reviewed corpus, further engineering/model work or no expansion. Nothing is sent, hosted or published without authorization for that action and its specific assets. |

G0 → G1 → G2 → G3 is the initial critical path. G4 and G5 are independent optional extensions after the relevant prerequisites: G4 needs access/mapping evidence, not recognition training; G5 needs suitable labels/evaluation, not a CSNTM match. Public release is not required for a private authorized demonstration.

### Page crosswalk and matching method

Store original identifiers independently from internal IDs: Gregory–Aland number, holding institution/shelfmark, NTVMR `docID`/`pageID`, transcription owner/version, image or IIIF canvas identifier, CSNTM image identifier when applicable, folio/recto/verso, printed pagination and capture sequence. Permit unknown and one-to-many relationships: two-page openings, multiple views of one page, fragments and separate commentary regions cannot be forced into a one-file/one-page scheme.

For each proposed link, retain evidence such as foliation, visible incipit/explicit, passage range, column arrangement and distinctive physical features. Biblical contents help propose candidates but are not unique identifiers; many witnesses contain the same passage. Handle missing leaves, inserted/blank pages, covers, reordered images and changed bindings explicitly. Candidate scores are workflow aids, not scholarly certainty. Do not promote a proposed match solely because its OCR output resembles a standard Bible text.

The record also contains image/transcript checksums, acquisition date, separate license/permission references, parent-image lineage, matching status, reviewer/date, ambiguity reason, line geometry status, annotation completeness, accepted revision and split membership. Contract mappings must be versioned. Re-imports, renamed files and changed upstream transcripts require a comparison and new revision, not silent replacement.

### Transcription fidelity, export and engineering checks

Inspect a real NTVMR TEI sample before designing a mapping. Account for page/column/line breaks, manuscript hands, corrections, abbreviations/expansions, nomina sacra, overbars, visible accents, supplied text and uncertain traces. Preserve unsupported markup in the original attachment and report the loss. Preserve the source transcription's own policy; map to the project's diplomatic policy only through recorded review. A normalized or editorial transcription is reference text until its visible readings are checked.

No automatically imported transcript or generated reconstruction becomes accepted L1 ground truth by default. Imported editorial/reference text, raw recognition, human-accepted diplomatic text and editorial/AI proposals remain distinct. Alignment must not force an edition's missing or expanded words onto visible image regions. Existing proportional forced-alignment boxes remain approximate and cannot replace reviewed geometry.

Engineering acceptance must cover repeated import, version changes, identical filenames in different witnesses, missing images/transcripts, unmatched IDs, one-to-many captures, unsupported TEI and stale review updates. Add a baseline-failing/candidate-passing regression case for each actual defect fixed. Reuse current review/export services where possible. Configure bounded, resumable source reads with transparent errors and approved access; do not evade access controls or bulk-fetch a collection to discover whether it is usable.

Two known interchange hazards from the [eScriptorium assessment review](#escriptorium-integration-review--2026-11-02) must have explicit tests before a cross-tool demonstration: internal import suffixes need reversible external-ID mappings, and exports need an explicit selected layer/run. Source ID and text-layer choices must survive native backup/restore. Standard OCR XML will not preserve all scholarly layers; include the authoritative native/manifest package and a loss report alongside bounded exports.

### Evaluation, resources and expansion decisions

For page matching, publish proposed/accepted/rejected/unresolved counts, confirmed mistakes and review time per page. On the pilot's accepted set, all links must be reviewed and no known incorrect match may remain. Do not infer matching accuracy across a collection from this deliberately small sample.

For labels, report actual paired pages, placed/reviewed lines, visible characters/marks, disputed or excluded spans and coverage gaps. Freeze scored annotations before evaluating candidates. For explicitly labeled within-witness diagnostics, group all captures/crops/derivatives of the same page together, and group both sides of an identified physical leaf together; never randomize lines from that unit across splits. For unseen-witness evaluation, group the entire manuscript and every derived asset together, following the main corpus policy. A second, unseen witness is required to measure cross-witness generalization, and repeated biblical passages must be disclosed when assessing language/AI assistance.

For recognition, use identical images/geometry and a declared Unicode/spacing/uncertainty policy for each candidate. Keep raw, AI-assisted and accepted readings separate, and do not count a memorized standard text as visual recognition. A Kraken/eScriptorium comparator may be run separately on authorized data if available; it is not a runtime dependency. Do not tune against a reused locked set or select the best test run. Establish candidate acceptance thresholds on development evidence before final evaluation; until then publish measurements without a pass/fail accuracy claim.

Use the current local CPU workflow initially, with bounded artifact storage and logged acquisition/review effort. No GPU purchase, hosted service or production deployment is needed to establish dataset feasibility. After the first three pages, record import/mapping losses and engineer/reviewer time, then estimate the remaining pilot from those observations. Re-estimate model experiments only after measuring reviewed line volume, training resources and learning curves; no fixed delivery date or accuracy promise is made here.

The initial demo package consists of an authorized source ledger, verified page crosswalk, reviewed line subset, original text/image references and hashes, reproducible local instructions, selected-layer exports and loss reports, review/effort measurements, limitations and the owner's next-step decision. Include actual assets only where their permissions allow the recipient to receive them. An open-source software license does not make the source images, transcripts or trained weights openly distributable.

Planning review: milestones distinguish pairing, labeling and model quality; engineering and corpus work have disjoint ownership; all acquisition/contact/implementation tasks remain future work in [todo.md](todo.md#ntvmr--csntm-pilot). Independent review clarified the split unit for within-witness versus unseen-witness evaluation and required registration/review before reusing geometry on CSNTM photographs. Local links, gate/task coverage and whitespace checks passed. This plan introduces no application code or new source assets.

**NT-01 source-discovery update — 2026-11-02:** the owner requested candidate/permission research. [The resulting source report](docs/data/ntvmr-pilot.md) recommends **P46's Michigan portion**, not its Chester Beatty captures: three Michigan JPEG endpoints are reachable and their actual published TEI has CC BY 4.0 notices; Michigan declares its papyrus images public domain with attribution. Six witnesses were screened using live page metadata and institutional/edition terms. Individual citation/exception verification, owner selection and Greek review remain open. The initial Chester Beatty lead was downgraded after its three tested legacy image URLs returned 404. This research step did not acquire photographs, implement a connector, train a model or contact institutions.

**NT-01 item/visual follow-up — 2026-11-02:** the owner selected P46 Michigan and authorized inspection of pages 1440/1450/1460. [The item and page review](docs/data/p46-michigan-page-review.md) confirms **item-level public-domain/PDM1.0 image notices** in Michigan's official manifests and preliminary image/transcript correspondence for all three pages. Two generic Recto labels conflict with detailed Verso fields; page 1460's catalogue passage starts at 2:9 although the visible text starts in 2:12; page 1440's catalogue count disagrees with observed/TEI rows. Preserve these discrepancies and the supplied text, historical corrections, contractions, word-division/case conventions and photographic-apparatus scope as distinct issues. Three width-2200 official IIIF renderings, original manifests/TEI and hashes are stored in a Git-ignored review package; partial timed-out NTVMR JPEG downloads were not used. No training was performed.

**Training gate refinement:** image rights and page identity do not establish usable CTC labels. Before NT-G5/NT-08, require qualified adjudication of the review ledger, actual source-frame line polygons, a declared original/corrected target state, preservation of visible abbreviation marks, and exclusion or verified subline cropping of restored/uncertain regions. These three pages are now development/review evidence; choose fresh acceptance data rather than calling them blind tests. NT-G0 remains partial until scholarly roles are appointed; the 10–20-page and 50–100-line milestones are not complete.

**Provisional training authorization — 2026-11-02:** the owner subsequently instructed “Continue with the plan and train.” Run one bounded, local C# research experiment while specialist adjudication remains pending. Filter out every line with supplied, unclear, apparatus, abbreviation or terminal-alignment flags; prepare new straightened narrow crops from the retained official Michigan width-2200 rasters and visually inspect them. Preserve the specialist packet unchanged. Use leaf 82 (1450/1460) for training and leaf 81 (1440) for development, with no blind test. Targets are provisional source Greek with editorial spaces removed, not adjudicated diplomatic text. Freeze the subset, source hashes, transform, train-only alphabet, seed and 120-epoch budget before training; reject impossible CTC alignments. Record untrained and trained CER/loss, checkpoint selection, reload equality and per-line predictions. Keep the model separate from existing packages and unpublished. This experiment does not complete NT-03/05/07/08, the reviewed-volume gates, a scholarly accuracy threshold, or cross-witness/no-regression evaluation.

**Training diagnostic extension:** preparation QA reduced the subset to 17 train / 7 development lines, excluded lower 1460 rows with clipping/neighbor ink, refined proposed centers within bounded ink windows, and selected grayscale after thresholded-input QA failed. The fixed 120-epoch run's selected checkpoint produced blank development text; recorded development CER equals 1.0 throughout the history. Intermediate strings were not saved. Preserve it as v1. A separately recorded v2 repeats the same frozen subset/transform/seed with a fixed 1,200-epoch budget to check learnability, without changing the data or treating development as an untouched test. Report both outcomes and latest/selected metrics; do not repeat budget searches indefinitely.

**Provisional training completed — 2026-11-02:** [the experiment report](docs/evaluation/p46-provisional-training.md) records both runs, exact preparation/label policy, source/crop hashes, learning curves and per-line predictions. v2 trained 1,200 epochs in 144.20 seconds; selected epoch 1,164 has train CER **67/578 = 11.59%** and development CER **162/219 = 73.97%**, versus blank development output in v1. The selected package reload reproduces every decoded string; independent evidence audit confirmed metrics and checkpoint identity. This demonstrates local training/selection/reload, with poor held-leaf recognition and substantial overfitting. It does not establish reliable transcription, Classical Greek coverage, model parity or a usable release. All 81 specialist decisions remain pending and the original packet ZIP hash is unchanged. Next, the corpus/reviewer instance owns NT-11 adjudication and NT-03/05 expansion; the training instance consumes accepted versioned lines/geometry/leaf IDs and freezes a larger experiment only after that handoff. Existing production models remain in place.

**Development-error investigation completed — 2026-11-02:** [the evidence report](docs/evaluation/p46-development-error-investigation.md) finds 96 deletions and 66 substitutions across all seven development lines, with only 123 output characters for 219 reference characters. Training has 64 deletions and three substitutions. The learning curves confirm poor generalization/overfitting on a tiny corpus; visibly fainter development ink is a supported appearance difference whose causal impact still needs an experiment. A fixed beam-8 inference comparison worsens development CER to 74.43%, despite slightly reducing deletions. Unknown labels, sigma folding, impossible CTC lengths, actual batch padding and a train/inference preprocessing mismatch do not explain this run. Independent audits found no confirmed current pipeline defect accounting for the score. Two inactive issues—implicit train/eval modes and zero/black padding or exposed augmentation borders—must be addressed before experiments that activate them. The investigation changes no weights and does not begin another training search.

### P46 next-run protocol — 2026-11-02

Objective: determine whether reviewed, more representative data and controlled image preparation improve within-P46 recognition. The previous 73.97% CER is historical evidence, not a matched baseline for a changed corpus. Do not claim that normalization, augmentation, larger models or more epochs have already solved the problem.

**Parallel ownership:** Instance B/corpus owns item rights, source crosswalk, scholarly labels, reviewed crop bounds and the immutable dataset revision. A Greek reviewer adjudicates readings/target policy; AI review alone does not populate accepted fields. Instance A/training owns diagnostics, C# preparation/training/evaluation, focused regression tests and run artifacts. B can expand/review data while A implements telemetry and mode/background tests. A's larger experiment depends on B's accepted handoff; both instances review the final source/split/label contract before it is frozen. Do not edit one another's owned records in place.

Keep reserved acceptance images and strings in a separate evaluator-owned package. Share only its IDs, counts, rights and hashes with the training instance until recipe/criteria are frozen. The corpus handoff gives A the training/development images and accepted labels; it does not expose acceptance contents.

| Step | Owner and dependencies | Concrete work | Acceptance and handoff |
| --- | --- | --- | --- |
| **D1 — validate and expand the corpus** | B + Greek reviewer; NT-02/03/05/11 | Review the current 24 selected labels against wider context, particularly faded 1440 and ragged 1450 edges. Confirm row identity, every surviving character, source/corrected target state and exact ink bounds. Retain original editions/decisions; write corrections as a new revision. Expand toward at least **100 usable training lines on four physical leaves**, **30 development lines on two additional leaves**, and reserve **30 acceptance lines on two further fresh leaves**. These are experiment-sizing targets, not sufficient-data or accuracy guarantees; rights and review may reduce actual usable counts. Include independently identified faded/clear conditions in training and development. | Handoff `P46-reviewed-v1`: exact asset/line IDs, rights/attribution, source and derivative hashes, coordinate transforms, accepted visible strings/profile, exclusions and reasons, reviewer/revision, physical-leaf/split assignments, per-page quality and character counts. Zero known wrong-row links, clipped target ink, restored-label characters or unresolved accepted spans. Flag classes with fewer than ten training occurrences and any development out-of-vocabulary items; never silently discard them to improve CER. |
| **D2 — make experiments observable and preparation consistent** | A; runs in parallel with D1 | Log train/dev CER, insertions/deletions/substitutions, emitted/reference lengths, blank posterior/argmax fraction and fixed sample strings at declared evaluation intervals. Add explicit model train/eval handling and restore caller state. Make background polarity/fill explicit for variable-width batches and exposed augmentation borders. Use one versioned C# transform in training and inference. Keep originals and actual float-input previews. | Focused failing-before/passing-after checks for mode behavior when dropout is enabled, deterministic evaluation, resumed training, white-background padding/shift borders, valid CTC lengths and same input/output when the shared transform is reloaded. Exercise both existing polarity conventions before changing shared defaults. Existing supported behavior must pass its relevant checks; v2's saved equal-width, dropout-zero outputs remain a control. Handoff versioned transform/config schema and test evidence to B for dataset compatibility review. |
| **D3 — verify learnability before the matrix** | A; reviewed D1 subset + D2 | Choose 4–8 clear reviewed **training-only** lines. On unchanged baseline architecture and greedy decoding, attempt near-complete fitting under a fixed cap of **4,000 optimizer updates**. Check finite losses/gradients, source-to-tensor correspondence and CTC repeat feasibility. Stop when the control passes or reaches its fixed cap; do not let default patience terminate the initial alignment plateau. | Require **≤2% training CER**, nonempty output on every control line and recorded edit/blank statistics. This is a technical control, not held-out quality. If it fails, stop the larger matrix and investigate labels/crops, gradients, polarity, sequence scale and optimization; do not compensate with an indefinite epoch search. Preserve failed controls. |
| **D4 — run a bounded matched comparison** | A executes; B checks immutable inputs; D1–D3 passed | Run T0/T1/T2 below, at seeds **42, 1729, 2026**, identical labels/splits/architecture/batch 4/learning rate 0.003 and a matched cap of **6,000 optimizer updates** per run. Derive whole-epoch count from `floor(6000 / ceil(trainLines/4))`, freeze the resulting actual update count before execution, and use it in every condition. Disable patience-based early stopping (or set patience at least to that frozen epoch count) so blank-output plateaus cannot truncate the comparison; still stop and record nonfinite/error failures. Log both CER and loss; select checkpoints by development CER. | Save all results from at most nine runs, seed median/range, per-leaf and clear/faded errors, deletion/substitution/length/blank metrics, learning curves, selected/latest checkpoints, reload parity, training time, inference throughput and peak memory. Archive failures. Reject destructive preprocessing before training; if T1 fails QA, do not run T1/T2. Do not tune against reserved acceptance leaves or resample lines across leaves. |
| **D5 — choose or stop** | Evaluator + A/B; D4 complete | Compare changes against T0 retrained on the **same new corpus**, with identical scoring. Freeze the selected recipe and planned acceptance evaluation before opening the reserved leaves. | Proposed research-selection gate: at least **5 percentage points lower median development CER**, improvement in at least two of three seeds and no development leaf’s median CER across the three seeds worsening by more than five points; report paired-seed values, all edits and exceptions. These are experiment-selection criteria, not scholarly release thresholds. Run the existing profile-matched regression checks before any shared-path/default promotion, and report throughput/RSS against the matched baseline. No automatic default replacement or claim of unchanged performance without that evidence. If no condition passes, stop after this matrix and inspect errors/coverage before authorizing a different architecture or data expansion. |

The comparison changes one factor at a time:

| Condition | Preparation and training change | Reason and control |
| --- | --- | --- |
| **T0 — matched control** | New reviewed geometry/labels; current height-32 grayscale, hidden size 64, downsample 4, dropout 0, weight decay 0, no augmentation; explicit corrected mode/background contracts. | Separates the effect of the new corpus from algorithm changes. All conditions share exactly this corpus and geometry. Old v2 is rescored only on compatible revised labels as an additional historical diagnostic, with incompatible targets reported explicitly. |
| **T1 — local illumination normalization** | Same as T0, plus one fixed local-background recipe before resize: luminance background from 90th-percentile values in 64×32-pixel tiles, bilinear interpolation, division by `max(background, 0.05)`, clip to `[0,1]`. Freeze constants after **training-image-only** visual QA; retain grayscale originals. | Tests the measured appearance difference without global Otsu or restoring ink. Inspect resulting tensors for faint-stroke loss, edge artifacts and amplified fibers; reject a destructive recipe before training. Do not tune tile sizes against development scores. Record normalization in the package and use it identically at inference. |
| **T2 — modest photometric augmentation** | Same as T1, adding training-only gamma sampled uniformly from `[0.9,1.1]` on luminance `[0,1]`; leave development untouched. Keep vertical shifts, erosion, blur and salt/pepper noise disabled. | Tests robustness to appearance variation as a single additional factor. Implement in C# with deterministic seeded behavior and checks that geometry, labels and black/white endpoints are preserved. Compare T2 with T1, and with T0 for overall selection. |

Before retraining, D1 may correct the source-reference sigma/case policy, but every condition must use the same reviewed profile. Sigma folding recovered no present errors and is not the primary corrective experiment. Keep leaf 82's surfaces together; page 1440 remains disclosed development material and cannot become training data while retaining its development role. Fresh acceptance material is held until recipe/criteria are frozen; a second witness is still required for generalization claims, and Classical Greek needs its own reviewed corpus.

Do not add dropout merely because overfitting is visible: its mode semantics must first pass D2, and this matrix already has a bounded scope. Larger input height, tighter sequence scaling, weight decay, bidirectional/context architecture or authorized transfer learning are follow-up hypotheses only if the reviewed-data/control/normalization results justify them. No training is started by this planning update.

**D2 authorized engineering completed — 2026-11-02:** [the implementation and verification record](docs/evaluation/training-engineering.md) documents explicit modes with caller-state restoration, per-image background metadata with legacy/JSON compatibility, safe batch/augmentation fill and opt-in training diagnostics. `EvaluateDetailed` and scheduled history record canonical scalar edits, lengths, valid-frame blank statistics and fixed samples. The CLI exposes `--diagnostics-every`/`--diagnostics-samples`; model manifests retain the settings and history. A shared decoder reuses each bounded evaluation minibatch's logits rather than forwarding the whole dataset again. Failing-before tests reproduced mode/padding defects; final verification passed **125 ML + 17 CLI + 13 application tests**, and the serial solution build has zero warnings/errors. Saved P46 v2 inference matches all 24 original predictions, and its receipt/weights are unchanged. No new P46 training or specialist acceptance occurred. Broader generic PAGE/application preprocessing unification remains NT-16b and must preserve old package polarity/preparation semantics; do not mistake this completed subset for that migration.

**NT-16b preprocessing completed — 2026-11-02:** [the contract and verification record](docs/evaluation/preprocessing-contract.md) documents one versioned C# pixel transform for declared P01-Otsu, legacy inverted-grayscale and ordinary luminance profiles. Configuration, checkpoints and manifests retain recipe/version/height/geometry; explicit profiles participate in config hashes and exact resume agreement. New typed packages use format 2 so old runtimes reject unsupported preparation, while this runtime still reads format 1 and preserves historical route defaults when metadata is absent. PAGE loading, evaluation, application/task recognition, image/XML CLI, project/surface workers and the API propagate the profile and model height. Generic page routes reject external geometry; the API returns 422 without creating L0 revisions. Legacy pixel, threshold and byte-order quirks are preserved, and malformed/conflicting contracts fail before native loading or package copying. Final checks passed **219 selected tests**, full serial solution build with **zero warnings/errors**, and exact replay of all **24** saved P46 predictions. The frozen receipt/weights remain unchanged; no Greek model retraining or scholarly label acceptance occurred. The next corpus handoff must include the declared pixel profile plus separate reviewed source/crop transforms; P46 rectification and the proposed T1 normalization remain distinct future work.

**Two-hour provisional training session — 2026-11-02:** the owner instructed “Continue with the plan and train for the next 2 hours.” Wall-clock start **15:37:39 CST / 21:37:39 UTC**, cutoff **17:37:39 CST / 23:37:39 UTC**. Preserve prior packages, source images, pending specialist worksheets and all existing split assignments. Specialist adjudication remains absent, so this is a separately versioned provisional engineering study, not completion of reviewed NT-15/17/18 or a release gate. Reuse the frozen 17 train / 7 development lines from v2, with both leaf-82 surfaces in training and leaf 81 development; no blind evaluation. Implement the already planned fixed local-background normalization and modest gamma augmentation with regression tests, inspect actual transformed inputs, then run a training-only 4–8-line control under 4,000 optimizer updates. If that provisional technical control reaches ≤2% training CER, freeze and run T0/T1/T2 at seeds 42/1729/2026, at matched ≤6,000 updates per run. Keep all results and diagnostic samples; score every condition against a new matched T0 using the shared input contract. Report failed controls and stop the matrix on an unresolved control failure; use remaining authorized time for cause analysis, recovery/resource checks and reproducible reporting, without an indefinite development-set search. Do not represent AI inspection as specialist acceptance or deploy a model by default.

**Control failure and bounded diagnosis — 15:53 CST:** the first frozen four-line raw-grayscale control completed all 4,000 updates, with best fit CER **32/134 = 23.88%** at epoch 3,986; the matrix was not started. Final error is mostly deletion (29 deletions/five substitutions), and retained previews still contain the expected rows. Its 1,431,225-byte diagnostic manifest also exposed a writer/loader inconsistency: the writer emitted a package exceeding the loader's 1 MiB manifest limit. Preserve this failed run, its weights/predictions and original protocol. Fix packaging by retaining bounded full training details separately while keeping the model header within its existing limit, with integrity/recovery tests. A separately frozen revision-2 diagnostic will run **one** additional four-line control with T1's already specified input-normalization recipe, using identical labels, seed, architecture and 4,000-update cap. This tests a concrete input remedy, not more epochs or development-label tuning; T0 failure remains part of the evidence. No matrix proceeds unless that technical input-remedy control passes. Specialist-dependent gates remain open regardless.

**Sequence-scale diagnosis — 16:10 CST:** revision 2's normalized height-32 control also completed 4,000 updates and failed: best CER **31/134 = 23.13%** (epoch 3,656), final CER 33.58%. The package now reloads and preserves full history; raw-control recovery reproduced all four outputs and 4,000 history entries with a 718,956-byte header. No matrix has run. Register one additional height-16 normalized control on the same four training-only lines, seed 42 and 4,000-update cap. This uses an existing supported input height, reduces the CTC sequence from 212 to 106 frames and the recurrent input feature dimension, and tests the sequence/feature scale hypothesis without new labels or development scoring. Preserve both failed height-32 controls; any subsequent matrix requires a passing technical control and a new matched baseline at its chosen input height. This is bounded training-only diagnosis, not fulfillment of the reviewed-data gate.

**Provisional control passed — 16:17 CST:** the registered height-16 normalized control reached **2/134 = 1.49% training CER at 1,594 updates**, with exact package input replay and decoded reload parity. This establishes technical learnability for this particular unadjudicated subset and input scale; it does not establish label truth or held-leaf accuracy, and reducing height also changes recurrent input features/resampling. Proceed with the frozen revision-3 matrix at **height 16**, otherwise retaining T0/T1/T2, seeds 42/1729/2026, 17 train / 7 development lines, greedy decoding and 6,000 updates per run. Each run gets a separate process; compare against the new matched height-16 T0, not the old height-32 73.97% measurement. No new source pages enter this matrix. Reviewed NT-15/17/18 and release gates remain open.

**Matrix closed — 16:27 CST:** all nine runs completed their 6,000 updates and package input/reload checks. Independent audit recomputed all 216 selected per-line predictions. Development CER medians/ranges are **T0 77.17% (77.17–78.08)**, **T1 79.45% (78.08–79.45)** and **T2 78.54% (76.26–79.91)**. T1 wins zero paired seeds against T0; T2 wins one. Neither reaches the ≥5-point median improvement and ≥2/3-win research screen; one exposed development leaf also cannot satisfy the full D5 criterion. The favorable T2/1729 result is not promoted. Final training CER reaches roughly 1.6–2.6% while final development error remains very high, so the small corpus/generalization gap persists despite resolving technical fitting at smaller input scale. Stop this preprocessing search and preserve every result. Remaining session work is recovery/resource/regression evidence and corpus handoff, not additional development-driven model selection. The runtime's zero peak-memory values are unavailable measurements, not zero-RAM results. [Six additional source candidates](docs/data/p46-expansion-candidates.md) now have item-level Michigan PDM and exact TEI CC BY notices checked; their images/labels remain unreviewed, unassigned and outside this study.

**Recovery and regression evidence — 16:47 CST:** [the study report](docs/evaluation/p46-two-hour-study.md) retains all selected/final scores and learning curves. On copied checkpoints, continuous continuation and intentional interruption/recovery produce identical parameter hashes and 24 predictions at update 6,020, exercising typed normalization, gamma state and optimizer reload. All 170 indexed matrix artifacts and eight original v2 receipt files still match their hashes. Full serial solution build passes with zero warnings/errors; 450 regression tests pass across ML, CLI, application, worker and API. A package publication edge found in independent review is being fixed before final closure; resource measurements and next-source staging remain session work. This does not reopen the closed matrix or accept any specialist decisions.

**Resource and source handoff — 16:54 CST:** fixed-seed inference with the frozen binaries passes all 72 input/prediction checks. For 24 lines, raw T0 combined median is **13.72 ms**, versus **318.63 ms T1 / 322.85 ms T2**; normalization preprocessing dominates the added cost. Darwin fresh-process peak RSS is **432.81 / 428.27 / 446.11 MiB**, covering initialization/verification/inference, not training or steady state; training peak RSS remains unavailable. Register only an exact-output implementation optimization (tile order-statistic selection and cached interpolation brackets), requiring pixel-bit parity and a separate resource comparison, without changing the recipe or reopening quality selection. [The current corpus handoff](docs/handoffs/NT-PILOT-B.md) also delivers a portable six-page source-review packet with preserved rights/TEIs and blank worksheets. All new source identities/labels remain pending specialist review, with no split or training eligibility.

**Durable parallel next assignments:** [B2](docs/tasks/p46-reviewed-corpus.md) owns reviewed source/geometry/target release and exposure accounting; [A2](docs/tasks/p46-next-training.md) owns the compatible C# loader, registered technical control/comparison and regression/resource evidence. Each has exclusive new paths, acceptance criteria and a versioned handoff. B2 source/review preparation and A2 synthetic loading checks can proceed independently. Actual fitting depends on an accepted, hashed dataset revision; reserved content stays with the evaluator. These briefs preserve the negative-study stopping rule and do not automatically start another search or accept AI-generated review decisions.

**Remaining volume candidate gap narrowed — 16:59 CST:** [earlier Michigan leaves78–80](docs/data/p46-earlier-leaves-candidates.md) have six item-specific PDM image manifests crosswalked to NTVMR1370–1420. Together with inventoried81–85 they propose eight physical leaves, not eight reviewed leaves. No new TEI bodies or image pixels were acquired/opened in this metadata-only pass; text rights/content, image availability, identity and usable counts remain unverified. Keep these separate for the evaluator's exposure/protocol decision. The 3605/3604 mismatch demonstrates why APIS IDs must be checked against actual capture/folio evidence rather than generated by sequence.

**Final engineering checks — 17:12 CST:** independent reviews reproduced/fixed reserved output directories causing partial package publication, target crossing within the existing best-checkpoint tolerance, and deferred native tensor-alias cleanup. Three minibatch/call scopes preserve exact synthetic Adam/resume state and all 48 frozen tensors / 216 selected P46 strings; this is not a permanent-leak or training-RSS claim. Exact-recipe optimization reduces normalized combined latency by 20–23% in controlled inference, while normalized inference remains about18× raw. All **475 selected regressions** pass and the full solution build has zero warnings/errors. A separately assembled 99-file training-source dependency supplement also builds cleanly and records its reconstruction limits. The tracked [provenance correction](docs/evaluation/p46-source-provenance-correction.md) supersedes the copied attribution file's incorrect rectangle description without changing frozen artifacts. No additional accuracy search or model promotion occurred.

**Scoped resource comparison — 17:16 CST:** all 144 input hashes/strings pass in the final separate inference comparison; prior61 resource evidence files remain unchanged. Before/scoped peak process RSS is **432.66→199.23 MiB T0, 428.53→195.28 T1, 428.72→195.52 T2**, roughly54% lower observed peaks on this workload. Combined median latency changes are −0.39% to +1.67%, with one process per condition/variant; no universal guarantee is inferred. Normalized inference still costs about19× raw. NT-20 records the remaining actual-training-RSS, final lifecycle and broader sustained/platform measurements; these gaps are not filled by alias counts or inference peaks.

**Final bounded training-resource probe registered — 17:24 CST:** use remaining authorized session time for six engineering-only processes: frozen versus current scoped implementation, T0/T1/T2, seed1729, CPU threads1, original height16/hidden64/batch4/LR0.003 and existing17 training lines only. Execute200 `TrainEpoch` calls =1,000 optimizer updates per process, maximum6,000 resource-probe updates. Do not load development inputs/labels, compute CER, select/package a quality candidate or change the closed matrix. Capture OS process peak RSS/time, tensor-alias boundaries, actual updates and exact workload/config/DLL hashes. Preparation is once, outside the training timer but within whole-process RSS. Cap each process at60seconds; do not start after17:32 and stop all work at17:37:39. If setup cannot meet that bound, retain the unavailable measurement. These are resource workloads, not additional accuracy comparisons; original full-budget matrix training RSS remains unavailable and NT-20's broader lifecycle/platform gates stay open.

**Training-resource result — 17:28 CST:** all six registered processes finish by17:27:25, exactly1,000 updates each, without timeout/development/evaluation/CER/checkpoints. Frozen/current peak RSS is **308.41→203.13 MiB T0, 306.42→193.39 T1, 312.52→193.39 T2**; training timers are6.040→6.119s,6.069→6.116s,6.781→6.319s. One process per row, whole implementation bundle and whole-process peaks preclude a universal or isolated-scope speed/memory claim. All102 training-input hashes and1,200 finite-loss calls pass; loss-sequence digests match, without assuming bit reproducibility. These6,000 engineering updates are separate from the63,594 accuracy-experiment updates and40 recovery-branch updates. Prior101 resource files remain unchanged. NT-20 becomes partial: this bounded no-validation/checkpoint workload does not reconstruct original full6,000-update matrix peaks or close broader lifecycle/platform checks. No new accuracy candidate or promotion occurs.

**Specialist packet prepared — 2026-11-02:** [the packet record](docs/data/p46-specialist-review-packet.md) documents a portable offline HTML/ZIP package with 81 proposed line-context crops, original TEI/manifests, full-page review images, source apparatus/uncertainty/overline display, 18 specific questions and blank CSV worksheets. Crops and row associations are provisional, with damaged terminal alignments explicitly uncertain; they are not accepted training geometry. All decisions remain pending. Independent review and visual/browser checks verified the packet, and source/crop hashes and coordinate maps preserve provenance. The packet has not been sent to anyone and no training or acceptance of labels occurred.

**Timed session closed — 17:38:10 CST / 23:38:10 UTC:** the authorized15:37:39–17:37:39 CST window is complete. NT-19 moves to completed.md;475 tests/build and frozen/source receipts pass. Preserve all negative/failed controls and the closed matrix, with no model promotion. The final registered training-resource probes are complete and separately counted; reviewed NT-15/17/18 and remaining NT-20 lifecycle/platform gates stay open. [Final report](docs/evaluation/p46-two-hour-study.md), [A1.1 engineering handoff](docs/handoffs/NT-PILOT-A.md), [B source handoff](docs/handoffs/NT-PILOT-B.md) and the disjoint A2/B2 briefs provide the next starting point.

### Second authorized two-hour session — 2026-11-02 evening

The owner again requested “Continue with the plan and train for the next 2 hours.” Wall-clock start **18:07:20 CST / 00:07:20 UTC on November3**, cutoff **20:07:20 CST / 02:07:20 UTC on November3**. NT-21 is a new provisional engineering/data-expansion session, preserving every prior protocol/result and all pending expert decisions. A short asynchronous check asks whether new specialist decisions exist; absent such evidence they remain pending. Renewed provisional training is authorized, but it does not satisfy the reviewed-data gates.

The negative T1/T2 result does not justify another normalization search. The next bounded hypothesis is **additional conservatively paired training examples**, using only the six already staged Michigan surfaces1470–1520 (leaves83–85). An independent corpus instance and a separate AI source/crop reviewer inspect exact images/TEIs, flags, identity and geometry. They exclude supplied/unclear/apparatus/abbreviation/continued-word and doubtful terminal/geometry records without forcing a line quota. All accepted diplomatic fields remain blank; training references are explicitly provisional under the same whitespace/NFD/source-case/sigma conventions. Do not open earlier78–80 images/TEIs or assign them acceptance status. Old17 train lines on leaf82 and7 exposed development lines on leaf81 remain unchanged; all new83–85 siblings are training-only. Preserve actual source/crop transforms, exclusions, licenses, hashes and exposure counts.

Root owns shared plans, a new artifact training harness and experiment report. The corpus agent owns its new expansion artifact prefix/report; the independent reviewer owns a separate review report/allowlist. A third agent investigates the trainer's owned optimizer final-use lifetime under NT-20 and may implement a narrow demonstrated IDisposable correction, preserving caller-owned models and existing APIs/modes/resume. No other instance edits those files. Freeze production binaries and the source/crop allowlist before comparison. If source/geometry confidence is inadequate, retain exclusions and use remaining time for lifecycle/resource/reproducibility checks rather than manufacture labels.

Register a raw-luminance height16 training-only control on the same four historical training IDs, seed42, batch4, unchanged hidden64/downsample4/dropout0/LR0.003 and≤4,000 updates. Require≤2% training CER and nonempty outputs; preserve failure and stop the larger comparison if it fails. If it passes and expansion QA passes, freeze two data conditions (old17 versus old17+new conservative candidates), three seeds42/1729/2026, the same shared **training-only** alphabet and architecture/preprocessing. To compare equal full-batch sample presentations, uniformly replicate each condition's complete unique training list only enough to make its size divisible by4; disclose copies/weights, which do not increase unique corpus counts. Choose a common exact optimizer budget≤6,000 using the least common multiple of the two per-epoch batch counts. Freeze it after corpus counts, before scores. Primary comparison is **final fixed-budget development CER**, with equal updates and four presentations per update; development-selected checkpoints are secondary diagnostics because validation-per-epoch frequency differs. Record matched optimizer-step diagnostic boundaries, actual epochs/update/presentation counts, final and selected strings, canonical edits, package/input replay and resources. Proposed provisional screen is≥5 points lower median final development CER and wins in≥2/3paired seeds; one exposed development leaf cannot establish the full scholarly/generalization gate. No automatic promotion follows. Stop after this registered comparison, report every seed/failure and update reviewed-data/coverage dependencies. No extra architecture/decoder search is authorized by this registration.

**Prospective leaf diagnostic added before any scores — 18:29 CST:** independent protocol review recommends one separately registered diagnostic to distinguish the old exposed leaf81 gap from broader within-witness failure. Freeze all three new physical-leaf folds83/84/85 at seed1729 only, excluding both sides of the held leaf from training while retaining original leaf82 training. With the independently frozen27 new rows, fold training counts32/34/39 give uniform whole-list replicas1/2/4 and8/17/39 full batches; the shared budget is **5,304 updates /21,216 presentations**,663/312/136 epochs. Raw height16 and all architecture/optimization settings remain unchanged. The alphabet comes from the original17 training lines only; report/abort on OOV without enlarging it from held labels. Fit may use already exposed leaf81 as secondary validation, but the new held-leaf labels are scored only at the **final** fixed-budget checkpoint. Report every held-leaf and leaf81 edit/count/result without thresholds, parameter/seed/recipe selection, retries or promotion. This is model-held-out, source-exposed, unadjudicated diagnostic evidence, not blind acceptance or an unseen hand. Source appearance, geometry, text content and label uncertainty confound any causal interpretation. A separate artifact-only C# harness and immutable registration are required; execute only if the raw technical control passes and after the main six runs. This is the final registered accuracy-related work for this renewed session; do not add a post-result search. Registration is recorded at18:29:26 CST, SHA-256 `5fe4e1646d9d16cec2886e55f7b7fe51425c65593854b15b8ef7dde51bfd7bf1`.

**Control passed and comparison frozen — 18:32 CST:** the new raw-luminance height16 four-line control reaches **2/134 =1.49% CER at1,655 updates**,18.75seconds, with nonempty outputs and exact latest/secondary-package reload. All24 historical raw input hashes agree before fitting. Independent corpus/reviewer checks freeze27 new rows/926reference scalars, yielding44 unique train lines/1,504scalars while development remains7/219 on leaf81. The harness validates all132 corpus files, exact independent per-ID text/crop/source hashes, both allowlists and source receipt before preparation. Old17 is uniformly repeated4times (68instances/17fullbatches), expanded44 once (44instances/11batches), giving a shared **5,984 optimizer updates /23,936 presentations per run**,352versus544epochs. Scheduled diagnostics share187-update boundaries; history training counts include replicas while finalTrain reports unique lines. Freeze old/expanded seed pairs in order42,1729,2026; primary is final checkpoint, secondary development selection is not the research screen. Production/source/DLL receipts and exact51 inputs are retained in the new artifact folder. No comparison scores have been consumed at this registration point.


**All registered accuracy fits closed — 18:51 CST:** the new 27-row/926-scalar source-reference expansion passes independent technical QA, yielding44 unique training lines/1,504scalars and the unchanged7 development lines/219scalars. It is not expert-adjudicated; all accepted fields remain blank. The six matched comparisons complete5,984updates each. Primary FINAL median development CER is **81.2785% old17 versus80.8219% expanded44**, a **0.4566-point** gain with wins in2/3paired seeds, failing the registered≥5-point screen. Secondary selected medians worsen77.17→78.08%; the favorable expanded2026 secondary75.34% is not promoted. All three prospectively registered leaf folds finish5,304updates each: held83=355/418(84.93%),84=259/332(78.01%),85=137/176(77.84%), pooled751/926(81.10%) across three different models. These source-exposed, unadjudicated within-witness diagnostics are not blind or unseen-hand evaluation. The high errors weaken an explanation limited to faint page1440, without isolating a cause. Total real-P46 training is53,471updates including the1,655-update control. No further accuracy fit or post-result recipe/seed/decoder search is run in this session. [Full study](docs/evaluation/p46-expanded-training-study.md), [leaf report](docs/evaluation/p46-leaf-diagnostic.md), [independent603-record audit](docs/evaluation/p46-expanded-study-independent-audit.md).

**Appearance and resource interpretation:** all51 exact float inputs pass the registered appearance audit, including historical24-input replay. Character and leaf counts expanded; faded-condition coverage is **not established** by mixed brightness/contrast proxies. Main process OS peaks are old212.70/210.16/215.00MiB and expanded213.19/197.17/222.09MiB; these single whole-process observations include initialization, fit, evaluation and packaging, and differ in validation frequency. They are not isolated throughput or steady-state memory comparisons. Python artifact scripts parse/filter TEI and construct rectified PNGs with exact sourceY maps; production C# transforms supplied crops to floats and trains/infers. External full-page rectification is not an implemented C# inference route. [Corpus](docs/data/p46-expanded-provisional-corpus.md), [appearance audit](docs/evaluation/p46-expanded-appearance.md).

**Post-fit engineering verified — 19:23 CST:** three narrow corrections now have before/after evidence: deterministic disposal of trainer-owned Adam state while preserving caller models; training-only CLI alphabets with explicit early development-OOV/source diagnostics; and disposal on failed digest-valid model-weight loading while preserving exceptions and successful caller ownership. Optional SourceId preserves construction/null-JSON compatibility but can affect record equality; that distinction is exposed for review. A parameter-transfer control invalidates the exploratory zero-`Tensor.TotalCount` oracle, so final loader tests use validated thread-local disposal statistics and explicit handle/ownership controls. All superseded evidence is retained. Full serial solution build passes0warnings/0errors; the named combined set passes493tests,0failures/skips; an inference-only C# replay preserves851saved strings including240historical records. No retraining follows the source changes. The [maintainer handoff](docs/handoffs/p46-evening-maintainer-review.md) freezes a five-production-file/three-test-file patch and21-file packet, hash`cd44091825aa492448913ccbfacc0294cae59a85545e52a0503aed4ce9245c05`, without copying images/weights or altering the index. Human review/DCO/acceptance remain pending. NT-15/17/18 and broader NT-20 gates stay open.


**Bounded lifecycle stress passed — 19:35 CST:** the separately preregistered artifact-only workload completes200serial plus4independent synchronous workers×100cycles,600successful model loads,1,200expected corrupt-weight failures and4,200exact checks of the same7already exposed saved prediction strings. Every iteration/worker/phase returns validated thread-local lifetime statistics to baseline and disposed model parameter handles are empty. A further100short-lived trainers complete200finite synthetic updates while preserving caller resources, without evaluation/CER/checkpoints. Whole-process maxRSS is256,589,824bytes(244.70MiB), descriptive candidate-only evidence. These are repeated route/lifetime checks, not4,200independent lines, new accuracy evaluation, matched throughput evidence or supported-platform proof. A separately registered longer fixed-count resource-only fork may sample actual OS RSS over time before20:00CST, without changing model selection or production code; its exact registration must precede execution and remains a separate supplement.


**Closed-output error audit — 19:39 CST:**69 final prediction records represent34unique source lines, not69independent lines:7old-development lines under6main models, plus27newly held lines under3different models. All are nonempty and none is exact. Expanded main predictions are shorter at every paired seed. Newly held I/D/S totals10/279/462 and70.95%reference-length output confirm both substitutions and shortening; surface1480 has92.8%length but92%CER, ruling out a length-only account. These deterministic minimum-edit alignments are tie-dependent descriptions, not localized glyph labels or a proven cause. [Error-pattern report](docs/evaluation/p46-expanded-error-patterns.md) proposes reviewed-reference sensitivity scoring, fresh evaluation data and fixed I/D/S/valid-frame diagnostics in a newly registered experiment, without launching one now. The separate [SourceId consumer audit](docs/evaluation/labeled-line-identity-audit.md) finds no current whole-line equality/hash consumer but retains the public external-client semantic change for maintainer review.


**Longer resource-only result — 19:46 CST:** the second registration completes25,000serial plus4×5,000independent-worker cycles,45,000successful loads,90,000expected failures and315,000repeated checks of the same7saved strings. All lifetime/handle/parity checks pass; no training, CER, checkpoint or selection occurs. **RSS growth remains unresolved:**226 OS samples increase240.41MiB first-to-last, chronological-quarter medians217.38/258.34/298.32/430.11MiB, and whole-process maximum496.02MiB. Sampled maximum478.45MiB excludes subsecond peaks; first sample occurs20.23seconds after serial work begins. No baseline comparison, memory-stability pass, allocator-cache explanation or proven native leak is asserted. Preserve exact DLL/model/input hashes and all samples; a separately registered five-route/GC discrimination probe can narrow follow-up scope before19:58, without production changes or a quality search. [Sustained report](docs/evaluation/sustained-lifecycle.md). NT-20 remains partial, and resource acceptance is not granted.


**Five-route resource discrimination closed — 19:50 CST:** five separately launched fresh processes each complete3,000fixed cycles (construct / success-load-no-inference / empty-load / truncated-load / retained-model inference), without retries, fitting, CER or production changes. All lifecycle/real-handle checks pass;6,000expected failures and21,000repeated saved-string parity checks are retained. Between cycles500and3,000 retained inference changes174.53→174.66MiB while construction and repeated loading/failure routes grow over that interval. Post-work collect/finalizers reduce managed live estimates substantially but hardly reduce RSS. Baselines include different first operations and each route has one short process, so neither allocator retention nor a native leak is established; forcedGC is not a production correction. Preserve45boundary/GC observations and all40registration/16execution pins. [Investigation](docs/evaluation/p46-memory-investigation.md) records a concrete next native-allocation/retained-object profiling task under NT-20. No more accuracy or resource probes are planned for this window.


**Second authorized session closed — 2026-11-02 20:07:32 CST:** the18:07:20–20:07:20CST wall-clock window is complete. NT-21 is archived verbatim in completed.md, now63items. All10registered accuracy fits/53,471P46updates and separately registered resource-only work are retained; no extra accuracy search occurred after the three prospective leaf diagnostics. The expanded-data screen fails and no model is promoted. Three narrow engineering fixes have before/after tests,493passing regressions and851exact saved-string replays; all339current source/test hashes still match final verification. Source packets/human fields and the frozen maintainer packet remain unchanged. Greek specialist adjudication, broader Classical Greek/hand coverage, fresh acceptance data, unexplained loading RSS growth and supported-route/platform profiling remain open. [Final study](docs/evaluation/p46-expanded-training-study.md), [review packet](docs/handoffs/p46-evening-maintainer-review.md), [closure receipt](.artifacts/experiments/p46-evening-report/SESSION-CLOSE.json) and [session index](.artifacts/experiments/p46-evening-report/SESSION-INDEX.json). Work was a timed training/engineering/review session, not a claim of two continuous CPU-training hours. No commit, human DCO, submission, deployment or release was created.

## Interchange and collaboration cycle — 2026-11-02 evening

The owner asked to continue `todo.md` for eight hours from **2026-11-02 21:00 CST**. This
session is code/evidence work, not a new P46 accuracy search. It preserves every prior
P46 protocol, result, pending specialist decision and artifact.

**Delivered:** a layered bounded TEI/EpiDoc profile (line splitting at `<lb>` with `@n`,
`@xml:id` and `break="no"`; L1 visible text; linked L2 supplied/expansion/regularized readings;
unclear/illegible/gap/alternatives/add/del/handShift; facsimile image links; `idno`/licence/
responsibility provenance; aggregated unsupported report; bounded checksummed original-markup
attachment; machine-readable export loss report), product `import tei` CLI/API and layered
export. Fixed EpiDoc bootstrap so apparatus and `<abbr>/<expan>` pairs are selected, not
concatenated, and supplied text no longer enters L1. NTVMR-style markup is handled without
losing word spaces: `<w>` spacing is preserved, non-numeric verse `@n`/`@part`, `<pb>`,
`<app><rdg type="orig"/><rdg type="corr">`, standalone `<abbr>` and `<hi rend="overline">`
map to line tags/L1/L2/sign annotations and round-trip (invented excerpt test; the fetched
public record was inspected only to shape that fixture and is not committed).

**Layout:** an opt-in two-column projection split accepts a clear vertical gutter, bands each
column independently and orders left then right (`layout detect --columns`, `?columns=true`).
It is backed by synthetic fixtures only; the default remains single-column and no real-page
accuracy claim is made ([evidence](docs/evaluation/layout-columns.md)).

**Shared editing:** project documents now carry an optimistic store version; stale saves fail
with `revision-conflict` instead of overwriting another writer. A scalar-level three-way merge
combines non-overlapping stale text revisions and refuses overlaps with both candidate texts;
the API exposes `merge: true` and the Angular editor requests it. Server-issued TTL sessions
(node/surface binding, heartbeat, revoke, list) provide the store-backed registry; presence may
derive identity from a session instead of a client-chosen string.

**Migration:** a read-only `migrate inventory` command classifies an existing tree by public
format, records sizes/SHA-256 and reports unclassified files without modifying it. Smoke on
the local M01 artifact tree inventoried 241 files (120 PAGE XML, 120 images, 1 archive); a
read-only run on the `~/Code/kraken` reference checkout inventoried 291 files/118.9 MB
(12 PAGE, 10 ALTO, 21 images, 13 model weights, 134 scripts) without changing it. A
`migrate import` smoke on the local M01 tree imported 120/120 PAGE records into a new project
with zero unsupported notes. It is an intake aid, not a migration or comparison claim.

**Evidence:** [interchange](docs/evaluation/interchange-v2.md),
[concurrency/collaboration](docs/evaluation/collaboration-concurrency.md) and the
[cycle handoff](docs/handoffs/interchange-collaboration-2026-11-02.md); new xUnit
coverage in Formats, Application, API, Core and Infrastructure; refreshed
[parity matrix](docs/release/parity.md) and operational docs. Remaining work is tracked in
`todo.md`: TEI schema pinning/validation, uncovered EpiDoc constructs, a session/presence UI,
merged geometry/annotation/review writes, and object/surface/fragment provenance. No
specialist adjudication, acceptance threshold, model promotion, deployment or release follows
from this cycle.
