#!/bin/bash
# install-login-button.sh [remove|status] : butonul „Creeaza cont” pe ecranul de conectare
# al interfetei web Emby. Adauga in index.html-ul Emby o linie care incarca scriptul servit
# de plugin (/emby/Registration/Assets/login.js). Butonul apare doar cat timp inregistrarea
# e deschisa. index.html apartine lui root si se rescrie la actualizarea Emby: dupa o
# actualizare, rulati din nou scriptul.
set -euo pipefail
INDEX=${EMBY_WEB_INDEX:-/opt/emby-server/system/dashboard-ui/index.html}
TAG='<script src="../emby/Registration/Assets/login.js" defer></script>'
MARK='registration-login-button'

[ -f "$INDEX" ] || { echo "Nu gasesc $INDEX"; exit 1; }
case "${1:-install}" in
  status)
    grep -q "$MARK" "$INDEX" && echo "instalat" || echo "neinstalat" ;;
  remove)
    [ "$(id -u)" = 0 ] || { echo "Ruleaza cu sudo."; exit 1; }
    sed -i "/$MARK/d" "$INDEX"
    echo "Butonul a fost scos din $INDEX" ;;
  install)
    [ "$(id -u)" = 0 ] || { echo "Ruleaza cu sudo."; exit 1; }
    if grep -q "$MARK" "$INDEX"; then echo "Deja instalat."; exit 0; fi
    cp -p "$INDEX" "$INDEX.before-registration"
    sed -i "s#</head>#    $TAG<!-- $MARK -->\n</head>#" "$INDEX"
    grep -q "$MARK" "$INDEX" || { echo "Nu am gasit </head> in $INDEX"; exit 1; }
    echo "Buton instalat in $INDEX (copie: $INDEX.before-registration)" ;;
  *) echo "Folosire: $0 [install|remove|status]"; exit 1 ;;
esac
