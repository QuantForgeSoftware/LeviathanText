# Known issues and limits (pilot)

Reconciled: 2026-11-05. Dated measurements remain scoped to their original inputs,
protocol and source snapshot; this document does not establish release acceptance.

## Data and claims

- Labels are **not specialist-adjudicated**; measured CER is pilot-scale and cannot support an
  acceptance claim. The P01 locked split is **spent** (including automatic suite reruns that are
  now prevented by the opt-in guard) — new claims need fresh held-out witnesses.
- M02a is one codex; M01 adds a second book but no hand-level holdout. Inscription labels are
  still blocked on image-aligned adjudication, though the EpiDoc text-first bootstrap and manual
  box placement now exist.
- Out-of-profile labels (PAGE markup leakage) exist in imported corpora; use
  `train lines --profile-strict` and `validate text` before training.
- The LM Studio VLM harness has transport, cleanup and generated grounding/blank
  controls. These do not establish natural Greek accuracy improvement. Reviewed Koine
  and Classical material, frozen physical-unit splits, the matched OCR comparator and
  independent confirmation remain open. See the [VLM synthesis](../evaluation/vlm-evidence-and-next-gates-2026-11-04.md).

## Recognition

- CPU-only; the same seed is **not bit-reproducible** across runs (multi-seed measurements in
  `evaluation/end-to-end-page-cer.md` show the spread, and layout CNN training uses the same
  backend).
- Layout matching is one-to-one greedy by IoU; earlier reports (before 2026-10-19) used a
  non-one-to-one matcher and are superseded by the regenerated records.
- L0 revisions persist the **mean** raw confidence (with scale); per-grapheme confidence stays
  in memory, so review-queue ordering is not exactly reproducible later.
- Retried recognition jobs are idempotent per run id (job id) and do not duplicate L0 sets.
- Large JPEG/PNG pages above 25 MP use bounded scanline band decoding per line; TIFF uses a fallback full-frame decode, and there is no general tiled decoder.
- Native-memory growth remains unresolved for sustained model lifecycles. Isolated
  macOS arm64 probes identified unreachable TorchSharp/libtorch module/name allocations;
  balanced managed wrappers and disposal do not eliminate those roots. See
  [allocation attribution](../evaluation/nt20-native-allocation-attribution-2026-11-04.md).
  Optional worker retirement limits process lifetime, not bytes or time, and is not a
  dependency fix. `PatchLayoutModel` now has explicit shared ownership and disposal; threshold
  aliases remain usable until their own disposal and the last owner releases the
  network. Focused native-wrapper and application-owner regressions are recorded
  in the [ownership report](../evaluation/patch-layout-ownership-2026-11-05.md).
  This fixes wrapper ownership, not the separately traced upstream allocation roots.

## Layout

- The default detector is single-column; curved and rotated pages are unsupported. An opt-in
  two-column projection split (`--columns`, `?columns=true`; `--rtl-columns` reverses column
  order) handles a clear vertical gutter and is tested only on synthetic pages — see
  [layout-columns.md](../evaluation/layout-columns.md).
  Row bands that merge across a shallow/irregular gutter still cannot be separated.
  Projection remains the default (combined pilot P 0.403 / R 0.473 under one-to-one matching).
  A patch/row CNN is **opt-in per surface kind**; it is strong on the training book (M02a
  P 0.965/R 0.987) and on a fresh minuscule book (M01 layout P 0.994/R 0.991), but degrades on
  papyri and multi-column material; a P01-trained variant improves aggregate P01 metrics yet
  fails on some dense pages and is not enabled.
- The learned row model underperforms projection (negative result recorded).
- Detection never overwrites existing layout; manual line add/delete is available. Geometry
  and order edits are recorded in a surface-level append-only audit (before/after snapshots),
  not as revisioned entities. `POST /surfaces/{id}/geometry-history/{editId}/restore` restores
  a line polygon/baseline from the recorded before-state; other edit kinds are not replayed.
  Editor undo and backups remain the broader recovery path.
- Structural deletion/split/merge [refuses any scoped revision or review evidence](../evaluation/structural-line-evidence-guard-2026-11-05.md),
  including L0/L2-only lines. Split/merge require finite placed polygons. A reviewed
  text-repartition workflow is still absent; the editor cannot silently move evidence
  onto new geometry. Order writes now support [expected ID-sequence snapshots](../evaluation/order-snapshot-guard-2026-11-05.md),
  which the UI supplies to reject stale committed reorders. Legacy callers omitting
  those optional fields retain only the existing project-version concurrency boundary.

## Imaging

- Local PNG/JPEG capture previews now use authorized capture-ID delivery with
  recorded/observed source identity, raw-frame scaling and current authorization
  checks. Actual API-hosted Chromium viewing/edit/save/reload is verified; see
  [UI-01 evidence and limits](../evaluation/ui01-capture-delivery-2026-11-05.md).
  TIFF/other formats still receive explicit refusals pending bounded codecs.
  Unverified remote views remain direct and read-only; compatible existing
  secondary-frame geometry is supported, but new secondary geometry and mixed-frame
  global ordering need explicit API work. Previews are not archival originals.
  The older451-file package remains historical; it was not rewritten by this fix.

