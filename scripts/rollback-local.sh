#!/usr/bin/env bash
# Roll back an updated local install to its most recent pre-update/backup directory.
# Usage: scripts/rollback-local.sh [prefix] [--list]
set -euo pipefail

PREFIX="${1:-$HOME/.leviathantext/app}"
LIST=0
for arg in "$@"; do [[ "$arg" == "--list" ]] && LIST=1; done

BACKUPS=()
while IFS= read -r candidate; do
  [[ -n "$candidate" ]] && BACKUPS+=("$candidate")
done < <(find "$(dirname "$PREFIX")" -maxdepth 1 -type d \
  \( -name "$(basename "$PREFIX").backup-*" -o -name "$(basename "$PREFIX").pre-update-*" \) \
  | sort)

if [[ "$LIST" -eq 1 ]]; then
  if [[ "${#BACKUPS[@]}" -eq 0 ]]; then echo "no backups for $PREFIX"; exit 0; fi
  printf '%s\n' "${BACKUPS[@]}"
  exit 0
fi
if [[ "${#BACKUPS[@]}" -eq 0 ]]; then
  echo "no backup found for $PREFIX" >&2
  exit 2
fi

BACKUP=""
LATEST_STAMP=""
for candidate in "${BACKUPS[@]}"; do
  candidate_stamp="${candidate##*-}"
  if [[ "$candidate_stamp" =~ ^[0-9]{8}T[0-9]{6}Z$ && "$candidate_stamp" > "$LATEST_STAMP" ]]; then
    BACKUP="$candidate"
    LATEST_STAMP="$candidate_stamp"
  fi
done
if [[ -z "$BACKUP" ]]; then
  echo "no timestamped backup found for $PREFIX" >&2
  exit 2
fi
VERIFY_DIR="$(mktemp -d "${TMPDIR:-/tmp}/lt-rollback.XXXXXX")"
trap 'rm -rf "$VERIFY_DIR"' EXIT
"$BACKUP/cli/LeviathanText.Cli" --store "$VERIFY_DIR/store.db" project list >/dev/null

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
if [[ -e "$PREFIX" ]]; then
  mv "$PREFIX" "$PREFIX.failed-$STAMP"
  echo "current install kept at $PREFIX.failed-$STAMP"
fi
mv "$BACKUP" "$PREFIX"
echo "PASS: restored $PREFIX from $BACKUP"
