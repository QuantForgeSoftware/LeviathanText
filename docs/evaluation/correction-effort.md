# AI-assisted correction effort (EVAL-05 method note)

- Owner: **Instance B (EVAL)**; contract set **C0-0.4-draft**; baseline `main` @ `c3a050d`.
- Companion: [acceptance-plan.md](acceptance-plan.md) §3 (AI-assisted row) and §5 (correction-effort measurement).
- Implementation: `src/LeviathanText.Evaluation/CorrectionEffortScorer.cs`; tests: `tests/LeviathanText.Evaluation.Tests/CorrectionEffortTests.cs`.

## What it measures

For each line the scorer takes the expert **reference**, the **raw** recognizer output, the human
**final** text, the reviewed **suggestions** and, when the harness recorded it, **correction time**.

| Entry | Meaning |
| --- | --- |
| `RawCer` / `FinalCer` | Case-sensitive scalar CER of the raw and reviewed output against the reference (reuses `RecognitionScorer`, canonical-equivalent NFC). |
| `CerImprovement` | `RawCer − FinalCer`; the quality the review added. |
| `ScalarEditsWithoutAssistance` | Levenshtein distance reference↔raw: the typing a reviewer would do with no AI. |
| `ScalarEditsWithAssistance` | Levenshtein distance raw↔final: the typing actually done with AI available. |
| `AcceptedSuggestionScalars` | Scalars the reviewer accepted from proposals (model-supplied text volume). |
| `UnsupportedAddedScalars` | Accepted scalars with **no support in the diplomatic source span**: candidate-reading additions, word-division content other than separators, non-mark accentuation additions. |
| `UnsupportedSubstitutedScalars` | Accepted letter replacements in word-division/accentuation tasks. |
| `SuppliedRestorationScalars` | Accepted expansion/restoration additions: expected, but must stay marked as supplied (L2), never merged into L1. |
| `WitnessVariantOverwrites` | Accepted candidate readings that replaced a span whose raw reading already matched the reference. |
| `Time` | Optional harness-measured baseline/assisted correction time with `Reduction`. |

`Aggregate(...)` reports the same entries micro-averaged over lines (distance sums), so long lines
weigh proportionally.

## Honest limits (recorded, not hidden)

- **Scalar edit distance cannot show an AI labor saving when the review fully corrects the line:**
  reference↔raw and raw↔reference distances are symmetric. The quality gain is `CerImprovement`;
  the labor gain is reported only through measured `CorrectionTime`.
- **Task separation is enforced at scoring time, not training time.** A candidate reading that adds
  a mark the source does not show is counted as unsupported even if it happens to match the
  reference; the model may still be right, but the *evidence* is not in the visible span.
- **Substitution classification uses an NFD multiset**, not a linguistic judgment; it cannot tell a
  legitimate alternative reading from an error. `WitnessVariantOverwrites` is the conservative
  line-level flag for that concern.
- Numbers remain **pilot-scale baselines**, not thresholds. Thresholds wait for the frozen
  acceptance sheet and a named custodian (acceptance-plan.md §1, §6).

## How a release report should use it

1. Score raw and AI-assisted runs on the **same held-out witnesses**; never tune on locked test.
2. Publish per track and per profile; do not pool manuscripts, papyri and inscriptions.
3. Report unsupported additions/restorations and witness-variant overwrites next to any accuracy
   number; an accuracy gain with unsupported additions is not a clean win.
