#!/usr/bin/env bash
# Update an existing single-user local install with an atomic backup/rollback point.
# Usage: scripts/update-local.sh [prefix]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PREFIX="${1:-$HOME/.leviathantext/app}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
BACKUP="$PREFIX.pre-update-$STAMP"

if [[ ! -x "$PREFIX/bin/leviathan" ]]; then
  echo "no existing install at $PREFIX; use scripts/install-local.sh" >&2
  exit 2
fi
if [[ -e "$BACKUP" ]]; then
  echo "backup already exists: $BACKUP" >&2
  exit 2
fi

echo "== backup =="
mv "$PREFIX" "$BACKUP"
echo "previous install: $BACKUP"

restore() {
  if [[ -e "$PREFIX" ]]; then
    mv "$PREFIX" "$PREFIX.failed-$STAMP" 2>/dev/null || true
  fi
  if [[ -e "$BACKUP" ]]; then
    mv "$BACKUP" "$PREFIX"
  fi
}
trap 'echo "update failed; restoring previous install" >&2; restore' ERR

echo "== install candidate =="
"$ROOT/scripts/install-local.sh" "$PREFIX"

echo "== verify =="
"$PREFIX/bin/leviathan" project list >/dev/null
trap - ERR
echo "PASS: updated $PREFIX (rollback: scripts/rollback-local.sh $PREFIX)"
