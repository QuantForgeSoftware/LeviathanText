#!/usr/bin/env bash
# Local install/update/rollback rehearsal. Packages twice and uses a disposable prefix.
# Usage: scripts/update-rollback-smoke.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TMP="$(mktemp -d "${TMPDIR:-/tmp}/lt-update.XXXXXX")"
PREFIX="$TMP/app"
cleanup() {
  local result=$?
  if [[ "$result" -ne 0 ]]; then
    for log in "$TMP"/{install,update,rollback}.log; do
      if [[ -f "$log" ]]; then cat "$log" >&2; fi
    done
  fi
  rm -rf "$TMP"
}
trap cleanup EXIT
export LEVIATHAN_STORE="$TMP/verification.db"

"$ROOT/scripts/install-local.sh" "$PREFIX" > "$TMP/install.log" 2>&1
"$ROOT/scripts/update-local.sh" "$PREFIX" > "$TMP/update.log" 2>&1
"$ROOT/scripts/rollback-local.sh" "$PREFIX" > "$TMP/rollback.log" 2>&1

[[ -x "$PREFIX/bin/leviathan" ]]
grep -q '^PASS: updated ' "$TMP/update.log"
grep -q '^PASS: restored ' "$TMP/rollback.log"
find "$TMP" -maxdepth 1 -type d -name 'app.failed-*' | grep -q .
[[ ! -e "$LEVIATHAN_STORE" ]]
"$PREFIX/bin/leviathan" project list >/dev/null

echo "PASS: update/rollback rehearsal in $PREFIX"
