# Nine-page P46 CLI source rehearsal

November 4, 2026. Exercised the current Release CLI against the nine archived Michigan width-2200 image and NTVMR TEI pairs pinned in [the source inventory](../data/ntvmr-source-inventory-v1.json). The run used disposable SQLite stores and no network. It ran no recognition, fitting or review-decision commands. The incoming source ledger and archived TEI files remained unchanged.

## Workflow and checks

1. Created a fresh project. Imported each TEI and its exact JPEG with `--kind papyrus-side --page <folio> --skip-duplicates`. Before invoking the CLI, checked each source hash and size against the inventory.
2. Repeated all nine commands. Each returned the original surface ID and a duplicate report. The exact native project JSON, optimistic version and project SQL row stayed unchanged after each repeat.
3. Exported each surface through both `export tei` and `export diplomatic`. Read the native project and independently verified all nine TEI attachment byte strings and hashes, nine capture byte hashes and dimensions, and absence of placed lines and review decisions.
4. Ran `backup` and `restore` into a separate store. Both databases passed SQLite integrity checks. The restored project SQL row exactly matched the original. All 18 restored export stdout and stderr pairs matched their pre-backup counterparts byte for byte.

**All 57 CLI calls exited zero.** Release CLI DLL SHA-256 was `80d981cbd882542c30c32aa79c95c33360afe32b4e4d05b4e87b0f7b85c382ca`. The run pins LeviathanText DLLs, schema and inventory before and after execution. A first harness invocation completed the CLI sequence but failed during the SQLite JSON audit because the audit expected an unwrapped project object. The reported run used a new store and corrected the audit to read the native contract wrapper.

## What survives and what requires review

| NTVMR page | Source `lb` elements | Native lines and L1 revisions | L2 revisions | Native annotations | Explicit TEI annotation-mapping losses |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1440 | 27 | 30 | 11 | 46 | 0 |
| 1450 | 26 | 23 | 8 | 33 | 2 |
| 1460 | 28 | 32 | 6 | 45 | 0 |
| 1470 | 27 | 31 | 7 | 39 | 1 |
| 1480 | 27 | 28 | 9 | 51 | 0 |
| 1490 | 27 | 30 | 8 | 57 | 1 |
| 1500 | 28 | 32 | 10 | 34 | 0 |
| 1510 | 26 | 29 | 8 | 50 | 2 |
| 1520 | 27 | 28 | 9 | 38 | 0 |
| **Total** | **243** | **263** | **76** | **393** | **6** |

The bounded importer segments source blocks and handles break constructs according to its own rules. Native line counts and source milestone counts are different quantities. The editor received **zero placed lines and zero review decisions**. No line is established as a physical target by import. All nine exported folio values and witness-level shelfmarks were retained. The native objects are one import per page, with no confirmed shared physical-leaf grouping. The separate source inventory binds provider page/capture IDs.

The CLI reported unsupported column-break markup on each page. It also reported flattening of nested markup, standalone abbreviations with no supplied expansion, and one gap with unknown extent. TEI exports separately reported six ambiguous annotation mappings across 1450, 1470, 1490 and 1510. Plain diplomatic exports report their layer, provenance and uncertainty losses. The original attachment provides exact source recovery in every case.

The baseline TEI export also omitted source `pb break="no"` attributes on 1450 and 1480, and omitted the original CC BY 4.0 URL on all nine pages. The native metadata retained the first availability paragraph with INTF attribution. The attachment retained the complete notice. These specific omissions did not appear in the baseline export loss report.

The backup had **zero image entries**. The CLI stores local capture paths pointing at the archived image directory and backs up images in the store's adjacent image-cache directory. The restored project still resolved these same archived paths. This proves database, source attachment and output recovery with the source tree present. A portable image recovery or eScriptorium round trip was outside this run.

## Local reproducibility evidence

`.artifacts/experiments/ntvmr-intake-rehearsal-v1.jiuwZJ/` contains the run script, CLI command/exit ledger, all outputs, source pins, original/backup/restored disposable stores, native snapshot and machine report. This establishes mechanical portions of NT-04 and NT-06. Scholarly acceptance, editor review and accepted-record comparison remain separate work.

## Focused TEI provenance correction

The bounded importer now retains a single explicit HTTP(S) licence target from `licence@target` or an escaped `rel="license"` link in source availability. It retains the selected folio's `pb@break`. Export includes the source attribution text, licence target and recorded folio break without re-emitting the escaped HTML. A legacy project can recover these two attributes from its matching bounded original TEI attachment. The importer does not assign an image or model license from a TEI notice.

The three targeted checks failed against the baseline and passed after the correction. Final isolated Release builds had zero warnings and errors. **235 application tests and 101 format tests passed**. The application run excluded the learned layout fitting fixture. An earlier isolated application invocation reported two missing external fixture paths. The final invocation supplied the original repository fixtures at the expected relative paths and reran the entire non-fitting selection. No project code was changed for the test harness paths.

The corrected CLI repeated all 57 commands in a new store. It exported the exact CC BY 4.0 target on all nine pages and `break="no"` on 1450 and 1480. The legacy fallback was exercised on a separate copy of the original baseline backup database. The nine legacy exports added exactly the two repaired attribute types, with all other parsed XML and stderr identical to baseline, and no change to the project SQL row. Nine fresh exports passed the bundled schema check. Native text, annotation counts, attachment hashes, duplicate behavior and prior annotation-mapping losses stayed identical. The original source and baseline receipts remain separately preserved.

The candidate build, failing baseline, final test logs and fresh/legacy comparison are in `.artifacts/experiments/ntvmr-provenance-fix-v1.dJxts4/`. A first candidate's archived replay caught the prose prefix before the NTVMR CDATA licence link. The final parser and regression fixture include that input form. Multiple distinct valid licence targets and non-web targets remain in the original source attachment.

## Exact recovery of three uncertainty spans

The six annotation-mapping losses were audited against the archived source TEI and original native revisions. The bounded exporter can now recover three nonempty Unclear spans when the existing contextual lookup fails. Removing every explicitly anchored editorial supply must reproduce the full diplomatic string exactly after the importer's whitespace collapse. The projected scalar range must be contiguous, contain no ambiguous collapsed whitespace, and avoid overlap with authoritative editorial or other rendered annotation ranges.

| NTVMR page and native line ordinal | Diplomatic scalar range | Editorial rendering range | Preserved source reading |
| --- | --- | --- | --- |
| 1450, line 17 | `[51,52)` | `[52,53)` | `ν` |
| 1470, line 30 | `[27,28)` | `[41,42)` | `κ` |
| 1510, line 29 | `[5,6)` | `[9,10)` | `ε` |

Each range re-imported at its exact original diplomatic offset, with both original revision texts preserved. The nine-page comparison changed only those three TEI line elements. No new geometry or review decision was recorded. Three losses remain explicit. Page 1450's first-hand `τω` is an alternate with an empty corrected branch and needs a whitespace-safe zero-width insertion. Page 1510 has a repeated `το` in the corrected layer and needs a distinct branch mapping. Page 1490's terminal gap follows a supplied suffix in the source TEI, while the two native texts and supplied ranges leave more than one possible rendered boundary. All exact source forms remain recoverable from the original attachments.

Two positive cases failed on the baseline. The final Formats suite passed 107 tests, including negative cases for unmatched editorial changes, discontiguous spans, and authoritative or other mapped overlap. The nine archived exports and three re-imports used a separate copied SQLite store and CLI dependency folder. Local evidence is in `.artifacts/experiments/tei-supplied-projection-v1.i6ljOl/`.
