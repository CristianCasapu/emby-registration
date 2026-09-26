# Plugin Emby „Înregistrare” — plan și progres

> Plugin pentru Emby 4.9.5 (poweredge) care permite crearea de conturi noi de către vizitatori, printr-o pagină publică protejată anti-roboți, cu drepturi minime implicite și administrare completă din panoul de control Emby.
>
> - **Repo:** `CristianCasapu/emby-registration` (public, branch `main`)
> - **Director local:** `~/src/emby-registration`
> - **Plugin GUID:** `f94669d3-60d5-4470-b709-8eda287b7b0c`
> - **DLL:** `/var/lib/emby/plugins/Registration.dll`, date în `/var/lib/emby/plugins/Registration/`
> - **Țintă:** Emby Server 4.9.5, .NET 8, C#; aceleași convenții ca `emby-access-log` și `headend`

---

## 0. Progres

Legendă: ⬜ neînceput · 🟨 în lucru · ✅ gata · ⛔ blocat · ❓ așteaptă decizie

| # | Etapă | Stare | Note |
|---|-------|-------|------|
| 0 | Decizii (secțiunea 9) | ✅ | Răspunse 2026-09-25 |
| 1 | Schelet proiect, repo GitHub public, cheie de deploy | ✅ | Push prin aliasul SSH `github-registration` (cheia de cont); token pentru release-uri încă lipsește |
| 2 | Spike tehnic: rută neautentificată, creare user, politici, PIN | ✅ | API-uri verificate prin reflecție pe 4.9.5; vezi „Descoperiri” |
| 3 | Configurație plugin + pagina de administrare | ✅ | 9 tab-uri; testat în Emby (aprobare/respingere din UI, salvare, invitații) |
| 4 | Pagina publică de înregistrare (UI) | ✅ | Testată headless (desktop, mobil, RO/EN) pe server de probă |
| 5 | Backend înregistrare + validări | ✅ | |
| 6 | Anti-roboți și limitare de rată | ✅ | |
| 7 | Politica implicită de acces + paznic de politică | ✅ | |
| 8 | Aprobare, verificare e-mail, notificări | ✅ | SMTP/Telegram netestate fără date reale |
| 9 | Actualizare din GitHub | ✅ | 0.9.0 → 1.0.0 instalat prin updater (semnătură verificată, backup), repornire prin plugin |
| 10 | Dezinstalare curată + export/ștergere date (GDPR) | ✅ | |
| 11 | Teste (unitare + UI headless) | ✅ | 81 unitare; `reg-ui.js` (pagina publică), `reg-e2e.js` (35 verificări pe Emby real), `reg-admin.js` (panou) |
| 12 | Documentație (README RO/EN), prima versiune `v1.0.0` | ✅ | v1.0.0 publicat |
| 14 | Trimite datele de acces (text, copiere, e-mail, WhatsApp, Telegram, SMS, partajare) | ✅ | Cerută 2026-09-25; parolă nouă generată/scrisă sau fără parolă |
| 15 | Buton „Creează cont nou” pe ecranul de conectare (web) | ✅ | `tools/install-login-button.sh`; aplicațiile native nu pot fi modificate |
| 16 | Un singur cont per dispozitiv / adresă, anti-roboți întărit, pagină doar pentru vizitatori legitimi | ✅ | v1.3.0; e2e pe Emby real (centre de date, Tor, adresă duplicată, dispozitiv, telefon, scripturi) |
| 17 | Cod de acces rotativ (5 caractere, fără caractere asemănătoare), blocare IP + dispozitiv 24 h după 2 greșeli, deblocare din plugin; verificare chei Turnstile | ✅ | v1.4.0; codul e oprit până îl pornește adminul |
| 13 | Deploy pe poweredge | ✅ | Instalat 2026-09-25; înregistrarea rămâne închisă până o deschide adminul |

Jurnal de progres (se completează pe parcurs):

