# Derivative crop bounds and ownership: isolated functional proof

Completed 2026-11-06 04:12 UTC. These are functional checks on generated 2×2 Skia images. They do not measure a process leak, throughput, resident memory, corpus behavior, or model performance. No fitting or external provider calls were involved. Production files were not changed by this proof.

The preregistered source and execution records are `.artifacts/experiments/derivative-functional-proof-v1/prepared-source.json` and `execution-registration.json`. `execution-results.json` retains every command, exit status and test outcome; individual TRX files and complete logs remain alongside it. Four serial isolated builds succeeded with zero warnings and errors. Only the two explicit derivative test classes were compiled; the six fitting-class exclusions were also supplied to the test runner.

| Cohort | Outcome | Meaning |
| --- | --- | --- |
| Actual preserved original Imaging DLL: crop class | 9 passed, 4 failed | All four overflowing left/top coordinates returned without the required typed refusal. Existing invalid bounds and valid edge crops passed. |
| Same original DLL: public ownership controls | 2 passed | Empty transforms preserve source identity; real crop/deskew/crop preserves exact pixels and metadata. |
| Source clone with crop-predicate-only patch | 13 passed | Subtraction-based bounds reject overflow while preserving the nine controls. |
| Pure extraction of old ownership algorithm | 4 passed, 4 failed | Replaced intermediates, later-transform failure, metadata failure, and returning the source alias leave owned bitmap handles undisposed. |
| Source clone with ownership patch | 8 passed | The four failures are corrected; source, identity-step, first-failure, final-return and public controls remain valid. |

The original binary is copied rather than rebuilt: its SHA-256 is `28f570f44400367f98ff51fea6775553fc2a5e48012365c308f4b0fbfe53af8b`, verified again in the original test runtime before execution. Original crop failures therefore demonstrate the actual public implementation. Ownership's observation seam is absent from that binary: its negative cohort is explicitly a **pure source extraction**, with the old loop unchanged and a per-call transform delegate added. Missing-helper failures were not used as evidence. Both public ownership controls separately ran against the actual original binary.

The ownership tests inspect real `SKBitmap.Handle` disposal and clean up recorded tiny bitmaps even when assertions fail. They establish deterministic ownership behavior, not a measured native memory leak. The candidate keeps the caller's source alive, disposes each replaced owned intermediate, retains the final output for its caller, and cleans up owned output on a later transform or metadata-publication exception. It does not fix an allocation made inside a transform that throws before returning it; native crop/canvas exception ownership remains separate.

The independently scoped patches are `.artifacts/experiments/derivative-crop-bounds-v1/crop-bounds.proposed.patch` and `.artifacts/experiments/derivative-ownership-v1/combined-ownership.proposed.patch`. They must be merged as hunks into the current production file; copying either complete snapshot would risk overwriting unrelated decoder changes. Crop bounds were deliberately left unchanged in the ownership extraction/candidate, preserving the separation of the proofs.

`closure.json` confirms all registered source and original-binary files remained byte-identical. Its results anchor is `e38511817da4aac18217da50817323cd9a58ebb07cf122189ccfd69d47ee70e1`. Each built runtime has its own pre-execution binary seal. Main integration and its full regression gate remain the parent agent's separate step.
