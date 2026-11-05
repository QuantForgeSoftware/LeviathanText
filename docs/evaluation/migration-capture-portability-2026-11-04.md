# Migration capture portability and rollback runbook

Use this rehearsal before a migration cutover that depends on local captures. Keep the source collection and the original project intact until the restored project and capture bytes pass verification. The [reference migration rehearsal](reference-migration-rehearsal-2026-11-04.md) covers imported document counts. The [P46 rehearsal](ntvmr-nine-page-rehearsal-2026-11-04.md) covers exact TEI attachment recovery. This runbook covers the image paths both rehearsals left open.

## What to retain

| Artifact | Includes | Recovery use |
| --- | --- | --- |
| SQLite backup directory | Consistent store snapshot, manifest with checksums, files in the store's sibling `images/` directory | Project state, revision and annotation history, original markup attachments, adjacent cache bytes |
| Separately pinned source snapshot | Authorized capture and annotation bytes, original provider IDs and rights, exact location and digest per capture | Recover and verify external files referenced by the database |
| Project bundle ZIP | Native project JSON, crosswalks, reports and derived exports. `--images` adds decodable local source bytes within per-file and aggregate bounds | Portable reference and offline edition. Inspect `images-omitted.txt`, manifest and actual archive entries before relying on coverage |

SQLite retains the capture path recorded at import or caching. Local imports normally record an absolute path. Restore copies the cache files beside the new store while keeping the original path values in the database. This can leave the cache bytes present and the capture unresolved. Record the path and byte digest for cache entries as well as external captures. A relative path already present in legacy records needs its original working-directory base recorded and tested.

The ZIP's native JSON likewise keeps the original locations. Edition HTML uses relative links to raw entries included with `--images` and can be moved as a complete extracted tree. The ZIP has no supported database-restore or path-rebinding route. Searchable PDFs inside a bundle can carry raster images even when `--images` is omitted. Review permission and content of the whole bundle before transfer.

## Capture and verify a cutover candidate

1. **Inventory.** Work on an authorized collection copy and disposable project. B records each capture's source ID, exact persisted path or URL, physical file path, owning root, path relative to that root, byte length, SHA-256, asset terms and relevant annotation digest. For cached remote captures, retain the original URL and cache lineage separately. Keep the exact path spelling and use a read-only independent digest check. Where native imports record a digest, compare it with the file hash. Document a legacy unknown digest rather than inferring identity.
2. **Pin and export.** A stops store writers for the rehearsal and records the CLI identity, project crosswalk and baseline audit, then creates the SQLite backup and image-enabled bundle using fresh destination paths. Verify the backup manifest and bundle ZIP integrity, account for all image omissions, compare included raw entry digests with the capture ledger and check required paths in the native JSON. Keep the separate authorized source snapshot even when the bundle includes all captures.
3. **Restore to a disposable store.** Use a fresh destination and preserve the existing source paths for the first pass. Verify the same project, surfaces, attachment hashes, crosswalk and line data. Independently rehash every required file resolved from each recorded capture path. Export an image-enabled bundle from the restored store and compare required images by capture ID and digest. Check representative offline edition links from a separate extracted directory.
4. **Rehearse relocation if applicable.** Restore the backup into the candidate tree and copy the separately pinned authorized captures and annotations into their planned locations. Preserve the logical path values in the restored database through an operator-controlled mount or alias at the expected original locations. On a disposable Unix fixture, a directory symlink was sufficient. Create aliases only at vacant authorized paths and verify each resolved path stays within the intended restored root. Review and test platform-specific path or volume requirements. When original paths cannot be made available, stop the rollback rehearsal at the path-resolution gate. A fresh import from a moved tree has new native IDs and requires a separate migration comparison.
5. **Exercise rollback and cutover checks.** Preserve the pre-cutover store snapshot, image/cache digest ledger, source snapshot and active path mapping. On rollback, stop writers, restore the matching snapshot and image tree, verify every digest and repeat a read-only export before opening editing. Record results for text, layer and IDs, image bytes and offline links separately. A successful command or ZIP with images is insufficient when a capture resolves to different bytes.

Use distinct paths and authorized capture roots when adapting these commands.

