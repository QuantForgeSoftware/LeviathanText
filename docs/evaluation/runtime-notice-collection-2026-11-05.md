# Packaged runtime notices and dependency evidence — 2026-11-05

The inspected interim package contains16 runtime NuGet packages but only the
project's own root LICENSE/NOTICE. Six exact upstream notice files are available
in five of their NuGet archives, including LibTorch and Skia native notices;
those were not included. Angular's generated `3rdpartylicenses.txt` also sits
outside `dist/web/browser`, so the browser-directory copy omitted it.

Packaging now invokes an offline collector against the three published runtime
`.deps.json` manifests, copies those exact public notices and nuspec files,
writes a deterministic runtime inventory, and includes Angular's exact generated
browser-license file. The final release manifest and tar cover every resulting
file. No remote license fetch is part of package construction.

The collector reads notices only from the restored NuGet archives, ignoring extra
files/private keys placed in an extracted cache directory. It records raw ZIP
SHA256/SHA512, checks raw SHA512 against the cached raw receipt, and separately
compares the NuGet content hash with restored metadata and runtime manifests.
[NuGet's content hash excludes signing information](https://github.com/NuGet/Home/wiki/Nupkg-Metadata-File);
it must not be confused with the raw signed ZIP hash. Canonical signed-package
hashes and signatures are **not** reverified by this collector. Nuspec identity,
source and declared license metadata remain evidence rather than a trust grant.

Path validation rejects traversal, absolute/noncanonical paths, symlink escapes,
published symbolic notices and case/Unicode aliases before creating output.
Package identities merge case-insensitively; notices remain byte-exact rather than
being renamed or normalized. Known notice lengths are checked against per-file
and aggregate budgets before payload inflation. Fresh-output refusal prevents
replacing an earlier receipt. Missing metadata/files, raw/content-hash mismatches
and ambiguous paths fail packaging rather than silently omitting evidence.

Fourteen generated collector regressions pass. Independent review reproduced
four failures covering case/Unicode aliases, file/directory-prefix collisions and
aggregate budget enforcement;
the corrected four independent controls pass. The original ten controls cover exact
bytes/hashes, cache-extra exclusion, native review flags, traversal, declared
missing licenses, corruption, symbolic archive/cache paths, separate signed/raw
hash definitions and existing-output preservation. Receipts are
`.artifacts/midday-verification/notice-tests-final.log` and
`.artifacts/runtime-notice-independent-review/`.

The observed16-package inventory still flags14 packages for review: eleven have
no embedded notice text, while native bundled-component coverage is not established
by package metadata or by the presence of a notice file. The inventory sets
`attestation`, `nativeSbomComplete` and `packageSignaturesVerified` to false.
Framework runtime, development-toolchain and dataset/model rights are separate.
Supplemental fixed-version upstream license sources are being investigated; no
blanket licensing or redistribution clearance is claimed.

ImageSharp remains a separate security/licensing replacement gate in Imaging.
Collecting its existing terms does not resolve its advisories or authorize use.
Actual final package file/hash/archive and operational verification is recorded
in the session handoff after the collector integration.

Dated supplemental upstream-license evidence is retained separately under
`docs/third-party/runtime-license-supplements/`. In particular the vendor PDFium
archive's native binary matches the NuGet binary exactly, exposing its BSD-style
and component notices in addition to differing package/build metadata. Exact
managed repository commits support further source-license copies. These files
are included by the ordinary documentation copy; they do not silently replace
nuspec declarations or turn the collector into a legal/native-SBOM attestation.
The final package verifier checks their exact hashes and catalog references.