| Data | Commit | Ce s-a făcut |
|------|--------|--------------|
| 2026-09-25 | — | Plan inițial |
| 2026-09-25 | — | Decizii D1–D8 răspunse și integrate în plan |
| 2026-09-25 | inițial | Implementare completă etapele 2–10, teste unitare și UI public |
| 2026-09-25 | 100c699 | Publicat pe GitHub (public) |
| 2026-09-25 | v0.9.0 | Primul release (pre-release), publicat de GitHub Actions, semnătură verificată |
| 2026-09-25 | v1.0.0 | Release 1.0.0, instalat pe server prin actualizarea din plugin |
| 2026-09-26 | v1.4.0 | Cod de acces, blocări separate IP/dispozitiv, buton „Verifică cheile” Turnstile |
| 2026-09-26 | v1.3.0 | Un cont per dispozitiv/adresă, doar vizitatori legitimi, 107 teste unitare |
| 2026-09-26 | v1.2.0 | Înregistrare deschisă (aprobare manuală), biblioteci Filme + Seriale (Copii e în Filme), buton de login instalat |
| 2026-09-25 | — | Deploy pe server; corectat: configurația nu se poate salva în constructorul plugin-ului; test e2e complet trecut |

### Descoperiri din spike (Emby 4.9.5)

- **PIN:** „Easy PIN” (conectare cu PIN doar în rețeaua locală) nu mai există în 4.9 (`updateEasyPassword` e refuzat de UI pentru servere ≥ 4.8.0.40). PIN-ul disponibil este **PIN-ul de profil** (`UserConfiguration.ProfilePin`, exact 4 cifre): aplicațiile îl cer la revenirea pe un dispozitiv deja conectat, dacă e activat pe acel dispozitiv. Plugin-ul folosește acest PIN.
- **Creare cont:** `IUserManager.CreateUser(name, UserPolicy)` primește politica direct, deci contul nu are nicio clipă drepturile implicite Emby.
- **Parola nu se păstrează:** contul Emby se creează **dezactivat chiar la cerere** (cu parola setată în Emby); aprobarea doar îl activează, respingerea/expirarea îl șterg. Username-ul e rezervat din prima clipă.
- **Login disclaimer:** Emby îl afișează cu `textContent` → doar text simplu, fără link apăsabil. Plugin-ul adaugă/scoate un text marcat, fără să atingă restul.
- **Pagina publică:** rute `[Unauthenticated]` (ca la Headend) sub `/emby/Registration/...`; aplicația Android Emby nu e implicată (se deschide în browser).
- **Headend:** retrage Live TV la conturile noi în primele 15 s; plugin-ul reaplică setările la aprobare (și după 20 s în modul automat).

---

## 1. Cerințe (din cererea utilizatorului)

| Cerință | Cum o acoperim |
|---------|----------------|
| Plugin Emby pentru înregistrare, activat din panoul de control | Pagină de configurare în Emby (Plugins → Înregistrare), comutator „Înregistrare deschisă” |
| Configurație: activare / dezactivare | Comutator global + fereastră de timp opțională + limită de conturi (secțiunea 3) |
| Dezinstalare | Din Emby (Plugins → Uninstall) + opțiune „șterge și datele plugin-ului” (secțiunea 10) |
| Date solicitate: username, prenume, nume, e-mail, telefon internațional | Formular cu validare client + server; telefon în format E.164 (`+40712345678`) |
| Protecție anti-roboți | Mai multe straturi (secțiunea 6): Cloudflare Turnstile / proof-of-work, honeypot, timp minim, limitare de rată |
| Acces implicit: fără descărcare, fără share, fără administrare | Politică implicită restrictivă, re-aplicată de un paznic (secțiunea 7) |
| Cod PIN ales la înregistrare (opțional) | Câmp PIN opțional → Emby „Easy PIN” (vezi decizia D5) |
| Fără afișare în panou | `IsHidden`, `IsHiddenRemotely`, `IsHiddenFromUnusedDevices` = true (utilizatorul nu apare pe ecranul de login) |
| Repo public pe GitHub, în contul meu | `CristianCasapu/emby-registration`, fără secrete / adrese interne în cod |
| Actualizare din GitHub | Verificare GitHub Releases + buton „Actualizează” în panou, cu verificare SHA-256 (secțiunea 8) |

---

## 2. Arhitectură

