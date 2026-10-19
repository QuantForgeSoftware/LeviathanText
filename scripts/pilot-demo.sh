#!/usr/bin/env bash
# End-to-end pilot smoke: import -> layout -> train -> recognize -> review queue ->
# exports -> search -> backup/restore. Requires the P01 dataset under .artifacts.
# Usage: scripts/pilot-demo.sh [work-dir]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK="${1:-$(mktemp -d "${TMPDIR:-/tmp}/lt-demo.XXXXXX")}"
DATA="$ROOT/.artifacts/datasets/p01-v0.9"
STORE="$WORK/store.db"

if [ ! -d "$DATA" ]; then
  echo "P01 dataset not found at $DATA; skipping demo" >&2
  exit 0
fi

CLI=(dotnet run --project "$ROOT/src/LeviathanText.Cli" --no-build -- --store "$STORE")

echo "== project + bulk import =="
PID="$("${CLI[@]}" project new demo | tail -1)"
"${CLI[@]}" import dir "$PID" "$DATA" | tail -1

echo "== train (5 epochs, pilot package) =="
"${CLI[@]}" train lines "$DATA" --out "$WORK/model" --epochs 5 2>&1 | tail -1

SURF="$(sqlite3 "$STORE" "select json from projects limit 1" | python3 -c "import json,sys; print(json.load(sys.stdin)['project']['objects'][0]['surfaces'][0]['id'])")"

echo "== recognize + review queue =="
"${CLI[@]}" recognize surface "$PID" "$SURF" --package "$WORK/model" --review-queue "$WORK/queue.csv" >/dev/null 2>"$WORK/rec.log"
grep -q '^line_id,mean_confidence' "$WORK/queue.csv"

echo "== exports =="
"${CLI[@]}" export diplomatic "$PID" "$SURF" >/dev/null
"${CLI[@]}" export alto "$PID" "$SURF" > "$WORK/page.alto.xml"
"${CLI[@]}" export lines "$PID" "$WORK/lines.csv" >/dev/null
"${CLI[@]}" export annotations "$PID" "$WORK/annotations.csv" >/dev/null
"${CLI[@]}" export bundle "$PID" "$WORK/bundle.zip" | tail -1
unzip -l "$WORK/bundle.zip" | grep -q 'manifest.json'

echo "== search + profile QA =="
"${CLI[@]}" search "$PID" νθρωπ --base 2>/dev/null | head -1 >/dev/null
"${CLI[@]}" validate text "$PID" >/dev/null 2>&1 || true

echo "== backup / restore =="
"${CLI[@]}" backup "$WORK/snapshot" | tail -1
"${CLI[@]}" restore "$WORK/snapshot" --into "$WORK/restored.db" | tail -1
sqlite3 "$WORK/restored.db" "select name from projects" | grep -q demo

echo "PASS: demo artefacts in $WORK"
