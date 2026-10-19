# Label hygiene: out-of-profile training lines (P01 dev)

- Date: 2026-10-19T20:54:12.2314090+00:00; profile `greek-diplomatic-pilot@1.0`
- Same architecture (CRNN h=64), seed 99, 60 epochs, identical label set; only the training list differs
- Out-of-profile = any scalar classified `Other` (e.g. PAGE markup `<`, `>`, `&`, brackets leaked into labels)
- Training lines: 176 baseline vs 169 clean (7 excluded); dev: 50 vs 46

| Variant | Dev CER |
| --- | --- |
| All training lines | 0.819 |
| Out-of-profile lines excluded | 0.802 |

## Reading

This is a single-seed, single-epoch-budget comparison on the P01 dev split; it is
evidence for cleaning labels, not a tuned result. P01's locked split was spent
earlier — no locked-test claim is made here. The production recommendation is to
run profile validation (`leviathan validate text --strict`) before training and
to exclude markup-contaminated labels by default once a second corpus confirms the
direction.