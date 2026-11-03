<!-- Copyright (c) 2026 John Baima. SPDX-License-Identifier: Apache-2.0 -->
# eScriptorium ↔ LeviathanText integration evaluation

Date: 2026-11-02. Scope: can [eScriptorium](https://gitlab.com/scripta/escriptorium) be made to
use LeviathanText as its recognition/segmentation engine? Sources: eScriptorium `develop`
(`app/apps/core/models.py`, `app/apps/core/tasks.py`, `app/escriptorium/settings.py`,
`app/requirements.txt`) and this repository at `fc96d5b`. This is an engineering assessment,
not a claim about either project's quality or accuracy.

## Verdict

**Technically yes, but not as a drop-in and probably not as an upstream change.** eScriptorium
calls kraken as an in-process Python library, not through an engine abstraction. A real engine
swap touches segmentation, recognition, training, model files, model versioning, confidence
graphs, deployment, and the UI's model assumptions. The realistic options are:

1. **Format-level interoperability (recommended near-term):** move PAGE/ALTO/TEI between the
   tools. This works today at a bounded level and requires no changes to either codebase.
2. **Sidecar recognition service (recommended if engine-level integration is required):** keep
   eScriptorium, add stateless LeviathanText endpoints plus a small eScriptorium adapter. This
   preserves eScriptorium's UI and kraken training/segmentation, and uses LeviathanText for
   Greek line recognition.
3. **Fork eScriptorium with a second engine:** feasible as a research project, but a
   maintenance burden and unlikely to be accepted upstream without a generic engine interface
   and a long-term owner.

Replacing kraken inside the Python process (a Python shim that reimplements kraken's API on top
of LeviathanText) is not sensible: it would be a large compatibility layer around a different
model format and runtime.

The decisive question is **why** the integration is wanted. If the goal is better Greek OCR,
there is no evidence yet that LeviathanText beats the current kraken models; a head-to-head
benchmark is mandatory. If the goal is LeviathanText's scholarly transcription model
(uncertainty, gaps, revisions, loss reports), the better product move is to migrate the project
into LeviathanText, not to bolt LeviathanText onto eScriptorium.

## 1. How eScriptorium uses kraken

Current `develop` pins `kraken~=7.1.0` and `dfine-kraken~=0.4.4`, and uses PyTorch Lightning.
The integration is direct and structural:

- `app/apps/core/models.py` imports `kraken.configs`, `kraken.containers`
  (`BaselineLine`, `Segmentation`), `kraken.tasks` (`RecognitionTaskModel`,
  `SegmentationTaskModel`), and `kraken.lib.segmentation.calculate_polygonal_environment`.
- `DocumentPart.segment()` loads a `SegmentationTaskModel`, runs `predict()`, converts
  `bbox` results to baselines, and creates `Block`/`Line` rows from kraken `Region`/`BaselineLine`
  objects.
- `DocumentPart.transcribe()` loads a `RecognitionTaskModel`, builds a one-line
  `Segmentation` per line, calls `predict()`, and stores `prediction`, per-character `cuts`
  (as `graphs` polygons) and `confidences` in `LineTranscription`.
- `app/apps/core/tasks.py` runs Celery tasks for segmentation, recognition, segmentation
  training, recognition training, collection training, and forced alignment. Training uses
  kraken training configs/data modules and Lightning checkpoints; `convert_models()` writes
  `.safetensors`; `OcrModel` keeps per-epoch versions and detects architecture from safetensors
  metadata.
- `OcrModel.file` accepts only `.mlmodel`/`.safetensors`. Model cloning, qualification and the
  UI all assume kraken model files and kraken metadata.
- `DocumentPart.make_masks()` uses kraken's `calculate_polygonal_environment` to derive line
  masks from baselines.
- `forced_align()` uses `kraken.tasks.ForcedAlignmentTaskModel` to align existing text to
  character positions.

There is no `ENGINE` setting or backend interface: replacing kraken means editing these call
sites or introducing an abstraction first.

eScriptorium's license is MIT; LeviathanText is Apache-2.0. The licenses are compatible for a
combined deployment or a fork.

## 2. What LeviathanText supplies today, and what is missing

| Capability | LeviathanText today | Gap for eScriptorium |
| --- | --- | --- |
| Segmentation | Projection baseline + opt-in learned line detector; boxes/polygons; single-column, LTR focus | No BLLA/DFINE compatibility; layout models are not interchangeable; weak on columns/rotation; no region-type training parity |
| Line geometry | Baselines and polygons; manual edit/order; PAGE import/export | No line masks derived from baselines; adapter must map polygon↔mask and synthesize baselines from boxes |
| Recognition | CRNN + CTC, CPU, per-grapheme confidence, L0 provenance | No character cut polygons for eScriptorium `graphs` (only a line bbox and per-grapheme confidence); multi-scalar graphemes do not fit eScriptorium's one-character graph schema |
| Model format | Directory package: `run-manifest.json`, `.model` weights, metadata sidecar, model card | Cannot load `.mlmodel`/`.safetensors`; no export to kraken; no model-hub/upload/versioning API; model distribution is not shipped |
| Training | CLI/PAGE-based train, resume, package, model card | No API service, no in-app per-epoch model versions, no Celery/Lightning progress integration; training from eScriptorium's DB would need an export/import bridge |
| Direction/script | LTR, Greek-first; explicit no full BiDi/vertical recognition | eScriptorium is multi-script and supports RTL/vertical; LeviathanText cannot replace those paths |
| Scale/deploy | SQLite single-writer, single worker guidance, CPU-only; optional API key | eScriptorium is a multi-user Django/Postgres/Redis/Celery deployment; a stateful LT project store per page would not fit |
| Interchange | PAGE, ALTO, hOCR, TEI/EpiDoc, IIIF import; loss reports | Bounded mappings; no character-level graphs on PAGE import/export; no native eScriptorium model |
| Annotations | L0/L1/L2 revisions, uncertainty/gap annotations, review ledger | eScriptorium has transcription layers, annotation components/ontology and entity types; no direct API mapping |

Two gaps deserve emphasis:

- **Model ecosystem.** eScriptorium's value includes a large catalogue of community
  kraken models across scripts. LeviathanText packages are a different architecture and
  serialization; converting existing models is not practical. A deployment would have to
  retrain Greek models in LeviathanText and register them as a separate engine.
- **Character graphs.** eScriptorium's editor displays per-character polygons and confidence.
  LeviathanText has the confidence signal but not the cut geometry. Approximating cuts by
  dividing the line bbox is possible but degrades the review UI and is not equivalent.

## 3. Integration options

### A. Format-level interchange (recommended near-term)

Flow: eScriptorium exports PAGE (or ALTO) for a document part → LeviathanText imports it,
recognizes, and exports PAGE/ALTO → eScriptorium imports the result as a new transcription
layer.

- Works with existing features: eScriptorium has PAGE/ALTO import/export; LeviathanText imports
  and exports PAGE/ALTO with loss reports.
- Preserves geometry, text and layer separation (one eScriptorium `Transcription` at a time).
- Loses character graphs/confidences on the round trip, and both PAGE mappings are deliberately
  bounded; verify zone types, `line_offset`, and image filename resolution on a real project.
- Effort: a few days for a manual pilot; 1–2 weeks for a robust bridge that handles image paths
  (shared media volume or copy), project/layer naming, and loss reporting.

This is the lowest-risk way to let eScriptorium users try LeviathanText on Greek material.
It does not make eScriptorium "use" LeviathanText as an engine; it makes the tools exchange
data.

### B. Sidecar recognition service (recommended if engine-level integration is required)

Keep eScriptorium's UI, database and kraken segmentation/training. Add:

1. **Stateless LeviathanText endpoints** (not the current project-centric API):
   - `POST /segment`: image + options → regions, lines, baselines/polygons;
   - `POST /recognize-lines`: image + line geometries + model → per-line text, per-grapheme
     confidence (optionally cut polygons).
   These avoid creating a temporary LeviathanText project per page and avoid its SQLite store
   in the hot path.
2. **An eScriptorium adapter** in `core/tasks.py`: an `engine` field or setting on `OcrModel`;
   `segment`/`transcribe` branch to the HTTP client; map eScriptorium baselines/masks to LT
   geometry and back; send progress through the existing channels events; timeouts/retries.
3. **Model registry integration:** mount the LT model directory read-only or add a package
   upload/registration endpoint; expose LT packages in `OcrModel` as an engine-specific model
   type; disable or delegate kraken-only operations (model conversion, per-epoch versions,
   forced alignment) for those models.
4. **Deployment:** a .NET API image in the compose stack, internal network only, shared media
   volume for images, health checks. Run multiple stateless LT API replicas if needed.

Scope for a recognition-only pilot: segmentation and training stay with kraken; LeviathanText
does line recognition on Greek. This keeps the change small and makes the accuracy comparison
meaningful. Add LT segmentation only after that works.

Effort estimate, one developer familiar with both stacks: 6–10 weeks for a tested pilot
(1–2 weeks LT endpoints, 2–4 weeks adapter/UI/model handling, 1–2 weeks deployment/tests,
2–4 weeks benchmark and correction). Training and model-lifecycle parity are excluded and would
add substantially more. These are planning ranges, not commitments.

### C. Fork with a full engine abstraction

Replace segmentation, recognition, training, forced alignment, model versioning, and the model
UI behind a backend interface. This is the only way to make eScriptorium genuinely
engine-neutral, and it is also the option most likely to diverge from upstream:

- eScriptorium's steering committee includes kraken's author; kraken is the reference engine.
- A second engine means two model formats, two training stacks, two confidence semantics, and
  two sets of failure modes to maintain.
- The UI, API, import/export and tests assume kraken containers and `.safetensors` metadata.

Effort: 3–6+ months for production quality, with high uncertainty. Upstream acceptance is
unlikely without a maintainer-funded generic engine interface. A downstream fork or patch set
is the realistic form.

### D. In-process Python shim

Reimplement kraken's Python classes on top of LeviathanText (or call the .NET runtime from
Python). This buys compatibility at the cost of a large, brittle layer and gives up
LeviathanText's own contracts. Not recommended.

