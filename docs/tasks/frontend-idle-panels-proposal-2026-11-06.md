# Isolated idle-loading proposal for the initial bundle warning

The header-stream production build passes but totals506.67kB against the unchanged500kB initial warning budget. Source-only proposal: add three Angular `@defer (on idle)` blocks around RunsPanel, the adjacent UsersPanel/AuditPanel pair, and RenderingCheck in App's template. Keep all existing DOM ordering, authentication/invitation/core workspace/jobs components eager. This is preferable to inventing a lazy transport placeholder with additional abort/current/settled lifecycle rules.

All four proposed targets are standalone and App has no ViewChild or other runtime references to them. Angular20 can split eligible standalone dependencies and load them on idle; deferral does not become a repeating show/hide condition. Explicit loading/error text keeps missing chunks visible as failures. These are framework semantics, not proof of this project's final byte reduction or behavior. [Angular20 deferred-view guide](https://v20.angular.dev/guide/templates/defer).

The four source files total13,130bytes (source text, not minified output). No byte savings are assumed. Exact build/chunk dependency evidence must show more than6.67kB leaves the initial graph and the budget remains unchanged. Shared ApiClient/auth/Angular code may stay eager, so source size cannot establish that result.

Prepare in an isolated copied web tree only after root approval and current security-browser proof closes. Main App/template/guards stay unchanged meanwhile. Candidate artifacts: `.artifacts/experiments/frontend-idle-panel-proposal-v1/{app.before.html,app.candidate.html,app-idle-panels.patch,source-proposal.json}`.

Required controls before adoption: unchanged full frontend suite and production build; deterministic manual defer-state tests for placeholders, completion and error; actual browser eventual run/account/audit/rendering visibility; original component standalone behavior and current credential/permission checks; no eager private request from inactive context. Once loaded, draft-bearing tools must remain mounted through existing boundaries. No new owner/admission policy, lazy project editor or budget increase.

Material tradeoff: these code bytes move to additional chunk requests. A first missing/offline chunk must show the explicit failure state; do not claim stronger offline availability or faster rendering without direct proof. The proposal changes code loading only and makes no performance/latency/security acceptance claim.
