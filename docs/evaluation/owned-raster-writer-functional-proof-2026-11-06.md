# Owned raster writer: independent A17 functional proof

Exact A17 Imaging binary `5672568a40436b80753ec7843f047aca36946826855453222d5e3d908c4f40ba` preserved every compared result across149 generated cases and1,341 public operations. There were no output differences. This is functional evidence only; no end-to-end speed, allocation or production-adoption claim follows.

The baseline was exact published A16 Imaging binary `e8958e662674c28b6d0b49b697de053465de42e6e164bed43ffc732b1ba9b7d0`. The candidate helper runtime differed only in Imaging DLL/PDB. Every dependency/native/input hash remained unchanged, and all149 candidate processes exited zero and were reaped.

The cases are deliberately separated:

- 133 historical cases selected by fixed owned-format filename extensions from the complete generated92, extension38 and prior-format3 subsets. Selection included malformed and unsupported cases rather than retaining only successful decodes.
- 16 newly generated holdouts: eight TGA origin/RLE combinations with seven alpha levels, two QOI alpha/opcode cases, and PNM P1–P6.

Actual A16 results were frozen before any A17 assessment. The combined oracle SHA-256 is `438ae818f58f1dcd956b6bfdd04913d77ddbc556b0a57c13be07f0c2551cb7de`, stored at `.artifacts/experiments/sec01-independent-v1/writer-all149-a16-oracle-freeze.json`. This avoids substituting older output from before the decoded-budget changes.

Each case compared Dimensions, Decode, three DecodeBounded budgets and four DecodeBand requests. Equality includes success/refusal type/code/message, complete small-image RGBA, dimensions, color/alpha metadata, scaling, source checksums and encoded-byte identity. Only operation elapsed/allocation instrumentation was removed from semantic equality. No tolerance or acceptance-policy change was introduced.

Candidate process registrations and complete outputs are in `runs/writer-history-exact-a17/` and `runs/writer-holdout-exact-a17/`. Consolidated evidence is `writer-a17-independent-analysis.json`; the exact two-file runtime delta is pinned in `writer-a17-stage-registration.json`.

The earlier copy-only timing experiment is not a pipeline proof. A separate registered representative resource comparison remains necessary. A17 does not include the separate RGBA workspace-cap proposal, OJPEG deferral, RGB8 validation-buffer proposal or later main crop/ownership changes. ImageSharp replacement and advisory closure remain unapproved.
