# SEC-01 production integration manifest — 2026-11-05

**Source-only proposal. No dependency adoption, production build or fresh audit is performed by this document.** Production Imaging still references ImageSharp3.1.11. Root owns the separate approved pixel-budget backport and the later `ProjectBundle` migration. The prototype, negative evidence and independent resource window remain distinct from a reviewed production integration.

The machine-readable inventory is `.artifacts/experiments/sec01-production-integration-plan-v1/integration-source-manifest.json`. It hashes the eight proposed owned source files, all153 vendor C# files, four vendor/build evidence files and the current direct consumers/package machinery. It deliberately leaves final assembly identity, security maintainer, published hashes and fresh audit **unset**. The current-file hashes are a point-in-time anchor; root's separately approved budget application will legitimately change the main decoder hash.

## 1. Exact runtime source scope

The latest draft is `.artifacts/experiments/sec01-adapter-v1/candidate-v14/src/LeviathanText.Imaging/`. v14 was prepared source-only during the quiet window, then built and independently checked after release:40 explicit gray-alpha refusals,45 known-format accepts and17 metadata/source-identity controls pass. v13 is the independently frozen resource candidate; do not relabel its results as a tested v14 binary.

| Target under `src/LeviathanText.Imaging/` | Action and required behavior |
| --- | --- |
| `ImageDecoder.cs` | Review/merge replacement fallback, admitted-source guards, streaming metadata/Band routing, optional immutable diagnostics and snapshot decodability API. Preserve root's budget correction and dimension preflight; do not overwrite current source wholesale from an old clone. Existing Decode/Bounded/Band/Dimensions public signatures stay. |
| `FallbackRasterDecoder.cs` | Add owned spec-based P1–P6 PNM, TGA and QOI header/raster walkers; constant-memory numeric parsing, admitted leading zeros, checked overflow, exact binary separator and alpha/origin rules. P7 was unsupported by the baseline; do not advertise it as preserved. |
| `ImageDecodeDiagnostics.cs` | Add per-instance warning/error counts and at most8×256-character immutable template summaries; no argument formatting or process-global logging changes. |
| `ImageValidation.cs` | Add exact-byte first-frame **decodability** traversal/result. Native JPEG can recover missing entropy; no strict-completeness/authenticity claim. Full-raster versus row/opcode strategy remains explicit. |
| `SafeTiffDecoder.cs` | Add first-IFD offset/count/type/metadata/block checks, original read-only source, raw orientation, bounded metadata cache and selected-block Band path. Defer unsupported two-channel grayscale-alpha pixel decoding. |
| `TiffJpegDecoder.cs` | Add admitted JPEG7 contiguous8-bit three-component RGB/YCbCr segments decoded through existing Skia; exact SOF/codec dimensions, table/segment bounds and complete native result status. Eligible failure cannot silently use known-different IJG colors. |
| `TiffSampleDecoder.cs` | Add precise palette/16-bit normalization and premultiplied storage with clipped Band writes. Preserve approved source-oracle policies; no generic tolerance. |
| `UnsupportedImageVariantException.cs` | Internal propagation of the explicit deferred-variant reason, avoiding a catch-all that disguises that decision. |
| `LeviathanText.Imaging.csproj` | Remove **only after all consumers migrate** the ImageSharp3.1.11 reference; add the approved source-fork project reference. Existing Skia/PDF dependencies remain unchanged. |

Do **not** copy prototype `Contracts`/`Core` clones or unchanged Binarization, crop/dewarp, PDF or searchable-PDF source files into production. They were only isolated build scaffolding. No Angular, model, training, labels, SourceMap, manuscript image or transcription change belongs to this integration.

## 2. Third-party source/build boundary

Proposed location, subject to root's identity decision: `src/ThirdParty/BitMiracle.LibTiff/`. This is not permission to add it now.

