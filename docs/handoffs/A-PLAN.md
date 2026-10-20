# Handoff — A-PLAN (Instance A: contracts and planning integration)

> **Plan-split note (2026-10-20):** the plan narrative, section numbering and execution records
> now live in [project-plan.md](../../project-plan.md); [todo.md](../../todo.md) is the actionable checklist. Historical
> hashes and `todo.md` citations below refer to the pre-split file.

## Status and owner

- Package: **A-PLAN v1**, brief at [`docs/tasks/instance-a-contracts-and-integration.md`](../tasks/instance-a-contracts-and-integration.md).
- Owner: Instance A (this session). Status: **active** (claimed 2026-10-18).
- Assignment scope: **documentation only**. No code, no scripts, no machine-readable schemas, no Git/worktree initialization, no dependency installation, no dataset/model downloads, no training, no cloud resources.
- Peer instance B: not started at assignment time. No `docs/handoffs/B-PLAN.md` and no `docs/data/*` files existed when this handoff was created. All cross-package expectations below are therefore recorded as pending B, not assumed.

## Baseline inputs

Baseline: **PLAN-2026-10-18-two-instance-v1** (todo Section 6). There is no Git repository; the SHA-256 hashes below are the recorded starting state, captured 2026-10-18 before any A-PLAN edits.

| File | SHA-256 |
| --- | --- |
| `todo.md` | `6466461445bf973d493566c5ff74f50d88fd6c91f63250d7e4a1928b9cc78ac9` |
| `Agent.md` | `536b3919dd168840fa014535223f86d801eef82c9a9fe4a62405e42f8f7b02cf` |
| `docs/tasks/instance-a-contracts-and-integration.md` | `72f52abf6f27f5be482035795b0d4c437670a734e2b5f06e333da27314a83e97` |
| `docs/tasks/instance-b-corpus-and-evaluation.md` | `1a88f2e17655c4bcb5ad1ee22a52db0bb4bfa733848581643c8a7b7307dcdc2c` |

Contract version at assignment: **none approved**. First proposal uses `C0-0.1-draft`.

B may compare this record against its own hash capture. If only `todo.md` differs because of A's progress/board edits, B should record the difference and proceed per its brief; substantive scope differences go through both handoffs.

## Contract revisions

| Revision | Documents | Status | Handoff revision announcing it |
| --- | --- | --- | --- |
| `C0-0.1-draft` | `docs/contracts/corpus-manifest.md`, `docs/contracts/greek-transcription.md` | published, open for B review | A-PLAN handoff rev 2 |
| `C0-0.2-draft` | all five `docs/contracts/*.md` | published, open for B review | A-PLAN handoff rev 3+ |

No revision is accepted yet. `C0-0.1-draft` and `C0-0.2-draft` are proposals for B's material-fit review; adoption requires B's recorded review and the dispositions in this handoff.

## Ready inputs for B

| B can consume | Where | Notes |
| --- | --- | --- |
| Manifest field dictionary (source cards → fields) | `docs/contracts/corpus-manifest.md` | Includes per-field required/optional status, unknown-value representation, rights-evidence fields, split grouping, annotation-completeness fields |
| Transcription profile proposal | `docs/contracts/greek-transcription.md` | Diplomatic/editorial layering, Unicode rules, uncertainty vocabulary |
| Worked mapping examples | `docs/contracts/examples.md` | Labeled invented cases; B should propose source-backed replacements |
| Scoring semantics | `docs/contracts/interchange-and-api.md` + `examples.md` | For B's acceptance-plan alignment |

The early-interface release intended for B is `C0-0.1-draft` (manifest + transcription). B may begin source cards before reading anything from A; that is its brief's step 1.

## Changed files

Maintained per release; see the current revision entry under "Releases" below. All changed paths are inside A's exclusive ownership and are Markdown only.

## Acceptance evidence

Filled per the brief's acceptance criteria; each maps to supporting sections of the contract documents:

| Criterion | Evidence |
| --- | --- |
| Every owned contract document exists with revision, status, source requirements | All five `docs/contracts/*.md` carry `C0-0.x-draft` headers with status lines and named todo source requirements (see Changed files / hashes below) |
| Identity, coordinates, offsets, layer separation, manifest fields, model I/O, errors, API events, scoring explicitly defined; unknowns represented | `domain-and-pipeline.md` §1–§6; `corpus-manifest.md` §2–§6; `greek-transcription.md` §1–§6; `interchange-and-api.md` §2; unknowns via explicit `unknown`/`pending`/`not-retrieved` conventions throughout |
| Examples cover all three materials + edge cases with expected outcomes; invented, excluded from counts | `examples.md` EX-1–EX-9 + coverage checklist §3; each case labeled invented and excluded from corpus counts |
| B can describe priority sources via the manifest without overstating ground truth | `corpus-manifest.md` §4 `label_granularity`, `pairing_status`, `annotation_completeness` fields, plus §7 open question 1 addressed to B |
| Supported interchange fields and unsupported/lossy cases listed; no unproven claims | `interchange-and-api.md` §1 (format inventory, EpiDoc table, loss reporting §1.3, IIIF §1.4); no GPU-portability or C#-feasibility claims made anywhere in the contracts |
| B's review of the exact revision + dispositions recorded | **Open (BLK-A-1).** B has not started; no B handoff or documents exist as of closure. Status therefore `review`, not `done` |
| Review of B in this handoff; todo reflects both statuses, gates unchecked | See "Review of B" below; todo board updated accordingly; all Phase 0/1/2 gates left unchecked |
| Markdown-only, owned paths; no runtime tests/benchmarks claimed | Changed files list below; documents explicitly state no code, tests, benchmarks, downloads or installs were performed |

## Review of B

Pending — **B has not started.** As of A-PLAN closure (2026-10-18): no `docs/handoffs/B-PLAN.md`, no `docs/data/manuscripts.md`, `docs/data/papyri.md`, `docs/data/inscriptions.md`, and no `docs/evaluation/acceptance-plan.md` exist in the workspace. A's own review of B therefore cannot be performed yet.

