# Searchable PDF export (2026-11-03)

Status: bounded convenience export. The native/TEI records remain authoritative; this is not a
scholarly interchange format.

## Behaviour

`export searchable-pdf` / `GET .../searchable-pdf` draws the capture image on a PDF page and
places each line's L1 text invisibly at the line's source rectangle (300 dpi mapped to PDF
points). The text remains selectable/searchable; PdfPig extraction confirms polytonic Greek
(`ἄνθρωπος`) survives the round trip.

- Missing capture dimensions fall back to the line geometry bounds.
- Lines without geometry are omitted and counted in `unplaced-lines`.
- The mandatory loss report counts L2 revisions, unclear/illegible/lost annotations,
  alternatives, provenance and unplaced lines. The CLI prints it; the API returns it with
  `?report=true`.
- Bundles include each surface's `searchable.pdf` and loss text when geometry permits.

## Tests

- `tests/LeviathanText.Imaging.Tests/SearchablePdfTests.cs`: image + selectable polytonic text;
  blank page when the image file is missing.
- API endpoint raw PDF and `?report=true`; CLI export; bundle inclusion.

## Limits

- Skia's PDF backend shapes text with the default typeface; very exotic combining sequences or
  scripts outside the task's Greek scope are not claimed.
- Only L1 visible text is searchable; L2 readings, alternatives, confidence and annotations are
  not embedded.
- Page rotation/curved baselines are not modelled; the text position is the axis-aligned
  rectangle.
- This does not produce a PDF/A or archival PDF claim.