- Copy exactly the153 hash-pinned `LibTiff/**/*.cs` files with every upstream copyright header and the complete original `license.txt`. Current patched footprint:65,048 lines /2,818,482 C# bytes. Keep the original `AssemblyVersion.txt` as evidence, not as a compiled template.
- Use a small explicit project plus owned version metadata; no upstream scripts. Preserve required upstream defines including `THREAD_SAFE_LIBTIFF`; nullable/implicit-usings policy is limited to the vendor project. Do not import prototype absolute paths or artifact references.
- Production assembly/project identity remains **unassigned**. A distinct identity such as `LeviathanText.ThirdParty.LibTiff` avoids accidentally loading an unpatched `BitMiracle.LibTiff.NET` package, but root must approve it and check any identity assumptions. The namespace can retain upstream names. The prototype name/version is not a production decision.
- Retain pinned commit `5ad1a8053becd52fa6ad91ca120aec1ad2763024`, source archive SHA256 `c00118ca62bf3409d30a57ef783e48e8ffc80f897aca3accfe7823998cff5368`, verified Git-blob/source-file inventory, cumulative three-file semantic patch, build recipe/defines and independent before/after receipts.
- The three changed vendor files are `LibTiff/Tiff.cs`, `LibTiff/Internal/Tiff_Read.cs`, and `LibTiff/Internal/Tiff_DirRead.cs`. The two earlier failed endian hypotheses remain historical negatives. Current patch fixes raw MM header classification, unsigned64-bit count conversion and BigTIFF inline extraction; Classic behavior is preserved.
- Upstream CS8981 (`box`) is a real fresh-build warning. Resolve it by an explicitly reviewed narrow source rename or an explicitly recorded warning exception; do not hide it with a blanket suppression and claim unconditional zero warnings. Any new change updates the patch/source manifest and validation scope.

The official license includes BitMiracle BSD-style terms and IJG/libtiff/ComponentAce attributions/terms. Preserve the exact source notice plus the package nuspec's separate2008–2024 attribution; do not edit the source2008–2023 copyright into a guessed common year. There is no embedded license file in the exact2.4.660 NuGet archive. The Git pin proves copied-source identity, **not** reproducible correspondence to that NuGet binary. No legal clearance is inferred.

## 3. Every current direct ImageSharp consumer

A source-only `rg` over tracked C# and project files identifies this complete current set; repeat it after integration.

| Current consumer | Required transition / owner |
| --- | --- |
| `src/LeviathanText.Imaging/ImageDecoder.cs` | Replace `Image.Load<Rgba32>` fallback and imports with the reviewed owned/fork adapter; Imaging owner. |
| `src/LeviathanText.Imaging/LeviathanText.Imaging.csproj` | Remove direct package reference only in the compiling atomic integration; Imaging owner. |
| `src/LeviathanText.Application/ProjectBundle.cs` | **Root-owned.** Replace ImageSharp Identify/TargetSize1/Load and two package-specific catch types with qualified snapshot decodability/typed failures after resource acceptance. |
| `tests/LeviathanText.Imaging.Tests/TiffPreviewTests.cs` | Replace ImageSharp multipage fixture generation with an owned minimal TIFF writer or fixed generated licensed bytes. Retain first-frame-only color, dimensions, source-SHA and bounded-output assertions. |

Already completed independently:

- `src/LeviathanText.Cli/VlmBenchmarkCommand.cs` now fingerprints the **actual Skia assembly** used by the AI PNG producer; matching CLI regression failed before and both selected CLI classes pass80/80 after.
- `tests/LeviathanText.Cli.Tests/VlmBenchmarkCommandTests.cs` expects that actual producer identity; `VlmCredentialTests.cs` now creates its generated PNG with Skia.
- `CaptureDisplayIndependentTests` fixture generation uses Skia plus owned EXIF bytes; no ImageSharp dependency there.
- `src/LeviathanText.AI/VisionImage.cs` already uses existing Skia for strict static PNG. Do not alter its distinct limits or add the TIFF fork to the VLM producer list: the current VLM path does not use it.

Historical documentation and receipts can still name ImageSharp. Zero active C#/project references and a fresh resolved graph are the removal checks, not deleting historical evidence. Existing private license files, if any, must not be copied, published or deleted as part of this work.

## 4. Root-owned archive integration contract

`ProjectBundle.ReadValidatedImage` must continue its bounded **exact byte snapshot**:25MiB per image,250MiB aggregate, bounded read/growth checks, original encoded bytes copied, and recorded source-checksum comparison. No source reopen, header-only check, thumbnail substitute or unvalidated raw-text export channel.

Use `ImageDecoder.ValidateEncodedSnapshot(bytes, scope)` only after the applicable resource gates pass. Catch the intended `RejectedArchive` validation failures in the existing image-omission/loss-report boundary alongside I/O failures; remove old ImageSharp-only catch types. Do not swallow unrelated authorization, persistence or integrity failures. Preserve native/TEI/bundle contents when one optional image is omitted and disclose the omission.

