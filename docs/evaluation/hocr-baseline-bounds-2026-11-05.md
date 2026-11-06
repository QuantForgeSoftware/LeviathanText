# hOCR baseline bounds fallback

Date: 2026-11-05. Generated geometry fixtures only; no real Greek geometry was adjudicated.

hOCR export previously wrote `bbox 0 0 0 0` whenever a line had no polygon, even when it had a valid baseline. Export now delegates extent calculation to `LineGeometry.Bounds`, preferring the nonempty boundary and falling back to baseline points. Both the line and its exported single-word span receive those bounds.

Existing nonempty-polygon extents and midpoint-to-even integer rounding are preserved. Direction attributes, vertical classes, signed text angles, baseline serialization, source objects, hOCR import placement, and recognition geometry are unchanged. Missing geometry still yields a zero box. A point or straight horizontal/vertical baseline keeps its zero-area bounds; export does not invent a text height or width.

Evidence:

- Thirteen generated regression cases failed before the fix: twelve LTR/RTL/top-to-bottom/bottom-to-top baseline fallbacks at true/false/default topline positions, plus a zero-length baseline away from the origin. Eight existing/control cases passed.
- After the fix, **21 hOCR tests plus 5 geometry-helper tests passed**. Assertions cover exact integer coordinates in line and word boxes, boundary precedence, existing rounding, degenerate/missing geometry, direction metadata, and unchanged input arrays.
- Topline placement is exercised by passing helper-generated baselines into export; this adapter does not introduce a new topline import policy.
- `git diff --check` passed.

```sh
dotnet test tests/LeviathanText.Formats.Tests/LeviathanText.Formats.Tests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -p:ArtifactsPath=/private/tmp/leviathan-streams-artifacts --filter 'FullyQualifiedName~HocrFormatTests|FullyQualifiedName~LineGeometryParityTests'
```

Remaining geometry parity: bbox-to-baseline adapter placement, explicit synthesis policy for missing imported baselines, broader adapter integration, UI conversion, shared-field conversion round trips, and specialist validation on real material. This bounded baseline-to-bbox integration does not close all sixteen portable container scenarios.
