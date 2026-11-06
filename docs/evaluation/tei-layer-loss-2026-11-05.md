# Explicit TEI textual-layer loss — 2026-11-05

A native line can have distinct L1 diplomatic and L2 editorial text without a
reversible source-linked inline annotation. The bounded TEI exporter renders L2
when present. Plain editorial output then re-imports as apparent L1, and previously
the report could incorrectly contain no layer loss. One linked span also failed
to disclose unlinked changes elsewhere on that line.

For every exported line with L2, the exporter now reconstructs its own generated
line using the existing bounded profile parser. It reports `diplomatic-layer` when
the generated markup does not recover native L1 (or absence of L1), and
`editorial-layer` when it does not recover explicit L2 and its text. Equal visible
and editorial text alone does not establish that an explicit L2 layer survived.
The check does not invent a reading, repair the output, re-anchor annotations or
change the native project. Keep native JSON/bundles when a layer loss is reported.

Exact `choice/orig-reg` mappings and exact L2 supplied spans remain reversible and
produce no added layer-loss entry. Partial choices, unlinked insertions/deletions,
editorial-only plain text and otherwise unmarked L2 now disclose their limitations.
A later L2 revision does not automatically inherit old L2 annotation anchors; any
resulting uncovered textual difference is reported too. The profile's prior
annotation-mapping/kind/geometry losses remain independent.

Before the guard, **6 new cases failed and2 linked controls passed**. Afterward,
the complete Formats suite passed **148/148**, including all9 new cases, with zero
warnings/errors. Independent review added an absent-L1/all-supplied-L2 control: an absent layer is not an explicit empty revision, so its presence change is also reported. Controls round-trip a decomposed combining-mark alternative and
a zero-width supplied insertion through the real exporter/importer. Logs are
`/tmp/lt-midday-tei-before.log` and `/tmp/lt-midday-tei-after.log`.

This is textual-layer reconstruction, not full annotation/revision/provenance,
official EpiDoc-schema, geometry or scholarly-equivalence certification. The check
uses the product's bounded importer semantics, including its whitespace handling;
it does not claim independence from that parser. Native AI acceptance adds exact
links only for supported cases and refuses ambiguous mappings rather than treating
an explicit loss report as acceptance evidence.
