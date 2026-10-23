# Greek transcription contract

Contract: **C0-0.4-draft** (proposal, not accepted). Issued 2026-10-18 by Instance A (A-PLAN). Status: **draft, open for review**; Greek-specialist adjudication pending (BLK-A-2). Revision history: 0.1 (initial) → 0.3 (no semantic change to this document; set-level version bump recorded in corpus-manifest.md).

Source requirements: project-plan Section 3 ("Greek transcription contract"), Section 4 (domain model), Section 7 (scoring semantics). This document proposes the versioned transcription profile, text-layer separation, Unicode handling and uncertainty vocabulary. It proposes conventions; it does not adjudicate them.

## 1. Layer model (what exists and what never collapses)

Five distinct layers. Identity and revision mechanics live in `domain-and-pipeline.md`; this document defines what each layer **means**.

| Layer | Content | May become a training label for visual recognition? |
| --- | --- | --- |
| **L0 raw recognition** | Untouched model output, as produced | n/a (it *is* model output) |
| **L1 diplomatic text** | A documented representation of what is **visible** on the witness: letters, visible marks, visible word division (or none), no expansions, no supplied accents | **Yes — the default and only automatic label source** |
| **L2 editorial reading** | Word division, accents, abbreviation expansions, spelling normalization, restorations; linked reversibly to L1 spans | **No** |
| **L3 scholarly annotations** | Uncertainty, gaps, erasures, hands, correction sequences, alternative readings; attach to L1 or L2 spans | No (but they scope *which* spans are scored) |
| **L4 application revisions** | Provenance of every change: who/what/when, model identity, acceptance status | No |

Invariants:

- Historical corrections **in the witness** (a scribe's or stone-cutter's correction) are L1/L3 evidence, not L4 application edits. Application history never overwrites witness history.
- An interpretation (plausible Greek, normalized spelling, restored letters) must never silently replace evidence. Every editorial intervention is a link from an L1 span, never an in-place mutation of it.
- Preserving imported text and L0 output is mandatory; derived forms (e.g. canonical NFC) are computed views with recorded mappings, not replacements (project-plan §3).

## 2. Diplomatic profile: what is transcribed

Default output is a **diplomatic transcription** of visible lettering. Proposed profile rules (each is a decision point flagged for the Greek specialist):

1. **Case**: transcribe the visible case (majuscule hands are majuscule); no case folding in L1.
2. **Sigma**: transcribe the visible form. Lunate sigma maps to a documented representative character per profile version (proposal: `ϲ` U+03F2 for lunate *when visibly lunate*; final vs medial sigma rendered as seen). Plain Unicode that cannot express a visible distinction gets a recorded **glyph-form annotation**, not a substituted character.
3. **Historical letters / digraphs** (εί vs ει-shaped inscriptions, 𝘈-shaped forms, epichoric letterforms in inscriptions): transcribe to the profile's agreed modern equivalent character, with a glyph-form annotation preserving what was visible. The specialist decides the mapping table per profile version.
4. **Ligatures**: transcribe as the sequence of letters represented, plus a ligature annotation with position. The ligature itself is visual information (glyph-form layer), not text.
5. **Punctuation**: transcribe **visible** marks only; middle dots, marks at line middle, and shapes without modern equivalents get the documented sign inventory (below). No punctuation is added by default.
6. **Numerals**: transcribe the visible sign (e.g. `α`, `ϛ`, strokes/axial bars on stones) with a numeral annotation where the profile's sign inventory requires it; do not convert to modern digits in L1.
7. **Abbreviations and nomina sacra**: transcribe the **visible letters and overbar** (e.g. overlined `κϲ`); expansion belongs to L2 with a reversible link. The overbar itself is an L1-visible mark and must be recorded (see sign inventory).
8. **Supralinear/interlinear additions, corrections, erasures in the witness**: transcribed as text, with L3 annotations for status (added/erased/overwritten) and, where relevant, hand attribution left `unattributed` unless evidenced.
9. **Scriptio continua**: preserved exactly. Word division is never inferred into L1 from the fact that the text is Greek.

### Sign inventory (versioned per profile)

The profile carries a closed, versioned inventory of nonalphabetic signs: overbars, supralinear letters, filling markers, lectionary/diplomatic ticks, visible dashes, obeli-like strokes, etc. Each sign has: an ID, a Unicode representation (existing character, private-use agreement, or empty = annotation-only), and rendering notes. Unknown marks are recorded as `unidentified-mark` annotations with position and description — never guessed into a nearby character.

## 3. Diacritics and combining sequences

- Transcribe **present** breathings, accents (acute/grave/circumflex), diaeresis, iota subscript/adscript as seen, using combining characters per the profile's canonical ordering (§4).
- **Presence and absence are both information.** Unaccented text stays unaccented; accents are never supplied into L1.
- Later manuscript hands with full polytonic accents are handled by the same rule; the profile does not collapse them to a "standard" form.
- Iota subscript vs adscript is a visible distinction: transcribe as seen (adscript as a following vowel+annotation or subscript combining U+0345 form per profile version); never normalize one into the other in L1.

## 4. Unicode rules

