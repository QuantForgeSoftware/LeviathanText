# Worker/store import route resource probe (2026-11-03)

Status: short bounded worker observation, not a leak proof. No recognition, training or model
loading is involved.

## Workload

`scripts/profile-worker-import.sh 200 .artifacts/worker-resource-200` starts the Debug API,
creates a project, enqueues one durable `import.dir` job with **200 copies** of the synthetic
PAGE fixture, starts a worker on the same store, and samples API/worker OS RSS while polling
the job.

## Observation

| Measure | API KiB | Worker KiB |
| --- | ---: | ---: |
| First sample | 130,288 | 50,240 |
| Last sample | 136,592 | 134,720 |
| Maximum sample | 136,592 | 150,704 |

The job succeeded and imported 200/200 objects. The worker's RSS rose during the burst and
settled at ~135 MiB; the API stayed ~130–137 MiB. Seven samples covered the short run, so this
does not establish steady-state behavior, a leak, or collection-scale sizing. No model load,
recognition, concurrent workers, or sustained queue is covered.

Evidence: `.artifacts/worker-resource-200/summary.json`, `rss.csv`, `api.log`, `worker.log`
(local artifacts; not committed).
