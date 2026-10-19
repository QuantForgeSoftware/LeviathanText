# Acceptance sheet — inscriptions track (draft, not viable yet)

- Owner: **Instance B (EVAL)**; template: [acceptance-plan.md](acceptance-plan.md) §6.
- Status: **blocked, not frozen**. This track has no image-aligned diplomatic labels and
  no trained model; the sheet records that honestly instead of inventing coverage.
- Sources: [inscriptions.md](../data/inscriptions.md), ledger
  [`i01.json`](../../data/manifests/inscriptions/i01.json).

| Required entry | This track | Status |
| --- | --- | --- |
| Corpus/version, object counts, evaluated lines/signs, unresolved spans, split hashes | I01 (I.Sicily / Crossreads, CC BY 4.0): **448 records with Greek text + usable image**, comment-aware audit; IIIF Presentation 3 import proven against a real manifest. **No image-aligned line geometry or transcription labels exist yet** | inventory recorded; labels missing |
| Transcription profile, Unicode/scoring version, whitespace & uncertainty rules | C0-0.4-draft profile applies in principle; carved-text conventions (line breaks, vacats, damage, ligatures) are **not adjudicated** | pending specialist |
| Raw-model results | **none** — no model was trained or evaluated on inscriptions | blocked |
| Language-assisted results | none | not started |
| AI-assisted results | none | not started |
| Manual vs automatic layout | no line geometry to import or detect; IIIF images are available | blocked |
| Numeric accuracy/coverage thresholds and correction-time target | **unset**; cannot be set without labels | blocked |
| Hardware, image sizes, throughput, peak RAM/VRAM, training budget | not measured for this track; images vary by IIIF service and capture | not measured |
| Expert-review process, named owners, evidence links | epigraphic reviewer **not named**; IIIF manifest/rights evidence recorded | pending |
| Per-profile strata (site/material/lighting) | site/material/lighting strata exist in the source but are **not yet enumerated for holdout** | not enumerated |
| Leakage check result | no split exists; dedup by object/inventory ID planned | blocked |
| Independence check | IIIF import is native; no Kraken/Python runtime | recorded |
| Confidence / calibration | none | not applicable |

## What would unblock it

1. Select a development pilot from the 448-record intersection.
2. Create source-linked visible readings, line geometry and uncertainty annotations
   (INS-01/DATA-03), preserving the published edition separately.
3. Freeze an unseen-site holdout and a custodian, then train the C# CRNN/CTC path.
4. Only then fill thresholds and sign-off.

Until those exist, no inscription accuracy claim is possible and the combined three-track
scope is incomplete.
