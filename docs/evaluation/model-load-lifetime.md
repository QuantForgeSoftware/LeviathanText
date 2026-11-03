# Failed model-load ownership — 2026-11-02

The recognition package loader allocated a `CrnnModel` and then loaded its weights without releasing that model when loading failed. A valid manifest digest proves byte identity, not that the bytes form readable weights. Synthetic empty and half-truncated weight files with matching recorded digests exercise the later failure path.

This is a post-training engineering correction. The six main and three leaf-ablation fits had completed before code changes began; their frozen source/DLL snapshots, checkpoints and results remain authoritative and unchanged. No additional accuracy fitting or Greek data acquisition was performed for this work.

## Reproduction and the measurement correction

The exact installed runtime is TorchSharp **0.107.0.0**, libtorch **2.10.0.0**, CPU. The initial synthetic probe repeated each failing load five times and retained its original exception type/message and caller-model check. `Tensor.TotalCount` grew by 18 per failure, and forced GC left a further 6 per constructed model in that counter. Those exploratory totals are preserved, but are **not** accepted as measurements of remaining live native resources.

A separate independent control explains the counter artifact: create a raw tensor, wrap it with `nn.Parameter(data)` and explicitly dispose both. The raw handle is empty after transfer and the parameter handle is empty after disposal, but `Tensor.TotalCount` still increases by 1 per cycle. Five fully disposed cycles leave that counter at 5 after GC. In the same operations, `DisposeScopeManager.Statistics.ThreadTotalLiveCount` correctly follows **0→1→1→0** for each cycle. The regression oracle therefore uses the independently validated thread-local disposal statistics, plus explicit zero-handle checks in the transfer control, rather than accepting a fixed allowance for counter growth.

This corrects an invalid resource-test oracle; it does not relax a valid lifetime requirement. Earlier fixed-model repeated-forward and owned-Adam *relative* checks do not repeatedly construct moved parameters and retain their demonstrated value. Neither counter represents unique native allocations, storage bytes, RSS or GPU completion.

The unchanged final five-case fixture was also executed against the **original frozen Training DLL** in a separate testhost. Both corrupt-weight cases fail with **+12 live-statistic entries** immediately after the first failure (15→27 and 23→35); the three controls pass. The candidate returns to the exact pre-call live-statistics baseline after every failed load, without forced GC. Caller model/tensor usability and model modes remain intact.

## Ownership correction

The loader now creates a bounded construction/load disposal scope. On failure it disposes the model it still owns and rethrows the original exception; the scope releases construction/load temporaries. Both empty and truncated fixtures preserve the direct library load's `EndOfStreamException` type and message.

On success, the complete returned record is allocated before ownership transfer. Scope-created temporaries move to an existing caller scope when one exists; with no outer scope they retain their former GC lifetime. Exact installed-runtime API probes verify both behaviors. The returned model remains caller-owned and usable for recognition, parameter access and saving byte-identical weights after the loader scope returns. The successful nested-scope test also verifies that the caller receives the temporary ownership and its preexisting tensor remains attached and usable.

There is no `CrnnModel` lifecycle override, tensor arithmetic, architecture, tokenizer, preprocessing, model-format or public loader API change. Direct child-module disposal and public registered-parameter disposal were investigated and correctly rejected as fixes for the misleading residual counter value. Their already-disposed handles, and the raw-to-Parameter control, prevented a speculative model patch.

Construction failure *before* a complete model exists, malformed architecture/OOM behavior, concurrent/GPU loads, allocator caching and sustained RSS remain separate checks. No universal leak-free or memory-performance claim is made.

## Verification

| Check | Result |
| --- | --- |
| Final five-case fixture against original frozen Training DLL | 2 intended lifetime failures; 3 controls passed |
| Candidate loader/ownership/counter/package/task/preprocessing/previous lifetime group | **51 passed**, zero failures/skips |
| Empty and half-truncated weights | Exact exception type/message preserved; prompt balanced lifetime statistics |
| Nested and no-outer successful load | Predictions, parameter arrays, caller resources and saved weight bytes preserved |
| Candidate build | No warnings/errors |

`ModelLoadLifetimeTests.cs` contains the two corrupt-package cases, two successful ownership cases and the independent parameter-transfer oracle control. The baseline uses the same final compiled test assembly with only the original frozen `LeviathanText.Training.dll` substituted in an isolated testhost. Production sources were never rolled back. `verified-baseline-receipt.json` records that DLL and test-assembly/source identity.

TRXs are `.artifacts/test-results/trainer-lifecycle/model-load-verified-before.trx` and `model-load-verified.trx`. The new artifact prefix `.artifacts/experiments/model-load-lifetime/` retains synthetic fixtures, original loader source, exact API/parameter/field probes, partial-investigation logs, final patch and source receipts. Earlier exploratory `model-load-before`, `dispose-only`, `load-scope` and `final` results used the invalid zero-`TotalCount` criterion and are explicitly superseded by the corrected final fixture, not hidden or presented as remaining native leaks.

Build `Probe.csproj` serially with cached dependencies; use `--scope-api`, `--scope-transfer`, `--parameter-count` or `--module-fields` to repeat its specific runtime controls. Existing JSON result paths are protected against overwrite. The initial generic probe still records exploratory `TotalCount` observations; use the corrected regression fixture for the accepted lifetime claim. Snapshot/compare before replacing any evidence. Current upstream [Parameter source](https://github.com/dotnet/TorchSharp/blob/main/src/TorchSharp/NN/Parameter.cs) provides supporting handle-transfer context, but is not claimed to be the exact NuGet source revision; the installed-runtime controls supply version-specific evidence.

The integrator separately owns the full solution regression and frozen-prediction replay before final closure. These tests establish loader ownership behavior, not improved manuscript recognition, specialist adjudication, model promotion or release acceptance.
