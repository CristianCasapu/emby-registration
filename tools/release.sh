#!/bin/bash
# release.sh X.Y.Z : teste, build, semnatura ECDSA P-256 cu cheia privata (in afara depozitului),
# tag si GitHub Release cu Registration.dll + Registration.dll.sig + SHA256SUMS.
# Cere: token GitHub fine-grained (Contents: read/write) in ~/src/.github-registration.token.
set -euo pipefail
cd "$(dirname "$0")/.."
VERSION=${1:?versiunea, ex. 1.0.0}
KEY=${RELEASE_KEY:-$HOME/.config/emby-registration/release-private.pem}
TOKEN_FILE=${GITHUB_TOKEN_FILE:-$HOME/src/.github-registration.token}
REPO=CristianCasapu/emby-registration
export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet

[ -z "$(git status --porcelain)" ] || { echo "Depozitul are modificari necomise."; exit 1; }
[ -f "$KEY" ] || { echo "Lipseste cheia de semnare $KEY"; exit 1; }
[ -f "$TOKEN_FILE" ] || { echo "Lipseste tokenul GitHub $TOKEN_FILE"; exit 1; }

sed -i "s#<Version>[^<]*</Version>#<Version>$VERSION</Version>#" Directory.Build.props
dotnet test -c Release -nologo -v q
OUT=$(mktemp -d)
dotnet build Registration/Registration.csproj -c Release -nologo -v q -o "$OUT/build"
cp "$OUT/build/Registration.dll" "$OUT/Registration.dll"
openssl dgst -sha256 -sign "$KEY" -out "$OUT/Registration.dll.sig" "$OUT/Registration.dll"
openssl dgst -sha256 -verify <(openssl ec -in "$KEY" -pubout 2>/dev/null) -signature "$OUT/Registration.dll.sig" "$OUT/Registration.dll"
(cd "$OUT" && sha256sum Registration.dll Registration.dll.sig > SHA256SUMS)

git add Directory.Build.props
git commit -q -m "Versiunea $VERSION" || true
git tag -a "v$VERSION" -m "Versiunea $VERSION"
git push -q origin HEAD "v$VERSION"

TOKEN=$(cat "$TOKEN_FILE")
NOTES=$(git log --pretty='- %s' "$(git describe --tags --abbrev=0 "v$VERSION^" 2>/dev/null || git rev-list --max-parents=0 HEAD)..v$VERSION" | grep -v "^- Versiunea" || true)
BODY=$(python3 -c 'import json,sys;print(json.dumps({"tag_name":sys.argv[1],"name":sys.argv[1],"body":sys.argv[2]}))' "v$VERSION" "$NOTES")
RELEASE=$(curl -sf -X POST -H "Authorization: Bearer $TOKEN" -H "Accept: application/vnd.github+json" "https://api.github.com/repos/$REPO/releases" -d "$BODY")
UPLOAD=$(echo "$RELEASE" | python3 -c 'import json,sys;print(json.load(sys.stdin)["upload_url"].split("{")[0])')
for f in Registration.dll Registration.dll.sig SHA256SUMS; do
  curl -sf -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/octet-stream" --data-binary @"$OUT/$f" "$UPLOAD?name=$f" -o /dev/null
done
echo "Release v$VERSION publicat: $(echo "$RELEASE" | python3 -c 'import json,sys;print(json.load(sys.stdin)["html_url"])')"
rm -rf "$OUT"
