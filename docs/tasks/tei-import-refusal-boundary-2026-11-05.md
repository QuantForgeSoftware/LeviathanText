# MID28 import refusal boundary

Implement only the `InterchangeService.ImportTei` parser boundary in ProjectService.cs
and separate generated application/API regression files. Keep missing-file prechecks
unchanged. Read source outside the catch; translate only InvalidDataException,
XmlException and FormatException from TeiXml.Import into StageContractViolation with
a constant actionable message. Do not catch image decoding, file IO, model or store
errors globally. Parsing must precede image decoding and all project persistence.

Before-fix fixtures prove cross-line alternatives, invalid/oversized dedicated
metadata and malformed XML fail as untyped server errors; require typed application
failure and HTTP422 after the fix, with exact persisted NativeJson/version/object/
attachment equality. Use existing invalid image bytes to prove parse-before-decode,
not a missing image path. Success controls use generated PNG and ASCII TEI to trace
physical context/language/break metadata through real native import, save, reload and
export, including both contributing verse contexts despite legacy scalar n tags.
No stored-project migration, automatic reimport, training or real corpus mutation.

Implemented and verified: four before application/parser failures and four HTTP500s
now typed StageContractViolation/422; positive native metadata controls and missing-
file/decode precedence controls pass. Focused application TEI17 and HTTP5 pass;
see `docs/evaluation/p46-physical-tei-lines-2026-11-05.md` for receipts and limits.
