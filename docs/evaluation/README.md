# Evaluation records

- [acceptance-plan.md](acceptance-plan.md) — evaluation design, per-track template, roles and gates.
- Acceptance-sheet drafts (not frozen; thresholds and sign-off pending):
  [papyri](acceptance-papyri-draft.md) · [manuscripts](acceptance-manuscripts-draft.md) ·
  [inscriptions](acceptance-inscriptions-draft.md) (blocked: no image-aligned labels).
- [pilot-p01-report.md](pilot-p01-report.md) — P01 locked-test result; the split was used
  twice, see [locked-set-reuse.md](locked-set-reuse.md).
- [decoding-comparison.md](decoding-comparison.md) — post-extraction-fix P01 dev
  measurements: greedy vs beam, calibration/risk–coverage, error analysis, and the
  architecture/augmentation/regularization/learning-rate/batch-size matrix.
- [model-card-p01-h16-aug.md](model-card-p01-h16-aug.md) — tracked card for the best measured P01 dev candidate (package is in the artifact store).
- [live-provider-smoke.md](live-provider-smoke.md) — opt-in local Ollama smoke test (transport only).
- [correction-effort.md](correction-effort.md) — AI-assisted review metric and its limits.
- [layout-comparison.md](layout-comparison.md) — automatic-layout comparison and the
  negative pilot finding (regenerated after the paper-relative threshold, band-split and
  dev-selected density-filter fixes).
- [layout-learned-comparison.md](layout-learned-comparison.md) — learned row-model attempt
  vs the corrected projection baseline: measured negative result with diagnosis.
- [layout-postfilter.md](layout-postfilter.md) — dev-selected post-filter evaluated on
  held-out pages (density filter: precision up, recall unchanged; now the detector default).
- [throughput.md](throughput.md) — measured import/training/recognition throughput on the
  pilot machine (what is and is not measured).
- [end-to-end-page-cer.md](end-to-end-page-cer.md) — page-level CER for the image-only
  workflow (layout + recognition), with a PAGE-box control that separates recognition from
  layout losses.
- [../handoffs/B-CODE.md](../handoffs/B-CODE.md) — implementation handoff and review notes.

All numbers are pilot-scale baselines unless a sheet explicitly says otherwise. No sheet is
frozen, and locked-test reuse rules apply to every future claim.
