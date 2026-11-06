# Independent label codec review — 2026-11-05

No additional high-confidence correctness defect was found in the reviewed MID32
change. This conclusion applies to the optional generic `LabelCodec` and its stated
mapping/scalar contract. It does not validate CTC integration, model weights,
checkpoint migration, training, OCR accuracy or performance. No production source
or committed test was changed by this review.

## Reviewed scope

Read `LabelCodec.cs`, both codec test classes and the
[author's invariant report](label-codec-invariants-2026-11-05.md). Reviewed input
snapshot ownership, public nested collections, scalar splitting and greedy match
boundaries, incompatible retained unit relations, shared/fresh incoming units,
removal and numeric compaction, input immutability, Strict inheritance and the
continued allowance of nonpositive generic integer IDs.

The merge construction preserves a one-to-one relationship between incoming units
and merged units. Common text keys align incoming units to retained old identities;
inconsistent lengths or split/coalesced identities refuse. Sorted retained old
identities compact first, and each unaligned incoming identity gets one subsequent
fresh ID. This is a generic mapping result, with no implied output-weight migration.
The returned removed-ID set does not expose mutable codec internals.

Outer and inner exposed mappings are copied read-only snapshots. Valid UTF-16 pairs
are processed as one scalar; isolated halves remain preserved under the existing
Core policy. Match endpoints cannot cut a valid pair, and unknown non-strict input
skips a complete scalar. No Unicode normalization or replacement is introduced.

## Independent verification

Exactly the two audited pure classes ran against the actual Recognition assembly:
`LabelCodecTests` and `LabelCodecInvariantTests`. **22/22 passed**, zero skipped,
failures or build warnings/errors. The whole ML suite was not run. These methods use
strings, dictionaries and integer arrays; none calls a model, tensor, fitting,
inference or provider entry point. Building their referenced projects does not
execute their training methods.

A separately registered deterministic probe tested **5,000 generated valid codec
pairs** (seed 231105): 1,988 compatible merges succeeded and 3,012 incompatible
merges refused. It checked:

- exact common-unit compatibility classification and all incoming unit equality
  relationships, so shared units cannot split and distinct units cannot coalesce;
- exact removed original IDs, sorted retained compaction and sorted fresh allocation;
- unchanged input mappings and individual incoming-key encode/decode round trips.

The probe also exhaustively checked **3,906 UTF-16 strings**, all lengths zero
through five over `a` and two high/two low surrogate code units. Its independent
scalar cursor expected mapped complete pairs, skipped unmapped complete pairs and
preserved isolated halves. Every encoding matched exactly; no match cut a valid
pair. These finite generated checks are evidence, not a proof over arbitrary input.

The property probe explicitly compiles the pinned current production source plus
its own driver and references Contracts only; it contains no duplicate retained
implementation or Recognition/model reference. The two-class test separately
verifies the actual production assembly. Source/test/probe SHA-256 values were
registered before the probe and rechecked unchanged afterward. All checks passed,
and both the test and probe processes exited zero.

Receipts and reproducible probe source are retained in
`.artifacts/experiments/label-codec-independent-review-v1/`: `registration.json`,
`codec-independent.trx`, `probe-result.json`, `verified-source-sha256.json` and the
explicit-Compile probe project. The independent test output uses
`/private/tmp/lt-midday-codec-review`; normal build outputs were not shared.

No new timing assertion, integer-ID restriction, dependency, model-profile selection
or automatic codec integration is recommended by this review. The author-owned
22 pure cases remain suitable for the coordinating engineering runner's explicit
class allowlist; all other ML classes remain excluded from this work session.
