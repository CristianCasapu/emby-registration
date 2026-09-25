#!/bin/bash
# release.sh X.Y.Z [--prerelease] : teste, build, semnatura ECDSA P-256 cu cheia privata (in
# afara depozitului), apoi un commit cu dist/ (in afara branch-ului main) etichetat vX.Y.Z.
# Push-ul tag-ului porneste .github/workflows/release.yml, care verifica semnatura si publica
# release-ul cu token-ul GitHub Actions: pe server ajunge cheia SSH, fara token personal.
set -euo pipefail
cd "$(dirname "$0")/.."
VERSION=${1:?versiunea, ex. 1.0.0}
PRERELEASE=${2:-}
KEY=${RELEASE_KEY:-$HOME/.config/emby-registration/release-private.pem}
export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet

[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "Versiunea trebuie sa fie X.Y.Z"; exit 1; }
[ -f "$KEY" ] || { echo "Lipseste cheia de semnare $KEY"; exit 1; }
[ "$(git branch --show-current)" = "main" ] || { echo "Release-ul se face din main."; exit 1; }
git rev-parse -q --verify "refs/tags/v$VERSION" >/dev/null && { echo "Tag-ul v$VERSION exista deja."; exit 1; }

[ -z "$(git status --porcelain)" ] || { echo "Depozitul are modificari necomise."; git status --short; exit 1; }
sed -i "s#<Version>[^<]*</Version>#<Version>$VERSION</Version>#" Directory.Build.props
if ! git diff --quiet; then
  git commit -q -am "Versiunea $VERSION"
fi

dotnet test -c Release -nologo -v q
OUT=$(mktemp -d)
dotnet build Registration/Registration.csproj -c Release -nologo -v q -o "$OUT/build"
mkdir -p "$OUT/dist"
cp "$OUT/build/Registration.dll" "$OUT/dist/"
openssl dgst -sha256 -sign "$KEY" -out "$OUT/dist/Registration.dll.sig" "$OUT/dist/Registration.dll"
openssl dgst -sha256 -verify Registration/Updates/release-key.pem -signature "$OUT/dist/Registration.dll.sig" "$OUT/dist/Registration.dll"
(cd "$OUT/dist" && sha256sum Registration.dll Registration.dll.sig > SHA256SUMS)
[ "$PRERELEASE" = "--prerelease" ] && echo "pre-release" > "$OUT/dist/PRERELEASE"

PREVIOUS=$(git describe --tags --abbrev=0 --match 'v*' 2>/dev/null || true)
{
  echo "## Versiunea $VERSION"
  echo
  git log --pretty='- %s' ${PREVIOUS:+"$PREVIOUS.."}HEAD | grep -v '^- Versiunea ' || true
  echo
  echo "Instalare: pune \`Registration.dll\` în directorul de plugin-uri Emby și repornește Emby. Actualizările următoare se instalează din pagina plugin-ului."
  echo
  echo "Semnătura (\`Registration.dll.sig\`, ECDSA P-256) se verifică cu cheia publică din depozit:"
  echo '```'
  echo "openssl dgst -sha256 -verify release-key.pem -signature Registration.dll.sig Registration.dll"
  echo '```'
} > "$OUT/dist/NOTES.md"

# Commit-ul etichetat = arborele din main + dist/, fara sa intre in istoria lui main.
INDEX=$(mktemp)
GIT_INDEX_FILE=$INDEX git read-tree HEAD
for f in "$OUT"/dist/*; do
  BLOB=$(git hash-object -w "$f")
  GIT_INDEX_FILE=$INDEX git update-index --add --cacheinfo 100644 "$BLOB" "dist/$(basename "$f")"
done
TREE=$(GIT_INDEX_FILE=$INDEX git write-tree)
COMMIT=$(git commit-tree "$TREE" -p HEAD -m "Release $VERSION")
rm -f "$INDEX"
git tag -a "v$VERSION" "$COMMIT" -m "Versiunea $VERSION"
git push -q origin main "v$VERSION"
echo "Tag v$VERSION trimis; GitHub Actions publica release-ul: https://github.com/CristianCasapu/emby-registration/actions"
rm -rf "$OUT"
