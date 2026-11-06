# P46 specialist packet: structural addendum — 2026-11-05

This addendum accompanies the unchanged three-page packet. It records engineering
findings and questions; it supplies no Greek adjudication or accepted crop/target.
All81 packet decisions remain pending. Original images, crops, transcription
references, worksheet, source TEI and their hashes have not been rewritten.

## New structural evidence

The original TEI has27/26/28 physical `lb` milestones on pages1440/1450/1460.
The general importer previously produced30/23/32 rows by folding nested/continued-
word milestones and treating verse-block boundaries as line boundaries. New imports
now follow physical source milestones. Existing85-row native projects are retained
unchanged; no automatic migration or training-target update occurs.

Matching the81 milestones to the81 proposed review rows establishes only a count
and source-order correspondence. It does not prove that a crop contains the whole
physical row, that a visible mark matches the source reading or that editorial
content belongs in a diplomatic label. The generic importer has not been shown to
be the generator of the earlier51-row training targets; this bug is not an
established cause of the historical development CER.

See [structural import evidence](../evaluation/p46-physical-tei-lines-2026-11-05.md)
and [independent review](../evaluation/tei-physical-independent-review-2026-11-05.md).
The source attachment remains authoritative; generated native IDs are not original
NTVMR line identifiers. These three TEIs have no explicit `lb@n` or `lb@xml:id`.

## Specific open questions

| Item | Evidence to inspect | Question requiring a decision |
| --- | --- | --- |
| **P46-1440-L10** | Page1440, folio81v, TEI sequence10; packet crop `crops/P46-1440-L10.png`; proposed review bbox480,987–2175,1118 in the2200×2536 derivative | Source-order context spans **B09K1V13 and B09K1V14**, but the packet lists only V14. Does the crop cover the complete physical row across that verse transition? Preserve any continued-word fragment and distinguish the verse-reference correction from a Greek reading correction. |
| All `continued-word` rows | Original `lb break="no"`, raw word markup, full page and crop | Where does each physical row begin/end inside a continued word? Do not join physical rows or insert/remove letters and spaces to create a normalized edition. |
| Page1450 alternatives | Original TEI apparatus and the native/export loss reports | Which visible reading, editorial alternative and uncertainty span belong to each exact crop? The historical85-row export reports ambiguous layer mapping; a current export without that report is not scholarly confirmation. |
| All proposed quads | Full review derivative, crop bbox and proposed quadrilateral | Is all visible ink, including overbars/supralinear marks and terminal strokes, retained? Geometry remains proposed, with no accepted rectification/alignment. |
| All81 review records | Original source, crop and handbook draft | Record accept/reject/alternative and uncertainty decisions independently of the published reference. Give the reviewed profile/version and reviewer identity; do not infer acceptance from import/export success. |

The packet generator has not been located, so its precise verse-association and
branch-selection policy remains an engineering provenance gap. Do not silently
repair the packet from this addendum. Return decisions as a **new dated review
artifact** naming the original line IDs and evidence hashes, with reading, geometry,
reference-association and uncertainty decisions separately identified.

Rights evidence is unchanged: the retained Michigan photographs have archived
item-level PDM1.0 notices; source transcription terms/attribution remain separately
recorded in the packet. This addendum grants no additional rights. It authorizes
no training, VLM confirmation, weight promotion or dataset release.
