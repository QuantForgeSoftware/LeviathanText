# TEI explicit language preservation — 2026-11-05

## Finding and bounded change

The actual native P46 compatibility replay found that source
`<text xml:lang="grc">` and header `language@ident="grc"` were retained only in
the source attachment: native line language arrays and TEI exports omitted them.
The earlier [compatibility report](p46-native-import-compatibility-2026-11-05.md)
records that failing build rather than claiming its empty arrays preserved the
source metadata.

`TeiXml` now resolves retained original text's language from its nearest
explicit `xml:lang` ancestor, including `text`, `body`, `div` and the document
root. An explicit empty attribute resets inheritance. This reads source
metadata; it does not identify language from glyphs, header descriptions or
scholarly interpretation. Physical segments retain an ordered line-level union
of effective languages from retained text-node parents. This includes both
retained L1/L2 alternative branches and supplied content; it does not encode
language-span positions, attribute languages to individual layer offsets or
infer BiDi behavior. Ignored note/bibliography/reference subtrees do not
contribute. Visible atoms without text use their original element scope, and
declared empty lines use the original milestone scope.

Header declarations are a separate ordered list, read only from the direct
`TEI/teiHeader/profileDesc/langUsage/language@ident` path. Header-only `grc`/`la`
declarations do not assign either language to lines without effective
`xml:lang`. Source/body/note lookalikes are excluded. Codes are retained exactly,
deduplicated ordinally, with at most 128 distinct codes of 1–256 UTF-16 units
each and valid XML characters. No language registry membership is inferred.

New imports store this declared list in native object metadata
`tei:declaredLanguages` and export it as header `langUsage`. Optional DTO
parameters were appended, preserving existing call sites. Stored invalid JSON,
null/invalid codes and excessive bounds cause a controlled
`StageContractViolation` (HTTP 422) before export; the project is retained.
The stored JSON field has a 262,144 UTF-16-code-unit parsing bound (a string
length limit, not a raw-byte limit). This is separate from XML
parser limits and the existing 2 MiB **source attachment retention** cap.

There is no migration or source-attachment fallback for old language arrays.
Existing projects require explicit reimport or deliberate metadata repair;
export does not guess omitted values from old markup.

## Regression evidence

The initial generated cases failed on the prior implementation: five Formats
and two Application tests. Final focused/full project runs pass with no build
warnings. These full Application/API runs were mistakenly unfiltered; the
scope violation below must accompany their counts:

| Project | Passing tests | Added language cases |
| --- | ---: | ---: |
| Formats | 223 | 27 |
| Application | 361 | 10 |
| API | 219 | 2 |

Tests cover text/body/div/root inheritance, closest override and empty reset,
header-only declarations, ignored source/body/note lookalikes, exact code order,
bounded invalid declarations, native reload/export, malformed stored JSON,
legacy no-inference behavior and actual HTTP 422 responses with no mutation or
private source text leakage. Independent review then found bare edition rows
without an `ab` had no language context. Four additional cases demonstrated
three failures and one empty-reset control before the correction. The physical
cursor now separately retains the original visible content's language scope;
declared empty rows use the original milestone scope. Metadata/source-context
collection remains unchanged. Direct text, wrapper override, wrapper empty reset
and a numbered empty wrapper row pass, and the complete Formats suite passes
again (213 tests). A subsequent independent inline-scope review found the
`ab` fallback still masked nearer inline override/reset, including a nested
opaque element. Four new cases failed before correction; original retained
text-parent collection now preserves these scopes, including an explicit
choice/supplied language-union and hidden-editorial exclusion control. The
complete Formats suite passed again (217 tests). The same original-scope policy
is now shared with the supported logical/no-`lb` path: three inline cases and
its choice/supplied union failed before correction, while the physical control
passed. Two further controls verify original language scope on visible textless
`gap` atoms in both modes. The final complete Formats suite passes 223 tests.
This is still only a line-level language union. The generated export is also validated against the
repository's pinned bounded TEI schema; this is not full TEI conformance.