```
Registration/
├── Plugin.cs                    BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
├── PluginConfiguration.cs       toate setările (secțiunea 3)
├── Api/
│   ├── PublicService.cs         rute [Unauthenticated]: pagina, /challenge, /check-username, /register, /verify-email
│   └── AdminService.cs          rute doar-admin: cereri, aprobare/respingere, statistici, update, export
├── Web/                         resurse încorporate (EmbeddedResource)
│   ├── register.html/.css/.js   pagina publică (fără dependențe externe în afară de Turnstile)
│   └── config.html/.js          panoul de administrare (tab-uri + carduri, ca la Headend)
├── Registration/
│   ├── RegistrationService.cs   fluxul: validare → anti-bot → creare user → politică → PIN → stocare
│   ├── Validators.cs            username, nume, e-mail, telefon E.164, parolă, PIN
│   └── PhoneNumbers.cs          prefixe de țară + lungimi valide (tabel mic, fără libphonenumber)
├── Security/
│   ├── Turnstile.cs             verificare server-side token Cloudflare
│   ├── ProofOfWork.cs           provocare SHA-256 (alternativă fără cont extern)
│   ├── FormToken.cs             token HMAC semnat (timestamp + IP) împotriva trimiterilor directe
│   ├── RateLimiter.cs           ferestre glisante pe IP / subrețea / global
│   └── Blocklists.cs            domenii e-mail temporare, username-uri rezervate, IP-uri blocate
├── Policy/
│   └── PolicyGuard.cs           aplică și re-aplică politica restrictivă (UserCreated, UserPolicyUpdated)
├── Storage/
│   └── RequestStore.cs          JSON atomic (tmp + rename) în /var/lib/emby/plugins/Registration/
├── Notifications/
│   ├── Mailer.cs                SMTP (MailKit nu — System.Net.Mail ajunge) pentru verificare / aprobare
│   └── AdminNotifier.cs         jurnal de activitate Emby + e-mail admin (+ opțional Telegram)
└── Updates/
    └── GitHubUpdater.cs         verificare release, descărcare, SHA-256, înlocuire DLL, cerere restart
Registration.Tests/              xUnit: validatori, rate limiter, form token, PoW, updater (fixture-uri)
tools/                           build.sh, release.sh, deploy.sh
```

**Principii**

- Contul se creează prin `IUserManager` (API intern Emby), nu prin HTTP cu cheie de admin.
- Datele în plus (prenume, nume, e-mail, telefon, IP, data) se păstrează în stocarea plugin-ului, legate de `UserId`. Emby nu are câmpuri pentru e-mail/telefon.
- Numele afișat în Emby: configurabil (`username` sau `Prenume N.`).
- Ruta publică ține pagina, CSS și JS pe aceeași origine (nu depinde de restricțiile aplicațiilor Emby pentru pagini de plugin: aplicația Android blochează paginile de plugin ne-listate, deci linkul se deschide în browser).

---

## 3. Configurație (panoul de control)

Pagina de administrare, cu tab-uri (stil Headend: carduri, tooltip de ajutor, valori implicite identice cu `PluginConfiguration.cs`).

### Tab „General”
- **Înregistrare deschisă** (implicit: oprit — plugin-ul nou instalat nu deschide nimic până nu vrei tu)
- Fereastră de timp opțională: deschis de la / până la
- Limită totală de conturi create prin plugin; limită pe zi
- **Mod aprobare:** aprobare manuală de admin (implicit, D1) / automat / cod de invitație obligatoriu
- Mesaj afișat când înregistrarea e închisă
- Link public (afișat pentru copiere + cod QR): `https://<domeniu>/emby/Registration/Page`
- Afișează link „Creează cont” pe ecranul de login Emby (prin „Login disclaimer”, dacă se confirmă că acceptă HTML — spike etapa 2)

### Tab „Formular”
- Câmpuri obligatorii fixe: username, prenume, nume, e-mail, telefon, parolă
- PIN opțional: activ / inactiv, lungime 4–8 cifre. Este „Easy PIN” din Emby, **valabil doar în rețeaua locală** (D5); pagina explică asta utilizatorului
- Țară implicită pentru telefon (+40), listă de țări permise (opțional)
- Reguli username: lungime 3–32, caractere `a-z 0-9 . _ -`, listă de nume rezervate (admin, root, emby, support…)
- Politică parolă: minim 10 caractere + verificare în Have I Been Pwned (D6: activă implicit, se poate opri; k-anonimitate, doar 5 caractere din hash-ul SHA-1 pleacă din server; dacă serviciul nu răspunde, înregistrarea nu se blochează)
- Text termeni / politică de confidențialitate + bifă de acord obligatorie
- Limbă: RO / EN (automat după browser)

