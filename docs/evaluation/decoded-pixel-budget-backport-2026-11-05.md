# Strict returned-pixel budget backport — 2026-11-05

Status: **the exact root-approved patch is applied and passes focused and central no-fitting verification**. This correction is independent of the permissive TIFF replacement. It retains ImageSharp3.1.11 and every existing public signature/backend.

`DecodeBounded` could return more pixels than its explicit budget. Independently frozen examples returned four JPEG pixels, two PNM pixels and three PBM pixels for budget one; thin 12000×1 images also overshot budget128. The approved [policy](strict-decoded-pixel-budget-proposal-2026-11-05.md) changes only overshooting results and invalid budget acceptance. Existing outputs already within budget remain unchanged.

The narrow patch rejects nonpositive budgets before decoding, preserving the missing-path error priority. For an oversized target it floors the uniform ideal dimensions, then clamps the longer axis by integer division if minimum-one rounding still exceeds the budget. A fallback source is resampled once. An oversized native minimum-scale intermediate is resampled once with its actual color/alpha representation and existing linear sampling. Original dimensions, encoded source checksum and independent ScaleX/ScaleY remain intact.

This bounds returned pixels. It does **not** bound native codec scratch, minimum-scale intermediate or temporary full-frame fallback memory in proportion to the requested budget. It does not establish model accuracy equivalence for callers previously consuming an oversized result.

## Evidence

Artifact directory: `.artifacts/experiments/sec01-budget-backport-v1/`.

- Root approved the independent five-fixture literal/derived oracle policy before v10 output assessment. All five candidate outputs match exact dimensions/pixels, stay within budget and preserve source identity.
- The production-backport experiment copies the exact current Imaging/Core/Contracts source and changes only `ImageDecoder`'s budget logic. Its ImageSharp package reference remains3.1.11.
- The same new public-API regression class runs against both copies: **before9 failed /5 passed; after14 passed /0 failed**. Cases cover thin PNG/JPEG, native minimum-scale correction, constant ASCII-P2 fallback, unchanged safe native/PNG/fallback/full outputs, invalid budgets and missing-path priority. The ASCII control avoids unrelated PNM source-correctness exceptions.
- Only `DecodedPixelBudgetTests` was selected. The exact six fitting exclusions were also supplied. No fitting, model or provider was used.
- `registration.json` pins70 source/test files before execution; `outcome.json` confirms unchanged hashes and that main `ImageDecoder.cs` is unchanged. `git apply --check` succeeds. Matching TRX/logs are retained. Failed-before versus successful-after durations are not a throughput comparison.

Review artifact: `production-budget.patch`, SHA256 `f751a9617593ac66304f826e812178e72ff3ac0094782bca430c9e0db6ac5d55`. It includes only the runtime file and the new regression class; no package, source-fork, TIFF/PNM decoder, archive validator, training or other application change. Root owns the subsequent production application and focused/full engineering verification. Those results are not yet claimed here.

## Production backport verification

After the independent measurement window released, root applied the exact approved patch. The production regression class passes14/14 in `.artifacts/overnight-2026-11-05/budget-backport/budget.trx`; the complete current no-fitting gate passes1935 with18 intentional skips and zero failures (`engineering-v3`). Compilation reports zero warnings/errors. Main Imaging still references ImageSharp3.1.11; this is the budget correction, not dependency-security closure. The earlier serial compiler retry and failed receipt are retained in the central gate records.