When B publishes its first handoff revision, A (or a successor instance holding A-PLAN) must review at least: (a) source-card field coverage against `corpus-manifest.md` §2–§4, (b) proposed contract changes with stable IDs and dispositions, (c) evaluation-plan alignment with `examples.md` §1 scoring semantics, (d) that no archive counts are presented as verified pairs. Findings go here as `A-REV-B-<n>` entries; A edits none of B's files.

## Decisions and blockers

Blockers recorded so far:

1. **BLK-A-1 (peer review unavailable).** B has not started; A-PLAN cannot reach `done`. Final status will be `review` until B reviews a named contract revision and A records dispositions.
2. **BLK-A-2 (Greek specialist).** Scholarly conventions in the transcription contract are proposals; no expert has adjudicated them. Nothing in `docs/contracts/greek-transcription.md` is authoritative for real labeling yet.
3. **BLK-A-3 (implementation authorization).** All contracts describe future C0/APP/FMT/IMG/UI behavior. Todo's implementation and acquisition gates remain unchecked; completing this package does not authorize code or downloads.

## Next actions

1. Mark A-PLAN active on the shared board in `todo.md`.
2. Publish `C0-0.1-draft` (manifest + transcription contracts) for B's early use.
3. Complete `C0-0.2-draft`: domain/pipeline, interchange/API, examples.
4. Review B when its handoff appears; record dispositions.
5. Close with final hashes and status `review` (blocked on B).

**Closure state (2026-10-18):** steps 1–3 and 5 complete. Step 4 remains open pending B (BLK-A-1). Final status: **`review`** — awaiting B's material-fit review of `C0-0.2-draft` and A's resulting dispositions. To resume: start Instance B per its brief, or re-brief an instance to hold A-PLAN open for the review exchange.

---

## Releases

### Handoff revision 1 — 2026-10-18

- Action: baseline recorded; ownership claimed; status `active`.
- Documents: `docs/handoffs/A-PLAN.md` (this file, initial publication).
- Hashes: see "Baseline inputs" for the four input hashes. This file's own SHA-256 at publication: `pending` (computed by readers; hashes of *other* documents are listed per release below).
- No other files changed yet.

### Handoff revision 2 — 2026-10-18

- Action: published **`C0-0.1-draft`** — the early interface for B (manifest field dictionary + Greek transcription/Unicode contract). Marked A-PLAN `active` on the shared board.
- Changed files and SHA-256:

| File | SHA-256 | Change |
| --- | --- | --- |
| `docs/contracts/corpus-manifest.md` | `e504c6ab63551ea2bc998ce4f579d94c9659f5fcc7a6ca4edde9231cd1b09874` | new (C0-0.1-draft) |
| `docs/contracts/greek-transcription.md` | `c5a01dbf07ee1f9ba9e33c6bc3032a3201964a1f5f5a13f9012c2d2a85555776` | new (C0-0.1-draft) |
| `todo.md` | `61ad97a2d3f3bc15e407b651bba81b59ffd4ad70d6dfbdf641e429e75283ce07` | A-PLAN status → active |

- Note for B: `C0-0.1-draft` will be superseded by `C0-0.2-draft` when the remaining three contract documents are complete. B may map source cards to 0.1 now; please review the final revision named in a later handoff release for acceptance.

### Handoff revision 3 (final for this assignment) — 2026-10-18

- Action: published **`C0-0.2-draft`** completing the five-document contract proposal; recorded acceptance evidence; B confirmed absent (no handoff/documents); A-PLAN closed at status **`review`**; shared board and execution record updated in `todo.md`.
- Changed files and SHA-256:

| File | SHA-256 | Change |
| --- | --- | --- |
| `docs/contracts/corpus-manifest.md` | `e504c6ab63551ea2bc998ce4f579d94c9659f5fcc7a6ca4edde9231cd1b09874` | unchanged since rev 2 (C0-0.1-draft) |
| `docs/contracts/greek-transcription.md` | `c5a01dbf07ee1f9ba9e33c6bc3032a3201964a1f5f5a13f9012c2d2a85555776` | unchanged since rev 2 (C0-0.1-draft) |
| `docs/contracts/domain-and-pipeline.md` | `8b14ef1e51761edbaed875ccb49a0d1a1315fbe2db1ae66d8b4c11af61313d9e` | new (C0-0.2-draft) |
| `docs/contracts/interchange-and-api.md` | `2ba8fb5185d988f1f3f6a2626b6eb4d0bd17752642ee0853efebe4743a492f18` | new (C0-0.2-draft) |
| `docs/contracts/examples.md` | `a38c956f9f5bbf6ec0227f93fb921a23f6ebfea9d6f4b8e8089a977745604a71` | new (C0-0.2-draft) |
| `todo.md` | `f1313ac4224fa5182d5a4f6e889cb85650d7dda7ede0ec45c66398fcf4d994ae` | board status → review; A-PLAN execution record added |
| `docs/handoffs/A-PLAN.md` | `794efc05bfe8ae8823a2d5c5f4b9ab20bc71e5cd643c354a7ec3ae9aaedbf578` | hash immediately before this final entry (this entry is not self-hashing) |

- Contract version to review: **`C0-0.2-draft`** (which incorporates `C0-0.1-draft` unchanged). B's review requests: stable IDs per issue, affected section, source-backed example, requested resolution — delivered in B's own handoff, per B's brief.
- Document checks performed: internal cross-consistency of the five contracts (layer/offset/frame/scoring references), coverage checklist in `examples.md` §3 against the brief's required case list, hash recomputation at each release, and verification that no owned-path or no-code boundary was crossed. No runtime tests or benchmarks were run — none are possible under this assignment, and none are claimed.

### Handoff revision 4 — 2026-10-18 (C0 implementation begins)

