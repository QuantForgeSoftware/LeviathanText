#!/usr/bin/env bash
# Rehearse the documented backup -> mutate -> restore -> verify path with a fresh store.
# Usage: scripts/backup-restore-rehearsal.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/lt-backup.XXXXXX")"
STORE="$WORK/live.db"
RESTORED="$WORK/restored.db"
CLI=(dotnet run --project "$ROOT/src/LeviathanText.Cli" --no-build --)

"${CLI[@]}" --store "$STORE" project new before-backup >/dev/null
"${CLI[@]}" --store "$STORE" backup "$WORK/snapshot" >/dev/null
test -s "$WORK/snapshot/backup-manifest.json"

# Mutate after the snapshot; restoration must not carry this state.
"${CLI[@]}" --store "$STORE" project new after-backup >/dev/null
"${CLI[@]}" --store "$RESTORED" restore "$WORK/snapshot" --into "$RESTORED" --force >/dev/null

LIST="$("${CLI[@]}" --store "$RESTORED" project list)"
echo "$LIST" | grep -q before-backup
if echo "$LIST" | grep -q after-backup; then
  echo "restored store contains post-snapshot state" >&2
  exit 1
fi
echo "PASS: backup/restore rehearsal ($RESTORED)"