- All text is stored as **logical-order Unicode text**; direction/display metadata is separate (project-plan §4).
- **Canonical NFC** is the interchange/storage form for diplomatic text, with these qualifications:
  - NFC is applied only where it does not lose a visible distinction the profile records; glyph-form annotations carry what NFC cannot.
  - **No compatibility folding (NFKC/NFKD), no case folding, no sigma substitution, no spelling modernization, no accent stripping is ever applied to L1.** Training-label normalization is a per-model-version decision recorded in the model card, applied to a derived view, never overwriting L1.
- Combining sequences: the profile defines a **canonical combining order** for Greek (proposal: breathing → diaeresis → accent → vowel-length/other, per character), applied at storage. Sequences not in canonical order on import are normalized **with an offset-preserving mapping recorded**; the original sequence is retained in the revision that imported it.
- **Offsets are revision-scoped Unicode scalar offsets** (project-plan §3):
  - Every annotation stores `[scalar_start, scalar_end)` against a named text revision. Scalar = code point, not UTF-16 unit, not grapheme.
  - Converters to C#/browser UTF-16 offsets and to grapheme boundaries are specified per profile version; conversions are pure functions over the revision's text (algorithmic spec deferred to implementation, but the contract is: conversion is deterministic, versioned, and validated against the revision's text).
  - Grapheme clusters (UAX #29 extended grapheme clusters) are the unit for editing, alignment and character-level scoring; a base + combining sequence is **one** grapheme.
  - Changing text normalization or the profile must not detach annotations: normalization happens as a **new revision** with explicit scalar-offset remapping of every annotation, or it is rejected.

## 5. Uncertainty and damage vocabulary (L3)

Distinct statuses (project-plan §3):

| Annotation | Meaning | Notes |
| --- | --- | --- |
| `not-yet-transcribed` | Nobody has worked on this span | Must be distinguishable from every illegibility state |
| `unclear` (visible but doubtful) | Letters visible, reading uncertain | Editorial underdots on specific characters; underdots are L3 uncertainty, not visible marks |
| `illegible` | Traces exist, letters not recoverable from **this** image | Count of lost characters may be estimated |
| `lost` (lacuna) | No surface remains | Gap extent: `known` (measured/structural, e.g. line-start), `estimated` (annotator estimate, with estimated count) or `unknown` |
| `supplied` (editorial) | Text supplied in L2 only | Never enters L1; carries source of supply |
| `alternative-reading` | Competing readings retained | Multiple readings coexist; none is silently chosen |
| `erased` / `added` / `overwritten` | Witness correction states | Include visible residue description |
| `different-hand` | Distinct writing agent | Attribution only with evidence; `unattributed` default |
| `abbreviation` | Visible abbreviation with linked L2 expansion | Reversible L1→L2 revision/range link; never rewrites L1 |
| `word-boundary` | Zero-width L2 word-division proposal | Boundary position only; never inserts spaces into L1 |
| `correction-sequence` | Ordered witness interventions | Sequence group/index and explicit/inferred/unknown order basis; never inferred from annotation time |
| `unannotated-region` | Region exists, no annotation decision made | Distinct from "no text present" |

**Bracket conventions**: Leiden-style brackets/underdots in imported editions are parsed into these annotations (or preserved in an attachment with an unsupported-markup report). They are **never** stored as literal characters inside L1 diplomatic text.

## 6. Profiles and versioning

- A **transcription profile** is a versioned document: case/sigma/historical-letter decisions, sign inventory, combining order, unknown-mark policy. Proposed first version: `greek-diplomatic-1.0-draft` (this document's proposals), pending specialist adjudication.
- Each dataset asset (manifest `transcription_profile` field) names the profile version its labels follow. Re-labeling under a new profile is a recorded, auditable conversion.
- Model cards record any label normalization applied at training time, including the exact derived view used.

## 7. Decisions adopted in `greek-diplomatic-1.0-draft`

These are the profile owner's initial decisions, subject to later specialist ratification. A
change requires a new profile version and a recorded conversion; it is never applied silently
to existing L1.

1. **Sigma:** use `ϲ` (U+03F2) only when the visible form is lunate; otherwise preserve visible
   `σ`/`ς`. Never substitute one form for another. Unknown forms get a `GlyphForm`.
2. **Historical letters/ligatures:** preserve the visible modern equivalent only when known;
   otherwise use `GlyphForm`. Ligatures are expanded to their represented letters plus a
   `GlyphForm`; no automatic epichoric mapping is claimed in 1.0-draft.
3. **Punctuation/numerals:** transcribe visible marks only; no modern punctuation insertion.
   Preserve Greek numeral signs/digits and do not convert them to modern values.
4. **Underdots:** per-character `Unclear` is primary. Span-level `Unclear` is allowed only when
   character boundaries cannot be established; `Illegible` remains distinct.
5. **Scriptio continua:** preserve it exactly in L1; never insert word spaces. Word division
   belongs to L2 boundary annotations with source-span links.
6. **Abbreviations:** visible letters and overbar stay in L1; expansion is an L2 revision with a
   reversible range link. The overbar is a `Sign`.
7. **Correction order:** record only when evidence supports it; `SequenceIndex` plus an
   `OrderBasis` (`explicit`/`inferred`/`unknown`). Annotation creation time is never evidence.
8. **Unknown marks:** known inventory symbols use `Sign`; visible forms Unicode cannot express
   use `GlyphForm`; unknown traces use `UnidentifiedMark`.

The executable policy and minimal sign inventory live in
`src/LeviathanText.Core/Unicode/GreekDiplomaticProfile.cs`; the profile inspector reports
without rewriting text.
