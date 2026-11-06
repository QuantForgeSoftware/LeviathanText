# Derivative crop overflow correction

Source-only proposal. Main source is unchanged; no test, native drawing, model or resource workload was executed. Root will run the real public API before/after after the current NativeAI full24 quiet window. Artifacts: `.artifacts/experiments/derivative-crop-bounds-v1/`.

`DerivativeFactory.CropBitmap` in current `src/LeviathanText.Imaging/ImageDecoder.cs` checks unchecked `Left+Width` and `Top+Height`. A positive `int.MaxValue` coordinate plus a one-pixel extent wraps negative and can pass the source-bounds condition, leading to a blank tiny derivative rather than the promised typed refusal. This is a public SDK coordinate correctness bug; the current normal single-crop LineCropper path is not evidence of real exploitation or corpus impact.

The proposed hunk checks positive extents, bounds the starting coordinates and compares extent with `source.Width-Left` / `source.Height-Top`. No unchecked end-coordinate sum remains. Valid exact-edge crops, pixel sampling, transform metadata, source ownership and decoder-class code are unchanged. This is separate from derivative intermediate ownership and native canvas exception cleanup.

The proposed public regression class uses a2×2 asymmetric bitmap and1px output extents for overflowing Left/Top. It disposes any unexpected successful old result before the expected-throw assertion fails, so the fail-before experiment is itself safe. Negative/empty/outside controls keep typed refusals; exact-origin/edge crops compare every actual pixel and exact transform metadata and verify caller source preservation. No large canvas, fitted model or source packet is involved.

Before/after must target current compiled main/proposed public `DerivativeFactory.Apply`, not a newly invented missing helper or a relabeled extraction baseline. Run only the named safe test class first and preserve failures, then full Imaging and appropriate central no-fitting verification after integration. Root owns any main merge; use only the predicate hunk to preserve root's decoder budget/dimension work and the separately proposed ownership change.
