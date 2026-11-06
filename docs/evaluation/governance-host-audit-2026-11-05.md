# Read-only hosting enforcement audit — 2026-11-05

Five authenticated GitHub GET requests inspected the existing repository. No
setting, rule, workflow, branch, commit, issue, comment or invitation was changed.

| Observed property | Result |
| --- | --- |
| Repository | Public `QuantForgeSoftware/LeviathanText`, not archived |
| Default branch | `main` |
| Remote main SHA | `413812654cbc2829a5016e78133fa40a8d4ee8fb` |
| Branch protected flag | false |
| Classic protection endpoint |404, specifically “Branch not protected” |
| Repository rulesets | Empty list |
| Registered GitHub Actions workflows |0 |

These observations replace “host enforcement unverified” with a concrete dated
finding: required branch review/status-check enforcement is not configured on
main through the inspected protection/ruleset mechanisms. They do not imply that
public visitors have write access. Collaborator/team permissions were not audited,
and external DCO-app installation was not checked. A DCO integration alone would
not make its status mandatory without an applicable hosting rule.

The project has Apache-2.0 licensing, a lead-maintainer governance model, DCO1.1
and contribution-evidence policy. Those govern the official project; Apache
licensing permits independent forks. The inactive functional CI example remains
under `docs/governance/`, so it creates no actual hosted check. Owner decisions
about trusted checks, reviewer identities, DCO validation and negative enforcement
tests remain in the [hosting runbook](../governance/github-enforcement.md).

Evidence: `.artifacts/governance-host-audit-2026-11-05/receipt.json`, repository,
main, ruleset and workflow JSON, plus the exact classic-protection404 response.
The CLI resolved existing credentials internally; no credential values were read,
printed or saved by the audit. This report does not authorize a remote mutation.
