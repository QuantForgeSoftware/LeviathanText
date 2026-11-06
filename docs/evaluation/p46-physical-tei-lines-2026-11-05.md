# P46 physical TEI line integrity — 2026-11-05

The 85-row generic TEI import was structurally incorrect. It folded nested and
`break="no"` physical milestones and invented boundaries at verse `ab` containers.
The corrected default applies to new imports and yields 27/26/28 rows from the
unchanged original TEIs. These counts match the packet's proposed row counts only;
they do not validate readings, crop-to-text correspondence or geometry. All 81
specialist decisions remain pending. No training, label revision, model promotion,
reimport or migration was performed.

There is no demonstrated causal link between this generic importer defect and the
old P46 training error rate. That training used packet/external line extraction;
its target-generation path must be independently traced before making such a claim.
The old 85-row portability rehearsal remains a historical capture, unchanged.

## Immutable source evidence

| Page / folio | SHA-256 of original TEI | Global `lb` count | `lb` directly in `w` | Old imported rows | New physical rows |
| --- | --- | ---: | ---: | ---: | ---: |
| 1440 / 081v | `a7ff05653a9fc90df92f9204f3b3fab64204b5ca91558828caa55d56eff5bff2` | 27 | 7 | 30 | 27 |
| 1450 / 082r | `b03c98e5f9fd924bb2093559b8ed8080ce633f1b3ed8285792b6abeb9c5c3742` | 26 | 11 | 23 | 26 |
| 1460 / 082v | `ed45977b6ad94c2d7a0696ce09a69543e267aa0893f57d5648a3fa37ccb82239` | 28 | 3 | 32 | 28 |

Each source has one leading milestone outside the selected book division; remaining
milestones occur directly in verse `ab` blocks or within words. No milestone has
`@n` or `@xml:id`, and none lies inside an alternative-reading branch. Thus packet
`L1`… labels are sequence labels, not explicit original XML identifiers. Crosswalk
identity uses the source hash, folio, full structural element path and milestone
ordinal, plus explicit verse/container context. No reading comparison is claimed.

The original files are retained at
`.artifacts/datasets/ntvmr-pilot/specialist-packet-v1/sources/`. The importer still
selects the same edition/book division; this was not a comparison of different
source editions. Original source hashes were rechecked after the replay.

