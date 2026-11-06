# Independent uncommitted backend/data-preservation review

Reviewed the HEAD diff and new source in persistence/restore, native placement references, bundle/relocation, image/PDF factories, TEI/ABBYY/hOCR import, scalar merge, geometry, review and recognition identity code. This is the backend slice of the parallel review; API authentication, worker/AI and frontend findings belong to their separate owners. No source was committed or staged.

## Fixed: checksum-valid null containers could overwrite healthy restore targets

`LocalStore.VerifyBackupDatabase` accepted deserialization success and valid project identity, but System.Text.Json required-member presence does not reject explicit null references. Thus a valid-checksum backup with `project.objects=null`, null object/surface entries, `object.surfaces=null` or `surface.lines=null` passed verification and `restore --force` returned success. All five generated original CLI cases actually replaced both the healthy SQLite database and a same-named image-cache sentinel. This is a data-loss finding, not just an exception-code difference.

The verifier now rejects null declared native collection/map containers and null collection entries before restore publication. It traverses object/fragment/surface lists, region/line geometry/languages/tags, ledgers and revision confidence-span collections. It preserves absent optional collections through their existing deserialization defaults, duplicate legacy identities, old-schema compatibility, explicit unplaced references and intentional nullable scalar metadata values. The guard reads only; it neither rewrites nor repairs a snapshot. Changes are confined to `VerifyBackupDatabase`; an exact prefix/suffix comparison confirms the concurrent transaction-authority fix elsewhere in LocalStore is unchanged.

Before: **30 malformed-container failures/one legacy-positive pass**, plus **five actual destructive forced restores**. After: **67 Infrastructure passes** (34 new including nested-list controls, 19 prior backup controls and 14 unplaced-reference controls) and **27 CLI passes** (five new plus 22 existing backup/restore controls). The strengthened positive additionally preserves a legacy null scalar metadata value. After refusal, source bytes, healthy target bytes, colliding cache bytes and unrelated cache bytes remain exact.

Scope is deliberately restore-only. Global NativeJson loading still permits explicit null graphs; broader intake validation/error translation and forensic compatibility remain a separate open policy. Required scalar/object semantic integrity, every historical migration and all logical corruption are not claimed validated.

## Fixed: ambiguous geometry identities could write to the wrong native surface

The new capture-bound line-order method first validated its selected capture/region, then delegated to a legacy global region lookup that selected the first matching region. Two unique surfaces sharing a legacy region ID caused an edit/audit to the wrong surface even when both regions were empty. Other first-match line/region selectors accepted ambiguous identities, and updating a duplicate surface ID could replace every matching surface with the first surface's content. Repeated local order IDs also reached an unhandled indexing failure.

Geometry mutation selectors now require a unique target surface/line/region and surface replacement independently checks uniqueness. Stored order identities must be unique before slot replacement. Missing entities keep their previous InputMissing behavior; ambiguous geometry targets receive typed FrameUnregistered refusal. No IDs are normalized or deduplicated and unrelated native evidence is preserved.

## Fixed: review corrections could extend invalid or ambiguous history

`ReviewService.Record` selected a first matching line and checked only whether its current revision ID existed locally. It could record/correct ambiguous line/surface IDs, duplicate current revision IDs, or an L1 pointer to another line's revision or to an L2 revision. A correction then created invalid cross-scope/layer ancestry; duplicate surface replacement could also discard another surface's evidence.

Recording now requires a unique line/surface plus exactly one current revision globally, anchored to that line and L1. Invalid targets receive typed StageContractViolation before any save. Historical reviews are not removed or reinterpreted.

The combined geometry/review original **13 cases produced 11 failures and two healthy controls**. Ten invalid cases actually saved/changed native state; one repeated-order case instead failed with raw IndexOutOfRangeException and no write. After correction all 13 pass, including exact no-write preservation and healthy selected-surface/correction behavior. The broader inspected pure geometry/review selection passes 72 cases before the subsequent recognition controls are included.

## Fixed: recognition target ambiguity was not preflighted globally

`RecognitionPipeline.Run` selected the first surface and later replaced all surfaces sharing that ID. Its new raw-revision allocator did not prevent that earlier selection/replacement problem. Selected line IDs were unique only within one surface, even though revision scopes/run identity are global.

The pipeline now rejects duplicate target surfaces and selected line identities occurring elsewhere in the project before capture access or model/label factories. Five generated public preflight controls use only empty captures and throwing sentinel factories: no model is created, loaded, trained or invoked. CrnnModel has no static initializer; its allocating constructor is never called. Before surface controls were **two failures/one missing-surface control**, and later line controls were **one global-duplicate failure/one already-refused local-duplicate control**. These failures are deliberately labeled **exception-order/code preflight evidence**, not an executed model-write/data-loss reproduction. All five now pass with factory/read/save counters zero.

Final Application selection passes **77/77**: 18 new identity/review/preflight cases plus 59 existing pure geometry/review controls. It excludes all actual model tests.

## Verification and remaining limits

Every invocation explicitly sets:

```
LEVIATHANTEXT_RUN_FITTING=0 LEVIATHANTEXT_RUN_CORPUS=0
LEVIATHANTEXT_RUN_LOCKED=0 LEVIATHAN_OLLAMA_LIVE=0
```

Only inspected class-name allowlists were used; no unfiltered Application/API/ML/Pilot suite ran. The focused selections are the new identity/preflight classes plus GeometryServiceTests, CaptureGeometryTests, CaptureExistingGeometryTests, ReviewServiceTests; new backup-container tests plus BackupNativeValidationTests/UnplacedNativePersistenceTests; and new restore-null-container tests plus RestoreNativeValidationCommandTests/BackupCommandTests. All final runs report zero failures/skips and no compiler warnings. No model, natural corpus, fitting, provider or LM Studio call was made. The initial new Review test fixture omitted two required empty collections and failed compilation before tests; its receipt remains separate from the authoritative 13-case behavioral baseline.

Receipts and before-source snapshots are under `.artifacts/experiments/uncommitted-review-{geometry,recognition-preflight,restore-containers}-v1/`; exact source hashes and eight actual before/after TRX counts are collected in `.artifacts/experiments/uncommitted-review-data-final-v1/verification.json`. Scoped `git diff --check` passes. All owned verification processes closed. The parent performs the later central engineering gate.

The other inspected merge/import/image/PDF/bundle diffs yielded no additional high-confidence new blocker in this slice. This is not a formal complete call-graph/security/resource proof. Existing declared limitations remain: ImageSharp advisories/parser compatibility, full-raster/native workspace/resource bounds, PDF multi-file publication not being all-or-nothing, global null-graph intake/forensic policy, and general corrupted-native graph qualification. No new dependency or blanket safety/performance claim is introduced.
