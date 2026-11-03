# Migration inventory and import smoke (2026-11-02)

Status: read-only inventory + bounded import evidence. This is **not** a Kraken product
comparison, a rights determination or a claim that every source record is usable.

## Inventory

`leviathan migrate inventory <tree> --out report.json` classifies files by content, records
size and SHA-256 (files ≤ 512 MiB) and reports unclassified files. It does not modify the tree
and does not follow symbolic-link directories.

| Tree | Files | Size | Main classes |
| --- | --- | --- | --- |
| Local M01 artifact tree | 241 | 233,173,268 B | 120 PAGE XML, 120 images, 1 archive |
| `~/Code/kraken` reference checkout (read-only) | 291 | 118,898,535 B | 12 PAGE, 10 ALTO, 21 images, 13 model weights, 134 scripts, docs/config/schema |

The reference checkout was only read; no product files, caches or checkouts under it were
modified. The inventory excludes files beyond the configured limit and records that truncation.

## Bounded import

`leviathan migrate import <tree> <new-project> --out ledger.csv` creates a new project, imports
PAGE/TEI/ALTO/hOCR documents with sibling-image resolution and writes
`source,kind,status,objectId,surfaceId,note` rows. Missing images and parse failures are
reported as `skipped`/`failed`; nothing is guessed.

- M01: **120/120 PAGE records imported**, zero unsupported notes, in **4.07 s wall**
  (~1,769 pages/min) with **914 MB peak RSS**, one process, fresh store. This includes .NET
  startup, image decode and original-markup attachment retention; it is not a sustained or
  concurrent-load benchmark.
- The importer is separate from `migrate inventory`; neither runs recognition, training or a
  product comparison.

## Limits

- No automatic project/model migration from Kraken; public formats only.
- The import tree is not compared against an independently running Kraken instance.
- Rights, item-level provenance and accepted-label decisions remain human-owned.
- Multi-page TEI needs `import tei --page <folio>` per image; `migrate import` currently imports a
  TEI whose stem image resolves locally in one pass.
- The 914 MB peak is a single observation on the pilot machine, not a memory-stability result.