- ImageSharp3.1.11 has six published advisories. The patched3.2.0 candidate
  audits clean but requires vendor-issued license keys for Release builds of its
  original two direct consumers. The owner selected investigation of permissive
  replacements. AI's strict-PNG path now uses existing Skia assets and its graph
  audits clean; Imaging still references3.1.11 and its six advisories remain a
  security release blocker. See the [dependency investigation](../evaluation/imagesharp-security-update-2026-11-05.md),
  [adopted PNG-path review](../evaluation/imagesharp-png-independent-review-2026-11-05.md)
  and [parallel replacement briefs](../tasks/permissive-imaging-replacement-2026-11-05.md).
  The [actual format inventory](../evaluation/imaging-fallback-format-inventory-2026-11-05.md)
  proves PBM(P1), TGA and QOI also need the fallback; replacing TIFF alone would
  remove demonstrated working imports. Broader variants and other RIDs remain unverified.

- TIFF is supported through the ImageSharp fallback; very large TIFF pages still decode the full frame. PDF page
  rasterization is implemented (PDFtoImage/PDFium; bounded DPI/page count/size); optional
  coordinate-bearing PDF text is imported as **L2 reference** with gap-based lines and a bounded
  two-column gutter order, but figures/rotation and a full reading-order model are not handled.
  Optional extraction now [refuses rotated/non-horizontal reference text](../evaluation/pdf-text-orientation-2026-11-05.md)
  before image derivatives or project writes; image-only rasterization remains available.
  [ABBYY FineReader10 import](../evaluation/abbyy-import-preservation-2026-11-05.md)
  preserves declared baselines, enclosing block bounds and character spaces, excludes
  recognition alternatives from L1, and reports covered losses. Official XSD conformance,
  other versions and character-level metadata preservation remain unverified.
- Whole-image loading/preparation paths can still decode before resizing. Bounded
  JPEG/PNG line-band extraction does not turn every import, transform or TIFF path into
  a tiled decoder or establish a universal peak-memory bound.
  General Skia decoding now checks dimensions before allocating the raster, with
  [measured oversized-input evidence](../evaluation/image-dimension-preflight-2026-11-05.md).
  The fallback still materializes its first frame before the caller's dimension check.

## Jobs and operations

- Cancellation is cooperative **between** batch items, not mid-image.
- Job leases/heartbeats, persisted priority and an optional per-owner active quota exist; there is no distributed scheduler or per-tenant rate limiter.
- SQLite single-writer store; the worker runs one job at a time per process.
- An optional [completed-attempt budget](../evaluation/nt20-worker-budget-2026-11-05.md)
  exits the worker after a selected count; an external supervisor must restart it even
  after successful exit. One project job may include many pages, so attempt counts do
  not bound memory, surfaces, lines or elapsed time. Representative supervisor/platform
  deployment and sustained full-page validation remain separate.
  Registered generated and retained-P46 worker comparisons completed8,000 jobs
  and120 page attempts respectively, with exact output parity and clean exits;
  recycling showed no consistent peak-RSS reduction. The
  [idle-only polling candidate](../evaluation/worker-idle-backoff-investigation-2026-11-05.md)
  is isolated research and has not changed production scheduling.
- Backup/restore does not implicitly repair persisted external image paths.
  [Explicit local-root relocation](../evaluation/capture-relocation-2026-11-05.md)
  verifies recorded hashes or readable original bytes and saves a receipt atomically;
  remote/relative paths and automatic external-file copying remain outside the contract.
  Generated restore/rebind/export tests are not real collection cutover acceptance.

## Export and PDF

- Searchable PDF is a convenience export: the capture image plus invisible L1 line text at
  axis-aligned source rectangles. It is not PDF/A, does not embed L2/alternatives/annotations,
  and omits lines without geometry (reported as loss). Page rotation/curved baselines are not
  modelled. See [searchable-pdf.md](../evaluation/searchable-pdf.md).

## Security

- Shared deployment has **local accounts with roles** (reader/annotator/reviewer/admin), hashed
  keys, rotation/disable and a read-only mode. There are no refresh tokens or SSO. Opt-in
  per-project membership, admin panels, one-time invitation tokens (unused tokens can be
  listed/revoked), advisory store-backed
  presence, server-issued TTL sessions (heartbeat/revoke, node/surface binding) and a bounded
  three-way merge for stale text revisions exist. Sessions are coordination records, not
  tokens; there are no edit locks. Annotation and review writes can name the revision they
  observed and fail on a stale one; geometry/order/retraction writes use the project version and
  fail on stale state rather than silently overwriting (no field-level merge).
  Basic auth audit entries exist (mutations/denials). The workspace has an API-key panel
  (localStorage; X-Api-Key + SSE query token); local-only mode remains the default. The surface
  view creates/heartbeats/revokes its session and shows the serving node; surface and workspace
  session panels list/revoke sessions visible to the current principal (admins see all). There
  is no cross-project presence feed or general workspace push/broker. Job SSE streams
  do exist; with membership enabled they reauthorize before each replay/live event and
  periodically while idle. Newly bound jobs reauthorize before dispatch and between
  project surfaces. An already-running surface can finish, and previously committed
  work remains. Historical unbound/global job ownership and complete revocation of
  every in-flight operation remain open. See [revocation scope](../evaluation/job-revocation-2026-11-05.md).
