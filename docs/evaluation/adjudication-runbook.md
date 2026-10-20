# Adjudication runbook (development pilot → frozen acceptance)

Purpose: turn reviewed lines into evidence that can support (or refuse) an acceptance claim.
This is a process record for DATA-03; it does not replace a named specialist or a custodian.

## 1. Choose the right objects

- Use a **development** witness set for threshold/model selection. Never tune on a locked test.
- Acceptance objects must be disjoint at the **physical witness/object** level (all views,
  fragments, crops and alternate lights of one object stay together). Record IDs and hashes in
  the ledger.
- If a locked split was already spent (P01), it cannot support a new threshold claim. Select
  fresh witnesses and record why they are fresh.

## 2. Double review

1. Import the witness (PAGE/EpiDoc/text-first as appropriate) and transcribe L1 in the editor.
2. Reviewer A records `accept`/`correct`/`dispute` on the exact revision; a `correct` decision
   creates a new L1 revision.
3. Reviewer B does the same **without seeing A's decision** where possible (separate session or
   filtered export).
4. The project page shows reviewed/double-reviewed/disputed/needs-adjudication counts; export
   `adjudication.csv` for lines whose reviewers disagree.
5. A specialist adjudicates disagreements and records the final decision/correction. The tool
   never resolves disagreement automatically.

## 3. Freeze the acceptance sheet

- Fill the per-track sheet with: corpus/version, object and line counts, split hashes, profile
  version, hardware, decoding settings, raw/language-assisted/AI results, per-stratum results,
  confidence/uncertainty handling, reviewer names and evidence links.
- Set numeric thresholds **before** scoring the acceptance objects; publish them with the sheet.
- Record every excluded span (gap/unclear/supplied) and whether it is scored exactly, separately
  or excluded. Do not silently score restorations as visible text.

## 4. Score and report

- Use one-to-one matching for layout and page-level results; report detection misses as
  deletions, not as silently skipped lines.
- Report seed spread where training is not bit-reproducible; do not quote the best single run.
- Keep the machine-readable artifacts (JSONL/CSV) plus the human report; state when a split is
  spent.

## 5. If the evidence is insufficient

Record a **negative result** and keep the previous baseline/default. Do not lower thresholds,
promote a model trained on the test set, or present unreviewed model output as transcription.