Root policy preserves known recoverable JPEG compatibility: the SOS-only/premature-EOI cases accepted by old TargetSize1 and native Skia remain **compatibility observations**, not strict-refusal passes. Both all-row traversal and full native `GetPixels` can report success for them. No entropy decoder or authenticity guarantee is introduced. PNG/GIF/WebP/TIFF full destinations remain resource-qualified behavior, not automatically a proven regression: compare the old implementation's actual memory first.

## 5. Project, notice/catalog and package changes

1. Add the selected vendor project to `LeviathanText.slnx` and the Imaging project reference. Do not add a dependency on an unpatched NuGet fork in parallel. No new native asset is expected from the managed fork; Skia is already present, but each RID still needs qualification.
2. Keep original project `LICENSE` unchanged. Add a concise third-party attribution/link in `NOTICE`, preserving the complete upstream notice separately and marking the modified source/version. The project's Apache terms do not relicense vendor code.
3. Add a **separate source-component catalog**, proposed `docs/third-party/runtime-source-components/`, rather than pretending a project reference is a NuGet package in the existing fixed-version supplement. Include source commit/archive/tree/patch hashes, exact public notice hashes, final component identity/version and pending review flags. Preserve every existing SQLite/native notice gap and conservative flag.
4. Extend packaging with a bounded explicit source-component collector or companion to `scripts/collect-third-party-notices.py`. The current collector deliberately includes only `.deps.json` libraries whose type is `package`; it will otherwise omit this source project. Preserve its existing NuGet/archive verification semantics. Add tests for missing component/notices, changed source/license/DLL hashes, unsafe or symlink paths, output aliases, duplicate IDs and absent published references.
5. Wire the component collector in `scripts/package-release.sh`. Package original notices, provenance/patch/build recipe and enough exact source evidence to reconstruct the changed source. Vendored source is only about3MiB, so a self-contained source copy is feasible; root chooses the distribution layout. Never copy cache-private `.lic` files.
6. Generate a new actual published graph and release manifest; do not predict package counts from an older16-package receipt. Verify all CLI/API/Worker copies of the chosen fork assembly and their `.deps.json` references, with exact hashes. Angular notices remain byte-exact and unrelated.

The result remains an inventory, **not a complete native SBOM, legal certification or signed provenance attestation**. A source-project catalog must not be described as a verified NuGet archive. Fresh NuGet vulnerability results also do not scan the owned fork.

## 6. Source-to-built-DLL chain required before release

Record a fresh approved source freeze: all compiled owned/vendor source and project/version/define files, exact vendor source archive/commit, cumulative patch, notice hashes, restored dependency identities/content hashes, SDK/runtime/RID and build command. Include the dirty working-tree state; a Git HEAD alone does not identify uncommitted source.

Publish only from that freeze into a fresh output directory. Record build logs/warnings, final assembly/informational version, all three hosted assembly copies and SHA256, resolved runtime references and packaged source/notices. Recheck the compiled-source hashes after build; bind the final package's component catalog and release-manifest/tar hashes to those actual bytes. A second clean isolated build can assess reproducibility; do not claim it solely from the deterministic-build switch or an old prototype DLL. Preserve prior package receipts as historical.

Fresh restore/audit and package tests run **after** the quiet window and approval. Cached/offline prototype results, `NuGetAudit=false` builds and NU1900 feed failures are not fresh security audits.

## 7. Exact outstanding acceptance gates