- Provider credentials are read from environment variables; hosted provider calls are unproven.
- New authenticated direct text, review, annotation, geometry and AI bulk-accept
  mutations [bind attribution to stable account IDs](../evaluation/authenticated-mutation-attribution-2026-11-05.md).
  Historical caller-labelled records are not retroactively verified. Local mode still
  trusts supplied labels, and account distinctness is not proof of scholarly independence.
- Native AI issuance/acceptance additionally checks database role/membership/local-mode
  authorization in the same SQLite transaction as the project save. This closes the
  reproduced revocation-before-write race; it does not make global configuration or
  network responses transactional. An authorized commit followed by later revocation
  can still be hidden by a final response denial. See [native AI boundaries](../evaluation/native-ai-http-2026-11-05.md).
- Model registry catalog/pull, admin UI, checksum verification, allow-listed hosts,
  redirect checks and bounded extraction exist. These controls do not supply approved
  public weights, establish their provenance or grant redistribution rights.
- Native proposals no longer publish unused revision-keyed payloads into the
  singleton job replay log. The [retention correction](../evaluation/native-ai-growth-resource-followup-2026-11-05.md)
  preserves native records and real job events. Actual job streams/logs still have
  no eviction policy; retained proposal histories also require whole-project
  serialization and growing ledger scans. The successful generated1,000-cycle
  [process run](../evaluation/native-ai-process-soak-2026-11-05.md) sampled up to
  708MiB RSS before that correction; no total memory fix or acceptance is claimed.
- PDF rendering uses native PDFium against files readable by the server process; size/page limits
  are enforced, but PDFium itself is not sandboxed. Treat PDFs as untrusted input. PDF text is
  imported as **L2 reference**, never as visible-text/L1 labels; large horizontal gaps
  form candidate lines and a bounded two-column gutter rule supplies order. This does
  not handle arbitrary columns, rotation, figures or semantic reading order.

## Interchange

- PAGE, ALTO and hOCR carry per-region/line direction and script metadata (PAGE `readingDirection`/`primaryScript`, ALTO `BASEDIRECTION`/`LANG`, hOCR `dir`/`ocr_vertical`); native JSON preserves it and TEI uses a bounded `@rend` mapping. Recognition remains LTR.
- ALTO/hOCR are bounded subsets (line boxes + line text; no word segmentation synthesis).
  [hOCR export](../evaluation/hocr-baseline-bounds-2026-11-05.md) uses boundary-first
  bounds with a baseline fallback; full baseline/bbox import synthesis, shared-field
  preservation and UI conversion acceptance remain open.
- TEI/EpiDoc is a bounded, layered profile: `<lb>` line splitting, L1/L2 supplied and
  expansion links, orig/reg, add/del/handShift, facsimile and provenance are mapped; unsupported
  markup is aggregated and the original XML is retained as a checksummed attachment when it is
  at most 2 MiB. Optional `validate tei --schema <xsd> --expect-schema-sha256 <hash>`
  checks a pinned local schema without network/DTD resolution, and an independently
  defined bounded profile XSD is included. Neither establishes full official TEI/EpiDoc
  conformance; RelaxNG/full apparatus validation remains absent. Spans that cannot be
  mapped between layers are reported (`annotation-mapping`), not guessed.
- Plain-text and CSV exports are lossy by design; the native project JSON is authoritative.
- New TEI imports use physical milestones across nested words and verse blocks,
  with a logical-block fallback only when no retained physical markers exist.
  [P46 structural evidence](../evaluation/p46-physical-tei-lines-2026-11-05.md)
  yields27/26/28 rows without migrating old85-row projects or accepting labels.
  [Explicit language metadata](../evaluation/tei-language-metadata-2026-11-05.md)
  now respects inherited/inline scopes and empty resets. Line language lists are
  unions across retained L1/L2 text scopes; per-span language positions are not
  reconstructed, and exporting multiple codes reports the bounded loss. Header
  declarations stay separate. Old omitted arrays are not inferred from attachments.
- Editor recovery records preserve draft text and its ancestor atomically. Non-overlapping
  edits rebase; conflicts and legacy drafts without ancestors require explicit comparison
  before replacement. Browser storage failures can still prevent the newest keystrokes
  from persisting. See [draft recovery evidence](../evaluation/editor-draft-reconciliation-2026-11-05.md).

## Contract status

See [`docs/contracts/self-review-2026-10-19.md`](../contracts/self-review-2026-10-19.md) for the
conformance matrix and the actions that should precede a contract bump.