- Action: **the user issued a separate instruction authorizing implementation** (the condition todo §1 required). Instance A began the C0 package as integrator. Git repository established; contract types and Core text machinery implemented against `C0-0.2-draft`; tests green.
- Git baseline: commit `728ec9c` (planning documents + contract drafts), then commit `5f6ac24` (C0 increment 1). Branch `main`.
- Toolchain decision: **.NET 10 (SDK 10.0.301, current LTS), target `net10.0`** pinned in `Directory.Build.props`; central files (solution `.slnx`, `.gitignore`, `Directory.Build.props`) are integrator-owned.
- Changed files (vs handoff rev 3): `LeviathanText.slnx`, `Directory.Build.props`, `.gitignore`, `src/LeviathanText.Contracts/*.cs` + csproj, `src/LeviathanText.Core/{Unicode,Revisions}/*.cs` + csproj, `tests/LeviathanText.{Contracts,Core}.Tests/**`, `todo.md` (implementation status + ownership-collision note). Content hashes at rev 4:
  - `todo.md` `5bdc08f9203a17dc0fc8d7d89ed8a175949147356152dc496002a418b8c587c5`
  - `tests/LeviathanText.Core.Tests/LeviathanText.Core.Tests.csproj` `29b6c2c85f6f982ece44cd2b2e96b7eb4193efb07b01d9554f7692b98367c04e`
  - `src/LeviathanText.Contracts/LeviathanText.Contracts.csproj` `499bda7f4e7a0fdde492e4b4d51710d66419c705a3fba8291e71153f396beebd`
  - `src/LeviathanText.Core/LeviathanText.Core.csproj` `86ea7a5908eadccd1702ebaf85eb74f513209993ad6331a64166eb8fb8dbfa34`
  - `Directory.Build.props` `149d11d465547a4586c03e68bc7baa41a6f0609b03a78f19a9fd6059c150bb59`
  - `LeviathanText.slnx` `ba222441b171e6b9a7b5ca6839215670425121481791275b28be562b44d1814c`
  - `.gitignore` `a3ed35d74cacd986ac967b4051626f4d05b2ac80707aab615419a53082cc8257`
- Tests/verification *now performed and real*: `dotnet build` succeeds; `dotnet test` → 28/28 Contracts.Tests, 44/44 Core.Tests pass. Bug found and fixed during verification: composed-cluster scalar mapping originally too coarse (all cluster scalars mapped to the first target scalar); replaced with prefix-based mapping so only actually composed scalars merge. Also fixed end-anchored range indexing (`SourceToTarget` carries a trailing boundary entry).
- **Ownership collision recorded**: a concurrent worker rewrote `tests/LeviathanText.Core.Tests/LeviathanText.Core.Tests.csproj` and `src/LeviathanText.Contracts/LeviathanText.Contracts.csproj` (C0-owned paths) and added AI/EVAL/ML/Recognition scaffolds. Disjoint packages accepted; centralization adopted; workers must route central-file changes through the integrator (todo §6). If the collision repeats, escalate to the user.
- Open questions from the contract documents' reviewer sections remain open for B/the owner.

### Handoff revision 5 — 2026-10-18 (C0 increment 2: serialization; contract set → C0-0.3-draft)

- Action: implemented native JSON serialization of contract records; bumped the contract set to **`C0-0.3-draft`** (real changes: `StageRun` I/O artifact shape; new interchange §2.0). All five contract documents re-versioned with revision history; `ContractVersion.Current` and fixtures updated in lockstep.
- Commit: `da56646`.
- Document hashes after revision: `corpus-manifest.md` `e3142749ea3748b929f87346e8f89c5d2d95df15d572adf3403012c3bccfeb74`; `greek-transcription.md` `868c98082dd95ef538f9c3163bc2dabb13d95d3e859e8a65643abb235bd8f654`; `domain-and-pipeline.md` `14b03ec33789afee90f13cc42b7e267cdad26f58cba8b0860d69f7fa03b9fdba`; `interchange-and-api.md` `81d3fa38b5c71bfbd085d0d7814e3483858516bb0308680ca2bcbd529618607f`; `examples.md` `ec08c7c4af26ae536a4783e03cf26dd6b1d1f898e9eb66c90470bf4343c1ddfd`; `todo.md` `e0c96962805e41d0a5873c45e6d70c4dcb0e919c181bbe27046d9de1bf77a5b0`.
- Verification: 41/41 Contracts.Tests + 46/46 Core.Tests pass, including round-trips over all Greek fixture lines and strict-rejection tests (unknown enum strings, integer enum values, unknown ScalarRange members, invalid Confidence strings).
- **Read-only review of concurrent worker's Evaluation package** (not A-PLAN acceptance; observations for the record): `src/LeviathanText.Evaluation/` (CanonicalText, EditScript, RecognitionScorer, UncertaintyScorer, PageScorer) conforms to examples.md §1 semantics — NFC-only canonicalization, scalar + grapheme CER, separate base/mark error attribution, explicit empty-reference convention. One divergence to resolve (finding `A-REV-EVAL-1`, severity low): `CanonicalText.ToScalars` throws on unpaired surrogates while Core's `ScalarText` treats a dangling surrogate half as its own scalar position; malformed-text handling must be uniform before scoring real data. Recommend Evaluation route scalar conversion through `LeviathanText.Core.Unicode.ScalarText` (their package; not edited by A).

### Handoff revision 6 — 2026-10-18 (C0 increment 3: frames, stage validity, jobs)

- Action: implemented coordinate frames/registration, stage checkpoint validity and idempotent job semantics as compiled contracts (`C0-0.3-draft`). Commit `44d4a9a`; todo record commit `e770081` (`todo.md` SHA-256 `6a72a51e9f1db4a4160b8410c55ebda9c76e72d26b74dcd128922bd9eb341907`).
- New contract surface: `CoordinateFrame` (capture/derivative pinned by checksum — a re-captured image is a *different frame*), `Registration` (bounded transform description + responsible agent), `JobRecord`/`LeaseInfo`/`JobStatus`, `StageArtifact` (from increment 2).
- New Core behavior, all test-proven: geometry transfer requires explicit symmetric registration (`FrameUnregistered` + `RegisterCaptures` remedy; EX-7 contract case now executable); stage checkpoints invalidate on input-checksum, model, config or contract-version change with closed-vocabulary reasons; jobs enforce idempotent creation via client key, idempotent identical publication, hard failure on divergent publication of a finalized job, bounded recorded retries and idempotent cancellation.
- Verification: 41/41 Contracts.Tests + 72/72 Core.Tests pass (`dotnet test` on both test projects). Solution-wide build is currently blocked only by the concurrent worker's in-flight `src/LeviathanText.Recognition/` edits (their package; A's projects build and test clean in isolation).