## 4. Risk register

| Risk | Severity | Mitigation |
| --- | --- | --- |
| Accuracy regression vs current kraken Greek models | High | Head-to-head CER on identical pages/lines, confidence calibration, correction-time study; do not claim parity before this |
| Model format/ecosystem lock-out | High | Treat LT as a separate engine; keep kraken models; retrain Greek models in LT; no conversion promises |
| Character graph/confidence mismatch | Medium | Add CTC cut geometry to LT, or accept line-level confidence and hide/adjust the graph UI for LT models |
| Training/versioning gap | High for full swap, low for sidecar | Keep kraken training initially; export LT training jobs later |
| RTL/vertical/multi-script | High for general eScriptorium | Enable LT only for LTR Greek models; keep kraken for other scripts |
| Deployment/operations | Medium | Stateless LT API, internal-only, shared media volume, API key, health checks, bounded request sizes |
| LT pilot maturity (TIFF, GPU, model distribution, single-writer SQLite) | Medium | Keep LT stateless and CPU-scaled for the pilot; do not use its project store as the system of record |
| Upstream divergence | Medium | Prefer the external bridge; if forking, keep the adapter isolated behind an interface and track upstream |
| Licensing | Low | MIT (eScriptorium) and Apache-2.0 (LeviathanText) are compatible; preserve notices and model/data rights separately |

## 5. Recommendation

1. **Do not port eScriptorium to LeviathanText as an engine now.** The cost is fork-scale, the
   model ecosystem does not transfer, and there is no accuracy evidence that justifies it.
2. **Ship interoperability first.** Validate eScriptorium PAGE → LeviathanText → PAGE on a real
   Greek project, with a written loss report. This is days-to-weeks work and answers the real
   user question: does LeviathanText produce better or more useful Greek transcriptions?
3. **If engine-level use is still wanted, build the stateless LT endpoints and a sidecar
   adapter** (option B), recognition-only at first. Keep the integration behind an `engine`
   seam so it can live as a downstream patch or a small upstream contribution.
4. **Run the benchmark before writing the adapter.** Reuse identical page images and line
   geometry; compare CER, confidence calibration, review time and throughput against the
   current kraken model. If LeviathanText does not win on the material that matters, the
   integration has no product case.

The project plan already takes this position: kraken's eScriptorium integration is "context,
not a dependency to reproduce or embed" (`project-plan.md`). This evaluation agrees, with one
addition: a clean stateless recognition API is worth building anyway, because it is the
smallest useful integration surface for any external editor, not just eScriptorium.
