<!-- Copyright (c) 2026 QuantForgeSoftware. All rights reserved. -->
# Transcription handbook — annotator guide (pilot draft)

Contract source: [`docs/contracts/greek-transcription.md`](../contracts/greek-transcription.md) (`C0-0.4-draft`) and [`docs/contracts/examples.md`](../contracts/examples.md). **This handbook is a proposal until the Greek specialist adjudicates the open decision points** (listed at the end). It describes how to label; it does not overrule the specialist.

## 1. What you are transcribing

You produce a **diplomatic transcription (L1)**: a documented representation of what is **visible** on the object. You do not produce an interpretation. If you can read plausible Greek but the letters are damaged, your judgment goes into **annotations**, not into "improved" text.

The five layers (never collapse them):

| Layer | Who writes it | Example (invented, per EX-4) |
| --- | --- | --- |
| L0 raw recognition | the model | `αλανθρωπ` (mis-read) |
| L1 diplomatic | **you** | overbarred `α̅λ̅α̅` letters as visible |
| L2 editorial | editor/AI proposal | expansion `ἀλλά`, accents |
| L3 annotations | **you** | `Lost`, `Unclear`, alternatives |
| L4 revisions | the app | who changed what, when |

**Rule zero:** an interpretation must never replace evidence. Expansions, accents and word division belong to L2 with reversible links.

## 2. Lettering rules

1. **Case**: transcribe the visible case. Majuscule hands stay majuscule.
2. **Sigma**: lunate sigma → `ϲ` (U+03F2) when visibly lunate; final/medial sigma as seen. If the form is ambiguous, transcribe the most likely character and add a `GlyphForm` annotation describing what is visible.
3. **Historical/epichoric letterforms**: transcribe to the profile's agreed modern equivalent; record the visible form as a `GlyphForm` annotation. (Mapping table: specialist decision — see §5.)
4. **Ligatures**: transcribe as the letters represented; add a `GlyphForm` annotation with position.
5. **Punctuation**: transcribe visible marks only (middle dots, ticks). Never add punctuation.
6. **Numerals**: transcribe the visible sign (e.g. axial-bar forms on stone) — never convert to modern digits in L1. Use the profile's sign inventory.
7. **Abbreviations / nomina sacra**: transcribe the visible letters **plus** the overbar as a `Sign` annotation (`overbar-*` inventory ID). Expansion goes to L2.
8. **Supralinear/interlinear additions**: transcribe as text with an `Added` annotation (and `DifferentHand` if evidenced; default `unattributed`).

## 3. Diacritics

- Transcribe breathings, accents, diaeresis, iota subscript/adscript **exactly as present** — including their absence.
- Unaccented text stays unaccented. Never supply accents because the text is Greek.
- Iota adscript vs subscript is visible information: transcribe as seen.
- Editing is grapheme-aware: a base + its combining marks is one unit; the app enforces canonical combining order (breathing → diaeresis → accent → subscript) at storage.

## 4. Uncertainty and damage vocabulary

Closed set — choose the closest status; never guess characters:

| Annotation | Use when |
| --- | --- |
| `NotYetTranscribed` | nobody has worked on this span (default state) |
| `Unclear` | letters visible, reading doubtful (imported editorial underdots become this) |
| `Illegible` | traces exist, letters not recoverable from this capture |
| `Lost` + extent | no surface remains; extent `Known` (measured/structural), `Estimated` (with count), or `Unknown` |
| `SuppliedEditorial` | text supplied in L2 only — never in L1 |
| `AlternativeReading` | competing readings; keep all, choose none |
| `Erased` / `Added` / `Overwritten` | witness corrections (visible residue described) |
| `DifferentHand` | distinct writing agent; `unattributed` unless evidenced |
| `CorrectionSequence` | only when evidence supports the order |
| `UnidentifiedMark` | a mark outside the sign inventory — describe it, never guess |

**Not the same thing:** "not yet transcribed" ≠ "illegible" ≠ "lost". Blank regions score additions as failures (EX-9), so annotate blanks honestly.

## 5. Open decision points (Greek specialist)

1. Lunate sigma encoding (dedicated character vs annotation-only).
2. Epichoric letterform mapping table for the pilot inscription corpus.
3. Numeral sign inventory (axial-bar forms, stroked numerals).
4. Per-character underdots vs span-level `Unclear` (or both).
5. Word-division layer representation for scriptio continua (span-linked token annotations recommended).
6. M02a/M01 label cautions: expansion-lite forms (`ἰῦ`, `χῦ`), diaeresis forms (`ϊ`), whether label spaces are visible spacing — see `data/manifests/manuscripts/m02a.json`.

## 6. Workflow in the application (pilot form)

1. **Create a project** (`leviathan project new` or the workspace). Import PAGE XML, a IIIF Presentation 3 manifest, or (later) uploaded captures.
   - IIIF imports keep every view as a separate capture and retain per-manifest attribution. Remote images are fetched once into a checksummed cache when analysis needs them; afterwards work is offline.
2. **Layout** — either use the imported PAGE geometry or run automatic detection (`layout detect` / "Detect layout"). Automatic detection never overwrites existing layout. Manual correction is done in the geometry overlay (drag a line; reorder regions and lines).
3. **Transcribe** in the line editor:
   - The diplomatic field is L1; type exactly what is visible (see §2–§3).
   - Save creates a revision (⌘/Ctrl+S); undo is revision-based (⌘/Ctrl+Z). Unsaved drafts survive reload.
   - Add annotations with the form: kind, scalar start/end, extent for gaps, alternative text for competing readings, and a note. The list under the editor shows them in scalar offsets.
4. **Review** with the dual pane: L1 diplomatic on the left, L2 editorial on the right (both link to the capture).
5. **AI assistance (optional)**: choose a configured provider and task, propose at the current revision, then accept explicitly. Acceptance creates an L2 revision; L1 is untouched. Provider credentials never reach the browser.
6. **Export**:
   - diplomatic plain text (with a mandatory loss report),
   - TEI/EpiDoc (bounded profile; unsupported fields reported),
   - training JSON (L1-only labels; editorial-supply lines excluded; uncertainty spans with exact-span complements).

## 7. Review, adjudication and retraction

- Every decision (accept/correct/dispute) is anchored to the exact revision you reviewed.
- A **correct** decision creates a new L1 revision (your correction) and keeps the reviewed
  revision in the ledger; never overwrite history.
- Two reviewers with different outcomes make a line an **adjudication candidate**; the project
  page lists those and exports `adjudication.csv`. A specialist resolves the disagreement by
  recording another decision/correction — the tool does not decide for you.
- If an annotation was wrong, **retract** it. The record stays visible in exports as retracted;
  active views and training data exclude it.
- Reviewers are identified by the account/name you pass; shared deployments authenticate keys
  and record an audit trail.

Remember: recognition output (L0) is model text, not an edition; only your accepted L1/L2 work is a transcription.
