<!-- Copyright (c) 2026 QuantForgeSoftware. All rights reserved. -->
# LabeledLine identity compatibility audit — 2026-11-02

**No current repository consumer relies on `LabeledLine` record equality, hashing, or whole-line deduplication.** Differing `SourceId` values therefore do not change the inspected training, collation, evaluation, split assignment, or label-selection paths through equality. The public record's equality semantics nevertheless change: an external caller can observe different equality for otherwise identical text/image records with different IDs. This is a bounded consumer audit, not a claim of universal behavioral compatibility.

## What changes in the record

[`LabeledLine`](/Users/johnbaima/Code/LeviathanText/src/LeviathanText.Training/VariableWidthCollator.cs:7) retains its two positional constructor parameters and gains an optional init-only `SourceId`. C# synthesized record equality and hash calculation include the new property's backing field. Two lines with the same text and the **same `LineImage` object**, but different SourceId strings, can now compare unequal. `LineImage` is a class with no value-equality override; equal pixel contents in different image objects did not previously make two lines equal.

Two old-constructor lines with null IDs retain their prior equality relationship to each other. This does not promise identical numeric hash codes across revisions, nor exclude observable changes to generated record formatting or serialization of non-null IDs. `with` copies preserve the metadata. The new metadata compatibility case checks constructor/JSON shape, SourceId propagation, and round trips; it is not a dedicated equality regression test, and none was run or added for this audit.

## Current consumers

| Path | Inspected behavior and equality relevance |
| --- | --- |
| [`P01LineDataset.cs`](/Users/johnbaima/Code/LeviathanText/src/LeviathanText.Training/P01LineDataset.cs:57) | Appends every prepared line to train/dev/test lists. Splits depend on ordered source files, not line equality. The revision dictionary is keyed by revision IDs. SourceId is assigned after text/image preparation; there is no deduplication by record or ID. |
| [`SyntheticLineDataset.cs`](/Users/johnbaima/Code/LeviathanText/src/LeviathanText.Training/SyntheticLineDataset.cs:23) | Appends generated records to a list with null IDs; no whole-line equality operation. |
| [`VariableWidthCollator.cs`](/Users/johnbaima/Code/LeviathanText/src/LeviathanText.Training/VariableWidthCollator.cs:32) | Projects Image and Text, keeps list order/count, and returns the input list as `CtcBatch.Lines`. No equality or hash lookup. No other production `CtcBatch.Lines` consumer was found. |
| [`CtcTrainer.cs`](/Users/johnbaima/Code/LeviathanText/src/LeviathanText.Training/CtcTrainer.cs:85) | Shuffles by random sort keys, slices batches by position, and augments through `with { Image = ... }`. Evaluation scores positional Text/prediction pairs. No set, equality comparison, record-keyed dictionary, or line deduplication. |
| CLI [`evaluate lines`](/Users/johnbaima/Code/LeviathanText/src/LeviathanText.Cli/Program.cs:455) and [`train lines`](/Users/johnbaima/Code/LeviathanText/src/LeviathanText.Cli/Program.cs:1101) | Evaluation pairs results by position. Training filters by Text and builds string grapheme sets/groups. SourceId affects the intended OOV diagnostic display; its value does not affect readiness decisions, vocabulary, counts, or sample selection. Split hashes use witness-name/split strings. |

The 34 relevant test files likewise consume ordered lists, projected text/images, or scalar/model results. Vocabulary `Distinct` calls use grapheme strings with ordinal comparison. Inspected membership, dictionaries, comparison assertions, and grouping operations concern strings, IDs, pixels, metadata, or prediction records; none compares or deduplicates whole `LabeledLine` records. The SourceId test compares the string property explicitly.

## Evidence and limits

The audit scanned **339 current nonignored C# files under `src/` and `tests/`**, then traced `LabeledLine`, its producers, `CtcBatch`, `VariableWidthCollator`, and `CtcTrainer` usages, including inferred-variable collection paths. There are five production files and 34 test files in that consumer inventory. [The source scan](/Users/johnbaima/Code/LeviathanText/.artifacts/reviews/labeled-line-identity-audit-2026-11-02/source-scan.json) records exact paths, current SHA-256 hashes, relevant usages, and collection/equality search candidates. [The receipt](/Users/johnbaima/Code/LeviathanText/.artifacts/reviews/labeled-line-identity-audit-2026-11-02/audit-receipt.json) identifies the inspected revision and result.

This is source inspection, not a compiler-symbol proof or runtime experiment. It does not inspect external clients, reflective/dynamic consumers, generated binaries, or historical experiment programs as supported production consumers. Public callers using `Equals`, `==`, `HashSet<LabeledLine>`, record-keyed dictionaries, or default `Distinct` can observe the new identity semantics. No production source change is justified by a discovered internal equality regression because none was found; the maintainer still decides whether the disclosed public semantic change is acceptable.

No source edits, builds, tests, fitting, dataset inspection, or native inference were performed. The frozen evening maintainer packet is unchanged; this audit is a separate review supplement.
