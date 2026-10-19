# Evaluation and acceptance plan (three-track)

- Package: **B-PLAN v1**, document `docs/evaluation/acceptance-plan.md`, draft **rev 1**, 2026-10-18.
- Owner: **Instance B**. Companion documents: [../data/manuscripts.md](../data/manuscripts.md), [../data/papyri.md](../data/papyri.md), [../data/inscriptions.md](../data/inscriptions.md), [../handoffs/B-PLAN.md](../handoffs/B-PLAN.md).
- C0 contract consumed: **`C0-0.3-draft`** (proposal, not accepted). A published all five `docs/contracts/*.md` mid-assignment; B verified their hashes and aligned scoring to `examples.md` §1 and `interchange-and-api.md` (`B-REV-A-1` resolved).
- Scope: todo Sections 6–7 and 9. **No numeric target is invented here.** Every threshold waits for the measured pilot.

**Core principle:** a pooled accuracy number across manuscripts, papyri and inscriptions is forbidden. Results are reported **per track**, and within a track **per material/hand/capture profile**, with unsupported profiles disclosed as blockers, not omitted.

## 1. Roles and custodian separation

| Role | Responsibility | Constraint |
| --- | --- | --- |
| Project owner | Selects pilot collections; sets acceptable quality | Consumes the frozen acceptance sheet |
| Greek specialist / reviewer | Owns transcription rules and adjudication | Independent from the training worker |
| Engineering owner | Records hardware/performance budgets | No access to locked labels for tuning |
| **Evaluator / custodian** | Holds the **locked test set**; runs scoring; owns the report | Must be a role separable from the model developer |
| Data workers (D-M/D-P/D-I) | Produce development and candidate test labels | Publish immutable, versioned artifacts |

**Custodian rule:** Instance B (or whoever later owns ML) is **not automatically entitled to the locked test labels** (brief acceptance criterion; todo Section 6). Before any acquisition/tuning begins, the project owner names a custodian distinct from the training owner. Tuning uses development sets only; the locked labels are released to the custodian and scored once per frozen candidate.

## 2. Split design (leakage prevention)

- **Object-level, not line-level.** All surfaces, recto/verso, fragments, crops, alternate captures, alternate lightings, derived glyph crops and derived editions of one physical object stay in **one** split (todo Section 5).
- **Generalization strata:** keep separate `dev` (training/fine-tuning), `dev-test` (iteration), and `locked-test` (custodian). Locked test is neither trained on, prompt-exampled, threshold-set, nor used for model selection.
- **Holdout kinds:**
  - Unseen **object/witness** (e.g. one whole Stavronikita book; one papyrus item).
  - Unseen **hand** (e.g. M01 Camillos Zanettus folios vs Antonius Eparchos).
  - Unseen **collection/site** (e.g. an inscription site not in dev).
- **Cross-source duplication checks:** dedup by object/TM/inventory ID and image lineage; audit **P01↔P02↔P03↔P04** overlap explicitly (todo DATA-02). P04 crops inherit P03 images.
- **Repeated-passage risk:** the same literary text (e.g. Homer) in P02 and P05, or familiar gospel text, can hide memorization. Flag repeated passages and evaluate on unfamiliar passages separately.
- **Reuse disclosure:** if a locked set is ever used to guide a later model, add fresh held-out witnesses and disclose the reused set's status (todo Section 7).

## 3. What is scored, per modality

| Modality | Primary metrics | Must preserve / reject |
| --- | --- | --- |
| Raw recognition (diplomatic) | **Case-sensitive scalar CER** on canonical-equivalent visible text; grapheme error rate; raw-sequence diagnostics | Do not strip marks; canonical equivalence ≠ editorial normalization |
| Mark-specific errors | Separate omissions/substitutions/insertions for accents, breathings, diaeresis, iota subscript | **Incorrectly supplied** accents/breathings on unmarked text count against the score |
| Uncertainty | Excluded-span counts, abstention/coverage, accuracy-vs-coverage | Reject empty output or a broad uncertainty mask as an artificially good score |
| Missed/blank regions | Additions on uninscribed/blank areas and wholly missed lines are **failures**, not omitted samples | Reject silent skipping |
| Word layer | WER **only** where an annotated word layer exists | Score proposed word division separately for scriptio continua |
| Layout | Region overlap, baseline precision/recall, line assignment, reading order, text-region coverage | Disclose matching tolerances; include disconnected fragments, rotated inscriptions, marginal/interlinear text, crop loss of marks |
| End-to-end vs layout | Transcription on ground-truth layout **and** complete automatic-pipeline output | Localizes errors to layout vs recognition |
| AI-assisted | Raw vs candidate-reading vs accepted editorial output, separately | Measure unsupported restorations, overwritten witness variants, expert correction time |
| Training | Train/reload/infer parity, reproducibility tolerance, checkpoint resume, leakage detection, real-data generalization | Reject parity claims without a reload demonstration |
| Formats | Schema validation on pinned schemas; gap/unclear/supplied, abbreviation/expansion, hands, correction-layer round trips | Report unsupported/lossy fields; external XML entities disabled |
| Operations | pages/min, startup latency, peak RAM/VRAM, large pages, cancellation, retry, crash recovery | Bounded queues verified |
| Independence | dependency/file inventory proves no Kraken/Python bootstrap required | — |

