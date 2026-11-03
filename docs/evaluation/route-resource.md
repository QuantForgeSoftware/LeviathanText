# API/store route resource probe (2026-11-03)

Status: bounded single-process route observation. It does not claim a memory-stability or
leak result and does not close NT-20.

## Workload

`scripts/profile-api-store.sh 500 .artifacts/route-resource` starts the Debug API on
localhost with a fresh store, creates one project and imports the image-less PAGE fixture
(no recognition, training, model loading or worker). It then issues **2,000 requests**:
`GET /projects`, `GET /projects/{id}`, `GET /surfaces/{id}/diplomatic` (via project path) and
`GET /crosswalk?lines=true`, sampling the API process's OS RSS every 10 iterations.

## Observation

| Measure | KiB |
| --- | ---: |
| First sample (after startup/import) | 144,080 |
| Last sample | 70,224 |
| Maximum sample | 144,080 |

RSS decreased after startup/import and stayed in the ~67–70 MiB range for the later samples;
there is no monotonic growth in this short read-only run. The samples are OS RSS for one
process, not managed/native allocations, and the first sample includes runtime/JIT/page
startup. No sustained, concurrent, worker, recognition or model-loading route is covered.

Evidence: `.artifacts/route-resource/summary.json` and `rss.csv` (local artifact store; not
committed). The script is repeatable on a machine with a Debug build and `curl`/`ps`.