| Gate | Current evidence and remaining work |
| --- | --- |
| Baseline pixel compatibility | Original166 inputs:1,242 comparisons pass. Three associated16 BE/planar cases retain the original21 strict differences as negative evidence. Root has approved the narrow canonical/source-equivalence policy; four unseen LE/BE controls pass all8 operations (32 passes). No generic tolerance or erased baseline results. |
| New capability correctness | JPEG/YCbCr q50/q95/q82 now match independent source references; gray1 planar additions have references. Gray2alpha deferral is independently verified:8 old-refused inputs×5 pixel routes =40 exact typed refusals,9 known formats×5 =45 accepts, plus17 valid Dimensions/source-identity controls. This does not claim the entire baseline matrix was rerun on v14. |
| Band | v11 matches118 TIFFs×4 frozen-v8 crop oracles (472 passes), all orientations/alpha/edge tiles/default strips/firstIFD/source identity. This does not erase separate baseline alpha differences. Resource proof must include the largest intersecting segment; a full-image strip can still need image-sized workspace. |
| Metadata | v8 cuts allocations but has457ms versus35ms large-table and404ms versus38ms long-comment CPU observations. v13 cache/token optimizations and leading-zero correction need frozen5-case/numeric/count/cycle/ABBA confirmation. Prior CPU and numeric failures remain. |
| Decoder resources | R's primary registered v13 ABBA pass is168 owned processes:14 inputs×3 operations×4 visits. Reverse BAAB is held for a separate proposal following the late1500-second policy; it is not part of the accepted current168+56 workload. Reports are pending, not inferred from a few initial passes. Memory includes encoded bytes, block/native scratch and full-frame destinations. |
| Archive resources | Separate registered56-process ABBA on14 selected inputs plus31 functional controls/metadata. Four native/full-TIFF destination strategies need actual old/new acceptance. Existing metadata1/16/64MiB controls complete, but the64MiB ICC fixture is a header-shaped incomplete profile (tagcount0/zero illuminant), not a full valid color-transform workload; sampled peaks are not hard global bounds. |
| Remaining format resource coverage | Large PNM/TGA decode performance is not covered merely by the7-format decoder workload; archive PNM is a different path. Add bounded representative workloads or retain an explicit coverage gap. No universal constant-ROI, time or RSS claim. |
| Admission policy | Review proposed512MiB encoded,12000 dimension,4096 tags,16MiB/tag,32MiB metadata,1million segments,64MiB encoded segment/staging and512MiB decoded-block limits. Do not silently lower old valid coverage to make measurements pass. A block budget is not a total-process bound. |
| Portable regressions | Promote generated format/crop/alpha/firstIFD/metadata/malformed/source-immutability/diagnostics/refusal controls into normal Imaging tests without artifact paths or vendor fixture generators. Add Application/API/CLI archive integration and the complete engineering gate. Timing observations stay in registered benchmarks, not flaky millisecond assertions. |
| No-fitting verification | Use the engineering entrypoint/exact six exclusions and current default fitting guards. No whole ML/Pilot suite, fitting opt-in, real provider, LM Studio or corpus/model run. |
| Platform/package | Actual publish/runtime/bundle proofs and source/DLL/notice hashes are still required after adoption. Current behavior is observed on osx-arm64; other RIDs/native assets are not automatically qualified. |
| Security/license ownership | Final component identity, human maintenance/security owner, update cadence, advisory applicability process and source-notice distribution approval remain **unassigned/not approved**. No source-fork SBOM/fresh audit is yet complete. |

Source changes are not permission to train, infer corpus accuracy, approve Greek readings, acquire sources, rewrite model weights or change source-frame identity. Approved PNM/Group3 corrections intentionally alter their named bad legacy pixels; unchanged predictions for every old model are not proven.

## 8. Security-maintenance decision required

Before declaring a production release supported, record the accountable Imaging/security maintainer and reviewer; do not silently assign the project owner. The unassigned human fork owner is a release/maintenance decision, not a SKILL rule, user prohibition or block on the already authorized prototype/engineering work. The role must monitor the pinned upstream's commits/releases/issues (no published advisory is not assurance), current native libtiff/IJG/compression security reports for **applicability**, dependency advisories and fuzz/resource findings. Define a periodic check plus expedited triage for credible reports, a reproducible patch/review/test process, source-version updates and a fallback/rollback decision when upstream stops maintaining the port.

The managed fork is65k lines of older TIFF/JPEG/compression implementation, not a security-clean tiny endian helper. Do not assume native libtiff4.7.x fixes are present in this C# port, or that native-only CVEs automatically apply unchanged. NuGet audit will not answer either question. SEC-01 stays open until the chosen production graph, correctness/resource gates, source/license records and ownership decision are reviewed together.

## Subsequent isolated snapshot-validation work

The [streaming TIFF proposal](tiff-snapshot-streaming-proposal-2026-11-05.md) now has a compiled isolated v16: shared first-IFD sample/JPEG/generic strip/tile traversal without an assembled page destination, explicit known native-JPEG recovery, unchanged gray-alpha deferrals, and conservative whole-source-frame block flags. R's independently rebuilt same-source runner matches154/154 v14 acceptance/refusal/dimension decisions plus40 gray-alpha pixel-route refusals/45 known accepts/17 metadata controls. Its Imaging DLL differs from the separate A-compiled frozen DLL; exact A-DLL coverage is still a gate, not silently inferred from source equivalence. No cause for the PE hash difference has been established.

No v16 resource acceptance is claimed. The measured v13 archive TIFF/RSS blocker remains open until the revised traversal is tested on registered exact inputs. Whole-image strips/tiles may still require image-sized codec workspace; metadata flags are not an RSS profiler. No main dependency migration, source-fork ownership assignment, fresh advisory audit or release approval follows from these functional controls.