### Tab „Acces implicit”
Politica aplicată fiecărui cont nou (vezi secțiunea 7); fiecare opțiune vizibilă, dar valorile periculoase (administrator etc.) sunt blocate pe „nu”.
- **Biblioteci accesibile** (D4): „Toate bibliotecile” sau „Doar cele selectate” (listă cu bife din bibliotecile serverului, citită live din Emby). Opțiunea „toate” include automat și bibliotecile adăugate ulterior.
- Live TV (implicit oprit; coordonat cu Headend, care îl oprește deja la userii noi)
- Număr maxim de fluxuri simultane (implicit 1), limită bitrate la distanță
- Expirare cont opțională (cont de probă: dezactivare automată după N zile)

### Tab „Anti-roboți”
- Furnizor: Cloudflare Turnstile (cheie site + cheie secretă) / Proof-of-work local / ambele
- Timp minim de completare (implicit 4 s), honeypot (mereu activ)
- Limite: încercări pe IP / oră, pe /24 (IPv4) sau /64 (IPv6) / zi, total / oră
- Domenii e-mail blocate (listă temporare inclusă + adăugiri manuale), IP-uri / țări blocate

### Tab „Cereri” (D1)
- Contor pe tab-ul principal (ex. „Cereri (3)”) — vezi imediat câte așteaptă.
- **Două sub-tab-uri cu contoare:**
  - **În așteptare (N)** — cereri noi, cele mai vechi primele; butoane mari Aprobă / Respinge pe fiecare rând + aprobare în bloc (bife);
  - **Procesate (N)** — aprobate și respinse (și expirate), cu filtru rapid pe stare (contoare separate: aprobate N, respinse N) și data deciziei.
- Căutare; detalii pe cerere (IP, țară, dispozitiv, ora, e-mail confirmat sau nu)
- Acțiuni: aprobă, respinge (cu motiv opțional trimis pe e-mail), șterge, blochează IP / domeniu
- Export CSV
- Coduri de invitație: generare, limită de utilizări, expirare, revocare

### Tab „Notificări”
- SMTP (server, port, TLS, utilizator, parolă, expeditor) + buton „Trimite e-mail de test”
- **Confirmare e-mail** (D2): comutator activ / inactiv. Activ → cererea ajunge la aprobare abia după ce utilizatorul confirmă adresa (link cu token, valabil 24 h). Inactiv → cererea ajunge direct în „În așteptare”. Se poate activa doar dacă SMTP e configurat și testat.
- **Notificări admin la cerere nouă** (D7) — fiecare canal cu comutatorul lui și buton „Trimite test”:
  - jurnal de activitate Emby (implicit activ);
  - e-mail către una sau mai multe adrese (implicit inactiv);
  - Telegram: token bot + chat id (implicit inactiv);
  - evenimente alese: cerere nouă, e-mail confirmat, cerere aprobată / respinsă, limită anti-bot depășită;
  - rezumat grupat (max. 1 mesaj la N minute) ca să nu fii inundat.
- **E-mailuri către utilizator** (dacă SMTP e configurat): cerere primită, cont aprobat (cu linkul serverului și aplicațiile), cerere respinsă (motiv opțional)

### Tab „Actualizări”
- Versiune instalată, ultima versiune pe GitHub, note de lansare
- Verificare automată zilnică (activ / inactiv), canal: stabil / pre-release
- Buton „Actualizează acum” + „Repornește Emby” (cu avertisment dacă există redări active)

### Tab „Întreținere”
- Statistici (conturi create, blocate de anti-bot, pe zi)
- „Șterge toate datele plugin-ului” (pregătire pentru dezinstalare)

---

## 4. Pagina publică de înregistrare — experiența utilizatorului

- **Aspect Emby:** fundal închis, logo-ul serverului, un singur card centrat; arată acasă lângă ecranul de login Emby. Mobil întâi (majoritatea vor veni de pe telefon), fără scroll orizontal.
- **Validare pe loc**, cu mesaje clare lângă câmp (nu doar la trimitere):
  - username disponibil / ocupat (cu limită de rată, rezultatul e același tip de mesaj pentru rezervat / ocupat)
  - telefon: selector de țară cu steag + prefix, formatare automată la tastare, salvat ca E.164
  - e-mail: sugestie la greșeli frecvente (`gmial.com` → `gmail.com`)
  - parolă: indicator de putere, buton afișează / ascunde, confirmare parolă
  - PIN: câmp numeric (`inputmode="numeric"`), explicație scurtă a ce face
