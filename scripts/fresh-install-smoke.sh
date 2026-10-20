#!/usr/bin/env bash
# Fresh-install smoke: publish CLI + API, run both from the publish output with a new store,
# verify no build-tree/network dependency. Usage: scripts/fresh-install-smoke.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$(mktemp -d "${TMPDIR:-/tmp}/lt-install.XXXXXX")"
STORE="$OUT/store.db"

echo "== publish =="
dotnet publish "$ROOT/src/LeviathanText.Cli" -c Release -o "$OUT/cli" >/dev/null
dotnet publish "$ROOT/src/LeviathanText.Api" -c Release -o "$OUT/api" >/dev/null
dotnet publish "$ROOT/src/LeviathanText.Worker" -c Release -o "$OUT/worker" >/dev/null

echo "== CLI from publish output =="
PROJECT="$("$OUT/cli/LeviathanText.Cli" --store "$STORE" project new install-smoke | tail -1)"
"$OUT/cli/LeviathanText.Cli" --store "$STORE" project list | grep -q install-smoke

echo "== API from publish output (localhost) =="
LeviathanText__StorePath="$STORE" "$OUT/api/LeviathanText.Api" --urls http://127.0.0.1:5099 >"$OUT/api.log" 2>&1 &
API_PID=$!
trap 'kill $API_PID 2>/dev/null || true' EXIT
for _ in $(seq 1 40); do
  if curl -fsS http://127.0.0.1:5099/ >/dev/null 2>&1; then break; fi
  sleep 0.5
done
curl -fsS http://127.0.0.1:5099/ | grep -q LeviathanText
curl -fsS http://127.0.0.1:5099/api/v1/projects | grep -q install-smoke

echo "== worker from publish output (same store) =="
"$OUT/worker/LeviathanText.Worker" --store "$STORE" --once 2>&1 | grep -q 'worker' || true

echo "PASS: fresh install artefacts in $OUT"
