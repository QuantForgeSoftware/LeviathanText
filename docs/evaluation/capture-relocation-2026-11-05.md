# Explicit capture relocation — 2026-11-05

Scope: native image-path portability under NT-06 and migration §8. This command
operates on an existing local project store and a separately copied image tree.
It does not assign reviewed status, copy files, change image identity, or transfer
rights. The original backup/restore and bundle formats remain unchanged.

```sh
leviathan --store /restored/store.db migrate rebind-images \
  col-PROJECT /original/image-tree /restored/image-tree --dry-run
leviathan --store /restored/store.db migrate rebind-images \
  col-PROJECT /original/image-tree /restored/image-tree
```

Use the actual project identifier emitted by `project list`; the `col-PROJECT`
above is a placeholder. Preserve the relative directory structure when copying.
Keep source and destination trees separate, stable and under operator control
throughout verification. These checks cannot prevent a concurrent filesystem
writer from replacing files after verification. Keep the original backup until
the rebound project has passed the intended export/recognition checks.

The JSON report lists each selected capture's object/surface/capture identity,
old/new absolute paths, SHA-256 and verification method. An empty capture list is
a no-op. Remote, relative and local references outside the selected old root are
unchanged; those paths must be handled separately. No implicit search by basename
or page enumeration is performed.

For a selected capture, a valid recorded SHA-256 establishes the expected bytes
when its original path is gone. If the original is still readable it must also
match that recorded digest. A legacy `unavailable` checksum requires a readable
original; its bytes establish the expected destination SHA-256. Other checksum
algorithms and malformed SHA-256 values are refused. A destination mismatch,
missing file or link escaping either selected root aborts the whole operation.
Root aliases are resolved for containment, and overlapping trees are refused.

The command builds every changed capture and verifies every selected file before
one optimistic project save. The same save appends a checksummed JSON attachment
with role `capture-relocation` on each changed physical object, retaining prior
paths, project version, time and verified hashes. Receipts obey the existing
2 MiB per-attachment bound. A concurrent project edit rejects the save; the
operator must reload and repeat. A dry run validates the same candidates without
saving paths or receipts. No matching captures means no version increment.

Native IDs, coordinate frames (including legacy unavailable checksums), line
geometry, text revisions, review ledgers, source attachments and rights references
remain intact. Recorded destination hashes in the relocation receipts do not
silently rewrite a coordinate frame's historical identity. External JSON output
is a convenience; durable provenance lives in the saved project transaction.

## Verification

Initial focused application verification:14 generated scenarios passed, covering
known-hash/original-byte proofs, dry-run/no-op, all-or-nothing mismatch handling,
missing originals, invalid/unsupported digests, link escape, unrelated references,
overlapping roots and concurrent edits. End-to-end CLI backup/restore/export
verification and final integration results are recorded in the session handoff.
This is engineering evidence from generated fixtures. Accepted Greek records,
real collection cutover and an eScriptorium round trip remain open.
