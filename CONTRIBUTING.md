# Contributing to LeviathanText

Read [GOVERNANCE.md](GOVERNANCE.md), [LICENSE](LICENSE), [DCO](DCO) and the relevant contracts in [docs/contracts/](docs/contracts/) before submitting a contribution.

## Acceptance rule

Every code contribution must demonstrate a specific improvement through reproducible evidence: correction of a defect, improved performance, or successful processing of previously unsupported texts or conditions. Contributions must preserve existing supported behavior and satisfy the project's regression and performance requirements. Passing checks is necessary but does not guarantee acceptance. The lead maintainer retains final authority over scope, design, merging and releases.

These are contribution requirements for the official project, not extra conditions on use, modification or redistribution under Apache 2.0. AI-assisted contributions are welcome and must meet the same requirements as other submissions.

## Scope and coordination

Keep changes focused and explain the user-visible benefit. Discuss architectural changes, new dependencies, broad refactoring, new platforms or changes to transcription/scoring rules with the maintainer before a large implementation. Pure code churn does not itself demonstrate an improvement. Documentation-only changes need appropriate document/link checks, not fabricated runtime benchmarks.

First-party backend, CLI, preprocessing and training code is C#. Browser code is TypeScript/Angular. Keep the product independent of Kraken implementations and runtimes. Preserve source images, diplomatic text, editorial readings, uncertainty, provenance and user choice of AI providers.

For parallel work, claim a package and its paths, record the base revision and contract version, and edit only that scope. Route shared contracts, central build files, schema changes and cross-package edits through the integrator. Provide a handoff with changed paths, evidence and unresolved dependencies. See [todo.md](todo.md) for current ownership and delivery gates.

## Evidence for the claimed improvement

Each code pull request must qualify under at least one category below. All categories also require the regression checks in the next section.

| Claim | Required evidence |
| --- | --- |
| Bug correction | A minimal reproduction and a meaningful regression test that fails for the intended defect on the named baseline and passes on the candidate. Explain expected behavior and show before/after results. The test must exercise behavior rather than mirror the implementation. |
| Performance improvement | A reproducible before/after benchmark on the same hardware, workload, runtime/build settings, model and output-quality requirements. Include repeated measurements, warm-up procedure, sample count, timing variation and memory/resource results. Demonstrate improvement beyond measurement noise without reducing correctness or recognition quality. |
| Broader text support | Legally usable representative examples, independently checked expected outcomes and tests showing the baseline's failure or limitation and the candidate's improvement. Describe the material, witness/hand, imaging conditions, label policy and limits of the new support. Re-evaluate existing supported text profiles. |

Record baseline and candidate commit IDs (or immutable content hashes before a commit exists), exact commands/configurations, environment, dataset/model versions and hashes, results and how a reviewer can reproduce them. Keep large corpora and models in their approved artifact store; link manifests and evidence instead of committing them to the repository.

For a bug or support test that cannot execute unchanged against the baseline, provide an equivalent baseline reproduction and explain why. An unrelated compile error is not evidence of the claimed bug. A new test that passes both versions does not establish that a correction occurred.

## Regression and performance gates

Run existing functional tests and the relevant integration checks for the changed behavior, plus the applicable supported-platform and performance checks. For changes that can affect recognition, layout, preprocessing, decoding or training, evaluate all affected existing manuscript, papyrus and inscription profiles. Explain any unaffected suites omitted from a costly evaluation so the reviewer can assess the scope.

- Compare against the current target branch; retain benchmark inputs, outputs and configuration so changes in the environment cannot masquerade as improvements.
- Preserve existing functionality, interchange semantics, Unicode/diacritic handling, source-coordinate mapping, revision integrity and scholarly distinctions.
- Report results per material and relevant hand/capture stratum. A pooled average must not hide a regression. Speed gains must not come from silently dropping lines, marks, uncertainty or image detail.
- Measure relevant latency/throughput and memory. Establish tolerances and the comparison method before evaluating the change. Tolerances address noise; known slowdowns or higher resource requirements are not dismissed as acceptable merely because another metric improves.
- Do not delete, weaken, skip or rewrite a valid test simply to get a passing result. A correction to erroneous ground truth or a defective benchmark requires independent justification and review, with both old and corrected results disclosed where comparable.
- Keep production code and test/benchmark changes reviewable separately. Show the improvement test against both baseline and candidate.
- Report failed, skipped and unavailable checks explicitly. Dataset-backed tests skipped because assets are absent are not proof of recognition quality. When a required check is unavailable or no valid baseline exists, obtain the missing evidence before acceptance.

