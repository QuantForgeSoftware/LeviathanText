# SQLitePCLRaw 2.1.13 source/license metadata follow-up

The four approved-catalog gaps remain open. This metadata-only follow-up found
a useful **version-declared source candidate**, not a cryptographic binding of
every package binary to that source. No approved catalog, dependency, binary or
legal-clearance flag was changed.

The prior catalog had checked absent `v2.1.13`/`2.1.13` tags. The official
`maint_21` history instead identifies commit
[`0873d14f6dcb3a8537444679468499ab9f5a782a`](https://github.com/ericsink/SQLitePCL.raw/commit/0873d14f6dcb3a8537444679468499ab9f5a782a),
dated 2026-08-13, whose patch explicitly changes `PATCH_VERSION` from 12 to 13
and removes the obsolete win-arm target reference. At this immutable commit,
[`version_stamp.cs`](https://github.com/ericsink/SQLitePCL.raw/blob/0873d14f6dcb3a8537444679468499ab9f5a782a/version_stamp/version_stamp.cs)
selects the release version 2.1.13. This is stronger than substituting a current
or adjacent-version license.

All four cached package archive/nuspec SHA-256 values match the approved
catalog. Their nuspec repository metadata still omits a commit. PE metadata
inspection, without loading or executing their code, finds the three managed
assemblies' informational version `2.1.13`, file version `2.1.13.3146` and only a
repository URL. The recorded version-generation formula maps day 3146 to
2026-08-13, consistent with the commit and
[NuGet's exact-version listing](https://www.nuget.org/packages/SQLitePCLRaw.core/2.1.13).
That consistency is corroboration, not binary/source attestation.

The cached native package's
`buildTransitive/net461/SQLitePCLRaw.lib.e_sqlite3.targets` is byte-identical to
the target file at that commit, SHA-256
`c876cc38cf2565a794ef53dd94006592afc6d4749fdcdd20d3123e69fab9cc8c`.
This binds one actual package source asset to the candidate, not all managed or
native compiled assets. The managed packages contain no PDB or SourceLink member;
their debug directories name external PDBs and have no embedded portable PDB.
A public NuGet flat-container symbol-package probe returned 404; the exact PDB
identity probe returned 403. The latter does not establish that symbols do not
exist elsewhere.

Exact fixed-commit public files are retained only as research evidence:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| [LICENSE.TXT](https://github.com/ericsink/SQLitePCL.raw/blob/0873d14f6dcb3a8537444679468499ab9f5a782a/LICENSE.TXT) | 11,358 | `cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30` |
| [NOTICE.TXT](https://github.com/ericsink/SQLitePCL.raw/blob/0873d14f6dcb3a8537444679468499ab9f5a782a/NOTICE.TXT) | 9,756 | `485b276b3d2bfaa26df348e1e5c84df3648981e09b88531a6a53006f0705c24b` |

Their Git blob SHA-1 values were independently calculated from exact bytes and
match the official immutable commit tree. The notice includes wrapper and
bundled-component material; its complete text was retained without treating the
wrapper's Apache declaration as a blanket grant for every native component.

Receipts, public response bytes, complete fixed-commit files, metadata-only
inspector source and `results.json` are under
`.artifacts/sqlite-license-metadata-followup/`. The exploratory inspector's build
emitted three nullable warnings, preserved in `metadata-build-and-output.log`;
its separately captured actual metadata output is valid JSON. These research
warnings do not modify the independently warning-free product build evidence.

A future independently reviewed supplement could label these files precisely
as **version-declared maintainer-commit evidence with one exact package-asset
match**. It must retain the missing complete package/source binding, native
component/RID provenance, SBOM, signature and legal-review limitations. This
follow-up does not clear any of the four existing catalog gaps or the 14 runtime
collector review flags. No model, provider, fitting, image decoding or package
code execution was used.
