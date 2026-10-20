# Examples and scoring semantics

Contract: **C0-0.4-draft** (proposal, not accepted). Issued 2026-10-18 by Instance A (A-PLAN). Status: **draft, open for review**. Revision history: 0.2 (initial) → 0.3 (no change to this document; set-level version bump recorded in corpus-manifest.md).

Source requirements: project-plan Section 7 (recognition, uncertainty, layout, AI scoring) and the brief's step 5. **All examples below are invented illustrations, labeled as such. They are not ground truth, derive from no real item, and are excluded from corpus counts.** B should propose source-backed replacements from real corpus material; structure, not the specific strings, is the contract.

## 1. Scoring semantics (summary contract)

| Rule | Contract |
| --- | --- |
| Primary metric | Character error rate on agreed visible-text spans, computed on **Unicode scalars after canonical-equivalence normalization only** (NFC, per greek-transcription.md §4), case-sensitive. Grapheme error rate computed alongside on extended grapheme clusters. |
| No editorial normalization | No compatibility folding, case folding, sigma substitution, or accent stripping of either side before scoring; normalization beyond NFC is a recorded, versioned scoring decision, not a default. |
| Mark errors | Omissions/substitutions/insertions are reported **separately for combining marks** — including incorrectly supplied accents/breathings on unmarked text, which are insertions (project-plan §7). A base letter scored with a wrong accent is 1 substitution (base) + 1 mark error, reported in both views. |
| Uncertainty | Spans marked `unclear`/`illegible`/`lost` are scored in a **separate uncertainty stratum**, never pooled into the main CER. Reports include excluded-span counts and accuracy-vs-coverage (abstention) curves. |
| Blank/missed failures | Text recognized on uninscribed/blank regions and wholly missed lines are **failures counted in the denominator**, not omitted samples. Empty output on an inscribed line is scored as full deletion. |
| WER | Only computed where an explicit word layer exists; proposed word division for scriptio continua is scored as its own task, never folded into WER. |
| Layout | Region overlap, baseline precision/recall, line assignment, reading-order accuracy with disclosed matching tolerances; manual-vs-automatic layout results always reported separately. |
| AI assistance | Raw recognition, candidate readings and accepted editorial output scored as separate results; unsupported restorations and overwritten witness variants are tracked errors, not style choices. |
| Model/lifecycle | Train/reload/infer parity, reproducibility tolerance, checkpoint recovery and split-leakage checks follow domain-and-pipeline.md and manifest split hashes. |

## 2. Illustrative cases

### EX-1 (manuscript) — nomina sacra with overbar and expansion

- **Inputs (invented):** A later-minuscule-style line visibly reading Κ𝛔 overlined (overbar above), surrounded by accented text: τῆς χάριτος.
- **Expected preservation:** L1 = the visible letters `Κϲ` (sigma per profile) + an overbar sign-inventory annotation; accents/breathings transcribed as combining sequences exactly as visible. L2 may hold the expansion (e.g. *κυρίου*) linked to the L1 span. Raw PAGE/edition import keeps the expanded form in the attachment, not in L1.
- **Expected scoring:** Recognition scored against L1 `Κϲ` (+ overbar as a mark event if in the label set). Scoring against the expanded *κυρίου* is a **different, separately-labeled result** (edition-assisted), never the visual-recognition score (project-plan §5 P02 warning).
- **Failure to reject:** A pipeline that replaces the overbarred `Κϲ` with expanded text in the diplomatic layer, or scores the expansion as if it were read visually.

### EX-2 — combining marks and canonical equivalence

- **Inputs (invented):** The same visible word ἄνθρωπος imported once with precomposed `ά` (U+03AC) and once decomposed (`α` + U+0341), plus an annotation anchored at scalar offset of the accented vowel.
- **Expected preservation:** Both imports normalize to one NFC storage form; the annotation offset maps identically after recorded, offset-preserving remapping; the original sequences retained in the importing revision. A lunate-vs-`σ` or subscript-vs-adscript distinction, which NFC does **not** fold, must remain distinct.
- **Expected scoring:** Predictions `ἄνθρωπος` in either combining form score as identical to NFC truth; a missing grave on `τῆς` is reported as a mark omission.
- **Failure to reject:** Any normalization that detaches the annotation from its span, or NFKC folding that silently rewrites `ϲ` → `σ` in L1.

### EX-3 (inscription) — scriptio continua and proposed word division

- **Inputs (invented):** A stoichedon inscription line, no word spaces: ΕΑΝΤΙΣΑΛΛΟΓΡΑΦΗ (with a lunate epsilon-form recorded as glyph-form annotation).
- **Expected preservation:** L1 preserves the unspaced letter sequence exactly; reading-order/arrangement metadata (stoichedon grid, boustrophedon if present) recorded as annotations. A proposed word division lives in L2 with per-boundary links.
- **Expected scoring:** CER on the unspaced sequence; proposed division scored as its own boundary-precision task. No WER claimed for this line.
- **Failure to reject:** Scoring a model's recognition *after* silently applying a proposed word division (or lexical restoration) and reporting that as visual-recognition accuracy.

### EX-4 — abbreviation expansion with reversible links

- **Inputs (invented):** Visible `̅α̅λ̅α̅` (overlined letters) in a papyrus; editorial expansion *ἀλλά* with accent not visible on the object.
- **Expected preservation:** L1 `αλ Pharao-letters + overbar annotation`; L2 expansion `ἀλλά` linked to that span with provenance. The accent exists only in L2.
- **Expected scoring:** Main CER scores `αλα`-visible spans; the expansion is scored (if at all) as a separate expansion-accuracy task.
- **Failure to reject:** Training visual recognition on the expanded *ἀλλά* as the label for the visible `̅α̅λ̅α̅` (editorial text as a training label — project-plan §3 invariant).

