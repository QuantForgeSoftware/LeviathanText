## Problem and demonstrated improvement

Describe the concrete defect, performance limitation or unsupported text/condition and the resulting behavior. Link an issue if available.

- [ ] Bug correction
- [ ] Performance improvement
- [ ] Broader text support
- [ ] Documentation-only change (explain the document checks; no runtime benefit claimed)

## Reproduction and comparison

- Baseline commit/content hash:
- Candidate commit/content hash evaluated:
- Exact commands/configuration and environment:
- Input dataset/model versions, hashes and evidence locations:
- Expected result and who checked it:

| Measure or behavior | Baseline | Candidate | Comparison method / evidence |
| --- | --- | --- | --- |
| Claimed improvement | | | |
| Existing correctness / text quality | | | |
| Relevant latency / throughput | | | |
| Relevant memory / resources | | | |

For a bug, show the regression test failing on the baseline and passing on the candidate. For speed, report repeated runs, warm-up, variation and the pre-established tolerance. For text support, describe representative examples, ground-truth review and results on existing profiles. Mark irrelevant rows with a reason; do not invent results.

## Regression evidence

- Tests/checks run and their results:
- Relevant supported platforms and text profiles evaluated:
- Per-material results and visible-mark / uncertainty effects:
- Benchmark configuration, tolerance and source of the baseline:
- Failures, skipped/unavailable checks and limitations:

## Scope, data and provenance

- Owned package/paths and contract version:
- Shared contracts, dependencies, formats or compatibility affected:
- Reused code or assets, sources, licenses and attribution:
- Dataset split/overlap checks and any acceptance-set exposure:
- Remaining risks and follow-up work:

## Contributor checklist

- [ ] I read [CONTRIBUTING.md](../CONTRIBUTING.md) and [GOVERNANCE.md](../GOVERNANCE.md).
- [ ] The submission demonstrates a qualifying improvement, or is documentation-only with appropriate verification.
- [ ] I supplied reproducible before/after evidence and the applicable regression/performance checks.
- [ ] I have not hidden regressions through pooled averages, reduced quality, weakened tests or skipped checks.
- [ ] I distinguished source evidence from editorial/AI output and documented any provisional ground truth.
- [ ] I did not tune against locked acceptance data; any exposure or reuse is disclosed.
- [ ] I identified third-party code/data/model rights and preserved required notices.
- [ ] I can truthfully certify [DCO 1.1](../DCO). Every commit I authored or modified has my own sign-off; I preserved prior valid sign-offs and rely on another contributor's sign-off only for their unchanged work.
- [ ] I reviewed and take responsibility for all submitted work, including AI-assisted output.
- [ ] I included no credentials, confidential data or unauthorized large artifacts.

## Maintainer review

- Reviewed candidate revision and target-branch revision:
- Independent evidence verification and any required reruns:
- Decision, reasons and unresolved blockers:

Passing checks does not itself authorize merging. Missing required evidence or a known regression blocks acceptance under the project policy.