- `autocomplete` corect pe fiecare câmp (manageri de parole, completare automată pe telefon).
- **Turnstile invizibil / gestionat** — majoritatea oamenilor nu văd nicio provocare.
- Buton de trimitere cu stare de încărcare; formularul nu se pierde la eroare.
- **Ecran de final** diferit după mod:
  - automat → „Contul e gata”, butonul „Intră în Emby” + linkuri către aplicații (Android, iOS, TV) + adresa serverului de introdus manual;
  - aprobare → „Cererea a fost trimisă, vei primi un e-mail”;
  - verificare e-mail → „Verifică-ți căsuța”.
- Mesaj prietenos când înregistrarea e închisă sau limita e atinsă.
- Accesibilitate: etichete reale, contrast, navigare din tastatură, erori anunțate (`aria-live`).
- RO / EN.

---

## 5. Fluxul de înregistrare (server)

1. `GET /Registration/Page` → HTML + token de formular semnat (HMAC, conține ora emiterii și hash-ul IP-ului).
2. `POST /Registration/Register`:
   1. înregistrarea e deschisă? limita nu e atinsă?
   2. limitare de rată (IP, subrețea, global) — înainte de orice muncă scumpă;
   3. honeypot gol, timp de completare ≥ minim, token de formular valid și nefolosit;
   4. Turnstile / proof-of-work verificat server-side;
   5. validare câmpuri (aceleași reguli ca în browser, sursa de adevăr e serverul);
   6. username liber (fără diferență majuscule/minuscule, normalizare Unicode NFKC, fără caractere care arată la fel), e-mail neînregistrat;
   7. după modul ales:
      - **aprobare (implicit):** [confirmare e-mail, dacă e activă] → „În așteptare” → la aprobare: creează user → politică → parolă → PIN → e-mail „cont aprobat”;
      - **automat:** creează user → politică → parolă → PIN → salvează → notifică;
      - în modurile cu așteptare se salvează cererea (parola ca hash PBKDF2 temporar, NU în clar) și creează contul abia la aprobare / verificare; cererile expiră după 7 zile;
   8. răspuns generic în caz de eroare internă; detaliile doar în jurnalul serverului.
3. Totul trece prin jurnalul de activitate Emby („Cont nou creat prin înregistrare: X de la IP Y”), deci apare și în plugin-ul Jurnal de acces.

---

## 6. Securitate

### Anti-roboți (în straturi)
| Strat | Rol |
|-------|-----|
| Cloudflare Turnstile | Provocare principală; serverul e deja în spatele Cloudflare; gratuit, fără CAPTCHA vizual |
| Proof-of-work local (SHA-256, dificultate ajustabilă) | Alternativă fără cont extern / fallback; costă câteva secunde de CPU pe client |
| Honeypot (câmp ascuns) + timp minim | Opresc roboții simpli fără cost pentru oameni |
| Token de formular HMAC, o singură folosire | Nu se poate trimite direct la API fără pagina încărcată |
| Limitare de rată IP / subrețea / global | Oprește avalanșele; răspuns 429 cu `Retry-After` |
| Domenii e-mail temporare blocate | Reduce conturile de unică folosință |

### Protecția contului și a serverului
- **Niciodată administrator:** plugin-ul nu are cale de cod care să seteze drepturi de admin; `PolicyGuard` verifică la creare și la orice `UserPolicyUpdated` pentru conturile create prin plugin că politica restrictivă rămâne (doar dacă admin-ul nu a „eliberat” explicit contul din panou — opțiune „gestionat manual”).
- Contul se creează **dezactivat** (`IsDisabled=true`) și se activează doar după ce politica e aplicată complet — nu există fereastră în care contul are drepturile implicite ale Emby.
- **Enumerare:** verificarea username-ului e limitată la rată; e-mailurile existente primesc același mesaj ca cele noi („dacă adresa e validă, vei primi un e-mail”).
- **XSS stocat:** prenumele / numele / username-ul sunt date introduse de străini și apar în panoul admin → totul se afișează cu `textContent`, niciodată `innerHTML`; validare strictă a caracterelor la intrare.
- **Anteturi pe pagina publică:** `Content-Security-Policy` strict (doar `self` + `challenges.cloudflare.com`), `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cache-Control: no-store`.
- **IP real:** în spatele Cloudflare, IP-ul vine din `CF-Connecting-IP` / `X-Forwarded-For`. Firewall-ul de margine (`edge-firewall.sh` din emby-access-log) face ca doar Cloudflare să ajungă la Emby, deci antetele sunt de încredere. Fără el, limitarea de rată pe IP poate fi ocolită — de menționat în README.
- **Secrete** (cheie Turnstile, parolă SMTP, cheie HMAC) doar în configurația plugin-ului pe server, niciodată în repo; cheia HMAC generată aleator la prima pornire.
- Parolele nu apar în jurnale; PIN-ul nu se stochează de plugin (îl primește doar Emby).
- **Repo public:** fără domenii, IP-uri interne, chei sau capturi cu date reale. Verificare înainte de fiecare push (`git grep` pe adrese / domenii cunoscute).

