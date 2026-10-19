# Project status — 2026-10-19 (pilot)

One-page operational status. Detailed plans live in [todo.md](../todo.md), completed work in
[completed.md](../completed.md), and package handoffs in [A-PLAN](handoffs/A-PLAN.md) and
[B-CODE](handoffs/B-CODE.md). Nothing below is a release claim.

## What works now (verified this cycle)

| Capability | Evidence |
| --- | --- |
| Native project model, revisions (L0/L1/L2), annotations | contract + service/HTTP tests |
| PAGE import/export, TEI/EpiDoc, ALTO, hOCR, IIIF P3 import | 24 format tests; roundtrip smokes |
| Plain-text export with mandatory loss report | export tests |
| Automatic layout (projection) | measured P 0.386 / R 0.506 on real pages; refuses overwrite |
| Manual correction (move/resize/order) | API + overlay editor, client undo |
| Recognition pipeline (C# CTC, CPU) | line-crop CER ~0.88 measured on M02a; L0 persisted with provenance |
| Image-only workflow | papyrus 4/4 lines recognized; M02a page 25 lines (19 non-empty) |
| End-to-end page CER (layout+recognition) | **0.944 mean** vs 0.905 PAGE-box control across 3 seeds on 6 held-out M02a pages (control range 0.878–0.953: layout effect not separable from training variance at this scale) |
| Training CLI | `train lines` → verified package (manifest + card) |
| Durable jobs | layout.detect, layout.project, recognize.surface, recognize.project, import.dir (cooperative cancellation) |
| Bulk import | 27 PAGE files in 0.6 s (in-process) |
| Export bundle | native + training + 4 formats + manifest |
| Backup/restore | consistent snapshot; checksummed restore (db + images) |
| Workspace | editor with mark rendering + L1/L2 diff, annotations (all kinds), alternatives→L2, geometry (draw/resize/delete/order), jobs history, search, models + recognition, exports, QA badge |
| Shared deployment guard | opt-in API key + read-only; **not** a user system |

## Verification

- 465 tests (464 passed + 1 opt-in skip), 0 failed, 13 suites; Release build 0 warnings/0 errors.
- 21 Angular specs; production build clean; `scripts/pilot-demo.sh` PASS.
- Contract self-review: [docs/contracts/self-review-2026-10-19.md](contracts/self-review-2026-10-19.md).

## Measured numbers (pilot scale)

- Recognition: P01 locked split spent; M02a dev CER ~0.84–0.88 depending on configuration.
- Page workflow: 0.944 mean CER including layout misses; control 0.905 (3 seeds; wide spread).
- Layout: density post-filter adopted (held-out precision 0.335→0.381, recall unchanged);
  merge-rule candidate **rejected** after out-of-domain regression (recorded).
- Throughput: 113 pages/min CLI import; 45 pages/s in-process bulk; layout job ~4 s incl. fetch.

## What is blocked on humans, not code

- Expert adjudication and annotation volume (the critical path for any acceptance claim).
- Inscription labels (I01 needs image-aligned, adjudicated labels).
- P02/P03 per-image rights decisions; specialist profile sign-off.
- Frozen acceptance sheet and fresh held-out witnesses (the P01 split is spent).

## Known engineering gaps

- Multi-column/curved/rotated layout; learned layout underperforms projection (recorded).
- No server-side geometry revision history (client undo + backups only).
- TIFF unsupported by the current imaging backend; full-resolution tiled decode not implemented.
- Shared-server auth is pilot-grade (key + read-only), no accounts/roles/audit.
- Hosted AI provider calls unproven (local Ollama transport smoke only).
- Backend/CPU-only; GPU paths and collection-scale resource profiling unmeasured.

## Next recommended steps

0. Limits and open issues: [docs/release/known-issues.md](release/known-issues.md).
1. Data: finish adjudication of the pilot labels; add fresh held-out witnesses for any claim.
2. Engineering: patch-level layout learning; confidence-aware review queue; tiled decode.
3. Integration: surface B's latest evaluation artifacts; reconcile contract increments before any bump.
