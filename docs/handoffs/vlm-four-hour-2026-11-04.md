# Four-hour LM Studio benchmark continuation

November 4, 2026, 15:48:32–19:48:32 CST. The scheduled workload uses generated Greek controls. The reviewed natural evaluation still depends on rights, crop-to-line review, and independent witnesses.

## Evidence

- [Diverse blanks](../evaluation/vlm-diverse-blanks-2026-11-04.md) compared plain text and transcription JSON across six background families. Each JSON profile left all 24 repeated blank slots empty. Both models transcribed 12 of 24 readable slots exactly under JSON.
- [Flat contrast](../evaluation/vlm-flat-contrast-2026-11-04.md) varied contrast at fixed geometry. At 4% contrast, stretching moved Mistral from zero to four exact matches and reduced NFC edits from 88 to 35. Qwen3 gained two exact matches while edits increased from 15 to 19.
- [Textured contrast](../evaluation/vlm-textured-contrast-2026-11-04.md) found that image-wise stretch gained two exact Mistral readings and lost two Qwen3 readings versus the faded inputs. Pair-shared stretch produced two truncated blank hallucinations. Five Mistral source slots failed protocol or transport checks, and the final idle assertion failed after an unrelated model loaded.
- [Greek profile triage](../evaluation/vlm-greek-profile-triage-2026-11-04.md) flagged 30 of 126 wrong glyph responses and none of 66 exact ones. The other 96 errors passed the profile checks.
- Three-model voting on the earlier compact controls agreed exactly under quorum two on 78 of 96 repeated text slots. Forty of those agreements were wrong. Quorum three agreed on 20 of 96, six wrong. Across 48 unique text fixtures, 20 yielded the same wrong reading on both repeats at quorum two. Three did so with unanimity. Examples include replacing lunate sigma with standard sigma and changing a breathing mark to an acute. The repeat audit is stored with the compact agreement report.
- [Natural intake leads](../evaluation/vlm-natural-intake-candidates-2026-11-04.md) record exact item matches, photo and annotation terms, and review gaps. The Petrie PDF remains outside the benchmark while conflicting photo notices are resolved.

The local P46 comparator audit verified 51 crop and tensor hashes and 153 score joins. All five proposed units are from one witness and all 51 lines have prior project exposure. The local paired bootstrap prototype passed invented-row tests, including 16 rejected cases. Keep its physical-unit inference separate from synthetic repeats and exposed P46 crops.

## Scheduled word screen

Four visits are registered for 16:30, 17:30, 18:30, and 19:25 CST. Each has 60 planned requests and begins only with an idle local server. The registration and missing-slot control are under the local artifact directory `.artifacts/vlm-benchmark/consensus-word-screen-v1/`.

Each visit stopped at the first idle assertion before loading a worker or submitting requests. All 240 planned request slots and 80 image/profile triples are unavailable. The pre-registered blocks and their exact denominators are preserved for reference. A future attempt should use a fresh registered schedule with an idle LM Studio window.

## Reconciliation

| Study | Planned | Completed | Other outcomes |
| --- | ---: | ---: | --- |
| Diverse blanks | 192 | 192 | 0 |
| Flat contrast | 250 | 250 | 0 |
| Textured contrast | 192 | 185 | 3 HTTP errors, 2 invalid outputs, 2 truncations |
| Scheduled word screen | 240 | 0 | 240 unavailable before submission |

All three submitted studies used generated images. The textured run's closing idle check detected an unrelated loaded instance. A direct server inspection confirmed the benchmark instances were gone. No training, natural-image inference, or model promotion occurred. LM Studio was left at Debug per the user's preference.

The four-hour window closed at 19:48:32 CST. The local closure audit verified 56 registered hashes and recorded 26 raw-output and score hashes. The word-screen registration and unavailable-slot ledger are retained separately.

After the user freed the server, a [separate consecutive retry](../evaluation/vlm-word-screen-retry-2026-11-04.md) completed all 240 requests and verified worker cleanup.