### Date personale (GDPR — nume, e-mail, telefon, IP)
- Bifă de acord + text de confidențialitate configurabil.
- Doar datele necesare; cererile respinse / expirate se șterg automat după N zile (implicit 30).
- Export și ștergere a datelor unui utilizator din panou; la ștergerea contului Emby se șterg și datele din plugin (`UserDeleted`).

---

## 7. Politica implicită de acces

Aplicată fiecărui cont nou (nume confirmate în `MediaBrowser.Model.dll` 4.9.5):

| Câmp | Valoare | De ce |
|------|---------|-------|
| `IsAdministrator` | false | fără administrare |
| `IsHidden`, `IsHiddenRemotely`, `IsHiddenFromUnusedDevices` | true | nu apare pe ecranul de login |
| `EnableContentDownloading` | false | fără descărcare |
| `EnableSyncTranscoding`, `EnableMediaConversion` | false | fără descărcare / conversie |
| `EnablePublicSharing`, `AllowSharingPersonalItems` | false | fără share |
| `AllowCameraUpload` | false | fără încărcare |
| `EnableContentDeletion`, `EnableContentDeletionFromFolders` = [] | false | fără ștergere |
| `EnableSubtitleManagement`, `EnableSubtitleDownloading` | false | fără administrare subtitrări |
| `EnableLiveTvManagement` | false | fără administrare Live TV |
| `EnableRemoteControlOfOtherUsers`, `EnableSharedDeviceControl` | false | nu controlează alți useri / dispozitive |
| `EnableUserPreferenceAccess` | true | își poate schimba parola și preferințele |
| `EnableAllFolders` / `EnabledFolders` | din configurație | „toate” → `EnableAllFolders=true`; „selectate” → lista aleasă |
| `EnableLiveTvAccess` | din configurație (implicit false) | |
| `SimultaneousStreamLimit` | din configurație (implicit 1) | |
| `RemoteClientBitrateLimit` | din configurație | |
| `IsDisabled` | false după aplicarea politicii (sau true până la aprobare) | |

Câmpurile exacte care lipsesc din listă (ex. gestionare colecții / playlist-uri, dacă există în 4.9.5) se verifică în spike și se adaugă tot pe „false”.

---

## 8. Actualizare din GitHub

Emby nu acceptă depozite de plugin-uri externe (catalogul oficial e fix), așa că actualizarea o face plugin-ul:

1. **Release:** `tools/release.sh vX.Y.Z` compilează, rulează testele, creează tag-ul și publică pe GitHub Release: `Registration.dll` + `Registration.dll.sha256` (+ semnătură, vezi mai jos). Versiunea din DLL = versiunea din tag.
2. **Verificare:** plugin-ul citește `https://api.github.com/repos/CristianCasapu/emby-registration/releases/latest` (repo public, fără token; cu `ETag` ca să nu consume limita), zilnic și la cerere din panou.
3. **Actualizare:** descarcă DLL-ul, verifică SHA-256 **și semnătura ECDSA P-256 (.NET 8 nu are Ed25519 inclus)** cu cheia publică încorporată în plugin (dacă cineva ar compromite contul GitHub, nu poate împinge un DLL nesemnat — cheia privată stă doar pe poweredge, în afara repo-ului). Scrie `Registration.dll.new`, apoi `rename` peste DLL (atomic, Linux permite înlocuirea unui fișier încărcat).
4. **Restart:** panoul arată „Repornire necesară”; butonul de restart verifică întâi redările active și cere confirmare dacă cineva se uită.
5. **Revenire:** versiunea anterioară păstrată ca `Registration.dll.bak` în directorul de date; buton „Revino la versiunea anterioară”.

