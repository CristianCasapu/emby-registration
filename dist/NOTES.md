## Versiunea 1.8.0

- Numele de utilizator: butonul de trimitere activ doar când e disponibil
- Mobil: formularul nu mai rămâne blocat fără explicație
- Aprobare imediat după creare: setările se reaplică după ce Headend termină
- Codul de familie: pagina nu mai aplică regula de adresă comună după deblocare
- Cod de familie static, schimbabil din plugin
- Administratorii conectați pot testa formularul
- Plan: v1.4.3
- Mesaje publice scurte și generice, fără indicii despre verificări
- Titluri potrivite pe ecranul de refuz (cont dublu, blocare, rețea)
- Butonul de login apare pentru toți; pagina de cod apare prima
- Cod de acces rotativ și blocări după coduri greșite
- Un cont per dispozitiv și per adresă; pagina doar pentru vizitatori legitimi
- Plan: înregistrarea e deschisă pe server
- Buton „Creează cont nou” pe ecranul de conectare al interfeței web Emby
- Trimite datele de acces: text cu adresă, port, utilizator și parolă; copiere, WhatsApp, Telegram, SMS, e-mail, partajare
- Plan: v1.0.0 publicat și instalat prin actualizarea din plugin
- Cheia formularului se generează la pornire, nu în constructor; etichete biblioteci
- README și plan: release prin GitHub Actions
- Release prin GitHub Actions, fără token personal pe server
- Plan: depozitul GitHub e publicat
- Înregistrare: conturi noi pentru Emby cu aprobare, anti-roboți și actualizare din GitHub

Instalare: pune `Registration.dll` în directorul de plugin-uri Emby și repornește Emby. Actualizările următoare se instalează din pagina plugin-ului.

Semnătura (`Registration.dll.sig`, ECDSA P-256) se verifică cu cheia publică din depozit:
```
openssl dgst -sha256 -verify release-key.pem -signature Registration.dll.sig Registration.dll
```
