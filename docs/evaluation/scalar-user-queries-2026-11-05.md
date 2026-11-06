# Scalar user queries and SQLite command ownership — 2026-11-05

The API's account-presence guard and known-principal reauthorization no longer load
every account. `LocalStore.HasUsers()` uses `SELECT 1 FROM users LIMIT 1`, including
disabled accounts. `FindUserById` uses the primary-key ID predicate and returns the
same public fields, including disabled state, as the full account listing.

Only the API middleware presence check, job-event stream reauthorization lookup and
worker project-job owner lookup switch to these methods. The administrative listing
still returns all users. Authorization policy, roles, bootstrap behavior, key hashes,
database schema and disabled-account handling are unchanged.

## Semantics and regression evidence

Six infrastructure cases cover an empty store, missing and parameterized exact IDs,
all four roles, Unicode/quoted names, timestamps, disabled-only presence and updates
through a second connection. Two API cases confirm anonymous local mode with no
accounts and rejection when the only account is disabled. Worker authorization adds
a missing-principal case alongside existing member/revoked/disabled/reader/admin and
unbound compatibility controls.

The first full infrastructure run passed 43/43 and the worker authorization set
passed 8/8. The targeted API run passed 13 cases, including the new bootstrap guard,
existing authentication and SSE controls. Four existing membership cases failed at
their import fixtures under isolated build outputs. An independent owned API
reproduction confirms the calculated fixture path is
`/private/tmp/LeviathanText.Formats.Tests/Fixtures/sample.page.xml`, which is absent;
the API correctly returns `400 inputMissing` with “Page XML path must be absolute
and readable on the server host.” The source fixture exists. These four results
are retained as fixture failures, not passing authorization evidence; normal-output
integration verification is tracked separately.

## Prespecified query comparison and discovered confound

The first frozen-source comparison used generated 1/100/10,000-account stores,
64 warmups per route/query, four paired ABBA measurements and 256 calls per block.
It checked every result, including account ID/name/role/timestamp/disabled state,
and recorded elapsed time, thread-allocated bytes and GC counts. All 12,288 measured
calls matched. At 10,000 accounts the presence query's median changed from
6,041.82 to 4.40 µs and approximately 3,123,604 to 720 allocated bytes per call;
the identity query changed from 5,962.50 to 6.83 µs and 3,123,685 to 1,237 bytes.

The first one-account identity result was slower, 9.77 to 19.48 µs. A separate
fresh-process diagnostic with two-second interleaved warmups worsened the small
candidate results instead of resolving the question: candidate presence/identity
reached 56.35/88.04 µs at one account. Both historical comparisons shared a
connection between legacy baseline calls and the new disposed commands. Those
small-store timings cannot establish a regression or speedup for the scalar query
in isolation.

Inspection and a dedicated provider-registry probe found the actual contamination:
50,000 `LoadUsers` calls grew the connection's weak command registry from 4 to
50,004 entries. The same `HasUsers` query measured 5.36 µs before those calls and
47.56 µs afterward. A peer connection against the same database retained one entry
and measured 4.31 µs. Explicit disposal of a new command encounters the accumulated
registry even though its own registration is released. Dead weak references still
occupy the registry; these counts do not mean all command objects remain live.

The next comparison retains the exact old frozen production source but gives the
baseline and candidate independent, equally warmed connections to the same immutable
generated database. It records both registry counts outside timed blocks. Separate
fresh processes run cardinalities 10,000, 100 and 1; each query warms both routes
for at least two seconds and 128 calls. Four ABBA pairs use 4,096 calls per block
at sizes 1/100 and 256 at 10,000, totaling 135,168 measured calls. All calls matched;
each candidate connection retained exactly one initial migration registration rather
than accumulating legacy baseline commands.

| Accounts | Query | Baseline → scalar median µs/call | Baseline → scalar allocated bytes/call |
| ---: | --- | ---: | ---: |
| 1 | Presence | 7.31 → 4.48 | 1,112 → 720 |
| 1 | Identity | 7.86 → 5.86 | 1,208 → 1,239 |
| 100 | Presence | 55.58 → 3.66 | 31,530 → 720 |
| 100 | Identity | 56.53 → 5.05 | 31,626 → 1,237 |
| 10,000 | Presence | 5,588.11 → 3.85 | 3,123,585 → 720 |
| 10,000 | Identity | 5,661.52 → 5.31 | 3,123,729 → 1,237 |

The one-account identity query allocates approximately 30 additional bytes per call;
the parameterized lookup is not an allocation improvement in that smallest fixture.
Its measured latency improves in the isolated control. At larger cardinalities both
allocation and latency improve substantially. All three owned benchmark processes
exited zero. Min/max block ranges, warmup counts and registry observations remain in
the receipts. No universal API throughput or latency claim follows from these
synchronous warm-query measurements on a shared development host.

## Deterministic command ownership correction

The registry evidence justified a separate application resource fix. All 43 remaining
LocalStore-owned `CreateCommand` declarations now use scoped disposal; all 54 owned
command sites present at that stage are covered. The later native-AI authorized-save
query also uses scoped disposal and has separate allowed/denied cleanup coverage.
No command escapes these synchronous/materialized methods.
SQL, transactions, locks, parameter binding and returned data are unchanged. A
before/after text check confirms this correction adds only the 43 `using` keywords.

Five new regressions fail before the correction: fresh in-memory migration leaves three
registrations; 100 paired user reads grow 4 to 204; ten materialization exceptions
grow 4 to 14; duplicate insertion and committed/conflicting project transactions
also leave entries. After correction, the full infrastructure suite passes 48/48,
including bounded registry counts on success, exception and transaction paths.
The tests inspect the actual registry in the pinned Microsoft.Data.Sqlite 10.0.13
provider rather than treating GC or row equality as evidence of cleanup.

A separately registered four-process ABBA replay isolates exactly those 43 ownership
edits. Each process creates one account, runs 50,000 full-list calls and measures
the same presence query before/after, with an independent peer control. Both old
processes retain 50,004 registry entries after exposure; both corrected processes
remain at zero. Post-exposure presence time is 62.05/58.24 µs in the old processes
and 4.87/4.49 µs in the corrected processes. All four processes exit zero, every
presence result is correct, and independent peer controls remain approximately
4.55–5.71 µs. The count endpoint establishes deterministic command-registry cleanup;
the timings are observational and do not establish a process RSS ceiling or global
throughput claim.

This correction followed the frozen worker-budget and P46 page-resource sources.
Their measurements retain the earlier command lifetimes and must not be presented
as resource measurements of this later complete source. The independent TorchSharp
constructor/native-retention evidence does not use SQLite and remains unresolved.

Evidence is retained under `.artifacts/experiments/scalar-user-query-v1/`, `v2/`,
`v3/`, `.artifacts/experiments/scalar-user-command-registry/` and
`.artifacts/experiments/localstore-command-disposal-v1/`. Failed compiler, fixture
and prior benchmark receipts are preserved. No fitting or natural accuracy scoring
was performed.
