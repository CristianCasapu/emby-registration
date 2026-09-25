## Versiunea 0.9.0

- Release prin GitHub Actions, fără token personal pe server
- Plan: depozitul GitHub e publicat
- Înregistrare: conturi noi pentru Emby cu aprobare, anti-roboți și actualizare din GitHub

Instalare: pune `Registration.dll` în directorul de plugin-uri Emby și repornește Emby. Actualizările următoare se instalează din pagina plugin-ului.

Semnătura (`Registration.dll.sig`, ECDSA P-256) se verifică cu cheia publică din depozit:
```
openssl dgst -sha256 -verify release-key.pem -signature Registration.dll.sig Registration.dll
```
