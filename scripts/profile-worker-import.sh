#!/usr/bin/env bash
# Bounded worker + store route probe: a durable import.dir job imports PAGE files with no
# recognition/model loading. Records API and worker OS RSS while the job runs.
# Usage: scripts/profile-worker-import.sh [pages] [out-dir]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PAGES="${1:-20}"
OUT="${2:-$ROOT/.artifacts/worker-resource}"
PORT="${PORT:-5141}"
STORE="$OUT/store.db"
SOURCE="$OUT/pages"
mkdir -p "$OUT" "$SOURCE"
rm -f "$STORE" "$STORE-shm" "$STORE-wal"
rm -f "$SOURCE"/*.xml
for i in $(seq 1 "$PAGES"); do
  cp "$ROOT/tests/LeviathanText.Formats.Tests/Fixtures/sample.page.xml" "$SOURCE/page-$i.xml"
done

# Clear stale readiness output in this shell before starting the background process.
: >"$OUT/api.log"
WORKER_PID=""
LeviathanText__StorePath="$STORE" \
  "$ROOT/src/LeviathanText.Api/bin/Debug/net10.0/LeviathanText.Api" --urls "http://127.0.0.1:$PORT" \
  --Logging:LogLevel:Microsoft.Hosting.Lifetime Information --Logging:Console:FormatterName simple \
  >>"$OUT/api.log" 2>&1 &
API_PID=$!
cleanup() {
  if [[ -n "${WORKER_PID:-}" ]]; then
    kill "$WORKER_PID" 2>/dev/null || true
    wait "$WORKER_PID" 2>/dev/null || true
  fi
  kill "$API_PID" 2>/dev/null || true
  wait "$API_PID" 2>/dev/null || true
}
trap cleanup EXIT
API_URL=""
for _ in $(seq 1 60); do
  if ! kill -0 "$API_PID" 2>/dev/null; then
    printf 'Profile API exited before listening; see %s\n' "$OUT/api.log" >&2
    exit 1
  fi
  # Only the spawned API writes this freshly truncated log. An existing server at
  # PORT cannot establish readiness; PORT=0 discovers the child's allocated port.
  API_URL=$(awk 'match($0, /Now listening on: http:\/\/127[.]0[.]0[.]1:[0-9]+/) {
    address=substr($0, RSTART, RLENGTH); sub(/^Now listening on: /, "", address); print address; exit
  }' "$OUT/api.log")
  if [[ -n "$API_URL" ]]; then
    if [[ "$PORT" != "0" && "$API_URL" != "http://127.0.0.1:$PORT" ]]; then
      printf 'Profile API reported an unexpected listener: %s\n' "$API_URL" >&2
      exit 1
    fi
    break
  fi
  sleep 0.5
done
if [[ -z "$API_URL" ]] || ! kill -0 "$API_PID" 2>/dev/null; then
  printf 'Profile API did not establish its listener; see %s\n' "$OUT/api.log" >&2
  exit 1
fi
curl -fsS "$API_URL/" | grep -q LeviathanText

PROJECT=$(curl -fsS -X POST "$API_URL/api/v1/projects" \
  -H 'Content-Type: application/json' -d '{"name":"worker-profile"}' \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')
JOB=$(curl -fsS -X POST "$API_URL/api/v1/jobs" \
  -H 'Content-Type: application/json' \
  -d "{\"type\":\"import.dir\",\"ownerId\":\"probe\",\"projectId\":\"$PROJECT\",\"sourceDirectory\":\"$SOURCE\"}" \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')

"$ROOT/src/LeviathanText.Worker/bin/Debug/net10.0/LeviathanText.Worker" --store "$STORE" \
  >"$OUT/worker.log" 2>&1 &
WORKER_PID=$!

echo "iteration,api_rss_kib,worker_rss_kib" > "$OUT/rss.csv"
STATUS=""
for i in $(seq 1 600); do
  STATUS=$(curl -fsS "$API_URL/api/v1/jobs/$JOB" \
    | python3 -c 'import json,sys; print(json.load(sys.stdin)["status"])')
  API_RSS=$(ps -o rss= -p "$API_PID" | tr -d ' ')
  WORKER_RSS=$(ps -o rss= -p "$WORKER_PID" | tr -d ' ')
  echo "$i,$API_RSS,$WORKER_RSS" >> "$OUT/rss.csv"
  case "$STATUS" in succeeded|failed|cancelled) break;; esac
  sleep 0.25
done

OBJECTS=$(curl -fsS "$API_URL/api/v1/projects/$PROJECT" | grep -o '"id":"obj-' | wc -l | tr -d ' ')
FIRST=$(awk -F, 'NR==2{print $2","$3}' "$OUT/rss.csv")
LAST=$(tail -1 "$OUT/rss.csv" | cut -d, -f2,3)
API_MAX=$(awk -F, 'NR>1{if($2>m)m=$2}END{print m}' "$OUT/rss.csv")
WORKER_MAX=$(awk -F, 'NR>1{if($3>m)m=$3}END{print m}' "$OUT/rss.csv")
printf '{"pages":%d,"objects":%s,"job_status":"%s","samples":%d,"api_first_rss_kib":"%s","worker_first_rss_kib":"%s","api_last_rss_kib":"%s","worker_last_rss_kib":"%s","api_max_rss_kib":%s,"worker_max_rss_kib":%s}\n' \
  "$PAGES" "$OBJECTS" "$STATUS" "$(($(wc -l < "$OUT/rss.csv") - 1))" \
  "${FIRST%,*}" "${FIRST#*,}" "${LAST%,*}" "${LAST#*,}" "$API_MAX" "$WORKER_MAX" > "$OUT/summary.json"
cat "$OUT/summary.json"