Compilarea: DLL-urile Emby 4.9.5 nu pot fi puse în repo public (și pachetul NuGet 4.9.1.90 nu are `IHasWebPages`), deci build-ul se face pe poweredge, contra `/opt/emby-server/system`, iar release-ul se publică de acolo. Pentru a crea release-uri e nevoie de un **token GitHub fine-grained** (doar repo-ul acesta, permisiune Contents: read/write), păstrat în `~/src/.github-registration.token` (D3). Cheia SSH doar poate împinge cod, nu poate crea release-uri.

---

## 9. Decizii (răspunse 2026-09-25)

| # | Întrebare | Decizie |
|---|-----------|---------|
| D1 | Activare automată sau cu aprobare? | **Cu aprobarea adminului.** Două sub-tab-uri cu contoare: „În așteptare (N)” și „Procesate (N)” |
| D2 | Confirmare e-mail? | **Configurabilă:** se poate lucra cu sau fără confirmare (comutator; cere SMTP) |
| D3 | Repo + token GitHub | Utilizatorul le creează **mai târziu**; până atunci lucrez local (git fără remote), updater-ul se testează cu un release fals local |
| D4 | Biblioteci pentru conturi noi | **Selectabile în setări:** „Toate” sau „Doar cele selectate” |
| D5 | PIN | **Easy PIN Emby, doar în rețeaua locală.** Legătura cu PIN-ul din Headend rămâne doar idee (secțiunea 13) |
| D6 | Have I Been Pwned | **Da**, activ implicit, se poate opri |
| D7 | Notificări admin | **Da, configurabile:** fiecare canal (jurnal Emby, e-mail, Telegram) se activează separat |
| D8 | Nume repo | **`emby-registration`** |

---

## 10. Dezinstalare

- Emby: Setări → Plugins → Înregistrare → Uninstall (Emby șterge DLL-ul la restart).
- Datele (`/var/lib/emby/plugins/Registration/`, configurația XML) Emby nu le șterge singur → buton „Șterge datele plugin-ului” în tab-ul Întreținere, cu confirmare; README explică și ștergerea manuală.
- Conturile create rămân conturi Emby normale (nu dispar la dezinstalare); politica lor rămâne cea restrictivă.
- Dezactivare fără dezinstalare: comutatorul „Înregistrare deschisă” oprit → pagina publică arată mesajul de închis, API-ul răspunde 403.

---

## 11. Teste

- **Unitare (xUnit):** validatori (telefon pe mai multe țări, username Unicode / sosii, e-mail), rate limiter, token HMAC (expirare, reutilizare, alt IP), proof-of-work, verificare SHA-256 + semnătură la update, stocare atomică.
- **Integrare pe server:** creare cont → verificare prin API Emby că politica e exact cea din tabel, că userul e ascuns pe login, că nu poate descărca (`/Items/{id}/Download` → 403).
- **UI headless** (`~/src/ui-harness`, admin temporar creat / șters per rulare): formular pe desktop și mobil, erori, ecrane de final, panoul admin (toate tab-urile, salvare, aprobare).
- **Anti-bot:** trimitere directă la API fără token, prea rapid, honeypot completat, 50 de cereri de pe un IP → toate respinse corect.
- Conturile de test se șterg la final.

---

## 12. Etape de lucru (detaliu pentru tabelul de progres)

1. **Schelet:** `~/src/emby-registration` (git local; remote adăugat când există repo-ul), `Registration.sln`, `Directory.Build.props` (ca la AccessLog), cheie SSH `~/.ssh/emby_registration_deploy` + alias `github-registration`; tu creezi repo-ul public și adaugi cheia.
2. **Spike:** rută `[Unauthenticated]` servește HTML; `IUserManager.CreateUser` + `UpdateUserPolicy` + setare parolă + Easy PIN; „Login disclaimer” acceptă HTML/link?; evenimentele `UserCreated` / `UserPolicyUpdated` / `UserDeleted`.
3. **Configurație + panou admin** (tab-uri, schemă de setări într-un singur loc în JS, ca la Headend).
4. **Pagina publică** (HTML/CSS/JS încorporate, RO/EN, mobil).
5. **Backend înregistrare** + stocare cereri.
6. **Anti-roboți** + limitare de rată + liste de blocare.
7. **Politica implicită** + `PolicyGuard`.
8. **Aprobare (2 sub-tab-uri cu contoare) / confirmare e-mail opțională / notificări configurabile pe canale.**
9. **Updater GitHub** + `tools/release.sh` + semnare Ed25519.
10. **Dezinstalare / GDPR.**
11. **Teste.**
12. **README** (RO + EN, capturi fără date reale) și release `v1.0.0`.
13. **Deploy** pe poweredge — restart doar după verificarea redărilor active în aceeași comandă.

