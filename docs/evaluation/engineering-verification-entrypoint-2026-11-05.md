# Reproducible engineering verification — 2026-11-05

`scripts/verify-engineering.sh` builds Release and runs twelve selected C# projects
with the exact non-fitting filter saved beside each suite's TRX/log. It additionally
runs the two individually receipted, explicitly audited pure LabelCodec classes
from the mixed ML project. Model/fitting tests from that project and all pilot
workflows are not executed (the solution build still compiles those projects). The six excluded fitting classes include the non-neural
`LearnedLineDetectorTests`; this corrects the earlier five-class audit boundary.
The script works from another directory, refuses unknown/duplicate flags, fails
on the first failing suite, and requires a fresh named receipt directory so old
verification evidence cannot be overwritten. Default receipts use a fresh temp path.
Restore first. `--no-build` explicitly selects existing Release outputs.

Six process-level wrapper tests use a fake dotnet executable to verify every
emitted test command retains the complete audited exclusions, the selected suite
count, fresh receipt behavior, invalid-input rejection, failure propagation, and refusal of missing/zero-execution/failing TRX receipts despite a zero process exit.
The lightweight counter gate is not a full XML schema validator or a substitute for trusted source review. These command-contract tests do not establish test-suite success by themselves;
actual C# receipts are retained separately under `.artifacts/midday-verification/`.
The final session handoff records the real invocation after source integration.

README now distinguishes these engineering suites from `dotnet test` on the whole
solution, which includes actual synthetic fitting and potentially dataset-backed
pilot runs. Test exclusions are explicit, not proof that an arbitrary future test
cannot perform fitting; new entry points still require inspection under lessons18.

An inactive [CI example](../governance/functional-regression.yml.example) is prepared
for maintainer review. It pins official action tag hashes observed via `git ls-remote`
on2026-11-05, requests read-only repository permissions, disables persisted checkout
credentials, records runtimes, verifies the arm64 pilot architecture, and retains
receipts. GitHub currently documents `macos-15` as an arm64 standard runner.
[Primary runner documentation](https://docs.github.com/en/actions/how-tos/write-workflows/choose-where-workflows-run/choose-the-runner-for-a-job).

The example is outside `.github/workflows`; it is not activated or a remotely passed
check. It covers functional regression only. Contribution-evidence attestation,
DCO-provider selection, reviewer identities, branch/release protection and actual
negative enforcement tests remain owner-governed work in the
[hosting runbook](../governance/github-enforcement.md). A local green run is not
hosting enforcement, scientific acceptance or supported-platform certification.

## Pure codec allowlist follow-up

The remaining generic codec work adds two exact-class selections, each with a
trailing dot in its FullyQualifiedName substring so similarly named classes cannot
match. `Codec.trx` and `CodecInvariants.trx` each require executed cases and zero
failures; a stale binary missing the new class cannot pass merely because the old
class still runs. `codec-filters.txt` retains both selections. The wrapper tests
assert all14 calls, the original six-class exclusions on12 full suites and the
exact two codec allowlists. Independent source review and pure generated probes
confirm no model, tensor or fitting calls in those classes. This is a narrow
exception to wholesale ML-project exclusion, not permission to run that project.

## Default fitting opt-in follow-up (MID-35)

An unfiltered project invocation exposed a gap in the command-filter boundary:
`LearnedLayoutIntegrationTests` and `LayoutModelApiTests` can fit synthetic models;
the latter performs fitting in its constructor. Body-level dynamic skipping would
therefore be too late. The incident's invocation/update accounting is recorded
separately; it is not natural-corpus training or accuracy evidence.

All cases in the six audited classes now use shared discovery-time
`FittingFact`/`FittingTheory` attributes from
[`tests/Shared/FittingTestAttributes.cs`](../../tests/Shared/FittingTestAttributes.cs).
They set xUnit's `Skip` unless the fresh test host sees exactly
`LEVIATHANTEXT_RUN_FITTING=1`. The helper is linked into the Application, API, CLI
and Layout test projects. A skipped case never constructs its test class, so the
API constructor cannot train. This applies to unfiltered project commands,
caller-supplied test filters and `--no-build` once the guarded binary has been built.
Old binaries remain old: `--no-build` cannot add guards to historical outputs.
The engineering script retains its original six exclusions as a second boundary.

After explicit fitting authorization, an operator can enable these cases, for
example `LEVIATHANTEXT_RUN_FITTING=1 dotnet test tests/LeviathanText.Layout.Tests`.
Setting the variable is an execution opt-in, not an authorization grant. Do not set
it in routine verification environments. Listing via `--list-tests` does not run
constructors or fitting bodies.

The bounded proof uses a harmless constructor marker, one guarded Fact, a guarded
two-row Theory and an ordinary control. The before-control, with ordinary xUnit
attributes, passes four cases and constructs the sentinel three times. With the
actual shared helper, both absent and explicitly disabled opt-in produce one
ordinary pass, two discovery skips and no marker; an explicit caller filter still
cannot construct the guarded class. Enabling opt-in only on that harmless probe
passes four cases and constructs it three times. No model, optimizer or training
library is referenced by the probe.

Actual targeted default runs discover and skip Application2, API1, CLI5 and
Layout13 cases (21 discovery cases total, zero test-body executions). A disabled
Theory is represented as one skipped discovery case. Opt-in **listing only** finds
23 expanded cases across those projects (CLI's three-row Theory expands); none
is executed. Builds have zero warnings/errors. Receipts, probe source and hashes
are retained under `.artifacts/fitting-default-guard-review/`, with `proof.json`
and the individual TRX files; these expected all-skipped runs are guard evidence,
not passing functional regression suites. An initial sandbox-blocked testhost and
an initial missing isolated restore are retained as unsuccessful attempts.

**Scope:** this guards these six existing classes only. It does not make
`dotnet test LeviathanText.slnx`, the mixed ML project or the Pilot project safe to
run unfiltered. ML contains additional fitting calls. Most pilot tests currently
skip only when datasets are absent; the locked-evaluation opt-in is not a general
pilot/fitting guard. Those selections still need separate authorization and a
future explicit gating audit. New fitting tests must adopt a guard and extend the
audited inventory; a six-class attribute change cannot prove arbitrary future
code is non-fitting.
