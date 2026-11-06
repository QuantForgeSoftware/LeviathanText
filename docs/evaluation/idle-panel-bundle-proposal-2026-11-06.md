# Rejected isolated idle-panel bundle proposal

Do not adopt this candidate. Its initial bundle reduction is only698bytes against the required6,679bytes, while total emitted JavaScript/CSS grows11,424bytes. Both builds retain the unchanged500,000-byte initial warning budget and both exceed it. Main source and production output were not replaced.

The matched copied trees use the final624-test SSE source base. They contain real source copies, with only node_modules linked to the existing pinned dependencies. The only production-source difference is App's template: three idle defer blocks around run history, account/audit tools and rendering diagnostics, with original DOM order and explicit loading/error placeholders. Core workspace/jobs/auth/invitation components stay eager. No new dependency was added.

| Actual build output | Matched baseline | Isolated candidate |
|---|---:|---:|
|Initial JavaScript/CSS bytes|506,679|505,981|
|Above unchanged warning budget|6,679|5,981|
|Total emitted JavaScript/CSS bytes|506,679|518,103|
|Emitted JavaScript files|2|8|
|Estimated initial transfer (builder, not measured latency)|126.04kB|130.10kB|

Both production builds exited successfully with their warnings retained. Initial graphs were computed from actual esbuild entry points and recursive static imports, excluding dynamic imports; total code counts actual emitted browser files, not intermediate component-style outputs. Stats files and all build logs remain in `.artifacts/experiments/frontend-idle-panel-proposal-v1/`, with `build-comparison.json` documenting exact paths/hashes and input contributions.

Esbuild attributes11,252 initial bytes removed to the four panels, but adds7,017bytes in Angular core's deferred-view implementation and1,018bytes in App instructions, with further shared-output/minification changes. Source file lengths were correctly treated as insufficient evidence of final savings. These contribution figures describe this bundle graph, not runtime CPU or browser performance.

Root rejected the candidate at its primary benefit gate and stopped further DOM/defer guard work. Eventual rendering, failed chunks, pending-import permission checks and an actual production-browser trial were therefore not run for this prototype; no behavior or latency acceptance is claimed. Existing final main frontend624/624 and independent SSE browser evidence belong to the unchanged production source, not this rejected copy.

No budget increase, main App edit, lazy transport proxy or additional target expansion was made. The original initial-size warning remains open for a separately designed change. [Angular20's deferred-loading guide](https://v20.angular.dev/guide/templates/defer) explains the mechanism; this negative measured build result determines the project's decision.
