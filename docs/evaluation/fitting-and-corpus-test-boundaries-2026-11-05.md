# TEST-01: discovery, corpus admission and fresh evidence outputs

Date: 2026-11-05. Implementation follows the separately approved
[task brief](../tasks/fitting-and-corpus-test-boundaries-2026-11-05.md).
This work executed no fitting, optimization, model inference, actual corpus reads,
reserved-label reads or real providers. It does not reset the spent P01 evaluation
split or supply any Greek-specialist decision.

## Changed boundaries

Three capabilities are independently default off. The exact value `1` is required:

| Capability | Environment gate | Scope |
| --- | --- | --- |
| Fitting | `LEVIATHANTEXT_RUN_FITTING` | Explicitly selected fitting/API paths |
| Corpus | `LEVIATHANTEXT_RUN_CORPUS` | Explicitly selected real-data access |
| LockedEvaluation | `LEVIATHANTEXT_RUN_LOCKED` | Explicitly selected reserved content/evaluation |

Flags remain execution gates, not user authorization. Real-data fitting combines
Fitting and Corpus; fitting plus reserved evaluation combines all three. Unknown
bits, missing `Required`, and explicit None on a capability attribute fail closed.
The pure policy's None remains valid for an operation requiring no capabilities.
Existing FittingFact/FittingTheory public names and exact flag behavior are unchanged.

The new shared plain attributes use the ordinary xUnit discoverer. Skippable
counterparts retain the existing cached SkippableFact 1.5.85 discoverer and runtime
`Skip.IfNot` behavior. Their constructor preserves the discoverer's `params Type[]`
ABI; capabilities use the named `Required` property. A harmless sentinel caught and
recorded the failed enum-constructor prototype before any real consumer execution.

Exactly 70 audited declarations now carry one capability attribute: 36 generated
ML fitting/API paths, nine real-data ML experiments, and 25 Pilot declarations.
Three generated paths are negative pre-update controls and still require the
fitting API gate. The inventory is source-level coverage, not a compiler call graph
or an assertion that every possible future test is safe.
`consumer-capabilities.json` gives every exact method and required combination.

The all-27 P01 XML import, fixed P01 line-crop XML import and first-six layout
comparison additionally require LockedEvaluation: their direct reads cover reserved
or unproven partition membership. Filename-only pairing remains Corpus-only.
Other literal XML-content readers were traced: the held-out layout comparisons
already require LockedEvaluation; the pipeline iterates only admitted dev metadata;
IIIF/manifests/inscription fixtures are separate corpus inputs without a claimed
reserved P01 partition. Image-only integration fixtures do not gain label-scoring
permission from their Corpus gate.

P01PilotTests no longer probes the dataset directory in a static field initializer.
Pilot fixture copying is conditioned on exact Corpus opt-in, so default builds do
not copy those assets. An earlier opted-in build can leave stale output fixtures;
the discovery gate is authoritative, and fresh isolated output/rebuilds are required
when checking absence. The verification here used fresh isolated outputs and found
no Pilot output Fixtures directory. Existing real missing-data runtime skips remain
available after an explicitly authorized opt-in.

## Partition loader contract

Both existing public P01LineDataset.Load signatures remain callable, including the
original compiled five-argument overload. They deliberately now load **TrainDev**
only. This is a safe-default behavior change: an empty TestLocked means not loaded,
not a measured corpus count of zero.

`LoadPartitions(..., DatasetPartitionAccess.TrainDevAndLocked, ...)` explicitly
admits full content. It is not itself authorization. The library consults no test
flag. Invalid access enum values and nonpositive split divisors fail before any IO.

The original ordinal XML index assigns partitions before filtering, including
missing-image entries in the index. Image pairing uses file metadata only, never
XML content. WitnessSplitMap preserves all paired witness assignments, including
unloaded reserved witnesses; missing-image XML retains its original index without
being added as a paired witness. Reserved XML reads and image decode happen only
after partition admission. TotalLines counts admitted loaded rows only.

Snapshot gains a nonpositional nullable LoadedPartitions property; its existing
constructor/deconstruction remains unchanged. Null denotes a manual/legacy snapshot
with unknown scope. Per-call internal IO delegates support generated recording
controls, with a narrow ML-test friend assembly; no mutable global hook exists.
Preprocessing and the legacy subset-raster path are unchanged for admitted samples.
Two existing generated one-page preprocessing fixtures now explicitly request their
generated reserved partition.

Root separately migrated CLI train/evaluate callers to explicit TrainDev, nullable
manifest locked counts and scope metadata, qualified model-card text, and omission
of unavailable registry metrics. This report's source freeze records our own loader
and tests; root owns the final CLI/manifest integration evidence.

