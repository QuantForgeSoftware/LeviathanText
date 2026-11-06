# Manual form acknowledgement preservation

A successful older annotation acknowledgement erased newer form input in the actual LineEditor. The narrow correction now clears each field only when its current raw value still equals the value captured for that submission. Different newer note/alternative/expansion values survive; unchanged submitted values still clear normally.

This is per-field raw-string equality, not form-generation or ABA identity. If a value changes away and back to the submitted value, it follows the existing clear behavior. The fix adds no uncertain-write lock, operation receipt, replay, atomic expansion transaction or shared mutation mutex. Those remain in the separate [manual uncertain-outcomes brief](../tasks/manual-annotation-uncertain-outcomes-2026-11-06.md).

## Actual before and isolated comparison

Current Angular source/configuration was copied into `.artifacts/experiments/manual-form-ack-v1/original/` before testing, with only node_modules linked to the existing installed toolchain. No production source or test was changed to obtain the before result. Four new cases exercise the real component, actual DOM events, explicit trusted workspace fixtures and recording RxJS Subjects:

- Submit a manual AlternativeReading annotation, edit note/alternative afterward, then acknowledge the old request: the original erased both newer values.
- Submit abbreviation L2 expansion, edit note/expansion while the first leg is held, acknowledge L2, then acknowledge the linked annotation: the original erased both newer values.
- The corresponding two unchanged-form controls clear the submitted values normally.

The clean first invocation executed all four: **2 intended behavioral failures and 2 passing controls**. All frozen payload, L2-linkage and unchanged-L1 assertions passed. There was no missing API/helper compilation failure. Original tests, sources, logs and results remain preserved.

A separate candidate copy changes only the equality-conditioned clearing and captures raw expansion/note values before existing trim/null conversion. The exact same four test bytes pass **4/4**. API payloads, captured ranges/anchors, L2 revision linkage and ordinary clear behavior remain intact. There was one original and one isolated candidate invocation, with no adaptive cases or thresholds.

Each invocation had a 60 s wall watchdog, 45 s per-process CPU cap, observed 1 GiB owned-tree RSS guard and 256 KiB combined log bound. They completed in about 2–3 s without a limit; all observed owned processes were cleaned and no tracked process remained. These observations are functional safeguards, not performance or native-memory acceptance. Registered original/candidate source hashes remain unchanged. Runtime occurred at 10:38–10:39 UTC; subsequent main gates finished around 10:41, before the 10:45 new-runtime cutoff. No providers, models, corpus or training were used.

## Reviewed integration

Root reviewed and approved the exact patch and four permanent DOM cases. Integration first verified that main LineEditor bytes still matched the original copy, then copied the exact candidate hunk and unchanged test source. Permanent source: `web/src/app/manual-form-ack.spec.ts`. The complete frontend passes **643/643**; production build passes with the unchanged budget warning: **507,074 initial bytes**, 7,074 above 500,000. The two NG0956 test warnings also appeared in the prior 639-test gate; no warning suppression or budget increase was made.

Final immutable source/configuration/assets are registered in `final-source-dist-freeze.json`, SHA-256 `960332485b1e4db0eec73652131e6ba4b16dd5919d07202bc848f23f7053e7e5`: 115 source/config files and 5 dist files. Assets are copied to `dist-final/`. The independent current-source 38-check browser replay uses a separate registration; do not relabel the earlier 639-source proof as coverage of these new assets.

Evidence: `before-registration.json`, `original.log`, `original-execution.json`, `candidate-registration.json`, `candidate.log`, `candidate-execution.json`, `equal-submitted-only.patch`, `main-integration.json`, `main-full-frontend.log`, `main-production-build.log` and `proof.json` under the artifact root. The broader annotation/L2 unknown-outcome and two-leg partial-commit findings remain open; this result closes only the tested newer-different-form clearing defect.
