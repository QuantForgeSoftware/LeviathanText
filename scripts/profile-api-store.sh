#!/usr/bin/env bash
# Bounded API + store route resource probe. Uses an image-less PAGE fixture; no recognition,
# training or model loading is involved. Records API-process OS RSS over repeated reads.
# Usage: scripts/profile-api-store.sh [iterations] [out-dir]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ITERATIONS="${1:-500}"
OUT="${2:-$ROOT/.artifacts/route-resource}"
PORT="${PORT:-5137}"
mkdir -p "$OUT"
STORE="$OUT/store.db"
rm -f "$STORE" "${STORE}-shm" "${STORE}-wal" "$OUT/rss.csv" "$OUT/api.log"

LeviathanText__StorePath="$STORE" \
  "$ROOT/src/LeviathanText.Api/bin/Debug/net10.0/LeviathanText.Api" --urls "http://127.0.0.1:$PORT" \
  >"$OUT/api.log" 2>&1 &
API_PID=$!
trap 'kill $API_PID 2>/dev/null || true' EXIT

for _ in $(seq 1 60); do
  curl -fsS "http://127.0.0.1:$PORT/" >/dev/null 2>&1 && break
  sleep 0.5
done
curl -fsS "http://127.0.0.1:$PORT/" | grep -q LeviathanText

PROJECT=$(curl -fsS -X POST "http://127.0.0.1:$PORT/api/v1/projects" \
  -H 'Content-Type: application/json' -d '{"name":"route-profile"}' \
  | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')
FIXTURE="$ROOT/tests/LeviathanText.Formats.Tests/Fixtures/sample.page.xml"
IMPORTED=$(curl -fsS -X POST "http://127.0.0.1:$PORT/api/v1/projects/$PROJECT/import-page" \
  -H 'Content-Type: application/json' -d "{\"pageXmlPath\":\"$FIXTURE\"}")
SURFACE=$(printf '%s' "$IMPORTED" | sed -n 's/.*"surfaceId":"\([^"]*\)".*/\1/p')

sample() {
  ps -o rss= -p "$API_PID" | tr -d ' '
}
echo "iteration,rss_kib" > "$OUT/rss.csv"
for i in $(seq 1 "$ITERATIONS"); do
  curl -fsS "http://127.0.0.1:$PORT/api/v1/projects" >/dev/null
  curl -fsS "http://127.0.0.1:$PORT/api/v1/projects/$PROJECT" >/dev/null
  curl -fsS "http://127.0.0.1:$PORT/api/v1/projects/$PROJECT/surfaces/$SURFACE/diplomatic" >/dev/null
  curl -fsS "http://127.0.0.1:$PORT/api/v1/projects/$PROJECT/crosswalk?lines=true" >/dev/null
  if (( i % 10 == 0 )); then echo "$i,$(sample)" >> "$OUT/rss.csv"; fi
done

FIRST=$(awk -F, 'NR==2{print $2}' "$OUT/rss.csv")
LAST=$(tail -1 "$OUT/rss.csv" | cut -d, -f2)
MAX=$(awk -F, 'NR>1{if($2>m)m=$2}END{print m}' "$OUT/rss.csv")
printf '{"iterations":%d,"routes_per_iteration":4,"first_rss_kib":%s,"last_rss_kib":%s,"max_rss_kib":%s,"project":"%s","surface":"%s"}\n' \
  "$ITERATIONS" "$FIRST" "$LAST" "$MAX" "$PROJECT" "$SURFACE" > "$OUT/summary.json"
cat "$OUT/summary.json"
