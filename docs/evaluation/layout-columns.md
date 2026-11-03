# Opt-in two-column projection split (2026-11-02)

Status: bounded geometric helper with synthetic tests. **No real-page accuracy claim.** The
default detector remains the unchanged single-column baseline.

## What it does

`ProjectionProfileLayout.Options.DetectColumns` (CLI `layout detect --columns`, API
`?columns=true`) computes a column-ink profile after binarization and content trimming. It
accepts a gutter only when:

- the low-ink run is at least 2.5% of the page width (minimum 4 px);
- its midpoint lies between 30% and 70% of the content width;
- both sides carry at least 15% of the inked columns.

When accepted, row bands are detected independently on the left and right x-ranges and
ordered left column first, then right (or right first with the opt-in RTL flag), with
contiguous `OrderIndex` values. Otherwise the single-column detector runs unchanged.
`LayoutResult.Method` records
`ProjectionProfileLayout-columns`, and the parameters hash includes the option.

This is geometric banding, not a learned region/column model. It cannot recover a page whose
row bands already merge across a shallow or irregular gutter, and it does not handle curved
or rotated layouts.

## Evidence

`tests/LeviathanText.Layout.Tests/LayoutTests.cs` (`ProjectionColumnTests`):

- a synthetic 800×600 page with two columns of four bands yields 4 baseline lines and 8
  column-split lines, left column first, each side on the correct x-range, with contiguous
  order indices;
- a synthetic single column falls back to the unchanged method and line count;
- `RightToLeftColumns` places the right column first with contiguous order indices.

The fixtures are synthetic black rectangles, not manuscript images. No precision/recall or CER
is measured here, and no model/acceptance threshold changes.
