# PDF reference-text orientation refusal

Date: 2026-11-05. Generated standard-font PDFs and raster comparisons only. No Greek reading or geometry acceptance is inferred.

## Reproduced problem

An independent generated PDF contains `ABCD` in Helvetica, 20pt, on a 300×200pt page. At 72dpi the unrotated PDF renders dark ink at `[40,55,94,70]` in top-left pixel coordinates; the extracted reference maps to `[40,55.26,95.56,70]`, within two pixels of those ink bounds.

Page and text rotations exposed unsupported grouping and coordinate behavior:

| Input | Rendered size / ink bounds | Prior optional extraction |
| --- | --- | --- |
| Page `/Rotate 90` | 200×300 / `[130,40,145,94]` | split characters; reversed-width boxes, e.g. left144.36/right130 |
| Page `/Rotate 180` | 300×200 / `[205,130,260,145]` | text emitted as `DCBA` |
| Page `/Rotate 270` | 200×300 / `[55,206,70,260]` | split/reversed characters and reversed-height boxes |
| Text matrix90°, page rotation0 | 300×200 / `[85,106,100,160]` | same reversed-height failure |
| Text matrix180°, page rotation0 | 300×200 / `[105,100,160,115]` | text emitted as `DCBA` |
| Text matrix270°, page rotation0 | 300×200 / `[200,70,215,124]` | same reversed-width failure |

The importer previously dropped reversed-width/height boxes through its size guard and could import reversed text as an L2 reference. Rotated text was documented as uninterpreted but was not explicitly refused.

## Bounded correction

Optional `PdfTextExtractor.Extract` now refuses any selected page whose resolved page rotation is nonzero or whose non-whitespace letters have a non-horizontal PdfPig orientation. It reports `StageContractViolation` with page scope, `pageRotation`, `textOrientation`, and the reason `unsupported_pdf_text_orientation`, and instructs the caller to retry without text extraction. A mixed horizontal/rotated page is refused as a whole; unsupported glyphs are not silently omitted. Arbitrary/diagonal orientation remains unsupported as well.

The API's existing error mapping makes this an HTTP422 contract error. Image-only rasterization remains available for orthogonal page rotations. No new coordinate-transform or reading-order support is claimed. The policy conservatively refuses nonzero page rotation even if a text matrix could counter-rotate a particular glyph; such combinations have not been validated.

`InterchangeService.ImportPdf` preflights the optional extraction before writing page derivatives and reuses that result. On orientation rejection the project is not saved, a new derivative directory is not created, and an existing derivative directory is untouched. Retrying the same PDF with `extractText:false` creates capture-only surfaces normally. This is a specific orientation-preflight guarantee, not a new claim of transactional rollback for every possible later rasterization or store failure.

Primary API basis: the pinned **PdfPig0.1.15** package's `lib/net8.0/UglyToad.PdfPig.xml`, entries `Page.Rotation`, `PageRotationDegrees.Value`, `Letter.TextOrientation`, and `TextOrientation`. Those docs identify clockwise page rotations0/90/180/270 and horizontal/upside-down/vertical/other letter orientation; generated raster evidence above establishes the current local behavior.

## Verification

- Nine new refusal scenarios failed before the correction: page90/180/270, inherited page90, text90/180/270/45, and mixed horizontal/rotated text. Nine imaging controls already passed.
- After correction, all **18 selected imaging tests passed**, including a plain reference-to-ink alignment control and three orthogonal image-only raster tests against rotated ink bounds (two-pixel raster tolerance).
- **3 application tests passed**: refusal preserves a new/existing derivative directory and unchanged project, image-only retry succeeds, and existing searchable-PDF export remains green.
- `git diff --check` passed. No fitting, model loading, live stores, or external PDF fixtures were used.

```sh
dotnet test tests/LeviathanText.Imaging.Tests/LeviathanText.Imaging.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:ArtifactsPath=/private/tmp/leviathan-streams-artifacts --filter 'FullyQualifiedName~PdfTextExtractorTests|FullyQualifiedName~PdfRendererTests'
dotnet test tests/LeviathanText.Application.Tests/LeviathanText.Application.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:ArtifactsPath=/private/tmp/leviathan-streams-artifacts --filter 'FullyQualifiedName~PdfRotationImportTests|FullyQualifiedName~SearchablePdfProjectTests'
```

Remaining PDF work includes validated rotated/mixed-direction text grouping and geometry, figures, full reading order, and broader crop/page-frame combinations. Ordinary horizontal L2 extraction is still reference material rather than visible-text evidence.
