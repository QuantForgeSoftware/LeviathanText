# Equivalent review paths — 2026-11-05

ASP.NET accepts both `/lines/{id}/review` and its trailing-slash form. The role
middleware matched only the former suffix, allowing an annotator to submit a
review through the latter. The review classifier now trims trailing slashes
before applying the existing decision-route check.

A generated, valid native project reproduces the authorization defect: before
the fix the annotator trailing-slash case returned success; the other five
role/path controls passed. Candidate controls pass6/6 for annotator, reader and
reviewer with both URL spellings. Forbidden cases retain the original project
version and empty decision ledger. Authenticated attribution remains bound to
the account. No Greek data, model execution or source review was involved.

Receipts are `/private/tmp/lt-midday-review-path-{before-fixed-fixture,after}.log` and the combined
`streams-role-final.trx` (19 passed). Earlier exploratory fixtures with an invalid
frame reference failed serialization; those are not evidence of the role defect.
Temporary test stores are owned and disposed by each factory. A final complete
API regression run is recorded in the session handoff.