The first independent actual-assembly review passed nine controls, including both
previously failing bare-row scopes, empty resets/declared rows, mixed bare/`ab`
source order and controlled malformed stored surrogate JSON refusal. No further
source blocker was found in that initial scope; the subsequent inline review
reopened the freeze and is separately recorded in the
[independent review](tei-language-independent-review-2026-11-05.md).
Final independent actual-assembly signoff passes 13/13 controls with no
warnings/errors after the shared physical/logical collector correction; no
further source blocker was found in the bounded union policy.

### Verification scope mistake

The three unfiltered Application runs (`app-full`, `app-full-final`, `app-final`)
and three unfiltered API runs (`api-full`, `api-full-final`, `api-final`) omitted
the engineering entry point's fitting exclusions. Even the initial Application
run with two missing-fixture failures ran the other passing tests. As a result,
`LearnedLayoutIntegrationTests` fitted a synthetic patch-layout model three
times, each for 40 epochs on four generated 260×320 band images, and
`LayoutModelApiTests` fitted one three times, each for 30 epochs on three
generated 240×300 band images. They also performed generated layout detection.
This violated the assigned no-fitting scope. It was not training on P46 or any
real manuscript, and no provider, production checkpoint or accepted label was
used. The tests' `Dispose` methods remove their generated image/model roots;
the API factory's separate randomly named temporary SQLite store has no explicit
cleanup, so complete removal of every generated store is not claimed.

The earlier `app-after` invocation failed compilation before tests; initial
before/after language-only runs were filtered. All retained invocation logs are
preserved under the evidence path below. No further fitting was run after this
was identified. The accepted final gate is the separately run **filtered**
`scripts/verify-engineering.sh` result: 1,684 passing tests, one existing live
Ollama skip, 14 selections and zero build warnings/errors, with receipts under
`.artifacts/midday-final-retention-engineering/`. Its Application 359/API 220
counts intentionally exclude the three tests above.

Source tests are `TeiLanguageMetadataTests`, `TeiLanguagePersistenceTests` and
`TeiLanguageHttpTests`. Before/final logs and the successful isolated Release
CLI publish log are retained under
`.artifacts/tei-language-after/evidence/`. Two unchanged tracked PAGE/TIFF test
fixtures were copied into the isolated build's expected artifact fixture paths;
an initial Application run's two missing-fixture failures were environmental,
not language assertions. The final full suite above includes both controls.

## Actual native P46 replay

Using the newly published CLI and fresh owned inputs/database, the three
original TEIs produce 27/26/28 lines (81 total). All 81 native line arrays now
contain exactly `grc`; each native object's declared-language field contains
`["grc"]`. Store-loaded exports retain header `grc` and effective `grc` on all
exported physical lines. All three validate against the bounded schema
SHA-256 `9d1e64264d1dd0d5d6594b6dca1a1a159b1022cd2086a73bb6bd19dfd1e624aa`.

The replay compares source-context tags, computational text layers and retained
source attachment bytes/checksums against the preceding physical import. They
match. A separately copied old rehearsal database remains 85 lines, version 5,
with its original empty language arrays and absent declared-language field.
Actual exports and duplicate-skipping imports leave its exact persisted native
JSON, SQL version and database bytes unchanged. The original rehearsal database
was not opened. All 104 original specialist-packet files and all 55 files in the
new CLI publish remain byte-identical to the replay's preregistered hashes.

The procedure, registration, command stdout/stderr and result receipt are under
`.artifacts/experiments/p46-native-language-v1/`; the final receipt is
`replay/results.json`. This is a new source build, so earlier final-supplements
package proofs remain historical. The subsequent final reviewed package passes
the same actual published-CLI replay, plus four preregistered generated CLI
controls for physical inline override/reset, logical inline override and ignored
note scope; every control retains exact diplomatic text `A`. Its receipts are
under `.artifacts/midday-final-package/final-language-reviewed/` and its exact
artifact/closure details are in the [package operations report](midday-package-operations-2026-11-05.md).

Existing Michigan Public Domain Mark 1.0 image and INTF CC BY 4.0 transcription
receipts remain the rights evidence; no new rights approval is implied. No
source packet, source map, accepted label, geometry, review decision, model or
checkpoint was changed. Page 1440 row 10's V13/V14 source-context association
remains a specialist question. The P46 compatibility replay itself performed
no training, inference or provider call; the separate unfiltered test mistake
above is not covered by that narrower statement.
