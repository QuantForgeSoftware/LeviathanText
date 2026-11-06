# Bounded ABBYY import preservation review — 2026-11-05

## Scope and result

The FineReader10 adapter now preserves source line baselines, valid enclosing
block rectangles, and standalone space characters. Recognized character content
is read from direct `formatting/charParams` text nodes. Character and word
recognition alternatives are excluded from L1 diplomatic text rather than
concatenated or selected as accepted readings. A document containing alternatives
without recognized character content produces no diplomatic revision for that line.

Malformed, incomplete or inverted line bounds no longer produce a fabricated
rectangle at coordinate zero. The text remains available, the native polygon and
baseline are empty, and an import loss is returned. Invalid declared baselines are
omitted. For compatibility, an absent baseline still uses the valid line box's
bottom edge, now with a `Note` explaining the fallback.

The single-page/text-block boundary is explicit: additional pages and non-text
blocks are omitted with `Loss` issues. Table-cell lines are not flattened into a
text block. Other covered losses include detailed block regions, document style
metadata, character attributes (boxes, confidence, boundaries), and formatting
attributes. Issues are summarized once per category, avoiding a warning per
character. This is bounded loss reporting, not an exhaustive validator of every
possible FineReader element or attribute. Existing application imports retain
original XML through their source-attachment path.

## Verification

Eight focused scenarios failed before the implementation: the existing roundtrip's
new baseline assertion plus seven added scenario cases demonstrated the loss of
spaces, accepted-text contamination by alternatives, unreported structure loss,
fabricated invalid geometry, and missing metadata warnings. The initial corrected
focused suite passed all ten cases. Additional tests cover alternative-only text
and valid, missing and invalid declared baselines.

The complete Formats suite passed **124/124**, with no compile or analyzer warnings:

```sh
dotnet test tests/LeviathanText.Formats.Tests/LeviathanText.Formats.Tests.csproj \
  --artifacts-path .artifacts/midday-interchange --verbosity minimal \
  -m:1 -p:UseSharedCompilation=false -nodeReuse:false
```

No tests in this suite fit a model. No collection or VLM training, data acquisition,
specialist decisions, rights changes, or recognition-accuracy measurements occurred.

An initial default-sandbox run could not open the test-runner socket. The approved
run used the local test-runner socket. A temporary output location under `/tmp`
also exposed the existing TEI schema fixture's fixed relative path assumption
(123 pass, one unrelated missing-schema failure). The final isolated output lives
inside the repository at the expected directory depth. An initial NuGet audit
network warning cleared after a fresh restore in the approved environment.

## Official schema evidence and remaining work

ABBYY's [FineReader10 XML documentation](https://help.abbyy.com/en-us/finereaderengine_mac/12/user_guide/specifications_xmlschema/)
describes line baselines as page-relative vertical coordinates and block bounds
as enclosing rectangle coordinates. Its [schema configuration guidance](https://support.abbyy.com/hc/en-us/articles/360003393939-How-to-Set-up-custom-XML-schema-in-FineReader-Engine)
locates `FineReader10-schema-v1.xsd` in an installed SDK. The official
[Engine 11 distribution guide](https://distrib.abbyy.com/abbyydownloads/FREngine11/FREngine_11.1.19.102_UserGuide.pdf)
lists that XSD with distribution set to “No” (Include files folder table, printed
page 1326). That specific distribution restriction does not establish a right to
vendor the schema in this repository. No ABBYY XSD was copied or bundled.

Official XSD validation remains open. A future implementation can accept an
operator-supplied, lawfully obtained local schema, require a pinned SHA-256, disable
network/DTD resolution, and report validation errors separately from native import
losses. A public redistributable schema grant or an independently defined bounded
profile would need to be identified before bundling an XSD. The latter would not
establish official FineReader conformance.

Further open work includes source-specific geometry/frame rotation validation,
character-level metadata preservation, richer block-region mapping, multi-page
ingestion, native region/identity roundtrips on export, and schema-version coverage.
The exporter still uses its existing line-level character box profile. This change
does not claim lossless ABBYY interchange or complete eScriptorium parity.
