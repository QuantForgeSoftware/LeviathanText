# Shared patch-layout model ownership — 2026-11-05

`PatchLayoutModel` now implements deterministic public disposal. Threshold aliases share one native network through a private counted owner. Each wrapper is independently disposable; disposal is idempotent and the final wrapper releases the native module. A disposed wrapper rejects public instance methods and property reads with `ObjectDisposedException`. An undisposed alias remains usable, retains its own threshold, and preserves the existing weights, metadata and model-hash semantics.

## Ownership and synchronization

The same shared gate protects alias creation, synchronous inference/decoding, save, property reads and disposal. A last-owner disposal waits for an admitted operation, and operations arriving after disposal fail before native access or save-directory creation. This serializes work across aliases: it is a deliberate safety/latency tradeoff, not a claim of concurrent inference throughput. Callers still own bitmap lifetime and must not dispose/mutate an image during an operation.

Successful construction/loading detaches only the **installed model parameters** from tensor dispose scopes. Constructor/load replacement temporaries are disposed with their isolated scope. Detaching all construction-scope objects is unsafe: generated counter tests caught seven retained temporaries in that discarded approach. Repeated final loads/disposals now return the native live counter exactly to baseline. The private untrained-network creation helper used by `Train` is covered by forward/save and lifetime checks; `Train` also cleans its model on failure and scopes optimizer/training temporaries. No training command or optimizer step was executed for this change.

Application ownership is explicit:

- The API's existing container-created singleton `LayoutModelRegistry` is disposable, so DI shutdown releases its models. `Resolve` returns a **borrowed** wrapper, not a transfer of ownership. Registry disposal is idempotent; disposed lookups fail.
- Worker model ownership covers host construction, polling, current-handler shutdown drain and exceptions with `finally` cleanup. Loader failures release accumulated wrappers. Case-insensitive kind replacement releases the previous wrapper before losing its reference.
- CLI inspect/detect directly loaded models and trained/threshold-tuned wrappers use `using`; the tuned alias and its original owner are both released. Borrowed registry models are not individually disposed by request handlers.
- Registry loading likewise releases replaced wrappers and drains accumulated models if construction fails unexpectedly.

This is application ownership for the pinned TorchSharp/module implementation. It does not claim a general upstream TorchSharp leak correction or make abandoned, undisposed wrappers safe automatically.

## Evidence

Evidence root: `.artifacts/midday-layout-ownership/`. `source-sha256.json` freezes the seven owned implementation/test files. Build logs are `.artifacts/midday-layout-ownership-before.log`, `.artifacts/midday-layout-ownership-after.log`, `.artifacts/midday-layout-registry.log`, `.artifacts/midday-layout-worker.log` and `.artifacts/midday-layout-cli.log`. Tests used Release, one MSBuild process, `UseSharedCompilation=false` and isolated outputs at `/private/tmp/leviathan-layout-ownership`.

The failed-before run records **one failure and one passing control**: the original public model is not assignable to `IDisposable`; successful loaded-model use after caller-scope exit already passes on the pinned implementation. No reproduced scope-escape defect is claimed. Baseline result: `ownership-before.trx`.

Final focused verification, **46 passed, zero failed/skipped**, with no compiler/analyzer warnings:

| Scope | Passed | Evidence |
| --- | ---: | --- |
| Random-untrained ownership plus existing load-lifetime controls | 14 | `ownership-after.trx` |
| Registry DI shutdown, duplicate keys and startup-failure native cleanup | 3 | `registry-lifetime.trx` |
| Worker completed-attempt budgets, polling, retry/finalization and shutdown controls | 21 | `worker-owners.trx` |
| CLI generated capture-relocation/backup preservation controls | 8 | `cli-owners.trx` |

The ownership tests verify both alias-disposal orders, repeated disposal, identical predictions/hash through aliases, exact saved weight bytes, threshold independence, native-counter retention until the last wrapper, exact counter restoration over eight repeated loads, unrelated caller-tensor survival, post-disposal failures, native operation-gate draining and eight concurrent inference/save/final-disposal races. Race outcomes are completed operations or `ObjectDisposedException`, with no partial save directory when disposal wins admission. Existing missing/digest/unreadable/truncated-load controls retain their cleanup behavior. Registry tests use generated random initial weights and exact lifetime counters, including unexpected logger failure during startup.

The gate-draining test holds the actual owner gate while admitting inference/save and confirms disposal waits; concurrent public-operation controls separately exercise normal admission. These finite tests are not an exhaustive scheduler/platform proof. CLI and worker controls verify integration compiles and existing operational behavior; they do not measure resident memory across production shutdowns.

All new model fixtures are random **untrained** parameters with fixed within-fixture inputs/comparisons; there are zero optimizer steps and no source-corpus fitting, accuracy claims, provider/model requests or changes to frozen benchmark/rehearsal binaries. Complete central engineering verification remains the integrator's final gate after this delta.
