# LeviathanText governance

Project steward: **QuantForgeSoftware**. Lead maintainer: **John Baima**.

## Open software and an accountable official project

LeviathanText's original software and accompanying project documentation are licensed under [Apache License 2.0](LICENSE), subject to the separately licensed material described in [NOTICE](NOTICE) and the [README](README.md#license-and-contributions).

Anyone may use, modify and distribute the software as the license permits. These governance and contribution rules apply to acceptance into the official repository and to official releases. They add no conditions to the software license and do not require permission for independent forks. Contributions do not confer ownership of the project, voting rights, merge access or a right to acceptance.

The official project prioritizes Koine and Classical Greek manuscripts, papyri and inscriptions, using C# for first-party backend/training code and Angular/TypeScript for its browser interface. AI-assisted development and user-selected AI providers are welcome. Human contributors remain responsible for their submissions.

## Authority and responsibilities

The lead maintainer has final authority over project direction, supported capabilities, architectural decisions, contribution acceptance, merge permissions, releases and appointment or removal of maintainers. Passing checks is necessary, but does not require acceptance of a contribution that is outside the project's scope or introduces unnecessary complexity.

The lead maintainer may delegate defined responsibilities in a reviewed change to this document. No additional maintainer or delegated merge authority is established by this initial policy. Repository administration and release credentials remain limited to explicitly authorized people or services.

Contributors own the preparation, provenance, tests and reproducible evidence for their changes. Reviewers verify the claimed improvement and assess regression risks. A designated Greek reviewer adjudicates scholarly labels and conventions; a model's output or a contributor's assertion is not a substitute for that review. Evaluators control held-out acceptance data and record any reuse or exposure.

## Acceptance and releases

All code contributions must satisfy [CONTRIBUTING.md](CONTRIBUTING.md), including a demonstrated bug correction, performance improvement or broader text-processing capability and the applicable regression checks. These requirements apply to maintainers as well as outside contributors and to human- and AI-assisted changes alike.

External contributions require approval from the lead maintainer or an explicitly appointed reviewer before merging. Maintainer-authored changes receive independent review when another authorized reviewer is available; otherwise the maintainer records the self-review and all required evidence. A self-review does not waive tests, benchmark evidence, provenance or DCO certification.

Review records must identify the candidate revision and the evidence used. Substantive changes after approval require renewed review. Integrate parallel work one change at a time or through a tested merge queue, checking the combined result against the current target branch. Do not treat two individually passing branches as proof that their combination passes.

Known functional regressions, verified performance degradation, weakened tests, contaminated acceptance data and unresolved rights issues block acceptance. An improvement in one metric or material does not compensate for a regression in another supported behavior. Performance tolerances distinguish measurement noise from degradation; they must be established before evaluating a contribution and must not be loosened to make it pass.

When essential evidence is unavailable, record the gap and keep the change under review. Do not represent skipped or unavailable checks as passes. Unsupported materials/platforms and incomplete evaluation remain explicit limitations; expanding them requires the corresponding evidence. The lead maintainer approves official releases and their documented support claims.

## Repository enforcement

Policy files describe the requirements; they do not configure a hosting service. At policy adoption, this checkout has no configured Git remote. The following hosting controls remain to be configured and verified by the lead maintainer:

- Require pull requests for external changes to the default and release branches, with designated maintainer review.
- Configure actual maintainer accounts in `CODEOWNERS` or the host's equivalent after verifying their identities and repository permissions. Do not use guessed accounts or placeholder owners.
- Require successful functional/regression checks, applicable performance and text-coverage evidence, and DCO validation. Use trusted check providers and verify that required jobs actually ran.
- Invalidate stale approvals after substantive updates and require checks against the current target branch or merge-queue result.
- Restrict direct pushes, force pushes, deletion of protected branches and release publication to the intended workflow. Prevent bypass of required quality checks, including by administrators and automation.
- Keep dependency/build execution for untrusted contributions isolated from release credentials and private data.

Until these controls are active, the maintainer must verify the same evidence manually before accepting changes. No automated enforcement or CI coverage is claimed by this document.

## Decisions and policy changes

Use an issue or pull request for a proposed change, its evidence and review. Record acceptance, requested corrections or rejection with a brief reason. Contributors may request reconsideration with new evidence; the lead maintainer resolves disagreements.

Changes to this policy or the contribution requirements require explicit lead-maintainer approval and a dated revision. A contribution may not quietly change its own acceptance criteria. A new policy cannot withdraw rights already granted under Apache 2.0.

Project names, logos and claims of official endorsement are separate from permission to use the code. Apache 2.0's trademark provision applies; this document does not claim trademark registration or create additional restrictions on lawful attribution.
