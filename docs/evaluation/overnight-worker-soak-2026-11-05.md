# Overnight worker and store soak

The registered run began November 4 at 20:28:04 CST and completed its final
job November 5 at 04:43:06 CST, spanning 29,701.67 seconds. Thirty-four waves
were spaced fifteen minutes apart. Every wave submitted two independent
directory-import jobs to two workers sharing one disposable SQLite store
and an owned loopback API. Each job imported 24 copies of the generated PAGE
fixture into a fresh project.

The binaries were frozen before execution. This run covers the first worker
polling and atomic-transition candidate. Later API isolation, authorization,
geometry, and grayscale changes were verified separately in the final-package
burst and integrated suites. Every frozen file and the registered runner still
matched its SHA-256 receipt at closure.

## Reconciliation

| Check | Result |
| --- | ---: |
| Planned and successful jobs | 68 / 68 |
| Project objects imported | 1,632 / 1,632 |
| Jobs completed per worker | 34 / 34 |
| Exact original PAGE attachments | 1,632 / 1,632 |
| OS RSS samples | 635 |
| Median observed submission-to-terminal interval | 0.829 s |
| Maximum observed interval | 1.147 s |

The independent verifier joined submission and outcome ledgers to all SQLite
job rows, every project's object count, exact attachment content/digests,
and unique worker terminal log entries. SQLite integrity checking passed.
Both workers and the API exited zero after cleanup. The observed intervals
include polling and queue wait.

## Process RSS

Values are sampled KiB for each process separately. Each maximum can occur
at a different time.

| Process | First post-wave RSS | Maximum sampled RSS | Last post-wave RSS |
| --- | ---: | ---: | ---: |
| API | 136,224 | 147,248 | 95,120 |
| Worker 1 | 92,912 | 110,592 | 74,640 |
| Worker 2 | 93,200 | 110,480 | 73,504 |

RSS varied during the run and ended below its first post-wave sample. This
describes an episodic synthetic import workload on this macOS host. Model
construction, OCR quality, real collection throughput, other operating
systems, and the diagnosed native TorchSharp retention require their own
evidence.

## Controls and final-package burst

The original redirected-stdin worker baseline completed its first two jobs
then both workers exited, leaving four of six planned jobs unavailable. The
worker candidate completed all six jobs over three short waves.

A separate eight-wave burst used the final packaged API and workers. It
completed all 16 jobs and 768 generated imports, with eight jobs per worker.
SQL rows, exact attachments, terminal logs and exit-zero cleanup reconciled.
Its short duration makes it a combined functionality check for the final
package. The independently registered eight-hour run retains its original
binary identity.

[Overnight reconciliation](../../.artifacts/worker-soak.mm984Z/overnight/reconciliation.json) ·
[Final-package burst](../../.artifacts/worker-soak.mm984Z/integrated-short/reconciliation.json) ·
[Worker transition evidence](durable-worker-transitions-2026-11-04.md).