No test suite proves correctness on every possible text. Acceptance applies to the documented tests, data and supported profiles; do not claim broader coverage than the evidence establishes. Current pilot measurements are not automatically release thresholds. See [the acceptance plan](docs/evaluation/acceptance-plan.md) and the per-track acceptance sheets in [docs/evaluation/](docs/evaluation/).

## Greek text, data and model evaluation

Keep diplomatic labels distinct from editorial restorations, expanded abbreviations, added word divisions and inferred accents. Retain unresolved readings, annotation completeness and source-image references. Have expected readings checked by a qualified reviewer; document any provisional labels and do not present them as adjudicated ground truth.

Use development data for implementation, prompt tuning, thresholds and model selection. Keep locked acceptance data with the designated evaluator. Group all views, fragments, recto/verso, crops and derived glyphs from the same physical object in one split. Report repeated passages and cross-source overlap. If acceptance data becomes development evidence, disclose the reuse and establish a fresh held-out evaluation before making a new generalization claim.

Report raw recognition separately from language/AI-assisted output, and manual/imported layout separately from automatic layout. Include visible marks, missed lines, blank-region insertions, uncertainty coverage and correction effort where affected. Improvement on a narrow hand or material is a limited support claim, not general Greek recognition.

## License, provenance and DCO sign-off

Original contributions intended for inclusion in the project's Apache-licensed software or documentation are submitted under Apache License 2.0, without additional terms, consistent with its Section 5. Contributors retain their copyright; this project does not require copyright assignment or a separate CLA. The [DCO](DCO) certifies origin and the right to submit; it does not grant additional relicensing rights.

Read DCO 1.1 and add your own `Signed-off-by` line to every commit you author or modify, normally with `git commit -s`. The line has the form `Signed-off-by: Your Name <your-address@example.com>`. This is a certification, not a cryptographic signature. Use an identity and address you are entitled to use, and understand that the contribution record and sign-off are public. Never add someone else's certification. Preserve prior valid contributor sign-offs when integrating or squashing work, and certify your own changes. An existing contributor's sign-off may be relied upon only for their unchanged contributed work; it does not certify your subsequent edits.

Submit work only when you can truthfully certify the DCO, including any necessary employer authorization. Identify reused code and its source/license; preserve notices and document compatibility. Do not assume that publicly visible code is licensed for reuse. Compatible third-party material must remain explicitly identified rather than being presented as original project code.

Images, corpora, transcriptions, annotations and model weights need asset-specific license/provenance records. The software license does not relicense them. Include source/version, exact rights evidence, attribution and permitted intended use. Keep restricted or unresolved assets out of distributed contributions until the rights are established. Ordinary user documents and outputs do not become Apache-licensed merely because LeviathanText processes them.

## AI-assisted work

AI tools may be used for coding, tests, documentation, research and annotation. A submitting human remains accountable for understanding and reviewing the change, its licensing/provenance, security and reproducible evidence. Do not submit unreviewed output, fabricated test results or an AI-generated assertion as independent ground truth. AI-generated training material must be labeled and kept distinguishable from real held-out documents. Never include credentials or confidential data in a contribution or tool transcript.

## Submission and review

Use the [pull-request template](.github/pull_request_template.md). Include the problem, benefit, evidence, affected contracts/data/licenses, regression results and limitations. Link the issue when one exists; a separate issue is not required for every small correction.

The maintainer may request a smaller change, more evidence or a different approach. Address findings and rerun affected checks after changes; approvals apply to the reviewed revision. Do not merge or release your own contribution without the authorization required by [GOVERNANCE.md](GOVERNANCE.md).

The local policy does not install automated checks. Until hosting enforcement is configured, include the same evidence for manual review. Source texts: [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0) and [Developer Certificate of Origin 1.1](https://developercertificate.org/).
