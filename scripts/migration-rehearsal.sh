#!/usr/bin/env bash
# Build a pre-v6 CLI, create a store with it, migrate with the current CLI, then prove the
# old binary refuses the newer store (rollback requires the pre-upgrade backup).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OLD_COMMIT="${1:-226a612}"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/lt-migrate.XXXXXX")"
WORK="$(cd "$WORK" && pwd -P)"
OLD="$WORK/old"
STORE="$WORK/live.db"
cleanup() {
  git -C "$ROOT" worktree remove --force "$OLD" >/dev/null 2>&1 || true
  rm -rf "$WORK"
}
trap cleanup EXIT

echo "== build old CLI ($OLD_COMMIT) =="
git -C "$ROOT" worktree add --detach "$OLD" "$OLD_COMMIT" >/dev/null
dotnet build "$OLD/src/LeviathanText.Cli/LeviathanText.Cli.csproj" -c Release

echo "== create old-schema store =="
OLDCLI="$OLD/src/LeviathanText.Cli/bin/Release/net10.0/LeviathanText.Cli"
"$OLDCLI" --store "$STORE" project new migration-before | tail -1
"$OLDCLI" --store "$STORE" project list | grep -q migration-before

echo "== migrate with current CLI =="
CURRENTCLI="$ROOT/src/LeviathanText.Cli/bin/Release/net10.0/LeviathanText.Cli"
"$CURRENTCLI" --store "$STORE" project list | grep -q migration-before

echo "== old binary must refuse newer schema =="
if "$OLDCLI" --store "$STORE" project list >"$WORK/old-after.log" 2>&1; then
  echo "old binary unexpectedly opened a newer store" >&2
  exit 1
fi
grep -q 'SchemaVersionUnsupported' "$WORK/old-after.log"

echo "== create a fresh project with current CLI =="
"$CURRENTCLI" --store "$STORE" project new migration-after | tail -1
"$CURRENTCLI" --store "$STORE" project list | grep -q migration-after

echo "PASS: migration rehearsal (old=$OLD_COMMIT; rollback correctly refused)"
