# Independent runtime notice collector review — 2026-11-05

The corrected collector is suitable for the stated offline packaging-evidence scope. It inventories all three published runtime dependency manifests, reads notice/nuspec bytes from package archives rather than arbitrary extracted cache files, and explicitly does not attest to signatures, complete native components or legal redistribution approval. No additional high-confidence blocker was found after the corrections below.

## Reproduced findings and corrections

**P2: filesystem aliases could leave partial output after successful preflight.** Exact duplicate archive names and `./` traversal aliases were rejected, but `LICENSE`/`license` and Unicode NFC/NFD notice names passed planning. On the actual macOS filesystem the second exclusive file creation failed after output already existed. Initial independent tests reproduced both failures. A follow-up `LICENSES` regular file plus `licenses/NOTICE.txt` reproduced the related ancestor-file/directory collision after the first fix.

The owner now reserves every output path prefix, including nuspec paths, using NFC/casefold identity and explicit file/directory kind. Collisions refuse before output creation. Package identity matching is also case-insensitive across manifests. The scheme is deliberately conservative for common filesystems; it does not certify every platform-specific filename rule.

**P2: the combined notice budget was applied after eager inflation.** The initial dictionary comprehension read every notice in one package before checking the aggregate 20-MiB budget. A scaled generated fixture with a 32-byte aggregate budget observed 55 notice bytes read before refusal. The owner now sums each selected ZIP member's declared size against the remaining aggregate allowance before reading any notice payload for that package; actual read lengths are checked against member sizes. This prevents the demonstrated eager-buffer overrun without claiming a process-wide memory limit.

No production collector source was edited by this reviewer. The owner ported the regressions into its maintained suite.

## Final checks

- Maintained collector suite: **14 passed**.
- Separate independent alias/budget cases: **four passed**; initial receipts record three failures and the subsequent ancestor-collision failure.
- Actual published input: all three runtime-manifest SHA-256 values matched the existing registered inventory before collection.
- Fresh independent output contains **16 runtime packages, six embedded notice files and 24 total files**. All six notice copies were independently compared byte-for-byte with their exact NuGet archive members and their recorded SHA-256 values.
- **11 packages** have no embedded notice text; the inventory retains **14 review-required package flags**, combining absent text and native-component coverage. It does not erase those gaps merely because SPDX expressions are present.
- `attestation`, `nativeSbomComplete` and `packageSignaturesVerified` are all explicitly false. The accompanying README also refuses a legal-approval interpretation.

Evidence: `.artifacts/runtime-notice-independent-review/`, including `before.log`, `intermediate.log`, `after-final.log`, `source-sha256.json`, the independent fixture source, `published-final/` and `published-verification.json`. The three actual manifest inputs came from `.artifacts/releases/midday-runtime/`; collection did not alter that frozen release.

## Hash definitions and scope

The raw archive SHA-512 comparison and restored-content-hash comparison are intentionally separate. NuGet's [metadata specification](https://github.com/NuGet/Home/wiki/Nupkg-Metadata-File) distinguishes the `.nupkg.sha512` receipt from `contentHash`, which excludes signature material. The collector streams the raw ZIP digest and compares its cached raw receipt; it separately compares the runtime manifest's NuGet content-hash string with restored metadata. It does **not** recompute the signed-package canonical content hash or validate a signature. NuGet's [package-signature specification](https://github.com/NuGet/Home/wiki/Package-Signatures-Technical-Details) describes the additional signing/verification machinery outside this collector.

The archive-only selection prevents later-added extracted-cache files, including private license keys, from being swept into output. Static containment checks reject package/archive paths resolving outside their selected roots; archive notice symlinks, unsafe paths, missing declared license files, duplicate entries and oversize evidence refuse before publication. The generated fixtures also preserve existing-output sentinels and distinguish raw versus signature-independent hash definitions.

This remains a stable-local-input packaging operation, not a proof against hostile concurrent cache/filesystem mutation, trusted-feed authenticity, binary-to-source correspondence, complete native SBOM coverage or license compliance. Framework/runtime notices, development dependencies, model/data rights and missing upstream notices require separate evidence. Any later supplemental upstream license catalogue must keep its provenance separate from byte-exact files embedded in the NuGet archives.
