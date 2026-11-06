# ImageSharp security update: isolated candidate and release blocker

Date: 2026-11-05. This records the initial isolated-upgrade investigation; no production dependency change was applied at that stage. **Subsequent approved [AI-only PNG adoption](imagesharp-png-independent-review-2026-11-05.md) replaces the AI reference with SkiaSharp. Imaging remains on 3.1.11 and the six-advisory security release gate remains open.** The before/after licensing evidence below remains historical evidence for the isolated 3.2.0 candidate.

## Advisory evidence

The original NuGet audit (`.artifacts/midday-verification/dependency-audit.json`, SHA-256 `8e608eb3fac68d8aadc6b9fc18871407a6e78dd375ca80429b08c78e84a6b90b`) reports ImageSharp in 18 affected project graphs: two direct references and 16 transitive references. There are six distinct advisories, four High and two Moderate. The vendor advisory ranges include 3.1.12; upgrading to that version does **not** close this gate.

| Advisory | Severity | Affected facility | First patched v3 release |
| --- | --- | --- | --- |
| [GHSA-v76p-62qx-wwq2](https://github.com/SixLabors/ImageSharp/security/advisories/GHSA-v76p-62qx-wwq2) | High | Tiled fax TIFF decoder | 3.2.0 |
| [GHSA-gwg2-r3hj-4w44](https://github.com/advisories/GHSA-gwg2-r3hj-4w44) | Moderate | ICC CLUT allocation | 3.2.0 |
| [GHSA-j3p4-wp97-rph4](https://github.com/SixLabors/ImageSharp/security/advisories/GHSA-j3p4-wp97-rph4) | High | Histogram equalization | 3.2.0 |
| [GHSA-j9gm-c75j-xc9q](https://github.com/advisories/GHSA-j9gm-c75j-xc9q) | High | TIFF T4 encoder | 3.2.0 |
| [GHSA-wmxv-xphr-5c9g](https://github.com/advisories/GHSA-wmxv-xphr-5c9g) | Moderate | BigTIFF IFD traversal | 3.2.0 |
| [GHSA-jjfr-hcj7-qf5w](https://github.com/SixLabors/ImageSharp/security/advisories/GHSA-jjfr-hcj7-qf5w) | High | TIFF T6 encoder | 3.2.0 |

The [vendor 3.2.0 release](https://github.com/SixLabors/ImageSharp/releases/tag/v3.2.0) backports security fixes and introduces build-time license enforcement. This is the smallest published same-major candidate covering all six; no major-version upgrade or new production dependency was introduced here.

## Isolated verification

Evidence directory: `.artifacts/midday-imagesharp-security/`. Candidate copies contain the current AI and Imaging source, copied project references set to absolute original dependency projects, and only their ImageSharp package references changed to 3.2.0. Artifacts are isolated at `/private/tmp/leviathan-imagesharp-320-candidate`. Original project files were compared byte-for-byte to saved snapshots and remain unchanged.

Both candidate restores succeeded. Their actual assets files resolve `SixLabors.ImageSharp/3.2.0`. Public NuGet vulnerability queries, including transitive dependencies, return zero listed vulnerabilities for each candidate graph (`after-ai-audit.json`, `after-imaging-audit.json`). This covers those two candidate graphs at query time, not a fresh whole-solution audit or proof against undisclosed vulnerabilities. `candidate-summary.json` records the version, audit counts, hashes and unchanged-source checks.

Default Release builds of **both** candidate assemblies exit 1 at `SixLabors_ValidateLicense`, reporting no license found. Logs: `release-ai.log`, `release-imaging.log`. No target was disabled, no license-validation property was bypassed, and no fabricated key was used. Thus no passing Release compatibility or patched test result is claimed. Focused AI/Imaging tests and the complete Release gate must run after a usable license is provisioned or a replacement is implemented.

Commands (relative to repository root):

```sh
dotnet restore .artifacts/midday-imagesharp-security/candidate/src/LeviathanText.AI/LeviathanText.AI.csproj -m:1 -p:ArtifactsPath=/private/tmp/leviathan-imagesharp-320-candidate
dotnet restore .artifacts/midday-imagesharp-security/candidate/src/LeviathanText.Imaging/LeviathanText.Imaging.csproj -m:1 -p:ArtifactsPath=/private/tmp/leviathan-imagesharp-320-candidate
dotnet build .artifacts/midday-imagesharp-security/candidate/src/LeviathanText.AI/LeviathanText.AI.csproj --no-restore -c Release -m:1 -p:UseSharedCompilation=false -p:ArtifactsPath=/private/tmp/leviathan-imagesharp-320-candidate
dotnet build .artifacts/midday-imagesharp-security/candidate/src/LeviathanText.Imaging/LeviathanText.Imaging.csproj --no-restore -c Release -m:1 -p:UseSharedCompilation=false -p:ArtifactsPath=/private/tmp/leviathan-imagesharp-320-candidate
ArtifactsPath=/private/tmp/leviathan-imagesharp-320-candidate dotnet list .artifacts/midday-imagesharp-security/candidate/src/LeviathanText.AI/LeviathanText.AI.csproj package --vulnerable --include-transitive --format json
ArtifactsPath=/private/tmp/leviathan-imagesharp-320-candidate dotnet list .artifacts/midday-imagesharp-security/candidate/src/LeviathanText.Imaging/LeviathanText.Imaging.csproj package --vulnerable --include-transitive --format json
```

## License and key provisioning facts

The downloaded 3.2.0 NuGet package has SHA-256 `31e4f2c76128326cad1395d497e08e97624159016fc95064b46f6283d85bb44d`. Its nuspec retains file-based `LICENSE`, `requireLicenseAcceptance=true`, and net6.0 assets without new package dependencies. Its LICENSE is **byte-identical** to the cached 3.1.11 LICENSE: SHA-256 `fdb7f24db8a6838eba1477242d2457bf6db2f3682b7cb3a16824e9f2f07936c2`. Both are Six Labors Split License 1.0. This comparison does not establish this project's eligibility for any licensing category.

The new package bundles a validation target/task. Its ordinary Release build requires a valid issued key for each direct consumer. [Official v3 setup documentation](https://docs-v3.sixlabors.com/articles/imagesharp/index.html) explains the supported choices:

- Default file discovery searches `sixlabors.lic` in the directory of each direct-reference project and its descendants. A file only at repository root needs an explicit path.
- `SixLaborsLicenseFile` supplies the path to the vendor-issued file, including a shared external path. `SixLaborsLicenseKey` can instead map to `$(SIXLABORS_LICENSE_KEY)`; the complete issued string is required.
- Community assembly-scoped keys may be committed for their named public-project assemblies; unrestricted community and commercial keys must remain private. Obtain a suitable key through the [vendor licensing portal](https://licensing.sixlabors.com/) or discuss commercial licensing through the [vendor pricing page](https://sixlabors.com/pricing/).

The two case-sensitive direct assembly names are **LeviathanText.AI** and **LeviathanText.Imaging**. No additional direct licensing-package reference is required. These are provisioning facts, not a determination of legal eligibility. Filename-only checks found no `sixlabors.lic` below either direct source project; only the presence boolean for `SIXLABORS_LICENSE_KEY` was inspected (false). No credential values were read or recorded.

Before adopting 3.2.0: obtain/identify the owner's appropriate issued key; configure its supported path or secure property without committing private key contents; repeat both Release builds and focused generated-image/vision-image tests; then run the whole-solution dependency audit and normal Release engineering gate. No frozen model or benchmark snapshot should be changed to accommodate a dependency update.

## Replacement feasibility: research only

Existing `ImageDecoder` already uses SkiaSharp first, falling back to ImageSharp when Skia cannot identify/decode an image; ImageSharp also supplies fallback dimensions. `VisionImage.FromPng` uses ImageSharp for static-PNG format detection, bounded dimensions and complete decode validation, while transmitting the exact original bytes and hash. Replacing it must preserve those contracts, especially animation rejection, truncated/corrupt data rejection and four-million-pixel preflight.

Reusing existing SkiaSharp for PNG/JPEG appears feasible: its [SKCodec API](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skcodec?view=skiasharp) exposes header information and explicit decode results. This is a candidate engineering inference, not a demonstrated equivalent decoder. AI would need a direct Skia reference or a deliberate shared imaging dependency; native runtime assets and disposal must be tested for all packaged targets. Skia alone should not be assumed to replace TIFF fallback.

[BitMiracle.LibTiff.NET 2.4.660](https://www.nuget.org/packages/BitMiracle.LibTiff.NET/2.4.660) is a managed TIFF candidate targeting netstandard2.0. Its [vendor repository](https://github.com/BitMiracle/libtiff.net) describes New BSD licensing; the [license file](https://github.com/BitMiracle/libtiff.net/blob/master/license.txt) includes retained upstream notices, so preserve the complete notices rather than replacing them with a generic BSD label. Vendor support is explicitly [limited](https://bitmiracle.com/open-source). Its [TIFF API](https://bitmiracle.github.io/libtiff.net/help/api/BitMiracle.LibTiff.Classic.Tiff.html) offers directory, strip/tile and RGBA operations; this does not prove parity with ImageSharp on our inputs.

A bounded replacement prototype should first inventory the currently accepted fallback formats and preserve or explicitly reject formats beyond TIFF. Generated tests must cover first-directory-only behavior; tiled/strip, fax, palette, grayscale/RGB and alpha TIFF; orientation, premultiplication and channel order; encoded/pixel limits before large allocation; corrupt/truncated and cyclic inputs; exact PNG transmission hashes; disposal and packaged native dependencies. BigTIFF and high-bit-depth support require explicit verification rather than assumed compatibility. Audit any chosen new package graph before approval. No replacement package has been added, no replacement production code was written, and no assurance of equivalent performance, TIFF coverage or scholarly recognition accuracy is made.
