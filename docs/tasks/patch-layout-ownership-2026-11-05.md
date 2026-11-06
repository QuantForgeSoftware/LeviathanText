# MID24: shared patch-layout model lifetime

## Proposed contract

`PatchLayoutModel` implements `IDisposable`. A private shared owner holds the one `PatchCnn`, a lock and a wrapper reference count. `WithThreshold` retains that owner and returns an independently disposable wrapper, preserving the existing model hash, metadata and threshold semantics. Disposing a wrapper is idempotent; the last wrapper disposes the native module exactly once. A disposed wrapper rejects public instance operations/properties with `ObjectDisposedException`; an undisposed alias remains usable.

Inference, decoding, save, alias creation and wrapper disposal use the same owner lock. Disposal therefore waits for an active synchronous operation, and later operations cannot race native module release. This deliberately serializes aliases' access to the shared native module; it does not promise concurrent inference throughput or coordinate caller-owned bitmap lifetime. No public raw module handle is exposed.

Successful `Load` detaches only the actual installed model parameters from ambient dispose scopes. The public owner becomes responsible for those parameters through the native module; other construction/loading temporaries are disposed with their isolated scope. Unrelated caller tensors remain in the caller's scope. Failed construction/loading retains the current explicit module cleanup and construction-scope cleanup. For `Train` (not executed in this work), isolate native model construction and detach its owned parameters, so returned models likewise survive caller scopes; optimizer/training changes are outside this slice.

## Application owners

- The API already registers `LayoutModelRegistry` using container-created `AddSingleton<T>`; make the registry disposable so DI shutdown releases all owned wrappers. Idempotent registry disposal and disposed lookups are explicit.
- Worker entry point owns loaded wrappers through a `try/finally` covering host construction and polling/shutdown. Disposal follows current-handler drain and also covers startup exceptions after loading.
- CLI inspect/detect/train owners use `using`, including both original and threshold-tuned aliases. No training command is executed for this task.

## Verification and handoff

Use random untrained fixture weights generated from the private production CNN, zero optimizer steps, fixed inputs and native lifetime counters. Capture the failed-before missing public deterministic-disposal contract, retaining successful caller-scope escape as a passing baseline control. The current pinned TorchSharp/load behavior already passes that control; no scope-escape failure is claimed. After implementation verify public disposal, alias survival in both orders, double disposal, final native cleanup, scope escape, exact weights/metadata/threshold/hash behavior and disposal waiting for an admitted operation. Preserve all existing failed-load cleanup controls and update the successful-load fixture to use public disposal instead of disposing the reflected module directly.

Focused tests must exclude fitting/training tests. Published/frozen experiment binaries remain unchanged; this task alters current source only after the parent finishes its stable-package snapshot. No upstream TorchSharp leak fix or natural accuracy claim is made.
