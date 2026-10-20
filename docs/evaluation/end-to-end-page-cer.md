# End-to-end page CER (layout + recognition), held-out manuscript pages

- Date: 2026-10-20T00:50:18.4138610+00:00; contract C0-0.4-draft
- Pipeline: image-only surface → automatic layout → recognition; control = same model on PAGE boxes
- Matching: detected↔PAGE line vertical IoU ≥ 0.5; **missed GT lines count as full deletions**
- Evaluation pages: 6 held-out M02a pages (file split i%5==0); *same books as training — workflow measurement, not a generalization claim*
- Seeds: 23, 24, 25 (CPU training is not bit-reproducible across runs; report the spread, not one run)

| Seed | Page CER (incl. misses) | Control CER (PAGE boxes) | Detection recall |
| --- | --- | --- | --- |
| 23 | 0.999 | 1 | 0.556 |
| 24 | 0.999 | 0.999 | 0.556 |
| 25 | 0.987 | 0.995 | 0.556 |
| **mean** | **0.995** | **0.998** | **0.556** |

Single-run variance is material (see the control column, which varies from
0.995 to 1 with identical code and data).
The earlier single-seed note of 0.938/0.881 was one favorable draw from this
distribution and should not be quoted as a stable result.

## Reading

Page CER includes layout misses and false lines; it is the honest number for the
image-only workflow. With this model quality the mean page-vs-control difference
(-0.003) is comparable to the seed spread, so this run **cannot cleanly
separate layout cost from training variance**. Detection recall is stable
(0.556) and caps the achievable page CER regardless of recognition quality:
improving recognition alone cannot recover lost lines. A stronger, more stable model
and more seeds are required before attributing page-level differences to layout.
Multi-column/curved layouts remain unsupported.