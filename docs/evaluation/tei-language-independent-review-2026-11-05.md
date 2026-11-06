# Independent TEI language review — 2026-11-05

Final language source is approved for the combined gate and fresh package after
four independently reproduced losses were corrected. Final independent verification
passes 13/13 cases with zero warnings/errors. No further blocker was found. The reviewer changed
no production or tracked test source; instance B owns the correction and regressions.

## Confirmed gap and correction

The first independent actual-assembly run had two failures and two passing reset
controls. Supported bare-edition `<lb/>A` lost effective `text@xml:lang="grc"`, and
`<w xml:lang="la"><lb/>A</w>` lost its explicit inline scope. The physical parser
collected only `ab` contexts, producing a non-null empty language list that suppressed
owner fallback. Both cases returned `[]` instead of the explicit source language.

B corrected this by collecting a separate ordered list of original visible language
sources (`ab` when present, otherwise the retained content's original element).
That list resets at every physical boundary. A declared empty row resolves from its
original milestone scope. Source-context metadata remains separate and unchanged;
no detached XML clone, header declaration, glyph or inferred script supplies a
language. An explicit empty scope is not replaced by an outer language.

The [XML 1.0 language-identification specification](https://www.w3.org/TR/xml/#sec-lang-tag)
defines inherited `xml:lang` scope and explicit empty override. The reviewed nearest-
ancestor helper follows those source semantics. This review does not claim full
BCP47 validation or arbitrary intra-row language-span/BiDi reconstruction.

## Independent verification

Final isolated actual-Formats-assembly review: **9/9 passed**, zero failures/skips,
compiler warnings or errors. Generated ASCII cases cover:

- bare edition inherited language and original inline override;
- inline and `ab` empty resets;
- declared empty physical rows with inherited or explicitly empty marker scope;
- mixed bare and `ab` contributing languages in source order;
- high/low unpaired surrogate JSON escapes producing one of the bounded export
  boundary's caught exception types, rather than an uncaught exception.

The first malformed-surrogate test used literal isolated halves as theory arguments;
xUnit collapsed their serialized IDs and ran one case. That diagnostic receipt is
retained but superseded by final ASCII-escape arguments, where both distinct cases
run. The final receipt contains all nine cases without duplicate-ID diagnostics.

Evidence is `.artifacts/experiments/tei-language-independent-v1/`: generated review
project/source, `language-before.trx`, `language-after.trx` and final source hashes.
Compile includes are explicit and no retained before implementation shadows the
actual project reference. Tests contain no models, fitting, inference, corpus reads
or provider calls.

## Remaining reviewed contracts

`DeclaredLanguages` is appended as optional DTO/context metadata, separate from the
existing document language view. `Document.Languages` remains an aggregate metadata
view; it is not a source for line assignments. Declarations come only from the direct
TEI header/profileDesc/langUsage/language path; source descriptions and ignored
body/note header lookalikes cannot supply declarations. Header-only values never
populate effective line languages.

New native object metadata serializes only the declared header list, and export
validates stored JSON before using it. Values are bounded by 128 distinct entries,
1–256 UTF-16 code units per entry and valid XML characters, with ordinal deduplication.
The stored JSON cap uses **256 K UTF-16 code units**, not a raw-byte admission bound.
Malformed/null/nonstring/invalid-code/oversized values cause the existing controlled
StageContractViolation path. Source parsing remains before image decode and project
save; export performs no save. B's application/HTTP regressions cover exact no-write
behavior and HTTP422, while this review additionally checked malformed scalar escapes.

No source-attachment fallback or migration repopulates old stored language arrays.
B's fresh P46 proof reports 81 explicit `grc` lines and preserved header declarations;
the old copied 85-row rehearsal remains unchanged. Those structural/metadata checks
do not adjudicate any Greek reading, geometry or specialist question. All earlier
frozen package proofs remain historical until the coordinating root completes its
new gate/package. No worker throughput candidate was adopted as part of this review.

## Inline-scope follow-up — corrected before final approval

Root source reading identified that `ab ?? source` still overrides nearer inline
language attributes. Three additional generated actual-assembly cases all failed:
inside `ab@xml:lang=grc`, retained `w@xml:lang=la` content returned grc, an explicit
inline empty reset returned grc, and a terminal wrapper containing `hi@xml:lang=la`
returned grc. Receipt: `inline-language-before.trx`. These are explicit source
metadata contradictions, not Greek language inference.

B retains production ownership. A bounded correction can collect effective language
identifiers from retained original content scopes, excluding ignored editorial
subtrees; this need not reconstruct arbitrary language spans. A bare owner fallback
or detached clone cannot recover nested overrides/resets. The earlier source-freeze approval was withdrawn while these corrections were
pending. No corpus, source reading or review status changed.

The follow-up also reproduced one supported no-milestone logical-block loss, retained
in `logical-inline-language-before.trx`. B extracted a shared original retained-text
scope collector and used it in both physical opaque-element handling and logical
segments. It ignores editorial text parents, preserves nearest inline overrides and
empty resets, and falls back to original element/milestone scope only when no retained
text scope exists. It collects a line-level identifier union across source L1/L2
branches; it does not reconstruct language-span positions or BiDi runs.

Final independent actual-assembly receipt: `all-language-after.trx`, **13/13 passed**,
zero skips/failures/warnings/errors. All four reproduced failures are covered and
corrected. Source hashes were refreshed after this review; final freeze approval
was sent to the coordinating root before its combined gate/package. No additional
test execution belongs to the subsequent read-only fitting-test audit.
