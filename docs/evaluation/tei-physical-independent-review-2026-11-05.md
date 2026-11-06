# Independent physical TEI review — 2026-11-05

Review covered the new physical cursor, inline fragmentation, milestone identity/page context, canonical exports, metadata bounds and logical fallback. Three related high-confidence findings were reproduced with invented ASCII fixtures and corrected by the implementation owners. No production Formats source was edited by this reviewer.

## Findings and corrections

**P2: skipped editorial subtrees affected witness structure.** The reading builder skips complete `note`, `bibl` and `ref` subtrees, but the physical cursor initially observed their nested `lb`/`pb`. For example, `<lb/>A<note>X<lb/>Y</note>B` split the visible witness `AB` into `A` and `B`. Hidden page breaks also split/relabelled witness rows; hidden line breaks could activate physical mode, supply a leading ID/number, or cause cross-line alternative refusal. Ignored context attributes and logical `ab` descendants could also influence import. A shared ignored-subtree predicate now consistently governs discovery, traversal, visibility, page provenance, metadata validation and fallback selection. Recursive visibility prevents wrappers containing only ignored text from manufacturing visible content/context. Ordinary real nested word milestones and real page changes remain active.

**P2: a hidden primary edition could override the visible witness edition.** Edition selection initially still searched skipped editorial subtrees. A `note` containing `div type="edition" subtype="primary"` could take priority over the actual visible edition; after subtree filtering it produced no witness rows. All edition-selection tiers now use the same visible-division candidate sequence. The implementation owner's controls additionally preserve legitimate primary-edition priority and book-division fallback.

**P2: an untranscribed native line could claim lossless conversion into an empty reading.** Export with neither L1 nor L2 emits an identified empty `ab`, `lb` and a not-yet-transcribed comment. The new approved empty-row semantics correctly preserve that identified row, but import creates an explicit empty L1 revision. The prior export report contained no layer loss when a graphic was supplied. Export now reports `diplomatic-layer` Loss for this absent-reading distinction. An explicitly present empty L1 remains lossless. This correction reports the limitation; it does not reinterpret comments as authoritative native state or change persisted projects.

## Independent evidence

Evidence root: `.artifacts/tei-physical-independent-review/`. The authoritative final receipts are:

- `clean-before.trx`: **11 failed, four passing controls**, against the exact saved pre-correction algorithm under a uniquely renamed namespace.
- `clean-after.trx`: **15 passed**, against the corrected production Formats assembly.

The baseline transformation changes only the class namespace and adds an import for unchanged public Formats dependencies; fixture aliases select that baseline explicitly. Original source hash/copy and the transformation are retained in `registration.json`, `clean-baseline-registration.json` and `TeiXml.before.cs`. Explicit compile inputs prevent copied evidence files from becoming test source accidentally.

An initial harness unintentionally included that retained source snapshot through its default compile glob, producing type-shadow warnings and invalidating its attempted current-assembly retest. Those earlier `before`/`after` receipts are superseded by the clean, explicit-input runs above. Only the clean receipts support the final comparison; both clean builds have no compiler/analyzer warnings.

The 15 independent cases cover all three skipped subtree names, hidden page pollution, hidden-only physical-mode activation, irrelevant alternative refusal, hidden leading identity, ignored metadata/logical rows, hidden edition selection and absent-versus-empty canonical layers. Passing controls preserve actual nested word breaks, real page transitions, ordinary skipped notes and an explicit empty witness reading. The implementation owner also reports a complete **196/196 Formats** pass with its ported/generated regressions; that broader receipt remains in the owner's [physical-line handoff](p46-physical-tei-lines-2026-11-05.md).

## Scope and remaining limits

No additional high-confidence blocker was found in the reviewed corrected physical cursor, source-context encoding/bounds or canonical identity/annotation paths. Source-context JSON remains untrusted provenance, not reading or identity authority. Cross-line alternatives that cannot safely map to both layers still refuse explicitly. No-milestone documents retain their bounded logical fallback. Existing whitespace collapse, unsupported-markup reports and single-field export limits remain documented limitations rather than inferred scholarly semantics.

This review does not independently certify every TEI construct, inherited-language convention or full schema compliance. It used generated ASCII evidence only. It did not reimport any saved collection, change original P46 sources/crops/labels, associate a crop with a reading, accept a specialist decision or run recognition/training. The earlier 85-row P46 portability rehearsal and its frozen binaries remain historical, unchanged evidence.
