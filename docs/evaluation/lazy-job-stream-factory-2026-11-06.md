# Native-import job stream factory: size gate not met

Status: isolated experiment rejected; no production adoption or behavior acceptance.

The proposal keeps JobsPanel eager but loads its header-stream implementation through a native import on first Watch. It preserves the proposed captured-credential/current-generation boundary and a finite module-loading deadline. These source intentions are not evidence that all behavior gates pass.

Both isolated Angular builds succeed using the same historical source baseline. The unchanged initial warning budget is500000 bytes. Actual emitted JS/CSS file sizes are:

| Measure | Original | Candidate |
|---|---:|---:|
| Initial bytes |506679|501338|
| Total JS/CSS bytes |506679|507470|
| Above initial warning budget |6679|1338|

Candidate initial assets comprise466092 main bytes,34585 polyfills bytes and661 shared-chunk bytes. Its lazy chunk is6132 bytes. Initial bytes fall5341, while total output grows791 bytes. This still misses the primary goal of meeting the500000-byte initial budget. No subsequent behavior qualification or main adoption is justified by these builds; there is no measured user-latency improvement.

The experiment predates the later raw-job authorization corrections. Current production initial assets total506787 bytes. Do not compare the candidate against that different source revision as a matched result.

Evidence remains in `.artifacts/experiments/lazy-job-stream-factory-v1/`: `source-v1.json`, `build-original-v1.log`, `build-candidate-v1.log`, and the original/candidate `dist/web/browser` trees. The historical source metadata's `sizeGate: pending` describes preparation; the emitted sizes establish the failed gate recorded here. Both isolated source trees and failed-gate artifacts remain available for review. Main remains eager.

See the [prospective proposal and ownership](../tasks/lazy-job-stream-factory-2026-11-06.md) and the separate [Angular idle-panel rejection](idle-panel-bundle-proposal-2026-11-06.md). A future design needs its own prospective size and behavior gates; this result does not authorize relaxing the budget or rerunning this candidate until it passes.