**Uncertainty-vs-blank:** "not yet transcribed" ≠ "illegible" ≠ "blank/uninscribed" (todo Section 3). Scoring must keep three states distinct.

## 4. Manual vs automatic layout

- Report two pipeline settings: **(a)** transcription on ground-truth/imported layout, and **(b)** fully automatic layout + tracing.
- Manual-layout assistance is **recorded and evaluated separately**; it may never be reported as standalone automatic recognition (todo Section 7).
- Per track, record extraction failures and whether marks near line boundaries were preserved.

## 5. Correction-effort measurement

- Establish **expert/manual correction time** as a baseline (raw model output → accepted diplomatic text), reported in minutes per line and minutes per 1,000 characters.
- Compare **raw** vs **AI-assisted** workflows on the same held-out witnesses before claiming any AI gain (todo Section 4/7).
- Correction time is a first-class acceptance entry, not an anecdote.

## 6. Per-track acceptance-sheet template

One filled sheet per track (manuscript / papyrus / inscription). Fields marked *(pending)* require measured pilot values; none are invented here.

- Papyri draft: [acceptance-papyri-draft.md](acceptance-papyri-draft.md) (not frozen; thresholds and specialist sign-off pending).
- Locked-set reuse history: [locked-set-reuse.md](locked-set-reuse.md).
- P01 measurement record: [decoding-comparison.md](decoding-comparison.md); AI-assisted method: [correction-effort.md](correction-effort.md).
- Manuscripts draft: [acceptance-manuscripts-draft.md](acceptance-manuscripts-draft.md) (transfer measurements only; hand holdout pending).
- Inscriptions draft: [acceptance-inscriptions-draft.md](acceptance-inscriptions-draft.md) (blocked: no image-aligned labels; no model).

| Required entry | Content for this track | Status |
| --- | --- | --- |
| Corpus/version, object counts, evaluated lines/signs, unresolved spans, split hashes | from MAN-01 / PAP-01 / INS-01 | *(pending)* |
| Transcription profile, Unicode/scoring version, whitespace & uncertainty rules | from C0 transcription contract + specialist | *(pending, C0 + BLK-2)* |
| Raw-model, language-assisted and AI-assisted results; manual vs automatic layout | separate rows | *(pending)* |
| Numeric accuracy/coverage thresholds and correction-time target | set after pilot baseline | **pending measurement** |
| Hardware, image sizes, throughput, peak RAM/VRAM, training budget | from Phase-0 machine run | *(pending)* |
| Expert-review process, named owners, evidence links | evaluator + Greek reviewer named | *(pending — owner to name)* |
| Per-profile strata (hand/capture/material) | disclosed; unsupported profiles listed as blockers | *(pending)* |
| Leakage check result | object/hand/collection dedup report | *(pending)* |
| Independence check | dependency inventory | *(pending)* |

## 7. Failure modes to reject explicitly

