#!/usr/bin/env bash
# Install a single-user local LeviathanText stack under a prefix, with CLI/API/worker wrappers.
# Usage: scripts/install-local.sh [prefix] [--force]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PREFIX="${1:-$HOME/.leviathantext/app}"
FORCE=0
for arg in "$@"; do [[ "$arg" == "--force" ]] && FORCE=1; done
if [[ -e "$PREFIX" && "$FORCE" -ne 1 ]]; then
  echo "refusing to overwrite $PREFIX; pass --force to replace" >&2
  exit 2
fi
mkdir -p "$(dirname "$PREFIX")"
TMP="$(mktemp -d "${TMPDIR:-/tmp}/lt-install.XXXXXX")"
trap 'rm -rf "$TMP"' EXIT

"$ROOT/scripts/package-release.sh" "$(dotnet --info | awk '/^ RID:/{print $2; exit}')" "$TMP/release" >/dev/null

if [[ -e "$PREFIX" ]]; then
  BACKUP="$PREFIX.backup-$(date -u +%Y%m%dT%H%M%SZ)"
  mv "$PREFIX" "$BACKUP"
  echo "previous install moved to $BACKUP"
fi
mkdir -p "$PREFIX"
cp -R "$TMP/release/." "$PREFIX/"
mkdir -p "$PREFIX/bin"

cat > "$PREFIX/bin/leviathan" <<WRAP
#!/usr/bin/env bash
exec "$PREFIX/cli/LeviathanText.Cli" --store "\${LEVIATHAN_STORE:-\$HOME/.leviathantext/store.db}" "\$@"
WRAP
cat > "$PREFIX/bin/leviathan-api" <<WRAP
#!/usr/bin/env bash
export LeviathanText__StorePath="\${LEVIATHAN_STORE:-\$HOME/.leviathantext/store.db}"
export LeviathanText__WebRoot="$PREFIX/web"
exec "$PREFIX/api/LeviathanText.Api" "\$@"
WRAP
cat > "$PREFIX/bin/leviathan-worker" <<WRAP
#!/usr/bin/env bash
exec "$PREFIX/worker/LeviathanText.Worker" --store "\${LEVIATHAN_STORE:-\$HOME/.leviathantext/store.db}" "\$@"
WRAP
chmod +x "$PREFIX/bin/leviathan" "$PREFIX/bin/leviathan-api" "$PREFIX/bin/leviathan-worker"

cat > "$PREFIX/INSTALL.txt" <<TXT
LeviathanText local install
prefix: $PREFIX

Add $PREFIX/bin to PATH, then:
  leviathan project new <name>
  leviathan import page <projectId> <page.xml>
  leviathan review packet <projectId> <out-dir>
  leviathan-api --urls http://127.0.0.1:5000   # workspace: /ui
  leviathan-worker --once

Store defaults to \$HOME/.leviathantext/store.db; set LEVIATHAN_STORE to override.
This is a single-user/local install; it does not configure SSO or a shared tenant.
Model packages are separate artifacts.
TXT

echo "PASS: installed $PREFIX"
"$PREFIX/bin/leviathan" project list >/dev/null
