#!/usr/bin/env bash
# Build a reproducible, framework-dependent release directory for one runtime identifier.
# Usage: scripts/package-release.sh [rid] [output-dir]
# Defaults: current SDK RID, .artifacts/releases/<rid>
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="${1:-$(dotnet --info | awk '/^ RID:/{print $2; exit}')}"
OUT="${2:-$ROOT/.artifacts/releases/$RID}"

if [[ -z "$RID" ]]; then
  echo "could not determine a runtime identifier; pass one explicitly (e.g. osx-arm64)" >&2
  exit 2
fi
if [[ -e "$OUT" ]]; then
  echo "refusing to overwrite existing release directory: $OUT" >&2
  exit 2
fi
COMMIT="$(git -C "$ROOT" rev-parse HEAD)"
DIRTY=false
if [[ -n "$(git -C "$ROOT" status --porcelain)" ]]; then DIRTY=true; fi

echo "== publish ($RID) =="
mkdir -p "$OUT/cli" "$OUT/api" "$OUT/worker" "$OUT/web"
BUILD_LOG="$(mktemp "${TMPDIR:-/tmp}/lt-package-build.XXXXXX")"
trap 'rm -f "$BUILD_LOG"' EXIT
for project in Cli Api Worker; do
  if ! dotnet publish "$ROOT/src/LeviathanText.$project" -c Release -r "$RID" --self-contained false \
    -m:1 -p:UseSharedCompilation=false \
    -o "$OUT/$(echo "$project" | tr '[:upper:]' '[:lower:]')" >"$BUILD_LOG" 2>&1; then
    cat "$BUILD_LOG" >&2
    exit 1
  fi
done

echo "== web =="
if ! ( cd "$ROOT/web" && npx ng build >"$BUILD_LOG" 2>&1 ); then
  cat "$BUILD_LOG" >&2
  exit 1
fi
cp -R "$ROOT/web/dist/web/browser/." "$OUT/web/"
cp "$ROOT/LICENSE" "$ROOT/NOTICE" "$OUT/"
cp "$ROOT/docs/release/known-issues.md" "$OUT/KNOWN-ISSUES.md"

echo "== independence check =="
if grep -R -q -E --include='*.csproj' --include='*.cs' 'PackageReference Include="[^"]*Kraken|using Kraken|Python\.Runtime|python3' "$ROOT/src"; then
  echo "first-party source references Kraken/Python; refusing to label this package independent" >&2
  exit 1
fi

DOTNET_VERSION="$(dotnet --version)"
cat > "$OUT/README.txt" <<TXT
LeviathanText pilot release
runtime: $RID
commit: $COMMIT
working tree dirty: $DIRTY
dotnet: $DOTNET_VERSION
built: $(date -u +%Y-%m-%dT%H:%M:%SZ)

This is a framework-dependent pilot build. Install the matching .NET runtime first.
  cli/LeviathanText.Cli --help
  LeviathanText__WebRoot="\$PWD/web" api/LeviathanText.Api --urls http://127.0.0.1:5000
      (workspace at http://127.0.0.1:5000/ui; omit WebRoot for API-only)
  worker/LeviathanText.Worker --once
  web/  Angular assets (also usable with any static file server)

Model packages are NOT bundled; train or authorize them separately. No Python or Kraken
runtime is required. See docs/release/packaging.md for supported/untested targets.
TXT

echo "== manifest =="
python3 - "$OUT" "$RID" "$COMMIT" "$DOTNET_VERSION" "$DIRTY" <<'PY'
import hashlib, json, os, sys
root, rid, commit, dotnet, dirty = sys.argv[1:6]
files = []
for dirpath, _, names in os.walk(root):
    for name in sorted(names):
        path = os.path.join(dirpath, name)
        rel = os.path.relpath(path, root)
        data = open(path, 'rb').read()
        files.append({"path": rel.replace(os.sep, '/'), "bytes": len(data),
                      "sha256": hashlib.sha256(data).hexdigest()})
manifest = {"product": "LeviathanText", "releaseKind": "pilot-framework-dependent",
            "runtime": rid, "commit": commit, "workingTreeDirty": dirty == "true", "dotnetVersion": dotnet,
            "pythonRequired": False, "krakenRequired": False,
            "modelArtifactsBundled": False, "files": files}
with open(os.path.join(root, "release-manifest.json"), "w", encoding="utf-8") as fh:
    json.dump(manifest, fh, indent=2, sort_keys=True)
    fh.write("\n")
print(f"{len(files)} files, {sum(f['bytes'] for f in files)} bytes")
PY

# BSD tar on macOS otherwise adds AppleDouble members absent from the manifest.
COPYFILE_DISABLE=1 tar -czf "$OUT.tar.gz" -C "$OUT" .
echo "== done =="
echo "directory: $OUT"
echo "archive:   $OUT.tar.gz"