1. A pooled three-track accuracy that hides an unsupported track.
2. Percentages computed after dropping missed lines, blank-region insertions, or unresolved spans.
3. Accent-stripped or normalized truth used to inflate visible-text scores (P02's labels strip accents — never use them as visible line truth).
4. Editorial expansions/restorations entering diplomatic/training labels (manuscript abbreviations, nomina sacra).
5. Same-object alternate captures straddling a split.
6. Memorized standard-edition text scoring as "visual recognition" (evaluate unfamiliar passages and damaged/blank areas).
7. Research-only or NC/restricted material presented as part of a broadly redistributable bundle.
8. A "pass" declared on one hand/material generalized to the whole track.

## 8. Measurement backlog (IDs, owners, evidence)

| Task | Owner | Prerequisite | Output | Completion evidence |
| --- | --- | --- | --- | --- |
| **EVAL-01** | Custodian | C0 scoring rules; manifests | Locked per-track test sets + split hashes; leakage report | Hash list; dedup report |
| **EVAL-02** | Custodian | EVAL-01; frozen candidate outputs | Per-track scoring run (raw + assisted) | Reproducible report; tool version recorded |
| **EVAL-03** | Greek reviewer | EVAL-01 | Adjudicated uncertainty/blank/missed labels | Adjudication notes; unresolved kept |
| **EVAL-04** | Engineering owner | Phase-0 machine run | Hardware/throughput/peak-memory table | Measured numbers on the target machine |
| **EVAL-05** | Evaluator + owner | EVAL-02..04 | Frozen acceptance sheet + pass/fail per track/profile | Signed sheet; unsupported profiles disclosed |
| **EVAL-06** | Evaluator | any model change | Regression check on previously supported profiles | Delta report; no silent regression |
| **EVAL-07** | Independent reviewer | EVAL-05 | Format/independence spot checks | Round-trip + dependency-inventory check |

## 9. Cross-material integration rule

New data tracks advance through checkpoint **I1** independently, but the combined initial scope is **not complete until all three material gates pass** (todo Section 6). A track that stays research-only must be **explicitly disclosed**, not silently dropped (todo Section 3).

## 10. Alignment with `C0-0.3-draft` and answers to A's reviewer questions

A's `examples.md` §1 scoring semantics and `interchange-and-api.md` §1 match this plan; no conflict found. B's answers to A's named open questions:

1. **`corpus-manifest.md` §7 Q1 — are `asset_kind` / `label_granularity` sufficient?** Mostly yes. `label_granularity` (`line`/`word`/`character-box`/`glyph`/`region`/`none`) expresses P02 character boxes and I01 editions. **Two residuals:** (a) P02's *preservation tags* and I03's damage states are qualifiers on a box, not granularity → propose an asset field `preservation_tags` (or record them as an `annotation-layer` asset); (b) no `asset_kind` value fits a text-only catalogue/metadata file (I02's PDF, P05's metadata) → propose `catalog`/`metadata-text` or an `other:<text>` escape. Raised as `B-CCR-6`.
2. **`corpus-manifest.md` §7 Q2 — `hand_style` controlled vocabulary?** For planning, free text + a `split_grouping_key` is sufficient; M01's hands are named persons (Antonius Eparchos, Camillos Zanettus). Do not invent a controlled vocabulary before the specialist works; a grouping key is what split design needs.
3. **`corpus-manifest.md` §7 Q3 — `reserved-acceptance` / custodian?** Confirmed: the project owner selects reserved-acceptance objects (`DATA-03`); the custodian must be a distinct named role (this plan, §1).
4. **`interchange-and-api.md` §4 Q1 — enumerate export/round-trip fixtures per material now?** **Yes**, fix the fixture **names/IDs** at contract level but not their content. Proposed fixture set, one row per material plus shared edges: `FX-MSS-nomina-sacra-overbar`, `FX-PAP-lacuna-unknown-extent`, `FX-INS-stoichedon-scriptio-continua`, `FX-shared-abbrev-expansion`, `FX-shared-lunate-sigma`, `FX-shared-combining-mark-offsets`, `FX-shared-blank-region`, `FX-shared-alternate-capture`. Content is populated from real sources after MAN-02/PAP-01/INS-02.
5. **`domain-and-pipeline.md` §7 Q1 — per-grapheme confidence sufficient, or reserve word-level now?** Per-grapheme is sufficient at contract level; do **not** add a word-level field now. WER applies only where an explicit word layer exists; word-level confidence can be a derived aggregation later without a contract change.
6. **`examples.md` §2 — source-backed replacements.** B will supply real cases after label creation (M01 nomina sacra/overbar, P01 lacuna of unknown extent, I01/I02 stoichedon or worn stone). Structure of EX-1/EX-3/EX-5/EX-6 is accepted; only the invented strings need replacement.

## 11. Open questions handed to A / the owner

1. Canonical-equivalence and grapheme-boundary definitions — **aligned**: A defines NFC-only, revision-scoped scalar offsets and UAX #29 graphemes; no change requested.
2. Acceptance-sheet storage as Markdown vs a future machine-readable fixture (deferred; no schema is created in this assignment).
3. Who is named the locked-test **custodian** — must be resolved before EVAL-01, and must differ from the model developer.
4. `preservation_tags` asset field and a catalogue/metadata `asset_kind` (`B-CCR-6`).
