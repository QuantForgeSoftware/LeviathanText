# Handoff — B-CODE (Instance B: ML, Evaluation and AI implementation)

## Status and owner

- Package: **B-CODE**, owner **Instance B**, 2026-10-18. Status: **review** (builds and tests green; awaiting A/integrator review of boundary crossings and contract requests).
- Scope: implement Instance B's coding ownership from todo Section 6 — **ML** (`Recognition`, `Training`), plus **Evaluation** and **AI** scaffolding — as a C# stack that builds and passes tests. No Python, no Kraken.
- Companion planning documents: [B-PLAN.md](B-PLAN.md), [../evaluation/acceptance-plan.md](../evaluation/acceptance-plan.md), [../data/](../data).

## Verified result

```
dotnet build LeviathanText.slnx -c Release   -> Build succeeded. 0 Errors, 0 Warnings
dotnet test  LeviathanText.slnx              -> 136 passed, 0 failed
    Contracts.Tests  41   (A)
    Core.Tests       72   (A + B integration tests)
    Evaluation.Tests  8   (B)
    AI.Tests          6   (B)
    ML.Tests          9   (B)
```

The ML suite proves the Phase 0 C# feasibility path end to end on **synthetic** data: variable-width CTC training → best-checkpoint selection → checkpoint reload into a fresh model → greedy decode → exact match, plus optimizer-state resume that continues the schedule. Synthetic fixtures are not corpus counts and make no accuracy claim about real Greek.

## What was built

| Project | Contents |
| --- | --- |
| `src/LeviathanText.Recognition` | `LabelSet` (grapheme↔CTC index, unknown-label failure), `LineImage`, `CtcDecoder` (greedy), `CrnnModel` (CNN+LSTM+CTC head, input 16×W, T=W/4), `LineBatcher` (variable-width padding), `TorchLineRecognizer` (L0 + per-grapheme confidence) |
| `src/LeviathanText.Training` | `VariableWidthCollator`, `SyntheticGraphemeRenderer`, `SyntheticLineDataset`, `TrainingConfig`, `CheckpointStore`, `CtcTrainer` (train / evaluate / best-checkpoint / early-stop / resume) |
| `src/LeviathanText.Evaluation` | `CanonicalText` (NFC + scalars + graphemes), `EditScript` (Levenshtein alignment), `RecognitionScorer` (scalar/grapheme CER + separate mark errors), `UncertaintyScorer` (excluded-span coverage), `PageScorer` (blank-region insertions, missed lines) |
| `src/LeviathanText.AI` | `IAiProvider`/`IRawAiProvider`, `SchemaValidatingAiProvider` (malformed → recorded failure), `ScriptedAiProvider`, `ProposalAcceptanceService` (accepted proposal → L2, L1 untouched, stale target rejected) |

