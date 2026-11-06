# Engineering-v9 execution-scope audit

Read-only receipt/source audit, followed by an explicitly authorized narrow runner guard correction. No complete engineering suite was rerun for this audit.

## Historical gate verified

`scripts/verify-engineering.sh --results <fresh-directory>` sets `LEVIATHANTEXT_RUN_FITTING=0`, `LEVIATHANTEXT_RUN_CORPUS=0` and `LEVIATHANTEXT_RUN_LOCKED=0`, builds Release serially without restore, then runs 13 selected projects plus exactly two ML utility class selections. Each call is `dotnet test <project> -c Release --no-build --no-restore -m:1 --filter <selection> --logger trx --results-directory <fresh-directory>`. Nonempty TRX execution and zero failures are required. `--no-build` deliberately reuses existing binaries; it cannot establish source-to-binary freshness by itself.

The shared filter excludes `TrainingDiagnosticsCommandTests`, `TrainingAlphabetTests`, `LearnedLayoutIntegrationTests`, `LayoutModelApiTests`, `PatchLayoutModelTests` and `LearnedLineDetectorTests`. The admitted projects are Contracts, Core, Application, Imaging, Infrastructure, Worker, Api, Cli, Formats, AI, Evaluation, Layout and TestPolicy. ML is never run wholesale: only `FullyQualifiedName~LeviathanText.ML.Tests.LabelCodecTests.` and `FullyQualifiedName~LeviathanText.ML.Tests.LabelCodecInvariantTests.` are selected. Pilot is not invoked. Independent discovery attributes also require exact opt-in values before fitting/corpus/locked test construction/data enumeration.

I recomputed every `UnitTestResult` in `.artifacts/overnight-2026-11-05/engineering-v9/*.trx`: **2,204 Passed, 18 NotExecuted, no other outcomes**. All 18 skips are identified: the live Ollama case plus 17 deliberately disabled/invalid capability sentinel cases. The build receipt reports zero warnings/errors. These are historical v9 outcomes, not a fresh full-suite result after the guard change below.

## Newly integrated cases checked

| Actual admitted class | v9 passed | Inspected work |
| --- | ---: | --- |
| `BitmapExceptionalOwnershipTests` | 39 | Tiny generated Skia PNG/JPEG/P6, per-call Imaging reflection probes, bitmap disposal/pixels and handoff; no trainer/provider |
| `IndependentRenewalTests` | 17 | Generated held handlers, manual clock/session doubles, lifecycle/nonce/fault checks; includes the handler-fault control |
| `LeaseRenewalSessionTests` | 17 | Fresh owned SQLite records/connections, contention and cancellation/replacement; no recognition handler/model |
| `InvalidLeaseRenewalOptionsTests` | 6 | Preclaim zero-write/session counters; no model/provider |
| `UnplacedNativePersistenceTests` | 14 | Generated native JSON/SQLite reopen/read-only/backup-copy checks; no image/source corpus reads |

The 17+17+6 renewal cases are the reported 40 controls; they are not an additional 40 on top of v9. Model ownership and preprocessing engineering cases elsewhere **do** construct/save/load and sometimes forward randomly initialized generated models. Examples are `PatchLayoutLoadLifetimeTests`, `PatchLayoutOwnershipTests`, `LayoutModelRegistryLifetimeTests`, `RecognitionRevisionIdentityTests` and `PreprocessingCommandTests`. This is a **no-fitting/no-real-corpus gate**, not a no-native-library/no-model-execution gate. The inspected preprocessing dataset is written under its own temporary root; its P01 loader call does not access the real P46 corpus. No optimizer/Train/TrainEpoch/backward invocation was found outside excluded/mixed-ML/Pilot fitting cases in the inspected admitted projects.

Provider protocol tests exercise doubles and, in some cases, owned loopback listeners with generated responses. For example `HttpProviderTests` injects its own HttpMessageHandler. Such network mechanics are not real LM Studio/Ollama inference. This bounded source/receipt audit is not whole-program call-graph verification, network sandboxing or proof about arbitrary future classes/dependencies.

## Real inherited-provider guard gap and correction

The historical runner did not overwrite `LEVIATHAN_OLLAMA_LIVE`. `LiveOllamaTests.Local_ollama_server_answers_a_real_request` is admitted by its filter and would use real HttpClients/catalog/proposal if a parent shell passed `LEVIATHAN_OLLAMA_LIVE=1`. The v9 TRX records that case **NotExecuted**, so this is a reproducible future-entrypoint gap, not evidence that v9 contacted a provider. An environment-read audit found no other literal live-provider opt-in in current test sources; `LEVIATHAN_OLLAMA_MODEL` only selects a model behind this gate.

The main runner now explicitly exports `LEVIATHAN_OLLAMA_LIVE=0` alongside the three capability flags. `EngineeringRunnerEnvironmentTests` invokes the actual shell runner with a fresh recording executable named `dotnet` first on its child PATH; it never launches a real suite/model/provider. Its synthetic one-case TRX files solely exercise receipt validation and are not scientific or engineering-suite pass evidence. Before correction, inherited1 produces **one failed/one passing case**: every one of 15 command records has `0|0|0|1`. After correction both cases pass, every command records `0|0|0|0`. The preexisting Python recording-runner test now records/asserts the fourth flag too, including the build invocation.

Actual narrow verification after correction:

- New generated shell controls: **2/2 pass** against isolated Release test binary.
- Main Release TestPolicy project with all four flags0: **18 passed, 17 intentional skips, 0 failures**.
- `python3 scripts/tests/test_verify_engineering.py`: **7/7 pass**.

Exact receipts and original script are under `.artifacts/experiments/engineering-runner-provider-guard-v1/`. No full 2,204-case rerun is claimed. These are environment guardrails, not permissions, an OS firewall or enforcement against arbitrary code/host configuration.

## Retained scope violation

The earlier language work's three unfiltered Application and three unfiltered API invocations did fit generated synthetic layout fixtures, as explicitly disclosed in `docs/evaluation/tei-language-metadata-2026-11-05.md` under “Verification scope mistake.” They must remain a historical exception to unqualified session-wide “no training” language. They used no P46/accepted labels/provider or production checkpoint. The subsequent filters/discovery guards and current v9 receipt do not erase that incident or justify rerunning fitting now.
