# Physical TEI line import correction — design for review

Owner: resource agent. Initial implementation ownership: `TeiXml.cs` and focused
Formats tests only. Status: implemented after parent approval; 196 Formats regressions pass. Independent review
corrections and structural replay are documented in the evaluation report.

## Confirmed problem

The three preserved NTVMR P46 TEI documents contain 27/26/28 `lb` milestones,
matching the review packet's 81 proposed rows structurally. The existing importer
instead emits 30/23/32 rows. It selects the same source edition: the difference is
not explained by importing a different edition. It splits every verse `ab`
independently, recognizes only direct-child `lb`, and treats `break="no"` as an
instruction to join physical lines. Nested word milestones are ignored.

TEI's `break` attribute describes orthographic token continuity, not whether the
physical line ends. A word can continue across an `lb break="no"`; the milestone
still separates physical lines. See [TEI att.breaking](https://www.tei-c.org/release/doc/tei-p5-doc/en/html/ref-att.breaking.html).
The existing test requiring joined text for `break="no"` codifies the wrong physical
interpretation and must change, with separate continuity metadata coverage.

## Compatibility decision

Correct the default for **new TEI imports**. Do not add a hidden alternate profile
that preserves incorrect physical geometry. Do not migrate, deduplicate, reimport
or reinterpret stored projects, frozen labels, the original 85-row portability
rehearsal, review packet or source TEI. Existing one-`ab`-per-line native exports
already contain a leading `lb`; they must retain exact line IDs, numbers, texts,
editorial annotations and export/import behavior after this correction.

Documents with no `lb` anywhere in the selected edition retain the existing bounded
logical-`ab` fallback. This explicitly does not claim physical line identity.
A mixed edition containing physical milestones must not silently treat every
unmarked verse boundary as a physical line. Substantial unmarked text before its
first milestone remains a clearly reported unnumbered physical prefix, never dropped.

## Proposed bounded algorithm

1. Preserve existing edition selection, source attachment, provenance, duplicate-ID
   checks, size limits and unsupported-construct reporting.
2. If the selected edition has physical `lb` milestones, traverse it once in source
   order with one physical-line cursor. `ab` and verse/chapter containers update
   context; they do not flush a row. Every `lb`, including one nested in `w`, and
   including `break="no"`, closes the prior row and starts the next. A leading
   milestone does not create an empty prefix row. Page context is read at each row's
   milestone/content rather than once at the enclosing `ab`; a `pb` changes folio
   context and must not join content across pages silently.
3. Split ordinary inline wrappers at physical boundaries by cloning their names and
   attributes around their respective fragments. This preserves `w`, `hi`, `unclear`,
   `supplied` and annotation offsets on each row. Preserve `break` and word-continuity
   metadata independently from the physical split. No punctuation, spaces or readings
   are inferred merely from a boundary. Strip indentation-only structural whitespace
   without losing explicit word spaces inside inline content.
4. A milestone's explicit `xml:id` and `n` identify its following row. Keep the
   existing leading-`lb` fallback to the containing line `ab`'s ID/number for canonical
   native exports. Do not reuse a verse-level `ab` ID across several physical rows.
   Apply existing stable-ID uniquification and duplicate warnings. Preserve an
   explicitly numbered/identified empty row; do not invent empty rows at container
   boundaries or an unmarked document end.
5. Collect **all** contributing `ab` source identifiers/numbers and structural
   contexts for each row, in source order, as bounded metadata. Preserve row `n`
   separately from verse `ab@n`. Use existing line tags with a dedicated namespaced
   source-context value if sufficient; do not overload the numeric row field or
   silently select one verse when the row crosses two. Source attachment remains
   authoritative. Export must preserve the context tag through existing `data-*`
   metadata or emit an explicit loss if the bounded profile cannot represent it.
   Validate supplied and generated dedicated metadata uniformly; treat it as untrusted
   provenance, refuse malformed/oversized/conflicting context, and never use it as
   source identity authority.
   No new native schema or inference of verse identity is needed.
6. A physical milestone **inside an alternative branch** (`choice`, `app`, `orig`/
   `reg`, `rdg`, `lem`) can make row-to-L2 correspondence ambiguous. Do not independently
   zip branch fragments or guess offsets. Initial safe policy: explicitly refuse
   such an import with a precise unsupported cross-line-alternative error before
   producing a publishable result. Alternative constructs wholly contained in one
   physical row retain the existing builder behavior and annotations. If the real
   P46 source hits this restriction, report exact structural locations and leave
   that import pending rather than weakening it to obtain expected counts.
7. Keep existing no-milestone `SplitSegments(ab)` behavior in a clearly separate
   logical fallback. Route physical fragments through the existing `SegmentBuilder`
   to avoid a second reading/annotation implementation.

## Required proof before accepting implementation

Generated fixtures must fail against the old importer, then pass after correction:

- Nested word `lb break="no"` splits rows and retains explicit milestone IDs/numbers.
- Two verse `ab` blocks on one physical row do not create a spurious boundary;
  a later milestone does create one. Combined fixture: three explicit physical rows
  crossing verse blocks, exact ASCII text per row, no speculative Greek corrections.
- Direct-child `break="no"`, `break="yes"` and absent `break` all delimit rows;
  continuity metadata remains distinguishable.
- Page boundaries within an `ab`, leading/trailing milestones, declared empty rows,
  duplicate IDs and mixed inline wrappers preserve content and bounded metadata.
- Same-row `choice`/`app` annotations round-trip unchanged; cross-line alternatives
  refuse explicitly with no partial import publication.
- No-`lb` legacy logical blocks and existing native export/import fixtures remain
  stable; line IDs, L1/L2 and anchored annotations are asserted, not merely counts.
- Run full Formats tests after focused regressions; inspect any changed round-trip
  assumptions before broadening code ownership.

Then replay only **structural** import counts/IDs/n/folio/context on the unchanged
three preserved TEIs. Expected physical counts are 27/26/28 if supported. Equality
with proposed crop-map counts establishes no reading, alignment, line geometry,
training-label or Greek-specialist acceptance. Keep the crop map and source files
unchanged and leave all specialist decisions pending.

## Structural crosswalk and remaining question

No `lb` in these three originals has `@n` or `@xml:id`; packet `L1`… labels are
sequence labels, not explicit source XML identities. Crosswalk keys must therefore
include immutable TEI hash, folio, full element path, global milestone ordinal and
contributing explicit verse `ab@n`, without presenting generated IDs as source IDs.
The packet's verse-reference association differs at page 1440 row 10: source-order
context spans V13/V14, while its stored `verse_refs` lists V14. This requires an
extractor/branch-association check; it is not authority to change a reading or crop.
The original packet generator has not yet been located, so its precise association
policy must not be claimed as inspected. The source TEI and preserved packet remain
unchanged while that question is recorded.

Independent review follow-up: note/bibl/ref subtrees are excluded consistently from
all milestone, metadata and page/identity decisions, matching the reading builder.
Ignored-only wrappers do not create witness rows/context; skipped-editorial Notes
remain explicit. Generated before failures and final196-case verification are in
the evaluation report. The parent separately owns placeholder layer-loss guarding.

The ignored-editorial predicate also filters all edition-selection tiers; visible
primary priority and fallback remain covered. Final combined Formats196 includes
the parent's explicit absent-L1 placeholder export-loss control.