## Fresh output contract

Eighteen historical report writers (nine ML experiments and nine Pilot reports)
now reserve fresh children under an explicit existing output container specified by
`LEVIATHANTEXT_TEST_OUTPUT_DIR`. Report and previously fixed model-package paths no
longer target tracked docs or reused .artifacts/pilot/models paths. ML comparison
checkpoint roots and selected Pilot checkpoint roots move under the same reservation.
Other already-unique temporary checkpoint fixtures retain their existing lifecycle;
this is not a comprehensive scientific-run packaging system.

The helper accepts physical paths under repository .artifacts/experiments or OS temp,
refuses historical docs/data/models/releases containers, relative/missing configuration,
symlink/reparse traversal, existing child directories and reservation collisions.
Each GUID child has an exclusive retained sibling token and in-directory receipt.
Repeated/parallel cooperative runs get different children and never reuse evidence.
Receipt creation can remain after a failed reservation; no historical receipt is deleted.

Reservation coordinates participating writers on one host. It is **not** an atomic
guarantee against a malicious filesystem actor swapping ancestors between checks.
macOS/Windows comparisons conservatively ignore case, including on a case-sensitive
macOS volume; a false refusal is preferred to permitting a docs alias through the
OS-temp exception. macOS callers use physical /private/... paths rather than /var
aliases. No reports were regenerated or copied over historical measurements here.

## Measured verification

Registration: `.artifacts/experiments/test01-boundaries-v1/registration.json` pins
71 source/audit inputs before edits. All experiment files below are local evidence,
not new scientific measurements.

| Run | Result and actual scope |
| --- | --- |
| Ungated harmless managed baseline | 10 pass, one runtime skip; 24 constructor/data/body markers. The no-entry safety assertion fails. |
| Initial Skippable ABI prototype | Four discovery failures, one control pass, six plain skips. Retained; corrected before consumer runs. |
| Eight capability combinations, managed sentinels only | 18 cases each; default one pass/17 skips and zero markers; all flags ten pass/eight skips. Independent single flags and combinations verified. |
| Final pure managed project | 16 pass/17 intentional skips, zero failures; includes 15 filesystem helper cases. No Training, Skia or Torch project reference. |
| Original public loader source, renamed only | One generated regression fails at the reserved invalid XML sentinel before reaching an admitted sample. |
| Final generated loader and existing preprocessing controls | 13 pass: 11 new generated cases/rows and two existing non-fitting controls. No actual dataset access. |
| Pre-fix output case-alias helper | One generated regression fails: a repository docs alias was incorrectly admitted through physical temp. |
| Final output helper | Alias regression passes within the final 15 helper cases. Historical content remains unchanged. |
| Final guarded real entrypoints, flags absent | ML 45 declarations and Pilot 25 declarations all skip; executed=0, not functional passing cases. No opted-in real bodies were run. |
| Isolated ML/Pilot/managed compilation | Final builds zero warnings/errors after NuGet metadata refresh. Earlier offline NU1900 warnings remain in historical logs. |

Primary receipts:

- `managed-marker-proof.json` and `harmless-matrix-final/proof.json` (only the pure
  managed sentinel assembly ever ran with capability flags enabled).
- `policy-default-alias-final/default.trx` (final managed project).
- `loader-original-public/before.trx`, `loader-generated-release/loader.trx`.
- `output-alias-original/before.trx` and the final managed TRX.
- `disabled-consumer-release-proof.json` records exact selected commands and TRX hashes.
- `source-freeze.json` records 51 owned/control source hashes, four compiled assembly
  hashes, and unchanged trainer/layout/codec controls; `final-source/` holds copies.

The two pure codec classes and existing Fitting attribute source remain unchanged.
No fitting implementation, native layout model or historical measurement was altered.
The no-model/corpus statements describe this task's enumerated commands/fixtures,
not a global system activity monitor or proof against future code bypassing policy.

## Safe verification and remaining integration

Default managed command (no model/corpus dependencies):

```sh
env -u LEVIATHANTEXT_RUN_FITTING -u LEVIATHANTEXT_RUN_CORPUS -u LEVIATHANTEXT_RUN_LOCKED \
  dotnet test tests/LeviathanText.TestPolicy.Tests/LeviathanText.TestPolicy.Tests.csproj
```

Root owns solution/engineering-runner integration, final public instructions, and
central verification. Keep its exact six existing class exclusions and the two
pure-codec selections. The new generated loader class remains an explicit audited
selection, not permission to run the ML/Pilot projects wholesale. Other native tests
and historical experiments still require deliberate selection and review. No real
opt-in experiment was executed to test these controls; an actual fit or reserved
evaluation needs a separate authorized, preregistered run.