Dependency pin (for the integrator's central version file): **TorchSharp 0.107.0 ↔ libtorch-cpu-osx-arm64 2.10.0** (verified on macOS arm64, .NET 10). The native package reference is conditioned on `IsOSPlatform('OSX')`; other platforms need their own libtorch package.

## Boundary crossings (require integrator review)

A and B ran concurrently. To keep the build working I made **two minimal repairs inside A's `LeviathanText.Core`** (both blocking; A was idle at the time). Both have survived A's subsequent edits. B owns neither file:

1. `Core/Revisions/RevisionChain.cs` — `RequireCurrentAnchor` did not compile (`CS0165` on `current`). Hoisted `var current = Current(...)` above the guard. Semantics unchanged. *(This blocked the entire solution.)*
2. `Core/Unicode/NfcMapper.cs` — `MapRange` indexed `SourceToTarget[range.End]`, which throws when a range ends at the scalar length (the common end-anchored annotation). Now clamps to `TargetScalarLength`. Semantics: end-anchored ranges map correctly.

Shared files B created that A (as C0/integrator owner) may want to take over: `Directory.Build.props` (net10.0, nullable), `.gitignore`, `LeviathanText.slnx`. B deliberately removed its earlier `Directory.Packages.props` (central package management) because it broke A's `Contracts.Tests.csproj` (`NU1008`).

## Contract change requests raised by the code

| ID | Request | Why |
| --- | --- | --- |
| `B-CCR-6` | Add a `preservation_tags` asset field (P02 tags, I03 damage) and a text-only `catalog`/`metadata-text` `asset_kind` value. | Already raised in B-PLAN; the ML/AI code paths confirm glyph/tag and catalogue assets have no home. |
| `B-CCR-7` | Add a `Proposal` entity kind (or state that proposals use string ids). | `EntityKind` has no Proposal, so `AiProposal.Id` is a string (`prop-<guid>`). |
| `B-CCR-8` | Confirm the model-package metadata fields (`ModelRef` exists) suffice for `CrnnModel` packages, or add `TokenizerHash`/`PreprocessingVersion`. | **Withdrawn** — `domain-and-pipeline.md` §3 already requires tokenizer/label set (+ hash), preprocessing recipe (+ version), runtime, metrics, checksums and license metadata; B aligns its model card to §3 rather than adding fields. |

## What is deliberately NOT done

- No real corpus was acquired or trained on; all fixtures are synthetic and labeled as such.
- No ONNX export path (Phase 0 decides portability separately).
- Greedy CTC decoding only (beam search deferred to measurement).
- No CLI/API/worker wiring (those are NOT B's packages).
- No numeric accuracy/performance claim.

## How to run

```
dotnet build LeviathanText.slnx -c Release
dotnet test  LeviathanText.slnx
dotnet test  tests/LeviathanText.ML.Tests/LeviathanText.ML.Tests.csproj   # train/reload/infer/resume proof
```

## C0-0.3-draft alignment (2026-10-18)

- Contract consumed: **`C0-0.3-draft`** (five documents; SHA-256 verified against A-PLAN rev 5). B's code compiles and tests against the compiled contract types (`ContractVersion.Current == "C0-0.3-draft"`).
- **Solution build unblocked.** `A-PLAN` rev 6 noted the solution build was blocked by B's in-flight `Recognition` edits; those are complete — `dotnet build -c Release` is clean (0 errors, 0 warnings) and 136 tests pass.
- **`A-REV-EVAL-1` fixed (A's finding, B's file).** `Evaluation.CanonicalText.ToScalars`/`FromScalars` now route scalar segmentation through `Core.Unicode.ScalarText`, so a dangling surrogate half is its own scalar exactly as Core treats it; a regression test covers both a dangling surrogate and a valid astral pair. `Evaluation.Tests` = 8/8.
- Contract requests from the code: `B-CCR-6` (preservation tags / catalogue `asset_kind`) and `B-CCR-7` (`Proposal` entity kind) remain **open**; `B-CCR-8` withdrawn (satisfied by `domain-and-pipeline.md` §3). See [B-PLAN.md](B-PLAN.md) for the full review and dispositions.
- Note for the serializer contract: B's `AiProposal.Request` carries `ProviderConfigRef` (opaque) and never credentials, consistent with `interchange-and-api.md` §2.3.

## Two-instance resumption (2026-10-19 06:55, Instance B)

- Baseline: `main` @ `23eb349`, contract set **`C0-0.4-draft`**. Working mode: two instances in one worktree with disjoint write paths (A = integrator/C0/APP/FMT/IMG/UI; B = ML/AI/EVAL).
- **Claimed packages:** **AI** (`src/LeviathanText.AI/`, `tests/LeviathanText.AI.Tests/`), **EVAL** (`src/LeviathanText.Evaluation/`, `tests/LeviathanText.Evaluation.Tests/`, new `docs/evaluation/` records), then **ML** (`src/LeviathanText.Recognition/`, `src/LeviathanText.Training/`, `tests/LeviathanText.ML.Tests/`) as time permits.
- B will not edit `todo.md`, `docs/contracts/`, `docs/handoffs/A-PLAN.md`, solution/project central files, `src/LeviathanText.{Application,Infrastructure,Api,Worker,Cli,Formats,Imaging}/`, or `web/`.
- Planned increments: (1) real local/hosted HTTP provider adapters with capability discovery, retries, bounded responses, credential isolation and spend caps; (2) evaluation of AI-assisted correction effort / unsupported additions; (3) ML decoder or confidence refinement if the first two land cleanly.
- Each increment is committed separately after `dotnet build -c Release` + the relevant test project pass; A can merge/cherry-pick or leave B's commits in place.

### Cross-package finding for IMG (2026-10-19, ML/EVAL)

The measured P01 decoding comparison (`docs/evaluation/decoding-comparison.md`, artifact
`.artifacts/pilot/p01-decoding-comparison.txt`) found that **50/50 dev lines have fewer CTC
time steps than reference scalars** (median T=8 vs median 18 scalars; resized crop width
median 35 px). The P01 line *crops* are therefore near-square, not long and thin, and the
decoder is structurally unable to emit a full line; that is why the error profile is
deletion-dominated (706–800 deletions vs 0 insertions).

B recommends IMG review P01 line-region geometry/deskew extraction before any accuracy
claim. The ML-side fallback (CRNN width downsample 4 → 2) removes the limit on 30/50 lines
and improves dev CER 0.8496 → 0.8257 (measured, `P01WidthDownsampleTests`); the default
stays 4 so existing checkpoints remain valid, and factor 2 is the recommended next-candidate
configuration. No IMG-owned file was touched.

### B progress log (2026-10-19, two-instance session)

| Commit | Package | Increment |
| --- | --- | --- |
| `c3a050d` | AI | Real local/hosted HTTP adapters: OpenAI-compatible + Ollama chat, capability discovery, retries/Retry-After, per-request timeout, bounded bodies, credential isolation (env-var ref only), spend caps. 20 tests. |
| `071bb3c` | EVAL | Correction-effort scorer + method note: raw vs reviewed CER, accepted-suggestion volume, unsupported additions/restorations, witness-overwrite flag, measured time. 19 tests (with `674767c`). |
| `ac6479d` | ML | CTC prefix beam search + forced-alignment confidence; opt-in recognizer mode (greedy default). |
| `0bccf6b` | ML/EVAL | Measured greedy-vs-beam on P01 dev; greedy stays default. |
| `674767c` | EVAL | Calibration (ECE/MCE) + risk–coverage selective prediction; measured AURC 0.85 > CER 0.84 on P01 dev (confidence barely orders). |
| `98789fd` | AI | Server-side proposal service: resolves source span from the target revision; rejects client-supplied evidence and out-of-range spans. |
| `b70879c` | EVAL | Ranked confusion/error analysis with separate mark counts; measured deletion-dominated P01 failure. |
| `08197d3` | ML | Configurable CRNN width downsample (1/2/4); measured A/B (factor 2: 30/50 reachable, CER 0.8257). |
| `4341277` | AI | Proposal reject/expire + mandated bulk-acceptance dry run (pure preview, batch conflict reporting). |
| `9024d2e` | ML | Training run manifest (architecture/config/label/split hashes, checkpoint digest, learning curve); P01 split hash matches the pilot report. |
| `0842cec` | ML | Adopted the concurrent integrator's Otsu interior-threshold fix into the ML-owned preprocessing file so P01 measurements are reproducible from HEAD. |

All of the above are green in `LeviathanText.AI.Tests`, `LeviathanText.Evaluation.Tests`,
`LeviathanText.ML.Tests`; the solution builds in Release. B did not edit `todo.md`,
contracts, `A-PLAN.md`, the solution file, `src/LeviathanText.Layout/`,
`src/LeviathanText.{Application,Infrastructure,Api,Worker,Cli,Formats,Imaging}/`,
`tests/LeviathanText.Pilot.Tests/`, or `web/`.
