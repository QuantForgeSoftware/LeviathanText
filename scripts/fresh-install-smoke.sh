#!/usr/bin/env bash
# Publish and exercise the CLI, API and worker against one disposable store.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$(mktemp -d "${TMPDIR:-/tmp}/lt-install.XXXXXX")"
STORE="$OUT/store.db"

echo "Fresh-install artifacts: $OUT"
for project in Cli Api Worker; do
  name="$(printf '%s' "$project" | tr '[:upper:]' '[:lower:]')"
  dotnet publish "$ROOT/src/LeviathanText.$project" -c Release -m:1 \
    -p:UseSharedCompilation=false -o "$OUT/$name" >"$OUT/publish-$name.log" 2>&1
done

cd "$OUT"
"$OUT/cli/LeviathanText.Cli" --store "$STORE" project new install-smoke >"$OUT/cli.log"
"$OUT/cli/LeviathanText.Cli" --store "$STORE" project list | grep -q install-smoke

LeviathanText__StorePath="$STORE" LeviathanText__Auth__ApiKey="" LeviathanText__Auth__ReadOnly=false \
  "$OUT/api/LeviathanText.Api" --urls http://127.0.0.1:0 \
  --Logging:LogLevel:Microsoft.Hosting.Lifetime Information --Logging:Console:FormatterName simple \
  >"$OUT/api.log" 2>&1 &
API_PID=$!
cleanup() {
  kill "$API_PID" 2>/dev/null || true
  wait "$API_PID" 2>/dev/null || true
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
API_URL=""
for _ in $(seq 1 60); do
  if ! kill -0 "$API_PID" 2>/dev/null; then
    echo "Published API exited before listening. See $OUT/api.log" >&2
    exit 1
  fi
  API_URL=$(awk 'match($0, /Now listening on: http:\/\/127[.]0[.]0[.]1:[0-9]+/) {
    address=substr($0, RSTART, RLENGTH); sub(/^Now listening on: /, "", address); print address; exit
  }' "$OUT/api.log")
  if [[ -n "$API_URL" ]]; then break; fi
  sleep 0.5
done
if [[ -z "$API_URL" ]] || ! kill -0 "$API_PID" 2>/dev/null; then
  echo "Published API did not announce a listener. See $OUT/api.log" >&2
  exit 1
fi
curl -fsS --max-time 5 "$API_URL/" | grep -q LeviathanText
curl -fsS --max-time 5 "$API_URL/api/v1/projects" | grep -q install-smoke

JOB=$(curl -fsS --max-time 5 -X POST "$API_URL/api/v1/jobs" \
  -H 'Content-Type: application/json' -d '{"type":"noop","ownerId":"install-smoke"}' \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')
LEVIATHANTEXT_LAYOUT_MODEL_DIR="$OUT/no-layout-models" \
  "$OUT/worker/LeviathanText.Worker" --store "$STORE" --once >"$OUT/worker.log" 2>&1
curl -fsS --max-time 5 "$API_URL/api/v1/jobs/$JOB" >"$OUT/job.json"
python3 - "$OUT/job.json" "$JOB" <<'PY'
import json
import sys

with open(sys.argv[1]) as stream:
    job = json.load(stream)
if job["id"] != sys.argv[2] or job["status"] != "succeeded" or len(job["outputs"]) != 1:
    raise SystemExit(f"Published worker did not complete the smoke job: {job}")
PY
echo "PASS: fresh install artifacts in $OUT"