### Handoff revision 7 — 2026-10-18 (night session; single-instance mode)

- Action: user made A the **only** instance and requested maximum progress on todo before 06:00 next day. A absorbed all packages.
- Merged B-CODE after independent verification: Release build clean, 136/136 tests (commit `72690ea`). B's two Core repairs reviewed and accepted. B-CCR-6/B-CCR-7 accepted → contract set **`C0-0.4-draft`** (commit `15fa2c7`).
- New packages implemented, each committed with green tests:
  - **FMT** `95b7bd3`: native persistence + diplomatic export + loss reports + bounded PAGE (2013/2019) + bounded TEI/EpiDoc round-trips.
  - **APP** `b7bf85e`: SQLite LocalStore (schema-versioned), Application services, local CLI (smoke-tested end to end).
  - **IMG** `1f3a4f3`: SkiaSharp decode/crop/deskew, recorded transforms, explicit unmappability, mark-preserving crops.
  - **PAP-01** `3bc2131`: P01 acquired, 59/59 md5-verified, audited 27 pairs / **321 real diplomatic lines** / 14,394 chars; DATA-01 ledger committed (`data/manifests/papyri/p01.json`); 5 unannotated images recorded.
  - **Real-data training** `a3e48b7`: CTC on real lines — witness-level split (train 176/dev 50/test 95 locked), train loss → 0, **dev CER 0.833 vs 1.0 empty baseline**, exact reload parity. Pilot-scale: generalization is data-limited; locked test never touched by tuning; no release claim.
- Suite state: **173/173 tests passing**, Release build 0 errors/0 warnings.
- I0 substitute: with single-instance mode, the former "B reviews C0" gate becomes a recorded self-review at I1 (contract docs re-read against the implemented packages); noted as pending below.
- Remaining before Phase 0 can be called *complete* (all pending, honestly): expert adjudication (DATA-03), corpus scale-up beyond 321 lines, all three tracks (manuscripts/inscriptions not yet acquired), locked-test evaluation, acceptance sheet with measured thresholds.

### Handoff revision 8 — 2026-10-19 morning (two-instance resumption; A review of B)

