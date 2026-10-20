#!/usr/bin/env bash
# Reproducible pilot profile: startup, PAGE import, layout detection, export, backup.
# Uses the P01 artifact store when present; otherwise exits with a clear skip message.
# Usage: scripts/profile-pilot.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CLI="$ROOT/src/LeviathanText.Cli/bin/Release/net10.0/LeviathanText.Cli"
DATA="$ROOT/.artifacts/datasets/p01-v0.9"
OUT="$ROOT/.artifacts/profile"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/lt-profile.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

if [[ ! -x "$CLI" ]]; then
  dotnet build "$ROOT/src/LeviathanText.Cli/LeviathanText.Cli.csproj" -c Release >/dev/null
fi
if [[ ! -d "$DATA" ]]; then
  echo "SKIP: P01 artifact store not present at $DATA" >&2
  exit 2
fi
XML="$(find "$DATA" -maxdepth 1 -name '*.xml' | sort | head -1)"
if [[ -z "$XML" ]]; then
  echo "SKIP: no PAGE XML in $DATA" >&2
  exit 2
fi
mkdir -p "$OUT"

measure() {
  local label="$1"; shift
  /usr/bin/time -l "$@" >"$WORK/$label.out" 2>"$WORK/$label.time"
  local real rss
  real="$(awk '/ real /{print $1; exit}' "$WORK/$label.time")"
  rss="$(awk '/maximum resident set size/{print $1; exit}' "$WORK/$label.time")"
  printf '%s\t%s\t%s\n' "$label" "$real" "$rss"
}

STORE="$WORK/pilot.db"
PROJECT="$("$CLI" --store "$STORE" project new profile-pilot | tail -1)"
IMAGE="${XML%.xml}.jpg"
IMPORT="$("$CLI" --store "$STORE" import image "$PROJECT" "$IMAGE" --kind papyrus-side)"
SURFACE="$(printf '%s\n' "$IMPORT" | sed -n 's/.*surface \([^ ]*\).*/\1/p' | head -1)"

{
  printf 'metric\treal_seconds\tmax_rss_bytes\n'
  measure project_list "$CLI" --store "$STORE" project list
  measure import_image "$CLI" --store "$STORE" import image "$PROJECT" "$IMAGE" --kind papyrus-side
  measure layout_detect "$CLI" --store "$STORE" layout detect "$PROJECT" "$SURFACE"
  measure export_page "$CLI" --store "$STORE" export page "$PROJECT" "$SURFACE"
  measure backup "$CLI" --store "$STORE" backup "$WORK/backup"
} | tee "$OUT/pilot-profile.tsv"

python3 - "$OUT/pilot-profile.tsv" "$OUT/pilot-profile.json" "$XML" <<'PY'
import json, sys
rows = []
for line in open(sys.argv[1], encoding='utf-8'):
    if line.startswith('metric'): continue
    name, real, rss = line.rstrip('\n').split('\t')
    rows.append({'metric': name, 'realSeconds': float(real), 'maxRssBytes': int(rss)})
json.dump({'source': sys.argv[3], 'rows': rows,
           'note': 'Single-page pilot profile; not sustained-collection throughput.'},
          open(sys.argv[2], 'w'), indent=2)
print('wrote', sys.argv[2])
PY
echo "profile: $OUT/pilot-profile.tsv"
