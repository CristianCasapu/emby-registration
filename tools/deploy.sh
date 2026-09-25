#!/bin/bash
# deploy.sh [--restart] : compileaza, copiaza DLL-ul in Emby si (optional) reporneste Emby,
# doar daca nimeni nu reda nimic si nicio redare n-a pornit in ultimele 10 minute.
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet
dotnet build Registration/Registration.csproj -c Release -nologo -v q
sudo install -o emby -g emby -m 644 Registration/bin/Release/net8.0/Registration.dll /var/lib/emby/plugins/Registration.dll
echo "DLL copiat in /var/lib/emby/plugins"
[ "${1:-}" = "--restart" ] || { echo "Fara repornire (se incarca la urmatoarea pornire a Emby)."; exit 0; }

KEY=$(cat "$HOME/src/.emby.key")
PLAYING=$(curl -s "http://127.0.0.1:8096/emby/Sessions?api_key=$KEY" | python3 -c "import json,sys;print(sum(1 for s in json.load(sys.stdin) if s.get('NowPlayingItem')))")
RECENT=$(python3 - <<'PY'
import datetime,re
cut=datetime.datetime.now()-datetime.timedelta(minutes=10); n=0
for line in open('/var/lib/emby/logs/embyserver.txt',errors='replace'):
    if 'Playback start' in line:
        m=re.match(r'(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)',line)
        if m and datetime.datetime.strptime(m.group(1),'%Y-%m-%d %H:%M:%S')>cut: n+=1
print(n)
PY
)
if [ "$PLAYING" != "0" ] || [ "$RECENT" != "0" ]; then
  echo "NU repornesc: $PLAYING redari active, $RECENT porniri in ultimele 10 minute."; exit 2
fi
sudo systemctl restart emby-server
echo "Emby repornit."
