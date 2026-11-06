<!-- Copyright (c) 2026 QuantForgeSoftware. All rights reserved. -->
# P46 native import compatibility — 2026-11-05

The final-supplements CLI produces **27/26/28 physical rows** from the three
original P46 TEIs in a fresh owned project. A separate copy of the earlier
rehearsal remains **30/23/32 rows (85 total)** after new-CLI reads, exports and
duplicate-import checks. This is storage/interchange verification, not Greek
reading, crop, geometry or training acceptance.

The investigation also found a real language-metadata gap in this frozen build:
all sources explicitly declare `grc`, but structured import and TEI export omit
it. Complete metadata preservation is therefore **not** claimed. The original
markup attachments retain the declarations exactly; that does not make their
omission from structured exports acceptable. A separate bounded correction was
requested after these observations.

## Registered sources and actual CLI execution

Evidence root:
`.artifacts/experiments/p46-native-import-compatibility-v1/`.
`registration.json` records hashes of all 104 original specialist-packet files,
all 437 published files (436 manifest entries plus the manifest), the original
old rehearsal database and the ten copied input/rights assets. Only owned copies
were imported. Sources/images/attribution/APIS receipts are retained under
`inputs/`; the full packet was not rewritten or moved.

The actual CLI is
`.artifacts/releases/midday-final-supplements/cli/LeviathanText.Cli`.
Every command uses an explicit owned `--store` path. Exact stdout/stderr is under
`logs/`; successful procedure assertions are in `resume-final.log`, `results.json`
and `export-results.json`. Two initial harness assumptions were corrected without
reimporting: the native polygon field name and the distinction between persisted
JSON's version field and the current SQL-assigned loaded version. Initial logs
remain available; neither was a product failure or a reason to alter source data.

| Page / selected folio | Fresh rows / L1 | Fresh L2 | Old copied rows / L1 | Old L2 |
| --- | ---: | ---: | ---: | ---: |
| 1440 / 081v | 27 | 11 | 30 | 11 |
| 1450 / 082r | 26 | 10 | 23 | 8 |
| 1460 / 082v | 28 | 7 | 32 | 6 |
| Total | 81 | 28 | 85 | 25 |

The fresh project has SQL version 4, three objects/captures, three exact original
TEI attachments and 46/33/45 annotations. Native line geometry and review
decisions remain empty. The images are unchanged width-2200 review derivatives,
not original-resolution or accepted line crops.

## Preservation and explicit loss

All three original TEI attachment bytes/checksums match the source copies and
registered originals. Capture/frame checksums match the copied review images.
Source folios, licence statement and CC BY 4.0 licence target survive native
storage and TEI export. All 81 encoded `tei-source-context` values match original
import and export/reimport byte-for-byte, including `break="no"` on 7/11/3 rows.
`metadata-results.json` records these comparisons using the published Formats
assembly, without compiling replacement product source.

Flat convenience tags retain the last value when a physical row crosses more
than one verse block; the encoded structured context retains all blocks.
**Page 1440 row 10 contains source contexts V13/V14 while the packet association
lists V14.** This remains a specialist/packet-extractor association question,
not an instruction to change a reading or crop.

All six CLI TEI exports (fresh and old, three pages each) pass the packaged bounded
XSD with its pinned SHA-256
`9d1e64264d1dd0d5d6594b6dca1a1a159b1022cd2086a73bb6bd19dfd1e624aa`.
Selected L1 plain-text exports explicitly report omitted editorial readings,
uncertainty, alternatives, provenance and geometry. Dual-layer TEI reports one
annotation-mapping loss, one diplomatic-layer loss and one editorial-layer loss
for page 1450 in both projects. Pages 1440/1460 report no TEI layer loss, **but
that report does not detect the language omission below**. No lossless round trip
is claimed. The CLI has no separate L2-only TEI option; no such capability was
invented for this verification.

Native bundles retain complete object graphs, IDs, attachment content and current
store-assigned versions, plus three image entries with exact frame checksums.
Repeated `--skip-duplicates` imports leave persisted NativeJSON and SQL versions
exactly unchanged. No source-map or physical-row migration is applied to old data.

## Old-project isolation and compatibility

The old `rehearsal/restored.db` was copied as a file before any SQLite access.
Only `old-copy.db` was opened by SQLite/the new CLI. Its 85 rows, version 5,
persisted NativeJSON string, IDs and six attachments (three original markup plus
three relocation receipts) remain exact; even the copied database file hash is
unchanged after all operations. Its images were mirrored into an owned directory;
the copied project's existing paths continue to reference the original
rehearsal-owned image copies read-only, preserving the native payload exactly.
Both image locations match recorded frame checksums.

The original old database hash, all 104 original packet hashes and all 437
published file hashes remain unchanged. No default store, original source packet,
source map, review decision, training label or checkpoint was modified.

## Language gap and rights boundaries

`language-gap.json` confirms that all three sources contain both
`langUsage/language@ident="grc"` and `text@xml:lang="grc"`. In this build,
`TeiXml.Import` returns empty document/line language arrays, persisted line arrays
are empty, and actual TEI exports have neither language declaration. Comparing
empty import/export arrays alone would falsely suggest preservation. This
observation triggered a separate explicit-language inheritance/header-metadata
fix; the source attachments remain available for an explicit future reimport.

Copied attribution records retain the existing Michigan Public Domain Mark 1.0
image evidence (not CC0) and INTF CC BY 4.0 transcription notices. These are
existing asset-specific receipts, not a new rights approval. No Greek source
text, restored span, verse association, crop alignment or model target was
adjudicated. The structural total 81 coinciding with review-item count does not
establish a one-to-one correspondence. No model, inference, training or provider
operation was run.

## Subsequent explicit-language fix

The separately approved [language-preservation fix and fresh native replay](tei-language-metadata-2026-11-05.md)
now retain explicit `grc` on all 81 newly imported lines and restore the header
declaration on export. This does not change the historical failing-language
observations above or the old 85-line project's stored arrays. The new replay
verifies that copied legacy project's exact JSON/version/database bytes remain
unchanged and all 104 original packet files remain byte-identical. An explicit
reimport is required to populate language metadata in an existing project.
