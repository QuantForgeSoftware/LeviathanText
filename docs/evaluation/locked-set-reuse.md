# P01 locked-set reuse record

- Owner: **Instance B (EVAL)**; contract **C0-0.4-draft**.
- Split: P01 v0.9 witness-level train 176 / dev 50 / **test 95 locked**.
- Split hash (both runs): `f36aaa2ecd742b5c037e2e5ef63c92810abf2ce906b5f1e19953ddf6272297c6`.
  The identical hash proves both evaluations scored the same objects.

## Runs

| # | When (UTC) | Trigger | Extraction | Locked scalar CER | Perfect lines |
| --- | --- | --- | --- | --- | --- |
| 1 | 2026-10-19T11:45Z | Original one-shot after training/config freeze (`23eb349`) | vertical padding 0.5 | **0.885** | 0/95 |
| 2 | 2026-10-19T13:32Z | Re-baseline after the IMG crop fix `0c6c768` (padding 0.25, 12 px cap) | capped padding | **0.828** | 0/95 |
| 3 | 2026-10-19T14:22Z | Unintentional third run while regenerating reports under the new extraction | capped padding | **0.825** | 0/95 |
| 4 | 2026-10-19T14:49Z | Further report regeneration (template now discloses multiple use) | capped padding | **0.834** | 0/95 |

All runs: `eval-pilot-crnn`, CRNN(h=64), 300 epochs, lr 0.003, seed 42, imported PAGE
layout, non-adjudicated Transkribus labels. Run-to-run variation across runs 2–4 is
0.825–0.834 CER. Run 4 is the current content of
[pilot-p01-report.md](pilot-p01-report.md); runs 2–3 share the same configuration. The
generator template now emits this disclosure so a rerun cannot silently restore “one-shot”.

## Status and rule

- The locked split is **spent for threshold/selection purposes**. It may not guide model
  choice, prompt examples, thresholds or further tuning.
- Run 2 was justified as a *measurement-integrity re-baseline* after a preprocessing
  defect changed the model's input distribution; it did not select the best configuration.
  Runs 3–4 were not planned and add no new evidence; they are recorded because the split
  was scored again. Do not use any of these numbers for selection.
  Dev-only comparisons (see [decoding-comparison.md](decoding-comparison.md)) were used for
  selection.
- Any further acceptance claim on P01 requires **fresh held-out witnesses** (new objects,
  not re-splits of these 27) plus disclosure of this history, per acceptance-plan §1.
- The best dev configuration (height 16, width 4, augmentation on, weight decay 0) has
  **never been scored on the locked split**. Doing so would be a third use; it requires
  fresh witnesses first.
