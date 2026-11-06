# P46 capture portability rehearsal — 2026-11-05

This rehearsal imported the retained P46 Michigan review derivatives and their source TEI into a fresh disposable native project, then exercised backup, restore, explicit capture relocation and export with a frozen CLI. It verified storage portability; it did not approve Greek readings, image alignment, line geometry, rights or recognition quality. No production code changed.

## Registered inputs and execution

Registration and evidence root: `.artifacts/experiments/p46-capture-relocation-v1/`. The parent prepared `registration.json`, `source-sha256.json`, a 206-file source snapshot and a published Release CLI. Before the first CLI command, this rehearsal froze all 109 published-file hashes in `binary-sha256.before.json` and all 104 original specialist-packet file hashes in `packet-sha256.before.json`. CLI assembly SHA-256: `00069a206ee0e5fbd56b0eede501191d7e0d772bfadd0e31d588e04486922e73`.

All commands used `dotnet bin/LeviathanText.Cli.dll` from that registered root, with stores, copied TEI, disposable image trees, exports and logs exclusively below `rehearsal/`. The original packet was read and hashed; it was never moved or rewritten. The successful procedure is retained as `rehearsal/run.py`; `rehearsal/results.json` contains assertions/results and `rehearsal/logs/` contains exact stdout/stderr.

| NTVMR page | TEI folio selector | Imported lines | L1 / L2 revisions | Annotations | Capture pixels |
| --- | --- | ---: | ---: | ---: | --- |
| 1440 | 081v | 30 | 30 / 11 | 46 | 2200 × 2536 |
| 1450 | 082r | 23 | 23 / 8 | 33 | 2200 × 2577 |
| 1460 | 082v | 32 | 32 / 6 | 45 | 2200 × 2626 |
| Total | | 85 | 85 / 25 | 124 | Three captures |

The whole-source TEI importer emits **85 lines**, distinct from the existing **81-item specialist review scope**. No correspondence between those sets was inferred and no pending decision was changed. All imported line baselines/polygons are empty, and the new project has zero review decisions. These width-2200 files are review derivatives, not original-resolution captures or accepted layout.

An initial `--page 1440` import was rejected because the source TEI page selector is `pb@n="081v"`, not the external NTVMR page number. The successful imports used the exact source folio values above. This operator correction did not change source XML or create an imported object from the rejected request.

## Preservation results

The fresh project `col-bfaa687ac5b7` contains three physical objects, three surfaces/captures, 110 revisions, 124 annotations and three original TEI attachments. After creation plus three imports its store version was 4.

1. **Backup and restore:** the frozen CLI's backup and restore commands succeeded. The restored database's stored NativeJSON string was byte-identical to the original and its version remained 4. Original source-attachment hashes/content, native IDs, text layers, provenance, external references, metadata, frames, empty geometry and rights-evidence references were retained.
2. **Missing-old-tree relocation:** copied image files were mirrored under `rehearsal/new/images/`, then only the three owned files under `rehearsal/old/images/` were deleted. The original specialist images remained present. All three relocation candidates were verified using `recorded-sha256`, not a readable old source.
3. **Dry run:** complete restored database file SHA-256 remained `d14088e20fa0c5e4fb3f7e59a2834c1e81ae6898ddc170ecba4d6a07a12c1387` before and after validation. No receipts, path changes or version increment occurred.
4. **Apply:** one save advanced version 4 → 5. Three image paths changed and three checksummed `capture-relocation` attachments were appended, one per object. After removing only those newly appended receipts and substituting the known old paths for the new capture paths, recursive comparison of the entire native project was exactly equal. No frame checksum, identity, text, annotation, source attachment, rights reference or geometry field was changed.
5. **Repeat:** a second relocation from the now-unused old root returned an empty selection. Stored NativeJSON and version 5 remained exactly unchanged.
6. **Exports:** all three diplomatic-text outputs were byte-identical before/after relocation. All three TEI outputs were byte-identical after replacing only the known old image-root path with the new image-root path; the actual exports correctly changed their `graphic@url`. All six export loss-report stderr streams remained byte-identical.
7. **Bundle:** export with `--images` produced 46 entries, including three image entries whose bytes match the registered SHA-256 digests, and `project.json` with exactly the persisted native objects and version 5. Bundle SHA-256: `e1766bcefa5bca1e1aa35a001cf93c8dc668a0cf860bb43b6b33d4aba6963852`. Automatically generated bundle derivative files were not used for fitting or quality claims.

Captured image digests:

| Page | SHA-256 |
| --- | --- |
| 1440 | `ed2b5873fcf76c410ed94fb0bc99d56ecd4c0af4b61ec23054ebcef1d338ca5d` |
| 1450 | `a13a5e4bfbdc3ee8299d47590efd84bd4479d41b7221e7c3367f9434df139608` |
| 1460 | `4079842d996f67a137c61b700b8e694f5c135ee2aecefdbe19e19e0a15a7d4a4` |

## TEI validation and remaining losses

All three relocated exports passed the CLI's `validate tei --schema ... --expect-schema-sha256 ... --strict` command, exit 0, using a copied bounded profile with SHA-256 `9d1e64264d1dd0d5d6594b6dca1a1a159b1022cd2086a73bb6bd19dfd1e624aa`. This is the repository's **C0-0.4-draft bounded profile**, not the full TEI P5, EpiDoc or source NTMSS schema. Each validation emits 101 informational schema notes, including header/attribute elements intentionally not fully constrained by the profile; there were zero Loss or Warning diagnostics. Exit 0 is not evidence of full-source semantic fidelity.

Page **1450** TEI export explicitly reports one annotation-mapping loss, one diplomatic-layer loss and one editorial-layer loss for an AlternativeReading whose exact span cannot be mapped unambiguously to the rendered layer. The native JSON and bundle retain that data. Pages 1440 and 1460 emit no TEI export-loss report, which remains limited to this adapter's supported representation and checks. No lossless TEI round trip is claimed for the three-page dataset.

Source import notes remain visible: standalone abbreviations have no invented expansions, nested markup may be flattened inside supported constructs, and column breaks are recorded as unsupported because the native model does not represent column structure. Diplomatic plain-text exports explicitly report their ordinary loss of geometry, uncertainty/alternative annotation structures, L2 readings and revision provenance. Relocation leaves those existing limitations unchanged.

The original TEI licensing strings/targets are retained as imported metadata and attachments; capture `rightsEvidenceRef` remains null. This rehearsal adds no item-level permission evidence, transfers no rights and does not authorize redistribution beyond the existing pilot terms.

## Final integrity checks and limits

At finish, all **104 original packet files**, including its manifest, matched their pre-command hashes; all **10 registered input assets** matched registration; all **109 published files** and all **206 registered source-snapshot files** matched their frozen hashes. `packet-sha256.after.json` and `binary-sha256.after.json` retain the final inventories. The disposable old images are absent, the new images are present, and the original source collection remains untouched.

This is one local-machine rehearsal on stable owned copies. It does not prove resistance to a concurrent filesystem writer, portability of a deployed shared service, source-platform round trips, eScriptorium interchange, original-resolution image handling or Greek transcription accuracy. Specialist decisions remain pending. No AI requests, recognition, model loading, training, fitting, acceptance or review-status mutation occurred.
