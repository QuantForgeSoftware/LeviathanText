# Reference checkout migration rehearsal

The read-only inventory hashed 291 files in the local Kraken reference checkout
before and after a public-format import into a disposable LeviathanText store.
The file records and hashes matched. No reference application was executed.

| Ledger outcome | Count | Evidence |
| --- | ---: | --- |
| Imported | 8 | Six PAGE XML and two ALTO. Eight original markup attachments matched source bytes and hashes. The project contains 67 lines and 66 revisions. |
| Skipped | 8 | ALTO records lacked a local raster with the record stem under the existing resolver. |
| Failed | 6 | Five PAGE namespace rejections and one malformed documentation example. |

Four imported rows had locally resolved raster files, comprising three unique
SHA-256 values. The two versions of the same PAGE fixture point to the same
image. Four PAGE fixtures retained detached image declarations. These rows
demonstrate markup ingestion only. No scholarly acceptance or comparison
against Kraken output was performed.

The rehearsal reproduced a false overlap finding when captures shared the
`unavailable:pending` checksum marker. The corrected audit ignores unavailable
checksums. An independent raster hash join above still identifies the real
overlap. The inventory also opened a database unnecessarily. It now runs without
opening the store. CLI restore now validates the backup before opening or
creating the target database, and rejects traversal or duplicate image names
in its manifest.

The candidate imported the same counts and restored its project row byte for
byte, including original attachments. SQLite integrity checks passed on both
stores. The backup contained no cached images. Imported local paths still refer
to the preserved source tree, so collection cutover needs separately verified
capture portability and comparison evidence.

Local artifacts are under `.artifacts/migration-rehearsal.1H1SPy/` with the
candidate in `candidate/`. The regression logs show the baseline failures and
candidate passes for overlap, inventory and restore.