### EX-5 (papyrus) — damaged gap, unclear traces, alternative readings

- **Inputs (invented):** A papyrus line: `[ ]νθρωπ[ ]` — line-start lacuna of structural (known) extent, a mid-line gap where fiber damage leaves traces judged illegible, and a final partial letter read by two editors as `π` or `ρ`.
- **Expected preservation:** `lost` with extent class `known` (line-start), `illegible` with trace description (mid-line), `alternative-reading` set {π, ρ} retained — none chosen silently. Underdots from the imported edition become `unclear` annotations, never literal characters in L1.
- **Expected scoring:** Exact scoring on fully agreed spans only; the gap and alternatives go to the uncertainty stratum with excluded-span counts; coverage reported.
- **Failure to reject:** A scorer that fills the lacuna with plausible Greek (from lexicon or memory) and scores the filled text as correct; or one that treats the unannotated-but-damaged region as background (project-plan §5 P03 warning: missing annotation ≠ background).

### EX-6 (inscription) — visible numerals and worn strokes

- **Inputs (invented):** A stone line ending in an axial-bar numeral form and a worn final letter visible only as stroke traces under raking light.
- **Expected preservation:** Numeral transcribed per the sign inventory (not converted to a modern digit in L1); worn letter transcribed only if the profile's evidence threshold is met, else `unclear`/`illegible` with trace note.
- **Expected scoring:** Numeral out-of-label-set predictions surface as `unknown-label` errors, not silent drops; the worn letter, if annotated `unclear`, scores in the uncertainty stratum.
- **Failure to reject:** A transcription profile update that rewrites the numeral into `6` inside L1 (normalization without a new revision + remapping).

### EX-7 — multiple captures of one surface

- **Inputs (invented):** One inscription face photographed frontally and with raking light; a line annotated on the raking-light capture.
- **Expected preservation:** Two capture entities, two coordinate frames; the annotation stays bound to the raking-light frame. Transferring geometry to the frontal capture requires an explicit registration record; until then the annotation does not display as authoritative on the frontal frame.
- **Expected scoring:** Evaluation identifies which capture ground truth was made against; cross-capture transfer accuracy reported only where registration exists.
- **Failure to reject:** Merging or overlaying coordinates across unregistered captures (project-plan §3).

### EX-8 — stale revision / conflict

- **Inputs (invented):** An `unclear` annotation anchored to diplomatic revision r3; another editor then creates r4 from a different r3-based edit; a client submits an edit to the annotation's span against r3.
- **Expected preservation:** The submission fails with `revision-conflict` naming r4; the annotation is not silently re-anchored; history intact. `not-yet-transcribed` vs `illegible` remain distinct throughout.
- **Expected scoring:** n/a (workflow case), but the failure demonstrates the acceptance invariant that revisions remain retrievable and editorial acts never overwrite evidence silently.
- **Failure to reject:** An integration that re-anchors annotations on every text change, or that lets a correction-acceptance overwrite L1 witness evidence.

### EX-9 — blank-region addition and wholly missed line (evaluation integrity)

- **Inputs (invented):** A page containing an uninscribed margin the recognizer fills with hallucinated text, plus an inscribed line the detector never proposes.
- **Expected scoring:** The hallucinated margin text is scored as insertions against a blank-region truth span; the missed line as full deletion — both inside the reported metrics, with counts (project-plan §7).
- **Failure to reject:** An evaluation harness that omits blank regions and undetected lines from the denominator, inflating accuracy.

## 3. Case coverage checklist (brief step 5)

| Required case | Covered |
| --- | --- |
| One per material (manuscript, papyrus, inscription) | EX-1, EX-5, EX-3/EX-6 |
| Combining marks | EX-2 |
| Scriptio continua | EX-3 |
| Abbreviation expansion | EX-1, EX-4 |
| Damaged gap | EX-5, EX-6 |
| Multiple captures | EX-7 |
| Stale revision | EX-8 |
| Uncertainty/blank/missed-line scoring | EX-5, EX-9 |

## 4. Planned named round-trip fixtures (IDs fixed at contract level; content from real sources)

Accepted from B's acceptance-plan §10 (answer to §7 Q1 of interchange-and-api.md): fixture **names** are pinned now, **content** is populated from real sources after acquisition/adjudication. Until populated, the invented cases above stand in.

| Fixture ID | Material | Distinction under test |
| --- | --- | --- |
| `FX-MSS-nomina-sacra-overbar` | manuscript | overbar sign + expansion link (EX-1) |
| `FX-PAP-lacuna-unknown-extent` | papyrus | gap extent unknown vs unannotated (EX-5) |
| `FX-INS-stoichedon-scriptio-continua` | inscription | unspaced scoring, no WER (EX-3) |
| `FX-shared-abbrev-expansion` | shared | reversible expansion links (EX-4) |
| `FX-shared-lunate-sigma` | shared | glyph-form annotation, no substitution (EX-6) |
| `FX-shared-combining-mark-offsets` | shared | NFC remap + UTF-16 conversion (EX-2) |
| `FX-shared-blank-region` | shared | insertions on uninscribed regions scored as failures (EX-9) |
| `FX-shared-alternate-capture` | shared | frame registration before geometry transfer (EX-7) |
