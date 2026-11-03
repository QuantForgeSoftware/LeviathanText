# Bounded TEI/EpiDoc interchange — layered profile (2026-11-02)

Status: implementation and test evidence for the bounded profile in
[interchange-and-api.md §1.2](../contracts/interchange-and-api.md). This is **not** a claim of
full TEI P5/EpiDoc support, schema validation or scholarly acceptance. Fixtures are invented
unless a record says otherwise.

## What changed

| Distinction | Before | Now |
| --- | --- | --- |
| Lines | One `<ab>` became one revision; `<lb>` and `@n` were ignored | Every `<lb>` starts a line; `@n`, `@xml:id`, `break="no"` and nested `textpart` divs are handled |
| Supplied text | Entered L1 | L1 excludes it; an L2 revision carries it and `SuppliedEditorial` is anchored there |
| Abbreviation | Text pair only, no link | L1 `<abbr>` + L2 `<expan>`, with `ExpansionRevisionId`/`ExpansionRange` round-tripping |
| Regularized reading | Not read | `<choice><orig>/<reg>` (and `sic/corr`) produce L1/L2 readings plus an `AlternativeReading` annotation |
| Corrections | Flattened into text | `<add>` → `Added`, `<del>` → L2 `Erased`, `<addSpan>/<delSpan>` → milestones, `<handShift>` → `DifferentHand` |
| Responsibility | Hard-coded importer author | `respStmt`, `change`, `idno`, `licence` and title are read into document provenance; export writes them back |
| Surface/image link | Absent | `<facsimile><surface xml:id><graphic url/n/width/height>` round-trips and is exported from the native capture |
| Unsupported markup | Reported per element and then discarded | Aggregated by element name/count; the complete source XML is returned and stored as a checksummed `NativeAttachment` (2 MiB cap) |
| Loss report | Inline XML comments only | `TeiExportResult.Report` is a `LossReport` with categories/counts; the XML also carries an inline comment |
| EpiDoc bootstrap text | `<app>` lemma+readings and `<abbr>+<expan>` were concatenated | One reading is selected; the pair is reported, never concatenated |
| NTVMR-style verse markup | Word spaces between `<w>` disappeared; non-numeric `@n` and `<pb>` lost; standalone `<abbr>`/`<hi rend="overline">` unsupported | Word spaces preserved; verse `@n`/`@part` kept as line tags; `<pb>` folio is recorded and exported; `<app><rdg type="orig"/><rdg type="corr">` maps L1/L2; overline becomes a `Sign` annotation and round-trips as `<hi>` |

## Verified behaviour

- `tests/LeviathanText.Formats.Tests/TeiProfileTests.cs` — scenarios covering line
  splitting/`break="no"`, layered supplied/expansion links, facsimile/provenance round-trip,
  additions/deletions/hand shifts, orig/reg, unsupported aggregation + original attachment,
  loss-report categories, `px`-suffixed graphic dimensions, cleaned availability HTML and
  deterministic ids on re-import when the source declares `@xml:id`.
- `tests/LeviathanText.Formats.Tests/TeiProfileTests.cs` also covers a NTVMR-style excerpt:
  spaces between `<w>` elements survive, `B09K1V10`-style `@n` and `@part` are retained,
  orig/corr readings map to L1/L2, overline `<hi>` becomes a `Sign`, and the export restores
  the apparatus and overline.
- `tests/LeviathanText.Formats.Tests/EpiDocImportTests.cs` — apparatus/choice pairs no longer
  concatenate; word spaces between inline elements are preserved.
- `tests/LeviathanText.Application.Tests/TeiImportTests.cs` — product `ImportTei` creates
  lines with numbers, L1/L2 revisions, annotations, image link, rights metadata, external
  refs and the original attachment; `ExportTeiWithReport` restores the facsimile and is
  lossless for that fixture.
- `tests/LeviathanText.Api.Tests/ApiContractTests.cs` — `POST /projects/{id}/import-tei`,
  `GET /projects/{id}/surfaces/{id}/tei` and retained-attachment download end-to-end.
- `tests/LeviathanText.Cli.Tests/TeiCommandTests.cs` — CLI import/export and
  `--skip-duplicates` reports the existing object without a second import.

## Real-record smoke (2026-11-02)

The public NTVMR P46 page 1440 TEI
(`docID=10046&pageID=1440&format=teiraw`, CC BY 4.0) was fetched for inspection only and is
not committed. `import tei` with a local stand-in image produced **30 lines / 11 L2 readings**
and retained the source as an attachment. Import notes were seven standalone-abbreviation
notes and nested `<w>`/`<hi>` flattening notes; there was no unsupported-element *loss*. The
export restored verse labels such as `n="B09K1V10"`, `<hi rend="overline">` spans and
`<rdg type="corr" hand="corrector">` readings. This is a structural smoke, not a review of
readings, word division, accents or image alignment, and it does not make the record
training/acceptance data.

## Limits retained

- No XSD/RelaxNG schema validation; only the pinned TEI namespace and DOCTYPE refusal.
- The profile covers the distinctions above, not all of EpiDoc (e.g. full `<listWit>`,
  `@cert`, span-to/from validation, text-critical apparatus structure).
- `<del>` text is kept in L2 as an `Erased` span for evidence; it is not a claim that the
  erased letters are legible in the diplomatic layer.
- Attachments are inline, capped at 2 MiB. Larger sources are still parsed but not retained;
  callers must keep the original file. The source SHA-256 is stored in project metadata even
  when the attachment is omitted, so repeat-import detection still works.
- Product `import tei` still requires a local image; remote TEI image URLs are recorded but
  not fetched.
- A TEI containing multiple `<pb>` pages is imported as one surface; page breaks are recorded
  in metadata/counts but are not split into multiple surfaces yet.
- Export maps annotations by exact text when layers differ. A span that cannot be located is
  reported (`annotation-mapping`) rather than guessed.
- No specialist has adjudicated a real record's readings, and no acceptance threshold is
  changed by this work.