- Action: two-instance mode restored. A = integrator, owns C0/APP/FMT/IMG/UI (`74387e4`); B claimed AI/EVAL/ML (`94e6a10`) and delivered 17 green commits (beam search + alignment confidence, calibration ECE/MCE + risk–coverage, correction-effort scorer, error analysis, per-track aggregation, provider registry + OpenAI-compatible/Ollama adapters, proposal service with server-side evidence resolution, bulk-acceptance dry-run mandate, training-run manifests, checkpoint label-hash rejection, ML A/B measurements on input height/width).
- **A's review of B (dispositions recorded; no blocking conflicts):**
  1. **Cross-package extraction finding accepted and fixed (A's file).** B measured that P01 crops are near-square (50/50 dev lines with T < reference scalars) because IMG's mandatory padding doubled crop height. Fix: `LineCropper` default padding 0.5 → **0.25 fraction with a 12px cap**, keeping crops line-shaped (commit `0c6c768`). The one-shot locked-test numbers in the pilot report were measured under the OLD extraction; the locked split is spent and was NOT re-run — re-measurement belongs to dev-split work (B's suite).
  2. **B's ready-for-A classes wired into the API (A's files, B untouched).** `AiProviderRegistry` (config-driven, env-var credential isolation), `AiProposalService` (server-side span/evidence resolution, client evidence discarded), `BulkProposalAcceptanceService` (preview → conflict-reporting accept), capability discovery at `GET /api/v1/providers` (credential names stripped) (commit `51eb654`).
  3. B's boundary discipline verified: no B edits to todo/contracts/A-PLAN/solution/central/Application-side files during the session — the two "M" files at resumption were A's own uncommitted transcription work, committed separately (`e9684c8`).
  4. **EVAL calibration finding noted for the acceptance sheet:** raw model confidence is badly miscalibrated (ECE 0.237) and barely orders (AURC > CER) — supports the contract rule that model confidence is never scholarly certainty; calibration becomes a model-card requirement before any confidence display.
  5. B's "live provider calls not tested" is acknowledged: no credentials/endpoints configured; the registry is wired but unproven against real servers — a Phase 2 gate item, honestly open.
- New deliverable: [docs/handbook/transcription-guide.md](../handbook/transcription-guide.md) (annotator handbook, pilot draft with the specialist's decision points).
- **Locked-set disclosure (reconciled 2026-10-19 10:06):** the pilot-p01 locked split has been scored **multiple times** (currently **four** runs: 0.885 pre-fix; 0.828 and 0.825 post-extraction; 0.834 in B's latest report regeneration). Per acceptance-plan §1 the set is **spent for threshold purposes**; acceptance requires fresh held-out witnesses and disclosure — see [docs/evaluation/locked-set-reuse.md](../evaluation/locked-set-reuse.md). The split hash is unchanged (`f36aaa2e…`), so the leakage audit can verify every run evaluated the same objects. **B's text-only edit to the A-owned generator template (making the disclosure non-revertible) is reviewed and accepted** (`e851271`); no metric or test behavior changed.
- **Live-provider follow-up resolved in part:** B's local **Ollama smoke now passes** over loopback (0.37 s, evidence in [docs/evaluation/live-provider-smoke.md](../evaluation/live-provider-smoke.md)); external hosted APIs remain unproven (no credentials configured) — a Phase 2 gate item, honestly open. B's stale note about "remaining xUnit analyzer warnings" is superseded: the tree now builds with **0 warnings**.
- Negative finding recorded: naive automatic layout inadequate for the corpus (recall ~0.1); harness reusable; trainable layout remains the named Phase 0 blocker ([docs/evaluation/layout-comparison.md](../evaluation/layout-comparison.md)).

### Handoff revision 9 — 2026-10-19 10:10 (session close; two-instance mode)
- **Final state:** 13 suites, **340 tests**, 0 failures; Release build **0 warnings / 0 errors**. Two-instance collaboration closed with all boundary crossings reviewed (B's two Core repairs from the first session; B's text-only disclosure-template edit) and A's integration items delivered (API write surface hardened by live smoke: contract JSON on minimal APIs, annotation DTO with bind-to-current anchor, closed-code→HTTP middleware; transcription endpoints; editor/dual-pane/jobs UI; streaming pipeline; model-package CLI loading).
- **Open gates (unchanged, honestly unchecked):** expert adjudication (DATA-03), corpus scale-up, trainable layout (negative finding recorded), inscription labels, TIFF handling, Kraken comparator, frozen acceptance sheet, external hosted-provider calls.
- **Records:** B's progress and evidence in [B-CODE.md](B-CODE.md); evaluation records indexed in [docs/evaluation/README.md](../evaluation/README.md); locked-set reuse disclosed in [locked-set-reuse.md](../evaluation/locked-set-reuse.md).
- **For B (small fix):** [docs/evaluation/README.md](../evaluation/README.md) still says the P01 split "was used twice"; it has been used **four times** (see locked-set-reuse.md). B owns that index — please correct the count before the next acceptance discussion.

### Handoff revision 10 — 2026-10-19 midday (cycle 2; A only, 4-hour window)

- **Scope delivered (A's packages):**
  - **Geometry/order correction:** `GeometryService` (validated polygon/baseline updates, region reading-order with set equality), `PUT /lines/{id}/geometry`, `PUT /surfaces/{id}/reading-order`, Angular overlay editor (image-frame drag + region up/down), 6 service + 2 HTTP tests. Known gap: geometry edits update the native document directly (no geometry revision history); client undo + project backups are the current recovery path — recorded, not hidden.
  - **Training-data export:** `TrainingDataExport` — L1-only labels, editorial-supply lines excluded outright, uncertainty spans with exact-span complements; CLI (`export training`) + API + workspace download; 8 tests + smoke.
  - **Editor completion:** annotation listing (service/API/UI), crash-recovery draft autosave/restore, Cmd/Ctrl+S/Z, tested scalar↔UTF-16 utils (12 web specs), TEI/training export actions, AI-assistance panel (propose → opt-in L2 acceptance; server-resolved evidence).
  - **Formats/API:** IIIF import via CLI + API (4-view smoke), contract JSON on minimal APIs, closed-code→HTTP middleware (404/409/422), annotation request DTO with bind-to-current anchor.
  - **Release evidence:** publish outputs verified (CLI runs from publish dir), independence checks documented in [docs/release/packaging.md](../release/packaging.md).
- **Layout iteration (meaningful result):** fixed three real-corpus preprocessing defects (faint-letter threshold, dark-surround crop, peak-only banding) — projection baseline precision 0.087→0.345, recall 0.096→0.506. The learned row-logit detector **underperforms** the corrected baseline; recorded as a negative result with diagnosis ([layout-learned-comparison.md](../evaluation/layout-learned-comparison.md)). Next iteration: patch-level CNN.
- **Verification:** full suite green after the final run (counts in the board record); web: 12/12 Chrome specs, production build clean.
- **Collaboration:** B's files untouched; B quiet during this window after its morning increments. Boundary state unchanged; no new contract requests raised. The licensing-policy files remain untracked (another worker's assignment).

### Handoff revision 11 — 2026-10-19 afternoon (cycle 3 close; A only)

- **Window:** 12:14–13:2x, single-instance work on A's packages (B quiet since its 10:35 handoff; licensing-policy files remain untracked for their author).
- **Delivered:**
  - **Durable jobs completed end to end:** `layout.detect` handler (surface carried as the job's input artifact), DB-backed job pickup fixed (API-enqueued jobs now execute in a separate worker process), remote captures resolved through the checksummed image cache. Live smoke: API enqueue → worker run → 12 detected lines, job `Succeeded`.
  - **Layout:** projection detector improved (paper-relative threshold + content-bounds crop + background-relative banding): precision 0.087→0.345, recall 0.096→0.506 on real pages; wired as a first-class stage (CLI/API/UI) that refuses to overwrite existing layout. Learned row-logit retried with scale-relative and column-texture features — improved (R 0.003→0.04) but still below the projection baseline; recorded as a negative result with diagnosis.
  - **Workflow proofs on both real tracks:** image-only papyrus page → 4 detected lines → 4 non-empty L0 outputs; held-out M02a manuscript page → 25 lines → 19 non-empty L0 outputs. L0 revisions are now persisted with model provenance (raw output never enters L1).
  - **Self-service training:** `train lines` CLI writes a complete model package (manifest + card, verified label/architecture metadata); loop verified: train → package → import → recognize.
  - **Ingestion/import fixes:** PAGE import resolves the referenced image path (was storing only a filename) and accepts a surface kind; bulk PAGE import through the bounded pipeline (27/27 files, 0.6 s).
  - **UI:** line-order editing, generic annotation form (all kinds/extent/alternatives), geometry resize handles, multi-view capture selector, detect-layout action.
  - **Measured limitations recorded:** TIFF unsupported by the current codec (now rejected cleanly, no crash); geometry edits still update the native document directly (no geometry revision history; client undo + backups — disclosed gap).
- **Verification:** full suite green (counts in the board record); 12 web specs; Release build 0 warnings.
- **Still open (unchanged):** Phase 0 specialist adjudication/annotation volume; inscription labels; shared-server auth; TIFF/backend selection; Kraken comparator; frozen acceptance sheet; hosted provider calls.

### Handoff revision 12 — 2026-10-19 (cycle 4; A only)

- **Interchange:** bounded ALTO v4 and hOCR adapters (export + import, unsupported-element
  reports, DTD refusal); legacy import (`document + image → surface`); PAGE export now records
  image dimensions and is tested for reading-order-preserving output.
- **Data integrity (important fix):** imported PAGE/ALTO/hOCR ids are namespaced per capture.
  Document-local ids (`ln1`) previously repeated across projects, letting globally addressed
  endpoints (`/lines/{id}`) act on another project's line. Regression test covers
  project-A/project-B isolation.
- **QA:** shared Greek transcription-profile report (CLI `validate text`, HTTP
  `profile-report`, included in export bundles) flags Latin letters, unknown scalars and
  unpaired marks without rewriting text; `train lines --profile-strict` excludes
  out-of-profile labels and reports counts. Label-hygiene measurement recorded (single seed:
  0.813 → 0.807 dev CER; direction, not proof).
- **Layout:** dev-selected merge rule **rejected** after held-out regression (P01 → zero
  matches). The density post-filter from the earlier experiment remains the detector default.
  Report now splits in-domain M02a from out-of-domain P01 so this cannot hide again.
- **Workflow:** image-only import (`import image`) unlocks manual layout for inscription
  photographs; manual line add/delete with transcribed-line protection; geometry edit undo;
  alternative reading → composed L2 revision (scalar-safe, speced); QA indicator in the
  workspace.
- **Jobs/ops:** durable `recognize.surface`, `recognize.project`, `import.dir`; API job listing
  with status filter; cooperative cancellation between batch items (Cancelled is preserved, not
  resurrected); backup/restore with image-checksum verification before writing; opt-in API-key
  + read-only guard for shared deployments (explicitly **not** a user system).
- **Measurements:** end-to-end page CER (layout + recognition) **0.944 mean** vs **0.905**
  PAGE-box control across 3 seeds on 6 held-out M02a pages — control range 0.878–0.953 means
  the layout effect is **not separable from training variance** at this model quality;
  detection recall is stable at 0.556. Batch recognition 27 surfaces / 321 lines in ~1.3 s;
  peak RSS ~200 MB single page / ~244 MB batch (CLI). Projection layout precision improved
  0.386 → 0.433 (content-bounds cropping) at equal recall.
- **Manual layout completion:** split merged detections and merge over-split ones (untranscribed
  lines only — transcribed evidence is refused), append-only geometry audit, editable/view-only
  page overlay with click-to-open and zoom, project deletion with a forced flag, project stats,
  manifest audit CLI.
- **Search/QA/review:** project search (literal + mark/case-folded base; scalar ranges and
  snippets; UI opens the hit across surfaces), profile-report CLI/API/bundle + QA badge,
  annotation and line CSV exports, mark-span rendering in the editor (unclear/lost/supplied/
  alternative/erased), L1/L2 prefix/suffix diff, model-package discovery and inline
  recognition (name-only, verified packages), confidence-aware review queue (CLI CSV, top-20 in the API response, persisted
  `GET /projects/{id}/review-queue` from stored mean L0 confidence, workspace list), model
  package discovery + inline recognition, L0-copy-in-editor that never auto-saves.
- **Geometry audit:** every geometry/order/add/delete action now appends a surface-level edit
  record (kind, author, timestamp, before/after snapshot), exposed by
  `GET /surfaces/{id}/geometry-history` and shown in the overlay; the contract's §1.3 records
  that this is an audit list, not revisioned entities.
- **Server hardening:** annotation ranges validated against the anchored revision (empty
  spans only for lost markers; estimated gaps need a count; alternatives need text); malformed
  identifiers return a client error instead of a 500; project stats endpoint; contract
  self-review recorded (`docs/contracts/self-review-2026-10-19.md`) with open actions.
- **Docs:** `docs/STATUS.md` (one-page status), `docs/operations.md` (run/backup/batch/auth),
  throughput + evaluation index updates, implemented endpoint inventory in the API contract.
- **Verification:** closing run **470 tests (469 passed + 1 opt-in skipped, 0 failed) across
  13 suites; Release build 0 warnings/0 errors**; 26 web specs green; `scripts/pilot-demo.sh`
  end-to-end PASS. Two self-review integrity actions were resolved in-cycle (mean L0
  confidence persisted; run-id idempotency for retries) and geometry/order edits now have a
  surface-level audit. B remains quiet since its morning handoff; licensing files still belong
  to their author.

### Handoff revision 13 — 2026-10-19 (trainable layout module)

- **New module:** `PatchLayoutModel` (`patch-row-cnn-v2`) in `LeviathanText.Layout`: aspect-
  preserving page preprocessing (content crop → height 256, aspect-preserving width), a small
  fully convolutional CNN (width collapsed deliberately; 1D row head), weighted BCE training,
  package save/load with architecture/metadata checks, threshold-tuned band decoding, and
  expansion of core predictions back to line boxes.
- **Measured (one-to-one matching):** M02a held-out P **0.965** / R **0.987** vs projection
  0.523/0.545; overall held-out P 0.734/R 0.686. Projection page-workflow baseline under
  the corrected matcher: 0.973 mean CER vs 0.943 PAGE-box control (3 seeds). Out-of-domain P01 stays weak for the M02a
  model; a P01-trained model improves aggregate P01 metrics (0.444/0.324 vs 0.337/0.176) but
  fails on dense pages and is **not adopted**. Page workflow (same recognizer): CER **0.971 →
  0.942**, detection recall ~0.54 → ~0.996.
- **Metric bug fixed:** layout comparison and page-CER matching were not one-to-one (one merged
  detection could match several GT lines, precision > 1). Matching is now greedy one-to-one;
  all layout/e2e reports were regenerated under the corrected metric.
- **Integration:** opt-in per-material selection — CLI `layout train` / `layout detect --model`,
  API configuration `LeviathanText:Layout:Models:<kind>`, worker
  `LEVIATHANTEXT_LAYOUT_MODEL_DIR/<kind>/`, and an Application-level detector parameter;
  projection remains the default. Model packages are validated (architecture match, weights
  digest) and carry a model card; broken packages are skipped with a warning.
- **Verification:** 482 tests (481 passed + 1 opt-in skipped), 0 failed across 13 suites;
  Release build 0 warnings/0 errors; 26 web specs; `scripts/pilot-demo.sh` PASS. The first
  solution-level run reported 39/45 ML tests once (test-host anomaly, no failures); rerun with
  TRX logging executed all 45 and reproduced the green result.
- **Not claimed:** multi-column/rotated layout, papyrus layout production use, or broader
  manuscript generalization from one codex; the P01 split remains spent.

### Handoff revision 14 — 2026-10-19 (double review / adjudication)

- **Review ledger:** append-only `ReviewDecision` records on surfaces, anchored to the exact
  reviewed revision; `Accept`/`Correct`/`Dispute`; a correction creates a new L1 revision and
  records both revision ids.
- **Adjudication:** per-project summary (transcribed/reviewed/double-reviewed/disputed/needs
  adjudication) and an adjudication CSV with each reviewer's decision; exposed via
  `review line` / `review summary` / `export adjudication`, HTTP endpoints, and the line editor.
- **Contract:** review decisions documented in domain-and-pipeline §1.3 as an audit ledger, not
  text revisions. Reviewer identity is a free string; accounts/roles remain open.
- **Verified:** service + HTTP + UI tests; CLI smoke (accept + dispute → adjudication row).

### Handoff revision 15 — 2026-10-19 (inscription bootstrap)

- **EpiDoc bootstrap:** ISicily-style records import the primary edition split at `<lb/>` as
  **L2 reference** lines (never L1), with the image as capture, no geometry, and explicit
  notes for flattened markup/edition normalization. Line ids are namespaced per import.
- **Manual placement workflow:** `GET /surfaces/{id}/unplaced-lines` plus a geometry-editor
  list ("Unplaced text lines"); selecting a line and drawing assigns its geometry through the
  existing validated geometry endpoint. Read-only view remains available.
- **CLI/API:** `import epidoc`, `POST /projects/{id}/import-epidoc`,
  `GET /surfaces/{id}/unplaced-lines`.
- **Smoke:** real ISic001151 + w800 image → 3 reference lines split correctly (including a
  `break="no"` hyphenation), 0 L1 revisions. Expert transcription/geometry work remains.

### Handoff revision 16 — 2026-10-19 (layout augmentation)

- Added explicit training augmentation to `PatchLayoutModel`: vertical scale 0.85–1.15,
  shift ±8 rows, intensity/noise jitter; image and mask transformed together. Metadata/model
  card record the settings; `--no-augment` opts out; CLI training enables it by default.
- Measured A/B (same seed/epochs): M02a P 0.965→0.988, R 0.987→1.0; P01 P 0.31→0.325,
  R 0.134→0.201; all-held-out P 0.734→0.754, R 0.686→0.718. Single seed/material pair —
  direction, not a generalization claim.
- Report updated; unit test covers augmented training on synthetic bands.

### Handoff revision 17 — 2026-10-20 (accounts and roles)

- **Store schema v2:** `users` table (id, unique name, role, hashed key, created, disabled);
  v1 databases migrate in place; newer schemas are refused (tests cover migration and refusal).
- **Authentication:** bootstrap admin key plus per-user hashed keys (SHA-256, shown once);
  Bearer / `X-Api-Key` / SSE `api_key` token. Disabled/rotated keys stop working immediately.
- **Authorization:** reader (GET), annotator (mutations), reviewer (+ review decisions), admin
  (+ user management and project deletion). Read-only mode still overrides all roles.
- **Management:** CLI `user add/list/disable/rotate`; HTTP `GET/POST /users`,
  `POST /users/{id}/disable|rotate` (admin only).
- **Limits:** no SSO/refresh tokens, per-object workspace membership, structured auth audit, or
  a workspace login prompt. Existing local-only tests and behavior remain unchanged.

### Handoff revision 18 — 2026-10-20 (confidence detail, auth audit, workspace auth)

- **Per-grapheme confidence persisted** on L0 revisions (`GraphemeConfidenceSpan` with scalar
  ranges and explicit scale); the review queue now uses exact means and reports the worst span.
  Line payloads expose the spans.
- **Auth audit** (store schema v3): mutations and denials recorded with user/role/status and no
  key material; `audit list` / `GET /audit` (admin). Migration v1→v3 tested; newer schemas refused.
- **Workspace auth:** API-key panel + HTTP interceptor (`X-Api-Key`), SSE query-token helper, and
  the missing `provideHttpClient` in the app config fixed.
- **Verified:** infrastructure user/audit tests, role authorization test extended, interceptor
  spec; counts recorded after the closing run.

### Handoff revision 19 — 2026-10-20 (annotation retraction, capability matrix)

- Annotation retractions are append-only; active views, training export and loss reports exclude
  retracted ids; annotation CSV records the retraction and reviewer; editor action + API + tests.
- Added `docs/release/parity.md`: an honest reference-capability matrix with evidence and
  blockers. It explicitly does **not** claim full parity.

### Handoff revision 20 — 2026-10-20 (evening block: layout learning, data tooling, shared deployment)

- **Trainable layout:** patch/row CNN (`patch-row-cnn-v2`) with aspect-preserving preprocessing,
  core-target decoding, dev-tuned threshold, augmentation, SHA-256 weights digest, data digest
  and model card; opt-in per material via CLI (`layout train/detect/inspect`), API
  `Layout:Models:<kind>`, and worker `LEVIATHANTEXT_LAYOUT_MODEL_DIR/<kind>`. Measured M02a
  P 0.965/R 0.987, page CER 0.971→0.942; P01 out-of-domain weak (a P01-trained variant is
  measured but not enabled).
- **Fresh-book evidence:** on 20 M01 pages (never used for layout training) the learned
  detector reaches P 0.994/R 0.991 vs projection 0.918/0.918; on a 6-page M01 page-CER sample
  detection recall rises 0.911→0.969 but page CER is neutral (0.801→0.805) because recognition
  dominates. Reported honestly; no broad generalization claim.
- **Metric correction:** layout and page-CER matching are one-to-one greedy by IoU; all layout
  and page reports were regenerated (earlier non-one-to-one reports are superseded).
- **Inscription bootstrap:** EpiDoc edition split at `<lb/>` imported as L2 reference (never L1)
  with manual unplaced-line placement; batch import by record directory; inventory scanner
  (records/Greek/graphics/local images) with manifest-count reconciliation recorded. ISic001151
  smoke green; expert labels remain the blocker.
- **Review workflow:** append-only accept/correct/dispute decisions anchored to revisions,
  correction revisions, project review dashboard, adjudication CSV, annotation retraction.
- **Shared deployment:** schema v3 (users + auth audit), local accounts with
  reader/annotator/reviewer/admin roles, hashed keys, rotation/disable, admin endpoints/CLI,
  auth audit, workspace API-key panel + interceptor; `provideHttpClient` fixed. SSO/workspace
  membership remain open.
- **Release:** fresh-install publish/run smoke script (PASS), dependency/no-Kraken evidence,
  layout timing/RSS, capability matrix (`docs/release/parity.md`).
- **Confidence:** L0 revisions persist mean and per-grapheme confidence spans; review queues
  use exact means and worst spans; the editor renders confidence bands.
- **Offline reads:** successful project/line/diplomatic reads are cached in localStorage with an
  offline banner; caches clear on key change; mutations are not replayed.
- **Verification:** **522 tests (520 passed + 2 opt-in skipped), 0 failed across 13 suites;
  Release 0 warnings/0 errors**; 35 web specs; demo and fresh-install smokes PASS.

### Handoff revision 21 — 2026-10-20 (imports, offline, safety, docs)

- **Bulk imports:** line-text CSV (rows through the revision service) and annotation CSV (rows
  through range/kind validation); CLI + HTTP + workspace upload; unknown rows reported, never
  silently dropped. `EntityId.TryParse` added.
- **Offline reads:** project/line/diplomatic cache with stale-data banner and cache clearing on
  key change; mutations are never replayed.
- **Workspace ergonomics:** editor identity (author/reviewer provenance), prev/next line
  navigation, platform-confidence rendering (high/medium/low bands) and accessibility labels.
- **Safety/release:** auth audit + role edge tests (accounts without a bootstrap key), malformed
  JSON client errors, dependency vulnerability snapshot, security threat model, architecture
  overview, changelog, upgrade/rollback guidance.
- **Plan reconciliation:** completed model/training items archived (55 total); remaining items
  annotated with delivered evidence and blockers.
- **Verification:** **516 tests (520 passed + 2 opt-in skips), 0 failed across 13 suites; Release
  0 warnings/0 errors**; 35 web specs; demo/fresh-install PASS; locked P01 evaluation remains opt-in.

### Handoff revision 22 — 2026-10-20 (per-project membership)

- Store schema v4 adds `project_members`; migration from v3 is in place (newer schemas refused).
- Optional `Auth:ProjectMembership=true` scopes non-admin access to member projects, including
  indirect entity routes; admins bypass; creators auto-join on API creation.
- Admin API/CLI membership management; membership revocation is immediate; auth audit records
  denials.
- Limits: no invitation UI, project-owner role or high-throughput entity→project index (the
  membership lookup scans projects at pilot scale).

### Handoff revision 23 — 2026-10-20 (experiment/run registry)

- Store schema v5 adds `runs`: kind/name/creator, dataset digest, config/metrics maps,
  learning-curve series and artifact paths. CLI `train lines` and `layout train` record runs
  automatically; layout loss history is persisted.
- CLI `runs list/show/compare`; API `GET /runs` and `/runs/{id}`; workspace runs panel with a
  sparkline and config details.
- Fixed `layout train` flag parsing (out/image-ext/epochs/seed were positional and fragile).
- Limits: no error-analysis drilldown, run deletion, or automatic report publishing; runs are an
  index, not acceptance evidence.
- **Verification:** 522 tests (520 passed + 2 opt-in skips), 0 failed; Release 0 warnings/0 errors;
  35 web specs.

### Handoff revision 24 — 2026-10-20 (orientation, PDF, release hardening)

- **Direction/script metadata:** per-region/line optional `direction` and `script` fields in the
  native model; PAGE `readingDirection`/`primaryScript`, hOCR `dir`/`ocr_vertical` and bounded
  TEI `rend` round-trip; API `PUT /lines|regions/{id}/orientation`; editor controls and
  direction/vertical display. No BiDi layout or vertical recognition claim; ALTO direction is
  not mapped.
- **PDF ingestion:** bounded PDFtoImage/PDFium page rasterization (200 MB, 500 pages, 12k px /
  50 MP per page) into one capture-only surface per page; CLI `import pdf` and
  `POST /projects/{id}/import-pdf`. No PDF text/layout extraction.
- **Run regression gate:** `runs regress` compares stored metrics with an absolute tolerance and exits nonzero on regression.
- **Evaluation drilldown:** `evaluate lines` scores a packaged model on dev/train, writes per-line
  CER/base/mark/confusion CSV/JSON + Markdown report and records an `evaluation` run with a split digest; locked
  test labels are not available to routine evaluation.
- **Deep links:** project/surface/line query parameters restore workspace state; selection updates the URL without adding history entries.
- **Member management UI:** admin-only project panel loads accounts/members and grants/revokes
  opt-in project access; invitation tokens/presence remain open.
- **Packaged workspace:** optional `LeviathanText:WebRoot` serves the Angular build under `/ui`
  with SPA fallback; `/` and `/api/v1` stay separate.
- **Release hardening:** `scripts/package-release.sh` (osx-arm64 framework-dependent package +
  SHA-256 manifest), `scripts/backup-restore-rehearsal.sh` PASS, and v2/v3/v4→v5 migration
  tests. Linux/Windows packages, installers/updaters and full rollback rehearsal remain open.
- **Verification:** **538 tests (536 passed + 2 opt-in skips), 0 failed across 13 suites;
  Release 0 warnings/0 errors**; 37 web specs; package and backup/restore rehearsals PASS.