```bash
leviathan --store "$project_store" project audit "$project_id" --json
leviathan --store "$project_store" export crosswalk "$project_id" --out "$crosswalk"
leviathan --store "$project_store" export bundle "$project_id" "$baseline_zip" --images
leviathan --store "$project_store" backup "$new_snapshot_directory"
leviathan --store "$fresh_restore_store" restore "$new_snapshot_directory"
leviathan --store "$fresh_restore_store" export bundle "$project_id" "$restored_zip" --images
```

### Coordination and cutover gate

A owns the versioned engineering handoff, replay commands, store snapshot and ID crosswalk checks. B owns the exact source inventory, item-level rights, source path/hash mapping and review inputs. The reviewer independently compares source and accepted records and reports unresolved decisions. The operator records elapsed hands-on time per stage, including path reconciliation and exceptions. This exercise supplies mechanical recovery evidence. Actual collection cutover still depends on approved data, discrepancies and configuration.

## Executed synthetic proof

November 4, 2026. Ran 12 successful commands from an already built isolated Release CLI, SHA-256 `4fe6c4bf545844cc727672cbcbf32133997dde04338180955c674903f66c9c3d`. The inputs were two generated 48×24 PNGs and two invented TEI records. One capture pointed to an external directory through a stable test alias. One referenced a manually staged image under the primary store's adjacent `images/` directory. No network fetch, recognition, training or review actions ran.

| Control | Result |
| --- | --- |
| Original backup and image-enabled bundle | The backup included one adjacent-cache image. The ZIP included both captures and matched the independent digests. The no-raw-images ZIP had no `images/` members and contained two PDFs with image objects. |
| Restore while original logical paths were available | The restored SQLite project row matched exactly and both captures exported with their original SHA-256 values. |
| Restore into a new tree with original paths hidden | The restored cache file existed and matched its backup hash. Both original logical paths were unresolved. Both captures appeared in `images-omitted.txt`. |
| Relocate underlying bytes while preserving logical paths | Pointed both authorized synthetic aliases into the moved tree. The database row stayed identical. Both images resolved inside the moved tree and matched the pinned digests in the new ZIP. |
| Valid image content changed at the same path | The bundle included the changed but decodable bytes while retaining the original native frame digest. Independent comparison detected the drift. Restored the original test bytes and checked both digests again. |
| Independent offline bundle extraction | Four image links resolved inside the extracted tree with exact capture digests. This covered the project-level edition and both surface editions. |

The local evidence is under `.artifacts/experiments/capture-portability-v1.HB2Yb8/` with the script, CLI receipts, source ledger, backup, ZIP files, restored stores and SHA-256 receipt. A first harness invocation compared the persisted JSON's embedded version with the exported store-populated version. The final harness compared the exported version with the SQLite row version and checked that the rest of the document was identical. The rehearsal covers a disposable macOS path layout and an operator-controlled relocation. It supplies no real collection cutover or scholarly acceptance evidence.

## Verified bundle behavior after the export correction

The PDF-image and source-drift rows above preserve the baseline evidence. In the corrected bundle path, raster inclusion is opt-in. The default ZIP excludes raw image entries and PDF raster backgrounds. Its PDFs render placed diplomatic text without a raster. A surface with no placed lines can have an empty PDF text layer. With `--images` or `?images=true`, the same validated image bytes feed the raw entry and PDF background, and a recorded SHA-256 mismatch omits the capture. Keep the independent source ledger for external paths and captures without a recorded digest. Standalone searchable-PDF export retains its separate image-bearing behavior.

An isolated localhost test submitted 50 requests to each frozen API build. The two sequential controls were followed by 12 bursts of four concurrent requests, alternating default and image opt-in. The baseline returned 14 successes and 36 shared-temporary-file HTTP 500 errors. Seven successful default ZIPs had a PDF raster. The corrected build returned 50 successful ZIPs, with exact requested manifest policy, no raw entries or PDF rasters in 25 default results, and one independently matched raw entry and one PDF raster in each of 25 opt-in results. Both owned servers exited and the corrected request temporary directory was empty. Baseline and candidate evidence is retained under `.artifacts/experiments/bundle-concurrency-v1.fsqwxS/` and `.artifacts/experiments/bundle-concurrency-candidate-v1.xN450k/`. The corrected API DLL SHA-256 is `d5e23febfb390d70be0ee41de0452bc163ae290a39832d808be29efb7522fba8`.
