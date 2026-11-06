<!-- Copyright (c) 2026 QuantForgeSoftware. All rights reserved. -->
# Angular CLI transitive security update candidate — 2026-11-05

## Result and scope

An isolated copy of `web` validated a bounded override candidate that reduces the
November 5 audit from **16 moderate findings to zero** without changing Angular
20.3.42 or application source. The full **149 browser tests pass** and the
production build succeeds without warnings. After approval, the exact bounded
package/lock pair was adopted in `web`, followed by clean installation, audit,
consumer regression, full browser suite and build. The live checkout also passes
149/149 tests and builds without warnings using the actual installed **Node
22.22.2** executable as well as host Node 26.8.1. No Angular major upgrade was
required.

The 16 findings represent two underlying advisories and their dependent package
chains, rather than 16 independent defects. Both are CLI/development dependencies:

| Package | Installed affected version | Advisory | Published patched version |
| --- | --- | --- | --- |
| glob | 13.0.6, two copies | [GHSA-3g3m-2fjr-6pw4](https://github.com/isaacs/node-glob/security/advisories/GHSA-3g3m-2fjr-6pw4): aborted walks can continue filesystem work | 14.0.2 |
| minimatch | 10.2.6, four copies | [GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249): pattern preprocessing can amplify work | 11.0.1 |

Official npm registry queries found glob 13 releases ending at 13.0.6 and
minimatch 10 releases ending at 10.2.6 on this date. No published same-major
patched backport was found. The maintainer's [minimatch 11 README](https://raw.githubusercontent.com/isaacs/minimatch/v11.0.1/README.md)
also says its new matching implementation will not be backported. A private
unpublished backport was not created.

## Exact bounded candidate

The candidate adds this root-package configuration:

```json
"overrides": {
  "@angular/cli": {
    "glob@^13.0.0": "14.0.2",
    "minimatch@^10.0.0": "11.0.1"
  }
}
```

The selectors apply only to the audited source majors under Angular CLI.
Karma's glob 7.2.3 and minimatch 3.1.5 stay unchanged. Future different major
versions do not silently inherit this override. No `npm audit --force` or Angular
22 migration was used.

The actual affected graph remains:

```text
@angular/cli 20.3.42
└─ pacote 21.5.1
   ├─ @npmcli/package-json 7.0.5 → glob 14.0.2 → minimatch 11.0.1
   ├─ cacache 20.0.4 → glob 14.0.2 → minimatch 11.0.1
   ├─ npm-packlist 10.0.4 → ignore-walk 8.0.0 → minimatch 11.0.1
   └─ sigstore 4.1.1 → @sigstore/tuf 4.0.2 → tuf-js 4.1.0
      └─ @tufjs/models 4.1.0 → minimatch 11.0.1
```

Exactly eight lockfile package locations change from the compatible-update
baseline: two glob copies, four minimatch copies, path-scurry 2.0.2 → 3.0.0,
and new `@isaacs/nonrecursive-glob-match` 1.0.3. No other package version changes.
glob, minimatch and path-scurry declare Node `18 || 20 || >=22`; the added matcher
does not declare an engine constraint in its installed package metadata.

## Compatibility assessment

These are real major updates. The [glob 14 maintainer changelog](https://raw.githubusercontent.com/isaacs/node-glob/v14.0.2/changelog.md)
documents Matcher objects replacing regular expressions, changed internal
Pattern/Processor interfaces, removed debug support, changed walker result
containers, and different rooted-ignore/Windows UNC handling. minimatch 11
removes the prior makeRe/matchOne surface and changes pattern matching internals.
The candidate is not a general claim that arbitrary glob/minimatch consumers can
be upgraded unchanged.

Inspection of the four installed direct consumers found their used interfaces
still present: package-json/cacache call `glob()`, ignore-walk uses
`Minimatch.match()` and `globParts`, and TUF calls the exported `minimatch()`.
Generated fixtures exercised these actual consumers in both installed graphs:

- package-json preparation discovers README, bin directory and gyp files;
- ignore-walk honors excluded directories, wildcard exclusions and a negated
  inclusion, including a Greek filename;
- pacote produces a local tarball with asserted included/excluded members;
- cacache writes content, verifies it through its glob traversal, and removes it;
- TUF delegated-path matching accepts the expected Greek text path and rejects
  wrong extensions, directories and extra path depth.

Baseline and candidate smoke outputs are byte-identical. This covers the
specific dependency calls beyond the application's browser suite. It does not
exercise every `ng add`, migration, registry/signature-network operation,
glob expression, platform path or upstream package test.

## Verification receipts

All artifacts below are under `.artifacts/npm-security-candidate/` (generated,
not release inputs):

| Evidence | Result / artifact |
| --- | --- |
| Original compatible-update audit | `.artifacts/midday-verification/npm-audit-updated.json`: 16 moderate, zero high/critical |
| Candidate package and reproducible lock | `web/package.json`, `web/package-lock.json` |
| Full browser suite | `browser-tests.log`: 149/149 passed |
| Full initial candidate build | `build.log`: success, no warnings |
| Tightened selectors, fresh install | `bounded-lock.log`, `bounded-ci.log`: success |
| Tightened candidate audit | `bounded-audit.json`: zero vulnerabilities; metadata 629 dependencies (10 production, 620 development, 105 optional; overlapping categories) |
| Exact package graph | `bounded-dependency-after.txt`, `lock-changes.json` |
| Graph identity after tightening | `bounded-verification.json`: all 630 lock `packages` entries byte-equivalent to the graph used by the 149-test run |
| CLI command discovery and final build | `bounded-cli-help.log`, `bounded-build.log`: success, no build warnings |
| Downstream consumer parity | `consumer-smoke.cjs`, `smoke-before.json`, `smoke-after.json`: identical asserted results |
| Official release comparisons / backport query | `research/*-compare.json`, `research/*-versions.json`, `research/glob-changelog.md` |
| Adopted checkout, clean host installation and audit | `main-ci.log`, `main-audit.json`: success, zero findings |
| Adopted checkout, host consumers/browser/build | `main-consumers.log`, `main-browser-tests.log`, `main-build.log`: consumer assertions pass, 149/149 tests, no build warnings |
| Adopted checkout, actual Node 22 executable | `main-node22-consumers.log`, `main-node22-browser-tests.log`, `main-node22-build.log`: consumer assertions pass, 149/149 tests, no build warnings |
| Regression script argument/output safety | `script-safety.json`: invalid flags/arguments/missing values/existing results directories rejected; retained result and sentinel unchanged; fresh results directory succeeds |

The initial install/build/browser commands ran on macOS arm64 with host Node 26.8.1,
npm 11.19.0 and Chrome Headless 156. The additional consumer smoke ran under
sandbox Node 22.22.2/npm 10.9.7 on the same host; the different executable paths
were checked explicitly. The adopted checkout's second browser/build run invoked
`/Users/johnbaima/.hermes/node/bin/node` directly, so Node 22 verification does not
depend on relabeling an environment variable. No Linux/Windows result is claimed.

## Licenses and remaining gates

All eight changed/added package locations declare `BlueOak-1.0.0`; the new matcher
ships `LICENSE.md`. Its [official license](https://blueoakcouncil.org/license/1.0.0)
requires recipients of redistributed copies to receive the license text or its
official link. Preserve this entry in development-toolchain notices/SBOM if that
toolchain is distributed. The root `NOTICE` now records the new matcher and this
official link as development-toolchain attribution. Application-bundle inclusion and notice inventory must
be checked by the packaging step; a development dependency alone does not prove
it is present in the shipped browser bundle.

The consumer smoke is now maintained at `scripts/verify-npm-consumers.cjs`,
registered as `npm run verify:consumers` in `web/`, and documented in README.
It requires the actual current hoisted CLI consumer packages and deliberately
fails if their resolution layout changes. Default fixtures use `mkdtemp` and are
cleaned in `finally`; optional `--results-dir` must name a fresh directory, and
caller directories are never recursively removed. Generated fixtures remain
offline and prepare scripts are disabled during tarball creation.

Remaining work: preserve the existing license notice in distributed development
toolchain inventories, and keep Windows/Linux toolchain and unusual ignore-pattern behavior as
explicit platform gates. Review/remove the override when the maintained Angular
CLI dependency chain declares patched majors itself. Audit zero is the observed
registry result on November 5, not a guarantee against future advisories.