---

## 12b. Trimiterea datelor de acces

Tab-ul „Trimite acces” (și butonul de pe fiecare cont aprobat): alegi utilizatorul (oricare, în afară de administratori), limba și parola din mesaj:
- **fără parolă** — utilizatorul și-o știe (plugin-ul nu păstrează parolele, deci nu o poate trimite);
- **parolă nouă generată** (`xxxx-xxxx-xxxx`, fără caractere care se confundă) sau **scrisă de admin** — se setează imediat în Emby și apare doar în mesaj.

Mesajul (editabil) conține adresa, portul, utilizatorul, parola, linkul web și instrucțiuni pentru aplicații. Trimitere: copiere, partajare nativă a telefonului (Messenger, Signal etc.), WhatsApp (`wa.me`, cu numărul din cerere), Telegram, SMS, e-mail din aplicația adminului (`mailto:`) sau de pe server prin SMTP. Schimbarea parolei se notează în jurnalul de activitate (fără parolă).

## 12c. Un cont per dispozitiv și per adresă; doar vizitatori legitimi (2026-09-26)

**Semnale de „același dispozitiv”** (blocare):
- identificator de dispozitiv semnat, emis la prima vizită, păstrat în cookie (`HttpOnly`, `SameSite=Strict`, ~13 luni) și în `localStorage`;
- conturile Emby cu care browserul e deja conectat în interfața web (aceeași origine: `servercredentials3`, doar id-urile, fără tokenuri);
- amprenta browserului (ecran, fus orar, limbi, procesor, WebGL, canvas) + aceeași rețea → blocare; amprentă singură → semnalată adminului (telefoane de același model au aceeași amprentă).

**Semnale de „aceeași adresă”** (IP-urile sunt dinamice, deci doar pe o fereastră de timp):
- aceeași adresă IP ca o cerere din ultimele 30 de zile → blocare;
- aceeași adresă IP ca un dispozitiv al unui cont existent, activ în ultimele 14 zile (lista de dispozitive Emby) → blocare;
- aceeași rețea (/24 IPv4, /64 IPv6) → semnalat adminului;
- Cloudflare WARP (AS13335): adresa e comună multor oameni → doar semnalat;
- același număr de telefon → blocare; același e-mail → răspuns identic, fără cont (existent).

**Doar vizitatori legitimi:**
- rețele de centre de date / VPN comerciale / Tor refuzate chiar la încărcarea paginii (ASN din baza GeoLite2 a plugin-ului Jurnal de acces; Tor = `CF-IPCountry: T1`);
- opțional: doar anumite țări;
- cererile trebuie să vină din pagină: antet `Origin` al site-ului, `Sec-Fetch-Site: same-origin`;
- semne de automatizare (`navigator.webdriver`, Chrome headless) → refuz;
- interacțiune reală: evenimente de tastare/atingere generate de utilizator (`isTrusted`);
- proof-of-work adaptiv: mai greu când vin multe cereri;
- Turnstile rămâne stratul cel mai puternic (cere cheile din contul Cloudflare).

Fiecare regulă are acțiunea configurabilă (blocare / semnalare / oprit); semnalările apar lângă cerere în tab-ul Cereri.

## 13. Idei pentru mai târziu

- Verificare telefon prin SMS / WhatsApp (cost per mesaj).
- Pagina „Am uitat parola” prin e-mail (Emby are doar resetare cu PIN pe rețeaua locală).
- Conturi de probă cu expirare și e-mail de reamintire înainte de expirare.
- Legătură cu Jurnal de acces: alertă când un cont nou se conectează dintr-o țară diferită de cea de la înregistrare.
- Pagina de profil pentru utilizator (își actualizează e-mailul / telefonul).
- PIN ales la înregistrare folosit și de Headend (AdultGuard) pentru canalele adulte (D5 — doar idee).
