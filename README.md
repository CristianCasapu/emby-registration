# Înregistrare — conturi noi pentru Emby

Plugin pentru Emby Server 4.9 prin care vizitatorii își cer singuri un cont, pe o pagină publică protejată împotriva roboților. Contul se creează dezactivat, ascuns și cu drepturi minime, iar administratorul îl aprobă din panoul Emby.

*English summary: self-service sign-up page for Emby 4.9 with anti-bot protection, admin approval, minimal default rights and signed self-updates from GitHub Releases.*

## Ce face

- **Pagină publică** `https://<server>/emby/Registration/Page`, în română și engleză, gândită întâi pentru telefon:
  - nume de utilizator (verificat pe loc dacă e liber), prenume, nume, e-mail (cu sugestii pentru greșeli de tipar), telefon în format internațional (selector de țară, `+40…`), parolă cu indicator de putere, PIN de profil opțional;
  - ecran final potrivit modului: cerere trimisă / verifică-ți e-mailul / cont gata.
- **Aprobare** din Tablou de bord → Înregistrare → Cereri: sub-tab-urile „În așteptare” și „Procesate”, cu contoare, căutare, aprobare/respingere individuală sau în bloc, export CSV.
- **Moduri:** aprobare manuală (implicit), doar cu cod de invitație, automat. Confirmarea adresei de e-mail se poate porni separat.
- **Drepturi implicite:** fără administrare, descărcare, sincronizare, partajare, ștergere; contul nu apare pe ecranul de conectare. Bibliotecile: toate sau doar cele alese. Un paznic retrage drepturile interzise dacă apar la un cont gestionat; adminul poate elibera un cont anume.
- **Notificări** pe canale pornite separat: jurnalul de activitate Emby, e-mail, Telegram; opțional grupate.
- **Actualizare din GitHub** din panou, cu verificarea semnăturii, și revenire la versiunea anterioară.

## Protecție anti-roboți și securitate

| Strat | Ce face |
|---|---|
| Cloudflare Turnstile (opțional) | Verificare invizibilă pentru majoritatea oamenilor; validată pe server |
| Proof-of-work | Browserul face un calcul SHA-256 de ~1 s cât se completează formularul |
| Capcană + timp minim | Un câmp ascuns și un timp minim de completare |
| Token de formular | HMAC-SHA256, legat de adresa IP, valabil 2 ore, folosit o singură dată |
| Limite | Pe IP, pe rețea (/24, /64) și în total; răspuns 429 cu `Retry-After` |
| Liste | E-mailuri temporare, domenii și adrese IP blocate |

- Parola nu este păstrată de plugin: contul Emby se creează dezactivat chiar la cerere, iar aprobarea doar îl activează.
- Parolele apărute în scurgeri publice sunt refuzate (Have I Been Pwned, k-anonimitate: din server pleacă doar 5 caractere din hash-ul SHA-1).
- Adresa de e-mail deja folosită primește același răspuns ca una nouă (nu se poate afla cine are cont).
- Pagina publică trimite `Content-Security-Policy` strict, `X-Frame-Options: DENY`, `no-store`; datele venite de la vizitatori se afișează doar ca text.
- Datele personale stau în `/var/lib/emby/plugins/Registration/data/registrations.json`, cu drepturi `0600`. Cererile respinse sau expirate se șterg după 30 de zile (configurabil). La ștergerea unui cont Emby, numele, e-mailul, telefonul și IP-ul cererii se șterg imediat.
- În spatele Cloudflare, adresa vizitatorului vine din antetele proxy. Fără un firewall care să accepte doar Cloudflare, antetele pot fi falsificate și limitele pe IP ocolite (vezi `edge-firewall.sh` din [emby-access-log](https://github.com/CristianCasapu/emby-access-log)).

## PIN

Emby 4.9 nu mai are PIN de conectare doar în rețeaua locală („Easy PIN”). PIN-ul cerut de formular este **PIN-ul de profil** Emby (4 cifre): aplicațiile îl cer când utilizatorul revine pe un dispozitiv pe care e deja conectat, dacă activează PIN-ul pe acel dispozitiv.

## Instalare

1. Descarcă `Registration.dll` din [Releases](https://github.com/CristianCasapu/emby-registration/releases) și pune-l în directorul de plugin-uri Emby (Linux: `/var/lib/emby/plugins/`).
2. Repornește Emby.
3. Tablou de bord → Înregistrare: alege bibliotecile, eventual Turnstile și SMTP, apoi pornește „Înregistrare deschisă”.

Actualizările următoare se instalează din tab-ul Actualizări.

## Dezinstalare

Tablou de bord → Plugin-uri → Înregistrare → Dezinstalează, apoi repornește Emby. Conturile create rămân conturi Emby obișnuite. Datele plugin-ului se șterg doar dacă e pornită opțiunea „La dezinstalare șterge și datele” (tab-ul Întreținere); altfel se pot șterge manual:

```sh
sudo rm -rf /var/lib/emby/plugins/Registration /var/lib/emby/plugins/configurations/Registration.xml
```

## Dezvoltare

```sh
dotnet test -c Release          # teste unitare
tools/deploy.sh [--restart]     # build + copiere în Emby (repornește doar dacă nu se redă nimic)
tools/release.sh 1.2.3          # teste, build, semnătură, tag; GitHub Actions publică release-ul
```

Compilarea folosește assembly-urile serverului din `/opt/emby-server/system` când există (pachetul NuGet 4.9.1.90 nu are `IHasWebPages`).

Release-urile se compilează și se semnează local (ECDSA P-256), într-un commit etichetat cu `dist/`; `.github/workflows/release.yml` verifică semnătura și publică fișierele, fără token personal pe server. cheia publică este `Registration/Updates/release-key.pem`, cheia privată nu este în depozit. Un DLL nesemnat cu ea este refuzat de actualizarea automată.

## Licență

MIT
