<!-- Copyright (c) 2026 QuantForgeSoftware. All rights reserved. -->
# Generic label codec invariant fixes — 2026-11-05

`LabelCodec` now treats an astral Greek symbol as one code point, owns immutable
mapping snapshots, rejects empty mappings before unsafe indexing/encoding, and
merges shared label units consistently. This is the optional generic sequence
codec only. **CTC `LabelSet`, model weights, checkpoints and training remain
unchanged.** No fitting, inference or provider calls were performed.

## Reproduced defects

The initial pure test selection ran 21 tests: **13 failed and eight passed**.
All seven original codec tests passed; a nonpositive-unit compatibility control
also passed. New failures demonstrate:

- U+1D200 (Greek vocal notation symbol) becomes two UTF-16 mappings instead of one;
- an empty text key is accepted, creating a non-progress encoding possibility;
- an empty unit sequence throws `IndexOutOfRangeException` rather than an argument error;
- caller-owned lists and the publicly returned dictionary remain mutable, allowing
  Encode's mapping to diverge from Decode's cached mapping;
- shared new incoming units are allocated repeatedly across sequences;
- a new incoming numeric ID can accidentally collide with a retained old unit,
  including a case that causes Merge to throw instead of producing a valid codec;
- incompatible common-sequence lengths and split/coalesced unit identities are
  accepted, despite no safe interpretation of retained unit identity.

The empty-text regression only attempts construction and expects rejection; it
**never calls the potentially nonterminating Encode**. The before log is
`/tmp/leviathan-label-codec-before.log`, with isolated outputs in
`.artifacts/label-codec-before/`.

An additional scalar-boundary test then showed that explicit isolated-surrogate
keys could still split a valid pair: expected `[3]`, actual `[1,2,3]`. It failed
before the matching-boundary correction, recorded in
`/tmp/leviathan-label-codec-boundary-before.log`.

## Final contract

String constructors and `AddLabels(string)` split valid surrogate pairs as a
single code point. Non-strict unknown text skips one scalar, and greedy text
matching cannot stop between a valid pair. Isolated surrogate halves remain
preserved as their own positions, consistently with Core `ScalarText`; there is
no normalization or silent U+FFFD replacement.

Construction rejects empty text or empty/null unit sequences with
`ArgumentException`, before creating lookup structures. Every unit sequence is
copied into a read-only collection and the outer mapping is read-only. Mutating
the caller's dictionary/list cannot change Count, MaxLabel, validity, Encode or
Decode. Existing singular/prefix-free checks remain in place. No new integer-ID
positivity restriction was introduced.

Merge uses these explicit identity rules:

1. Retain old unit relationships for common text keys. Common sequences must have
   equal lengths and define a consistent **bijection** between incoming and old
   unit IDs at each position. Conflicting split/coalesced identities throw
   `ArgumentException`; no checkpoint meaning is guessed.
2. Report removed old IDs only when no retained sequence uses them. Compact
   retained old units in sorted numeric order.
3. Translate aligned incoming units through that old compaction. Allocate each
   remaining incoming unit exactly once, in sorted numeric order, using fresh
   IDs regardless of numeric collisions with the old codec.
4. Preserve the old codec's Strict policy, construct a validated prefix-free
   result and leave both inputs unchanged.

This returns a codec mapping and original removed IDs. It does not migrate a
checkpoint, assign output weights or enable this codec in a model profile.

## Verification

The final **22/22 pure tests pass with no compile warnings**: seven original
`LabelCodecTests` plus 15 `LabelCodecInvariantTests`. Controls cover one-to-one,
many-to-one, one-to-many and many-to-many round trips; unknown labels; existing
prefix-free rejection; astral/isolated-surrogate handling; immutable outer/inner
collections; shared units; collisions; removal/compaction; deterministic fresh
IDs; incompatible retained mappings; nonpositive IDs and Strict behavior.

The command selected only codec classes in the ML test project:

```sh
dotnet test tests/LeviathanText.ML.Tests/LeviathanText.ML.Tests.csproj \
  --artifacts-path .artifacts/label-codec-after \
  --filter 'FullyQualifiedName~LabelCodec'
```

The ML project also contains fitting tests; **do not run the whole project to
reproduce this verification**. The coordinating engineering runner maintains an
explicit class allowlist. These selected codec methods manipulate strings,
dictionaries and integer arrays, without tensor/model operations. Candidate log:
`/tmp/leviathan-label-codec-after.log`. `git diff --check` passes.
