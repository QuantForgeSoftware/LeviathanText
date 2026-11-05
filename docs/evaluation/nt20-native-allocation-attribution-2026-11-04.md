# NT-20 native allocation attribution — 2026-11-04

Repeated construction and disposal of TorchSharp modules accounts for a reproducible native allocation increase in the local macOS ARM64 OCR model lifecycle. Darwin's leak scanner independently reported unreachable allocations with roots in `THSNN_custom_module` and `make_sharable_string` after the OCR model had been disposed. The attribution covers these measured leak roots. Accounting for the remaining RSS growth in the sustained mixed workload is still open.

## Replicated process measurements

Two fresh processes per route each ran 12,000 operations with one Torch thread and a 180-second cap. The second round used the reverse route order. The registered harnesses pin source, executable, managed and native dependencies, and the exact previously used secondary P46 checkpoint and truncated fixture. The routes measure lifecycle operations only. Every operation closes its tensor scope and returns to the same TorchSharp live-wrapper count.

The native sampler reads `TASK_VM_INFO` and process-wide Darwin `malloc_zone_statistics(NULL)` from inside the child. The table compares cycle 500 to the final sample taken one second after two diagnostic full collections and finalizer waits. The live heap estimate is taken separately from allocator in-use bytes, allocation block counts and resident memory. Collections are confined to this probe.

| Route | Added malloc blocks, round 1 / 2 | Malloc in-use MiB change | Resident MiB change |
| --- | ---: | ---: | ---: |
| Idle scope | 1,595 / 1,644 | +0.77 / +0.76 | +3.42 / +3.42 |
| Weight digest | 2,989 / 2,959 | +3.67 / +3.67 | +12.80 / +12.67 |
| Package metadata and label hash | 1,002 / 1,010 | −1.53 / −1.53 | −3.00 / −2.67 |
| Construct/dispose CRNN | 392,231 / 392,237 | +25.27 / +25.58 | +28.59 / +29.17 |
| Raw weight load | 393,969 / 393,974 | +25.96 / +25.96 | +29.39 / +29.33 |
| Raw truncated weight load | 395,864 / 395,864 | +25.99 / +25.68 | +29.62 / +28.66 |
| Valid package load | 392,464 / 393,142 | +16.51 / +16.75 | +22.16 / +22.42 |
| Truncated package load | 393,185 / 393,257 | +19.13 / +19.20 | +22.44 / +22.81 |

The CRNN construction routes' managed heap estimates fell about 8.6 MiB from cycle 500 to the final post-GC sample, and the last GC's reported committed size was approximately unchanged. Digest-only grew RSS with a different signature, including +8.09 MiB managed GC commitment in both repeats. Treat each route's change as its own observation because allocation and working-set effects differ.

A second fixed matrix used public TorchSharp constructors without package I/O or CRNN logic. Over the same interval, the repeats added about 91,164 blocks for Conv2d, 92,220 for Linear, 47,957 for LSTM, 23,490 for MaxPool2d and 23,536 for an empty custom module. The `ones(128, 64)` control added about 1,747 blocks. The repeated CRNN construction control reproduced roughly 392,244 added blocks. Each of the 32 fixed processes across both matrices exited zero.

## Native allocation stacks

Each targeted scanner process ran 1,000 cycles, then waited after disposing its scopes and running two full collections. Darwin `leaks` with MallocStackLogging classified these process allocations as unreachable.

| Route | Unreachable blocks | Bytes | Observed stack roots |
| --- | ---: | ---: | --- |
| Plain tensor | 0 | 0 | None |
| Empty custom module | 2,000 | 80,000 | `THSNN_custom_module` |
| Linear module | 1,084 | 39,680 | `THSNN_custom_module` |
| CRNN, initial | 25,481 | 1,685,872 | `THSNN_custom_module`, `make_sharable_string` |
| CRNN, replicate | 25,480 | 1,685,472 | `THSNN_custom_module`, `make_sharable_string` |

The installed native library disassembly calls `make_sharable_string` from `THSNN_Module_get_named_parameters`. The installed TorchSharp managed assembly copies returned parameter names with `Marshal.PtrToStringAnsi` in `_named_parameters` and leaves the name pointers allocated there. The native scanner reports that same call chain. The custom-module allocations also reproduce through the empty TorchSharp subclass, independently of LeviathanText model loading. The upstream managed source shows the same parameter-name copy pattern. [TorchSharp Module source](https://github.com/dotnet/TorchSharp/blob/main/src/TorchSharp/NN/Module.cs#L529-L548).

The initial ancillary `malloc_history` invocation exited nonzero because this macOS version uses `-allByCount`. Its failure receipt is retained. The independent `leaks` scans succeeded, returning 1 when leaks were present and 0 on the tensor control.

## Disposition

The evidence supports an upstream dependency investigation. Application wrapper disposal balances throughout, and extra full collections leave the traced native leak roots. Validate any proposed fixed dependency against the same isolated constructor controls, exact package/prediction parity, and a registered sustained worker comparison before changing the release gate. The current evidence is scoped to macOS 27.0.1 arm64, TorchSharp 0.107.0 and libtorch CPU 2.10.0. Full-page, concurrent worker and cross-platform resource validation remain separate.

Local evidence: `.artifacts/experiments/nt20-native-attribution-v1/` and `.artifacts/experiments/nt20-module-isolation-v1/`, including registration JSON, all process receipts and raw Darwin stack scans under `native-trace/`.
