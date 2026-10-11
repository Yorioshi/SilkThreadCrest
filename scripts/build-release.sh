#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
GAME=${1:-${GAME_MANAGED_DIR:-}}
CORE=${2:-${BEPINEX_CORE_DIR:-}}
if [[ -z "$GAME" || -z "$CORE" ]]; then
  echo 'Uso: scripts/build-release.sh "/caminho/Silksong_Data/Managed" "/caminho/BepInEx/core"' >&2
  exit 2
fi
for file in Assembly-CSharp.dll TeamCherry.TK2D.dll Newtonsoft.Json.dll; do
  [[ -f "$GAME/$file" ]] || { echo "Dependência ausente: $GAME/$file" >&2; exit 2; }
done
for file in BepInEx.dll 0Harmony.dll; do
  [[ -f "$CORE/$file" ]] || { echo "Dependência ausente: $CORE/$file" >&2; exit 2; }
done
bash "$ROOT/scripts/test.sh"
dotnet build "$ROOT/src/SilkThreadCrest.csproj" -c Release -v:minimal "-p:GameManagedDir=$GAME" "-p:BepInExCoreDir=$CORE"
VERSION=$(sed -n 's/.*PluginVersion = "\([^"]*\)";.*/\1/p' "$ROOT/src/SilkThreadCrestPlugin.cs")
[[ -n "$VERSION" ]] || { echo 'Não foi possível ler PluginVersion.' >&2; exit 2; }
OUT="$ROOT/artifacts/$VERSION"
mkdir -p "$OUT"
STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT
mkdir -p "$STAGE/windows/BepInEx/plugins/SilkThreadCrest" "$STAGE/source"
cp "$ROOT/src/bin/Release/net472/SilkThreadCrest.dll" "$STAGE/windows/BepInEx/plugins/SilkThreadCrest/"
cp -R "$ROOT/Skins" "$STAGE/windows/BepInEx/plugins/SilkThreadCrest/"
cp "$ROOT/README.md" "$STAGE/windows/"
for name in src Skins docs scripts tests .github README.md AGENTS.md CONTRIBUTING.md CHANGELOG.md NOTICE.md .gitignore .gitattributes; do
  [[ ! -e "$ROOT/$name" ]] || cp -R "$ROOT/$name" "$STAGE/source/"
done
find "$STAGE/source" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
cp "$ROOT/src/bin/Release/net472/SilkThreadCrest.dll" "$OUT/SilkThreadCrest-$VERSION.dll"
# A fresh ZIP prevents obsolete entries from surviving a rebuild.
(cd "$STAGE/windows" && zip -qr "$STAGE/windows.zip" .)
(cd "$STAGE/source" && zip -qr "$STAGE/source.zip" .)
mv "$STAGE/windows.zip" "$OUT/SilkThreadCrest-$VERSION-Windows.zip"
mv "$STAGE/source.zip" "$OUT/SilkThreadCrest-$VERSION-Source.zip"
unzip -tq "$OUT/SilkThreadCrest-$VERSION-Windows.zip"
unzip -tq "$OUT/SilkThreadCrest-$VERSION-Source.zip"
ls -lh "$OUT"
echo 'Build criada. Validação manual no jogo continua necessária.'
