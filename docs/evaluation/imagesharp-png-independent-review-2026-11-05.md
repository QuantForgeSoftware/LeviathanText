# Independent PNG replacement review and partial adoption — 2026-11-05

The reviewed AI-only replacement is adopted in current source after integrator approval. `LeviathanText.AI` now uses the existing product's SkiaSharp 4.153.1 family rather than directly referencing ImageSharp. **Imaging still references ImageSharp 3.1.11; all six product-wide advisory issues remain open.** This is neither a complete permissive-dependency/SBOM certification nor completion of the security release gate.

## Independent finding and correction

The initial prototype accepted a PNG containing an orphan `fcTL` or `fdAT` chunk without `acTL`, while the existing ImageSharp implementation rejected both generated inputs. Its explicit guard only rejected `acTL`. This broadened acceptance around the existing static-only contract; adoption was held until the candidate explicitly rejected all three animation-control/data chunk types. The [PNG specification](https://www.w3.org/TR/png-3/) defines these three APNG chunk types and uses `acTL` to recognize an animation.

The prototype owner corrected that guard and added the two independently generated fixtures. Independent retesting of the corrected source confirms both are rejected. Reviewed/adopted `VisionImage.cs` SHA-256: `9b85a2f7182d92165d9c6bbe995350b37cb28ed0c464cb0982e99b6c9717de20`.

Both baseline and corrected candidate accept the generated unknown-critical `ABCD` chunk. The specification treats unknown critical chunks as fatal for a decoder attempting image extraction. This is a pre-existing acceptance weakness, not a newly introduced mismatch; the candidate must not be described as a full PNG conformance validator. It checks the stated framing/CRC/static-image constraints and delegates pixel decoding to Skia. The candidate also deliberately rejects some previously tolerated malformed/trailing input, as recorded in the [prototype research](imagesharp-replacement-research-2026-11-05.md).

## Additional independent probes

Evidence root: `.artifacts/imagesharp-replacement-independent-review/`. The review compiled separate copies of the baseline, original candidate and corrected candidate, without editing shared prototype files or calling a model/provider.

- **Eight additional valid PNG cases** pass both decoders: Adam7 RGB8; grayscale 1/2/4-bit; indexed 1/2/4-bit with transparency; and RGB `tRNS`. All preserve dimensions, the exact original transmitted data URI and SHA-256. These are engineering encodings, not real manuscript validation.
- **136 independent IEEE CRC comparisons** exercise both the ARM fast path and portable slicing-by-eight implementation at 17 lengths from zero through 8 MiB, with offsets 0/1/7/9. An independently implemented per-bit reference supplies expected values; all match. The host executes ARM IEEE instructions. This supplements the prototype's known-vector and 0–1024-byte/alignment controls; it is not an Intel/Linux execution proof.
- **Thirteen framing/metadata probes**, then all 21 combined cases against the corrected candidate, complete within a 30-second per-process timeout. These include invalid chunk-type bytes, reserved-bit ancillary chunks, nonconsecutive IDAT, orphan animation chunks, unknown critical chunks, plain PNG and generated compressed ICC/text payloads representing 1/16/64 MiB when expanded.
- The compressed-metadata probes caused no observed timeout or memory exhaustion. Candidate process peak RSS was approximately 58–61 MB, compared with approximately 85 MB for the baseline's larger ICC samples. These single cold-process probes are not throughput comparisons, valid-ICC coverage or proof of a global native allocation/CPU ceiling.

Input arithmetic is bounded before native decode: the copied encoded bytes are limited to 8 MiB, chunk spans cannot extend past that snapshot, dimensions must be nonzero and their unsigned product cannot exceed four million pixels. The native destination bitmap is therefore at most about 16 MB of RGBA pixels. Stream, codec and bitmap disposal are explicit. Native scratch/profile work, process-wide concurrency, codec defects and allocation failures remain outside a hard application-enforced memory budget.

## Throughput and adoption decision

The final post-guard ABBA registration contains 12 separate processes and seven registered fixture/harness/AI binary hashes. Independent hash comparison found zero mismatches. Receipts are `.artifacts/imagesharp-replacement/abba-final/{registered-manifest.json,summary.json}`.

| Generated input | Baseline median ms | Candidate median ms | Candidate / baseline |
| --- | ---: | ---: | ---: |
| 16 × 8 | 0.007599 | 0.006871 | 0.904 |
| 512 × 256 | 1.740287 | 1.750520 | 1.006 |
| 2048 × 1024 | 27.260670 | 28.251630 | 1.036 |

The large-image candidate median is **3.6% slower**; observed per-process ranges overlap. This supports a bounded pilot tradeoff, not proof of identical performance or a statistically established regression. The candidate reduces measured managed allocation, and the prototype reports lower median process peak RSS; managed counters alone exclude native allocation.

My recommendation was to adopt this **AI-only macOS arm64 pilot change** after the animation guard correction and full offline tests, rather than make it depend on the unresolved TIFF replacement. The integrator approved that bounded scope. It removes the AI assembly's direct vendor dependency and reuses an existing native asset family, while keeping the remaining Imaging/security work explicit. It does not justify broad platform, full-format or zero-performance-regression claims.

SkiaSharp's [wrapper license](https://github.com/mono/SkiaSharp/blob/v4.153.1/LICENSE.txt) is MIT; the actual native package also carries its full `THIRD-PARTY-NOTICES.txt`. Those notices include optional/alternative and legacy material, including FreeType and a GIF MPL/GPL/LGPL section. Local binary strings identify `SkWuffsCodec`, which does not by itself prove whether every legacy notice applies to the shipped binary. Preserve the complete notices and finish the RID-specific SBOM/license inventory; do not label the entire native asset simply MIT or infer legal conclusions from wrapper metadata.

Standalone AI consumers now need native Skia assets, unlike the previous fully managed decoder. The restored default graph has macOS and Win32 native packages and no Linux native package. macOS arm64 execution is demonstrated; Intel, Windows and Linux execution/packaging remain separate gates. This does not introduce a new native family into the full application, which already used Skia in Imaging.

## Current-source adoption and verification

The before-change production files were hashed and copied under `.artifacts/imagesharp-ai-adoption/` before editing. Only the exact reviewed PNG implementation, the AI package reference, three existing fixture ports, a shared Skia test helper, the corpus/CRC test class and 20 generated PNG fixtures were adopted. Existing test assertions remain intact, including the transmitted grayscale-pixel checks. The corpus paths are repository-relative; no source/test project references an artifact candidate directory.

- Independent corrected prototype offline suite: **215/215 passed**, with the live-provider class explicitly excluded.
- Current main-source AI suite: **215 passed, one existing opt-in live Ollama test skipped**, zero failures. No real provider request ran.
- Current AI NuGet graph: **zero listed advisories**, and its actual resolved assets contain no ImageSharp. Graph: SkiaSharp 4.153.1, its macOS/Win32 assets, Contracts, Core and Evaluation.
- Complete solution Release compilation: **passed, zero warnings/errors**; no fitting or training tests were executed by this build.
- `git diff --check` and the owned source/test scan for SixLabors and artifact-path references passed.

Main adoption receipts: `registration.json`, `main-ai.trx`, `ai-tests.log`, `ai-audit.json` and `solution-build.log` under `.artifacts/imagesharp-ai-adoption/`. Full central test verification and actual packaged application execution after this delta remain the integrator's final gate. Frozen resource, P46 and earlier package experiment binaries were not rewritten. No TIFF replacement, rights change, specialist decision, model loading or training occurred.
