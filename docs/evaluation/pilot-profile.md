# Pilot operation profile (single page)

Measured 2026-10-20 on the macOS arm64 pilot machine with the Release CLI. This is a
**single-page startup/import/detection/export/backup profile**, not sustained collection
throughput. The harness is `scripts/profile-pilot.sh`; raw output is written under
`.artifacts/profile/` and is not committed.

Source: one P01 Zenon page image (`P.Cair.Zen.*`), layout detection on a capture-only surface, then a 27-file PAGE directory import.

| Operation | Real seconds | Peak RSS |
| --- | ---: | ---: |
| `project list` (process startup + SQLite) | 0.06 | 85.2 MB |
| `import image` | 0.09 | 90.6 MB |
| `layout detect` (projection baseline) | 0.15 | 111.4 MB |
| `export page` | 0.11 | 105.9 MB |
| `import dir` (27 PAGE files) | 0.27 | 120.5 MB |
| `backup` | 0.08 | 89.8 MB |

Caveats: one run; no recognition, sustained batch, concurrent worker or
multi-tenant load; filesystem cache effects not controlled. The earlier per-line
training/inference measurements remain in the evaluation records. Do not treat this table as a
release throughput guarantee.
