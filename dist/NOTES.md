## Versiunea 1.3.0

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