TEI's `break="no"` says the orthographic token continues across a boundary; it does
not cancel the physical line milestone. The correction follows the
[official TEI breaking attribute documentation](https://www.tei-c.org/release/doc/tei-p5-doc/en/html/ref-att.breaking.html).

## Correction and preserved compatibility

The [approved design](../tasks/tei-physical-lines-2026-11-05.md) describes the new
physical cursor. Verse/container transitions preserve source context without
flushing a row. All nested/direct physical milestones split rows. Inline wrappers
are fragmented and passed through the existing reading/annotation builder, retaining
its whitespace policy. Declared row IDs/numbers and page contexts follow milestones;
explicitly identified/numbered empty rows are preserved. No-milestone editions
retain the previous logical-`ab` fallback.

Milestones inside alternative branches refuse explicitly before returning a partial
import: this avoids guessing L1/L2 row correspondence. Ordinary same-row alternatives
retain existing behavior. Source contexts, including all contributing verse labels,
attributes and enclosing division identities, are preserved in bounded base64 JSON
line metadata. The exporter carries it as `data-tei-source-context`; the encoded
value is safe from whitespace splitting and reimport preserves the original context.
All generated and supplied dedicated context payloads are capped at 64 KiB encoded,
validated as whitespace-free base64 JSON objects, and treated as untrusted provenance,
never as identity or reading authority. Validation also covers logical fallback and
export; conflicting supplied contexts on one row refuse instead of silently exporting
the first. Orthographic `break` metadata survives export/reimport. All explicit
contributing `xml:lang` values are retained by typed import; the single-`xml:lang`
export limit produces an explicit `line-languages` Loss while source-context keeps
the original attributes. Script and BiDi are not inferred. Unmarked physical
prefixes retain content with an explicit Note. Existing canonical one-`ab`-per-line
native exports retain line IDs, numbers, texts and anchored annotation round trips.

## Regression and replay receipts

Two invented ASCII fixtures failed before the correction:

- A nested word milestone expected rows `A`, `B`; old import returned `AB`.
- A verse transition without a physical milestone expected `AB`, `C`; old import
  returned `A`, `B`, `C`.

Both now pass. Independent review added eight reproduced failures for forged/oversized metadata,
conflicting supplied contexts, mixed languages and an unmarked prefix. All were
corrected, with additional controls for fallback validation and forged identity.
The final full Formats run passes **196/196**, zero skipped/failed,
with no build warnings/errors. Added tests cover direct `break` variants, nested
words, verse transitions, inline annotation fragmentation, page transitions inside
an `ab`, empty declared rows, cross-line alternative refusal, safe context round
trip and the no-milestone fallback. Existing native round-trip tests remain green.
The former `break="no"` joining expectation was corrected; the unrelated synthetic
NTVMR layered-reading fixture now declares its intended third physical row with an
explicit milestone instead of relying on the old verse-boundary bug.

Evidence: `.artifacts/experiments/tei-physical-lines-v1/`, including before/final TRX,
`source-milestones.json` structural paths, `physical-import-structure.json` replay,
probe source and production/test source hashes. The replay outputs line metadata
only, not source reading strings. The initial private/tmp full-suite receipt had one
expected obsolete fixture assertion and one existing relative-XSD lookup failure;
final isolated repository-local artifacts restore the existing fixture path and
all tests pass. No normal build outputs were used concurrently with parent checks.

## Pending association question

Page 1440 proposed row 10 crosses explicit source verse contexts V13/V14, while the
packet's stored `verse_refs` lists V14. Preserve this difference for the specialist
and packet-extractor review. The original packet generator has not been located,
so its exact branch/association policy has not been inspected. Do not revise its
reading, crop or verse association from a structural import count. Original packet,
source transcriptions, geometry and all pending statuses remain unchanged.

## Application and HTTP refusal boundary

Independent integration review found that safe parser refusals were not translated
at the native import boundary. Four generated cases (cross-line alternatives,
malformed dedicated context, oversized supplied context and malformed XML) threw
raw parser exceptions in the application and returned HTTP500. The corresponding
positive generated native metadata control passed before the boundary correction.
Receipts `app-before-source-identity-control.trx` and
`http-before-valid-controls.trx` retain this evidence.

`InterchangeService.ImportTei` now translates only InvalidDataException,
XmlException and FormatException thrown by `TeiXml.Import` into the existing
StageContractViolation contract, producing HTTP422 with a constant actionable
message. Missing-file checks still precede parsing. File reads, image decoding and
store writes remain outside this narrow catch. Source parser reasons, reading
fragments and private paths are not reflected in the response.

The four refusal cases now preserve exact persisted NativeJson, project version,
objects and attachments; existing invalid image bytes prove parsing refuses before
decode, and no derivative is created. Separate controls preserve missing-file
InputMissing precedence and independent image RejectedArchive errors. Actual native
import/save/reload/export is exercised at both application and HTTP levels: both
contributing V1/V2 contexts survive the scalar legacy n tag, language values survive
typed import, multilingual export declares its bounded loss, break=no survives,
original XML attachment is exact and exporting performs no save. Native internal
IDs remain import-namespaced as before; original source XML IDs remain separately
preserved interchange identities.

Final focused application TEI verification: 17 passed. New HTTP integration: 5
passed. Both builds report zero warnings/errors. Receipts are
`app-boundary-final.trx` and `http-boundary-fixed.trx`. Full Formats subsequently passes 196
cases after the ignored-editorial correction; parent owns the subsequent combined release gate. No global exception
handler, API route, decoder, model or persistence policy changed.

## Ignored editorial subtrees

A second independent review identified ignored-subtree failures; its corrected
standalone baseline later reproduced 11 failures with four positive controls,
including the edition-selection and placeholder-loss followups. Physical milestones inside `note`, `bibl` and `ref` incorrectly split or relabeled
witness rows, activated physical mode, supplied a hidden identity or caused a false
alternative refusal. Hidden context attributes and logical `ab` children could also
influence witness import. The authoritative independent before receipt is
`.artifacts/tei-physical-independent-review/clean-before.trx`; its true-current
actual-assembly retest `clean-after.trx` passes 15/15 with zero warnings/errors.
See [the independent review](tei-physical-independent-review-2026-11-05.md).

The same editorial-subtree exclusion now governs physical-mode selection, alternative
guards, leading identities, global page positions/provenance, source-context
validation, logical block selection and physical traversal. Skipped content retains
an explicit Note. Real nested-word and page milestones remain effective. Two further
ASCII fixtures proved ignored-only wrappers could still invent empty witness rows;
recursive visible-content checks now ignore editorial text, and ignored-only blocks
cannot supply fictitious source context. Dedicated tests preserve both the absence
of that invented context and the presence of the skipped-editorial Note.

The intermediate Formats receipt `physical-editorial-final-context.trx` passed 188/188 with
zero warnings/errors. The original three P46 TEI hashes and structural counts are
replayed again; all source/packet/training decisions remain unchanged. The parent separately added an explicit `diplomatic-layer` Loss when an absent L1
placeholder would reimport as an explicit empty reading; it introduces no new
absence format or interpretation of comments.

A final consistency check found hidden primary editions could override the visible
witness edition. Three generated note/bibl/ref cases reproduced zero imported rows.
All three edition-selection tiers now use the same visible-division candidate set;
additional controls retain real primary-edition priority and visible-book fallback.
The final combined Formats receipt `physical-hidden-edition-final.trx` passes
196/196, zero warnings/errors, including the parent's two placeholder-loss controls.
The independent reviewer corrected its standalone harness compile glob after it
noticed its retained before-source copy shadowed the current Formats assembly;
its initial receipt must not be described as a warning-free current-code retest.
Our full production-project receipts contain no such copied implementation source.
