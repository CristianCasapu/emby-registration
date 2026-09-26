// Pagina publica de inregistrare. Fara dependinte; tot textul venit de la server sau de la
// utilizator se afiseaza cu textContent. Regulile de validare repeta ce verifica serverul,
// doar pentru mesaje pe loc: serverul are ultimul cuvant.
(function () {
    'use strict';

    var TEXT = {
        ro: {
            title: 'Cont nou',
            loading: 'Se încarcă…',
            closedTitle: 'Înregistrarea nu este deschisă',
            titleUnavailable: 'Indisponibil',
            closed: 'Momentan nu se pot crea conturi noi.',
            not_yet: 'Înregistrarea nu a început încă.',
            ended: 'Perioada de înregistrare s-a încheiat.',
            unavailable: 'Înregistrarea nu este disponibilă.',
            locked: 'Reîncarcă pagina.',
            lockedTitle: 'Cod de acces',
            lockedText: 'Introdu codul primit.',
            codeLabel: 'Codul',
            unlock: 'Continuă',
            introApproval: 'Completează formularul. Contul devine activ după ce administratorul aprobă cererea; vei primi un e-mail.',
            introAutomatic: 'Completează formularul și contul tău va fi creat imediat.',
            introInvite: 'Ai nevoie de un cod de invitație de la administrator. Contul devine activ după aprobare.',
            introConfirm: ' Întâi îți vom trimite un e-mail ca să confirmi adresa.',
            adminMode: 'Mod administrator (test): codul și verificările sunt ocolite. ',
            username: 'Nume de utilizator',
            usernameHelp: 'Litere mici, cifre și . _ - ; îl vei scrie la fiecare conectare (contul nu apare în lista de pe ecranul de conectare).',
            firstName: 'Prenume',
            lastName: 'Nume',
            email: 'E-mail',
            didYouMean: 'Ai vrut să scrii',
            phone: 'Telefon',
            phoneHelp: 'Exemplu: {example}',
            password: 'Parolă',
            passwordHelp: 'Cel puțin {min} caractere. O frază din câteva cuvinte e ușor de ținut minte și greu de ghicit.',
            password2: 'Confirmă parola',
            pin: 'PIN de profil',
            optional: '(opțional)',
            pinHelp: '4 cifre. Aplicațiile Emby ți-l cer când revii pe un dispozitiv pe care ești deja conectat, dacă activezi PIN-ul pe acel dispozitiv. Util pe dispozitive folosite de mai multe persoane.',
            inviteCode: 'Cod de invitație',
            consent: 'Sunt de acord ca datele de mai sus să fie păstrate de administratorul serverului pentru gestionarea contului.',
            privacy: 'Confidențialitate',
            submit: 'Trimite cererea',
            submitAuto: 'Creează contul',
            haveAccount: 'Ai deja cont?',
            signIn: 'Intră în cont',
            show: 'Arată',
            hide: 'Ascunde',
            working: 'Se verifică…',
            waitSeconds: 'Poți trimite formularul în câteva secunde.',
            openEmby: 'Intră în Emby',
            appsHelp: 'În aplicațiile Emby (Android, iOS, Android TV, Fire TV, Samsung, LG) alege „Adaugă server” și introdu adresa:',
            getApps: 'Descarcă aplicațiile Emby',
            donePendingTitle: 'Cererea a fost trimisă',
            donePending: 'Administratorul va verifica cererea. Vei primi un e-mail când contul este activ.',
            doneEmailTitle: 'Verifică-ți e-mailul',
            doneEmail: 'Ți-am trimis un link de confirmare. Deschide-l ca să trimiți cererea mai departe. Dacă nu îl găsești, uită-te și în Spam.',
            doneApprovedTitle: 'Contul este gata',
            doneApproved: 'Te poți conecta acum cu numele de utilizator și parola alese.',
            confirmedTitle: 'Adresa a fost confirmată',
            confirmed: 'Mulțumim! Cererea a ajuns la administrator. Vei primi un e-mail când contul este activ.',
            confirmedAuto: 'Mulțumim! Contul tău este activ.',
            confirmAlreadyTitle: 'Adresa era deja confirmată',
            confirmAlready: 'Nu mai trebuie să faci nimic.',
            confirmBadTitle: 'Linkul nu mai este valabil',
            confirmBad: 'Linkul de confirmare a expirat sau nu este corect. Poți trimite o cerere nouă.',
            confirmBusy: 'Încearcă mai târziu.',
            errors: {
                required: 'Câmp obligatoriu.',
                username_length: 'Între {min} și {max} caractere.',
                username_chars: 'Doar litere mici fără diacritice, cifre și . _ - (nu la început sau sfârșit, nu două la rând).',
                username_reserved: 'Acest nume nu poate fi folosit.',
                username_taken: 'Numele este deja folosit. Alege altul.',
                name_invalid: 'Folosește doar litere, spațiu, cratimă sau apostrof.',
                email_invalid: 'Adresa de e-mail nu pare corectă.',
                email_rejected: 'Adresa nu este acceptată.',
                phone_invalid: 'Numărul nu pare corect pentru țara aleasă.',
                phone_rejected: 'Numărul nu este acceptat.',
                password_short: 'Parola trebuie să aibă cel puțin {min} caractere.',
                password_long: 'Parola este prea lungă (maximum 128 de caractere).',
                password_weak: 'Parola este prea simplă.',
                password_username: 'Parola nu trebuie să conțină numele de utilizator.',
                password_pwned: 'Parolă nesigură. Alege alta.',
                password_mismatch: 'Parolele nu coincid.',
                pin_invalid: 'PIN-ul are exact 4 cifre.',
                pin_weak: 'PIN-ul este prea simplu (ex. 1111, 1234).',
                consent_required: 'Este necesar acordul tău.',
                invite_invalid: 'Codul de invitație nu este valabil.',
                code_length: 'Cod incomplet.',
                wrong_code: 'Cod greșit.',
                error: 'A apărut o eroare. Încearcă din nou.',
                
                
                
                
                server_error: 'A apărut o eroare. Încearcă mai târziu.',
                network: 'Serverul nu răspunde. Verifică conexiunea și încearcă din nou.',
                fields: 'Verifică câmpurile marcate.'
            }
        },
        en: {
            title: 'New account',
            loading: 'Loading…',
            closedTitle: 'Registration is not open',
            titleUnavailable: 'Unavailable',
            closed: 'New accounts cannot be created at the moment.',
            not_yet: 'Registration has not started yet.',
            ended: 'The registration period has ended.',
            unavailable: 'Registration is not available.',
            locked: 'Reload the page.',
            lockedTitle: 'Access code',
            lockedText: 'Enter the code you received.',
            codeLabel: 'Code',
            unlock: 'Continue',
            introApproval: 'Fill in the form. Your account becomes active once the administrator approves it; you will get an email.',
            introAutomatic: 'Fill in the form and your account will be created right away.',
            introInvite: 'You need an invitation code from the administrator. Your account becomes active after approval.',
            introConfirm: ' First, we will email you to confirm your address.',
            adminMode: 'Administrator mode (test): the code and checks are skipped. ',
            username: 'Username',
            usernameHelp: 'Lowercase letters, digits and . _ - ; you will type it every time you sign in (the account is not listed on the sign-in screen).',
            firstName: 'First name',
            lastName: 'Last name',
            email: 'Email',
            didYouMean: 'Did you mean',
            phone: 'Phone',
            phoneHelp: 'Example: {example}',
            password: 'Password',
            passwordHelp: 'At least {min} characters. A phrase of a few words is easy to remember and hard to guess.',
            password2: 'Confirm password',
            pin: 'Profile PIN',
            optional: '(optional)',
            pinHelp: '4 digits. Emby apps ask for it when you return to a device you are already signed in on, if you turn the PIN on for that device. Useful on shared devices.',
            inviteCode: 'Invitation code',
            consent: 'I agree that the data above is kept by the server administrator to manage my account.',
            privacy: 'Privacy',
            submit: 'Send request',
            submitAuto: 'Create account',
            haveAccount: 'Already have an account?',
            signIn: 'Sign in',
            show: 'Show',
            hide: 'Hide',
            working: 'Checking…',
            waitSeconds: 'You can submit the form in a few seconds.',
            openEmby: 'Open Emby',
            appsHelp: 'In the Emby apps (Android, iOS, Android TV, Fire TV, Samsung, LG) choose "Add server" and enter:',
            getApps: 'Get the Emby apps',
            donePendingTitle: 'Request sent',
            donePending: 'The administrator will review your request. You will get an email when your account is active.',
            doneEmailTitle: 'Check your email',
            doneEmail: 'We sent you a confirmation link. Open it to forward your request. If you cannot find it, check your spam folder.',
            doneApprovedTitle: 'Your account is ready',
            doneApproved: 'You can now sign in with the username and password you chose.',
            confirmedTitle: 'Email confirmed',
            confirmed: 'Thank you! Your request is now with the administrator. You will get an email when your account is active.',
            confirmedAuto: 'Thank you! Your account is active.',
            confirmAlreadyTitle: 'Email already confirmed',
            confirmAlready: 'There is nothing else to do.',
            confirmBadTitle: 'This link is no longer valid',
            confirmBad: 'The confirmation link has expired or is not correct. You can send a new request.',
            confirmBusy: 'Please try again later.',
            errors: {
                required: 'Required.',
                username_length: 'Between {min} and {max} characters.',
                username_chars: 'Only lowercase letters, digits and . _ - (not at the start or end, not two in a row).',
                username_reserved: 'This name cannot be used.',
                username_taken: 'This name is taken. Please choose another.',
                name_invalid: 'Use only letters, spaces, hyphens or apostrophes.',
                email_invalid: 'This email address does not look right.',
                email_rejected: 'This address is not accepted.',
                phone_invalid: 'This number does not look right for the selected country.',
                phone_rejected: 'This number is not accepted.',
                password_short: 'The password must have at least {min} characters.',
                password_long: 'The password is too long (128 characters at most).',
                password_weak: 'The password is too simple.',
                password_username: 'The password must not contain the username.',
                password_pwned: 'Unsafe password. Choose another.',
                password_mismatch: 'The passwords do not match.',
                pin_invalid: 'The PIN has exactly 4 digits.',
                pin_weak: 'The PIN is too simple (e.g. 1111, 1234).',
                consent_required: 'Your consent is required.',
                invite_invalid: 'The invitation code is not valid.',
                code_length: 'Incomplete code.',
                wrong_code: 'Wrong code.',
                error: 'An error occurred. Please try again.',
                
                
                
                
                server_error: 'An error occurred. Please try again later.',
                network: 'The server is not responding. Check your connection and try again.',
                fields: 'Please check the highlighted fields.'
            }
        }
    };

    var COMMON_DOMAINS = ['gmail.com', 'yahoo.com', 'yahoo.ro', 'hotmail.com', 'outlook.com', 'icloud.com', 'live.com', 'protonmail.com', 'proton.me', 'yandex.com', 'gmx.com', 'mail.com', 'aol.com'];
    var COMMON_PASSWORDS = ['password', 'parola', 'qwerty', '123456', 'iloveyou', 'admin', 'letmein', 'welcome', 'monkey', 'dragon', 'football', 'emby'];

    var lang = pickLanguage();
    var info = null;
    var pow = null;          // { token, solution, promise }
    var turnstileId = null;
    var turnstileToken = null;
    var usernameTimer = null;
    var usernameSeq = 0;
    var touched = {};
    var activity = { inputs: 0, gestures: 0 };

    // Doar evenimentele generate de om (isTrusted); scripturile care umplu campurile nu le produc.
    document.addEventListener('input', function (e) { if (e.isTrusted) { activity.inputs++; } }, true);
    ['keydown', 'pointerdown', 'touchstart'].forEach(function (type) {
        document.addEventListener(type, function (e) { if (e.isTrusted) { activity.gestures++; } }, { capture: true, passive: true });
    });

    function storedDevice() {
        try { return localStorage.getItem('registration.device') || ''; } catch (e) { return ''; }
    }

    function storeDevice(value) {
        try { if (value) { localStorage.setItem('registration.device', value); } } catch (e) { /* stocare blocata */ }
    }

    // Conturile Emby cu care acest browser e conectat in interfata web (aceeasi origine). Doar id-urile.
    function embyUsers() {
        var ids = [];
        try {
            var data = JSON.parse(localStorage.getItem('servercredentials3') || '{}');
            (data.Servers || []).forEach(function (server) {
                if (server.UserId) { ids.push(server.UserId); }
                (server.Users || []).forEach(function (u) { if (u && (u.UserId || u.Id)) { ids.push(u.UserId || u.Id); } });
            });
        } catch (e) { /* fara date */ }
        return ids.filter(function (id, i) { return /^[0-9a-f-]{32,36}$/i.test(id) && ids.indexOf(id) === i; }).slice(0, 20);
    }

    function automationSigns() {
        var signs = [];
        if (navigator.webdriver) { signs.push('webdriver'); }
        if (/HeadlessChrome|PhantomJS|Electron|puppeteer|playwright/i.test(navigator.userAgent)) { signs.push('headless'); }
        return signs.join(',');
    }

    // Amprenta: caracteristici stabile ale browserului, reduse la un hash (nu se trimit separat).
    function fingerprint() {
        var parts = [navigator.userAgent, navigator.platform, navigator.hardwareConcurrency, navigator.deviceMemory, navigator.maxTouchPoints,
            screen.width + 'x' + screen.height + 'x' + screen.colorDepth, window.devicePixelRatio,
            (navigator.languages || []).join(','), (Intl.DateTimeFormat().resolvedOptions() || {}).timeZone];
        try {
            var gl = document.createElement('canvas').getContext('webgl');
            var ext = gl && gl.getExtension('WEBGL_debug_renderer_info');
            if (ext) { parts.push(gl.getParameter(ext.UNMASKED_VENDOR_WEBGL), gl.getParameter(ext.UNMASKED_RENDERER_WEBGL)); }
        } catch (e) { /* fara WebGL */ }
        try {
            var canvas = document.createElement('canvas');
            canvas.width = 220; canvas.height = 30;
            var ctx = canvas.getContext('2d');
            ctx.textBaseline = 'top'; ctx.font = '16px Arial'; ctx.fillStyle = '#f60'; ctx.fillRect(100, 1, 62, 20);
            ctx.fillStyle = '#069'; ctx.fillText('Emby înregistrare ✓ 1.2', 2, 15);
            parts.push(canvas.toDataURL());
        } catch (e) { /* fara canvas */ }
        return hash(parts.join('|'));
    }

    // cyrb53, de doua ori cu seminte diferite: 128 de biti in hex.
    function hash(text) {
        function cyrb(seed) {
            var h1 = 0xdeadbeef ^ seed, h2 = 0x41c6ce57 ^ seed;
            for (var i = 0; i < text.length; i++) {
                var ch = text.charCodeAt(i);
                h1 = Math.imul(h1 ^ ch, 2654435761);
                h2 = Math.imul(h2 ^ ch, 1597334677);
            }
            h1 = Math.imul(h1 ^ (h1 >>> 16), 2246822507) ^ Math.imul(h2 ^ (h2 >>> 13), 3266489909);
            h2 = Math.imul(h2 ^ (h2 >>> 16), 2246822507) ^ Math.imul(h1 ^ (h1 >>> 13), 3266489909);
            return (h2 >>> 0).toString(16).padStart(8, '0') + (h1 >>> 0).toString(16).padStart(8, '0');
        }
        return cyrb(1) + cyrb(2);
    }

    function $(id) { return document.getElementById(id); }

    function t(key, values) {
        var parts = key.split('.');
        var value = TEXT[lang];
        for (var i = 0; i < parts.length && value != null; i++) { value = value[parts[i]]; }
        if (value == null) { value = key; }
        return String(value).replace(/\{(\w+)\}/g, function (_, name) {
            return values && values[name] != null ? values[name] : '';
        });
    }

    function pickLanguage() {
        try {
            var saved = localStorage.getItem('registration.lang');
            if (saved === 'ro' || saved === 'en') { return saved; }
        } catch (e) { /* stocare blocata */ }
        var nav = (navigator.languages && navigator.languages[0]) || navigator.language || 'ro';
        return /^ro\b/i.test(nav) ? 'ro' : (/^en\b/i.test(nav) ? 'en' : 'ro');
    }

    function show(id) {
        ['loading', 'closed', 'locked', 'form', 'done'].forEach(function (name) {
            $(name).classList.toggle('hidden', name !== id);
        });
    }

    function applyTexts() {
        document.documentElement.lang = lang;
        $('langToggle').textContent = lang === 'ro' ? 'EN' : 'RO';
        document.querySelectorAll('[data-t]').forEach(function (el) { el.textContent = t(el.getAttribute('data-t')); });
        document.querySelectorAll('[data-toggle]').forEach(function (btn) {
            btn.textContent = $(btn.getAttribute('data-toggle')).type === 'password' ? t('show') : t('hide');
        });
        document.title = t('title') + (info && info.ServerName ? ' – ' + info.ServerName : '');
        if (info) { fillForm(); }
    }

    // --- Comunicare cu serverul --------------------------------------------------------------

    // Sesiunea Emby din acest browser, daca exista (serverul o accepta doar pentru administratori).
    function embyToken() {
        try {
            var servers = (JSON.parse(localStorage.getItem('servercredentials3') || '{}').Servers || []).map(function (s) {
                var current = (s.Users || []).filter(function (u) { return u && u.UserId === s.UserId && u.AccessToken; })[0];
                return { token: (current && current.AccessToken) || s.AccessToken, addresses: [s.ManualAddress, s.RemoteAddress, s.LocalAddress] };
            }).filter(function (s) { return s.token; });
            var here = servers.filter(function (s) {
                return s.addresses.some(function (a) { return a && a.indexOf(location.host) >= 0; });
            });
            return ((here[0] || servers[0]) || {}).token || '';
        } catch (e) {
            return '';
        }
    }

    function api(path, body) {
        var options = { method: body ? 'POST' : 'GET', headers: { Accept: 'application/json' }, credentials: 'same-origin', cache: 'no-store' };
        var token = embyToken();
        if (token) { options.headers['X-Emby-Token'] = token; }
        if (body) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
        }
        return fetch(path, options).then(function (response) {
            return response.json().catch(function () { return { Outcome: 'Error', Error: 'server_error' }; });
        });
    }

    function loadInfo() {
        var query = '?Device=' + encodeURIComponent(storedDevice()) + '&Users=' + encodeURIComponent(embyUsers().join(','));
        return api('Info' + query).then(function (data) {
            info = data;
            storeDevice(info.Device);
            if (info.Token) { startPow(); }
            return info;
        });
    }

    // Proof-of-work in fundal, cat se completeaza formularul.
    function startPow() {
        var token = info.Token, bits = info.PowBits;
        if (!bits) {
            pow = { token: token, solution: '', promise: Promise.resolve('') };
            return;
        }
        var state = { token: token, solution: null };
        state.promise = new Promise(function (resolve, reject) {
            var worker;
            try {
                worker = new Worker('Assets/pow.js');
            } catch (e) {
                reject(e);
                return;
            }
            worker.onmessage = function (event) {
                if (event.data.solution != null) {
                    state.solution = event.data.solution;
                    worker.terminate();
                    resolve(state.solution);
                }
            };
            worker.onerror = function (e) { worker.terminate(); reject(e); };
            worker.postMessage({ token: token, bits: bits });
        });
        pow = state;
    }

    // --- Turnstile ------------------------------------------------------------------------

    function loadTurnstile() {
        if (!info.TurnstileSiteKey || turnstileId !== null) { return; }
        window.onRegistrationTurnstile = function () {
            turnstileId = window.turnstile.render('#turnstile', {
                sitekey: info.TurnstileSiteKey,
                language: lang,
                theme: 'dark',
                appearance: 'interaction-only',
                callback: function (token) { turnstileToken = token; },
                'expired-callback': function () { turnstileToken = null; },
                'error-callback': function () { turnstileToken = null; }
            });
        };
        var script = document.createElement('script');
        script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit&onload=onRegistrationTurnstile';
        script.async = true;
        script.defer = true;
        document.head.appendChild(script);
    }

    function resetTurnstile() {
        turnstileToken = null;
        if (turnstileId !== null && window.turnstile) { window.turnstile.reset(turnstileId); }
    }

    function waitTurnstile(timeoutMs) {
        if (!info.TurnstileSiteKey) { return Promise.resolve(null); }
        return new Promise(function (resolve) {
            var started = Date.now();
            (function poll() {
                if (turnstileToken || Date.now() - started > timeoutMs) { resolve(turnstileToken); return; }
                setTimeout(poll, 150);
            })();
        });
    }

    // --- Formular ----------------------------------------------------------------------------

    function fillForm() {
        var intro = info.Mode === 'Automatic' ? t('introAutomatic') : info.Mode === 'InviteOnly' ? t('introInvite') : t('introApproval');
        if (info.EmailConfirmation) { intro += t('introConfirm'); }
        if (info.Admin) { intro = t('adminMode') + intro; }
        $('intro').textContent = intro;
        $('serverName').textContent = info.ServerName || '';
        $('pinField').classList.toggle('hidden', !info.PinEnabled);
        $('inviteField').classList.toggle('hidden', info.Mode !== 'InviteOnly');
        $('consentField').classList.toggle('hidden', !info.RequireConsent);
        $('privacy').classList.toggle('hidden', !info.PrivacyText);
        $('privacyText').textContent = info.PrivacyText || '';
        $('passwordHelp').textContent = t('passwordHelp', { min: info.PasswordMinLength });
        $('username').maxLength = info.UsernameMaxLength || 32;
        document.querySelector('#submit .label').textContent = info.Mode === 'Automatic' && !info.EmailConfirmation ? t('submitAuto') : t('submit');
        fillCountries();
        updatePhoneHelp();
    }

    function flag(iso) {
        return String.fromCodePoint.apply(null, iso.toUpperCase().split('').map(function (c) { return 0x1F1E6 + c.charCodeAt(0) - 65; }));
    }

    function fillCountries() {
        var select = $('country');
        var current = select.value || info.DefaultCountry;
        while (select.firstChild) { select.removeChild(select.firstChild); }
        var countries = info.Countries.slice().sort(function (a, b) {
            if (a.Iso === info.DefaultCountry) { return -1; }
            if (b.Iso === info.DefaultCountry) { return 1; }
            return (lang === 'en' ? a.NameEn : a.Name).localeCompare(lang === 'en' ? b.NameEn : b.Name, lang);
        });
        countries.forEach(function (c) {
            var option = document.createElement('option');
            option.value = c.Iso;
            option.textContent = flag(c.Iso) + ' +' + c.Dial + ' ' + (lang === 'en' ? c.NameEn : c.Name);
            select.appendChild(option);
        });
        select.value = countries.some(function (c) { return c.Iso === current; }) ? current : (countries[0] && countries[0].Iso);
    }

    function country() {
        var iso = $('country').value;
        return info.Countries.filter(function (c) { return c.Iso === iso; })[0];
    }

    function updatePhoneHelp() {
        var c = country();
        $('phoneHelp').textContent = c ? t('phoneHelp', { example: '+' + c.Dial + ' ' + group(c.Example) }) : '';
        var phone = $('phone');
        phone.placeholder = c ? group(c.Example) : '';
    }

    function group(digits) {
        return digits.replace(/(\d{3})(?=\d)/g, '$1 ');
    }

    /** Numarul in format E.164 sau null; accepta +, 00 si 0 in fata numarului national. */
    function phoneE164() {
        var raw = $('phone').value.trim();
        if (!raw) { return null; }
        if (/[^\d\s\-.()+/]/.test(raw)) { return null; }
        var digits = raw.replace(/\D/g, '');
        if (raw.charAt(0) === '+') { return '+' + digits; }
        if (raw.indexOf('00') === 0) { return '+' + digits.slice(2); }
        var c = country();
        return c ? '+' + c.Dial + digits.replace(/^0+/, '') : null;
    }

    function phoneError() {
        var e164 = phoneE164();
        if (!e164) { return 'phone_invalid'; }
        if (!/^\+[1-9]\d{7,14}$/.test(e164)) { return 'phone_invalid'; }
        var match = info.Countries.filter(function (c) { return e164.indexOf('+' + c.Dial) === 0; })
            .sort(function (a, b) { return b.Dial.length - a.Dial.length; })[0];
        if (match) {
            var national = e164.length - 1 - match.Dial.length;
            if (national < match.MinDigits || national > match.MaxDigits) { return 'phone_invalid'; }
        }
        return null;
    }

    function usernameError(value) {
        if (!value) { return 'required'; }
        if (value.length < info.UsernameMinLength || value.length > info.UsernameMaxLength) { return 'username_length'; }
        if (!/^[a-z0-9](?:[a-z0-9._-]*[a-z0-9])?$/.test(value) || /[._-]{2,}/.test(value)) { return 'username_chars'; }
        return null;
    }

    function nameError(value) {
        if (!value) { return 'required'; }
        return /^[\p{L}\p{M}](?:[\p{L}\p{M}' .\-]*[\p{L}\p{M}.])?$/u.test(value) && value.length <= 50 ? null : 'name_invalid';
    }

    function emailError(value) {
        if (!value) { return 'required'; }
        return /^[^\s@]+@[^\s@]+\.[a-z]{2,63}$/i.test(value) && value.length <= 254 ? null : 'email_invalid';
    }

    function passwordError(value) {
        if (!value) { return 'required'; }
        if (value.length < info.PasswordMinLength) { return 'password_short'; }
        if (value.length > 128) { return 'password_long'; }
        if (new Set(value).size < 4) { return 'password_weak'; }
        var user = $('username').value.trim().toLowerCase();
        if (user.length >= 3 && value.toLowerCase().indexOf(user) >= 0) { return 'password_username'; }
        return null;
    }

    function pinError(value) {
        if (!value) { return null; }
        if (!/^\d{4}$/.test(value)) { return 'pin_invalid'; }
        if (new Set(value).size === 1 || '0123456789012'.indexOf(value) >= 0 || '9876543210987'.indexOf(value) >= 0) { return 'pin_weak'; }
        return null;
    }

    function strength(value) {
        if (!value) { return 0; }
        var score = 0;
        if (value.length >= info.PasswordMinLength) { score++; }
        if (value.length >= 14) { score++; }
        var classes = [/[a-z]/, /[A-Z]/, /\d/, /[^A-Za-z0-9]/].filter(function (r) { return r.test(value); }).length;
        if (classes >= 3) { score++; }
        if (value.length >= 18 || (classes === 4 && value.length >= 12)) { score++; }
        var lower = value.toLowerCase();
        if (COMMON_PASSWORDS.some(function (p) { return lower.indexOf(p) >= 0; }) || new Set(value).size < 5) { score = Math.min(score, 1); }
        return Math.max(1, score);
    }

    function setError(field, code, values) {
        var box = document.querySelector('[data-field="' + field + '"]');
        if (!box) { return; }
        var error = box.querySelector('.error');
        var text = code ? t('errors.' + code, values || errorValues(code)) : '';
        error.textContent = text;
        box.classList.toggle('invalid', !!code);
        var input = box.querySelector('input:not([type=checkbox]), select') || box.querySelector('input');
        if (input) { input.setAttribute('aria-invalid', code ? 'true' : 'false'); }
    }

    function errorValues(code) {
        if (code === 'username_length') { return { min: info.UsernameMinLength, max: info.UsernameMaxLength }; }
        if (code === 'password_short') { return { min: info.PasswordMinLength }; }
        return null;
    }

    function validateField(field) {
        var v;
        switch (field) {
            case 'username':
                v = usernameError($('username').value.trim().toLowerCase());
                break;
            case 'firstName':
            case 'lastName':
                v = nameError($(field).value.trim());
                break;
            case 'email':
                v = emailError($('email').value.trim());
                suggestEmail();
                break;
            case 'phone':
                v = $('phone').value.trim() ? phoneError() : 'required';
                break;
            case 'password':
                v = passwordError($('password').value);
                if (touched.password2) { validateField('password2'); }
                break;
            case 'password2':
                v = !$('password2').value ? 'required' : ($('password2').value !== $('password').value ? 'password_mismatch' : null);
                break;
            case 'pin':
                v = info.PinEnabled ? pinError($('pin').value.trim()) : null;
                break;
            case 'inviteCode':
                v = info.Mode === 'InviteOnly' && !$('inviteCode').value.trim() ? 'required' : null;
                break;
            case 'consent':
                v = info.RequireConsent && !$('consent').checked ? 'consent_required' : null;
                break;
            default:
                v = null;
        }
        // Eroarea de disponibilitate a username-ului vine de la server; nu o stergem aici.
        var box = document.querySelector('[data-field="' + field + '"]');
        if (field === 'username' && !v && box.dataset.serverError) {
            v = box.dataset.serverError;
        }
        setError(field, v);
        return !v;
    }

    var FIELDS = ['username', 'firstName', 'lastName', 'email', 'phone', 'password', 'password2', 'pin', 'inviteCode', 'consent'];

    // --- Disponibilitatea username-ului ----------------------------------------------------------

    function checkUsername() {
        var box = document.querySelector('[data-field="username"]');
        var status = $('usernameStatus');
        var value = $('username').value.trim().toLowerCase();
        delete box.dataset.serverError;
        status.className = 'status';
        status.textContent = '';
        box.classList.remove('valid');
        if (usernameError(value)) { return; }
        var seq = ++usernameSeq;
        status.className = 'status wait';
        api('CheckUsername', { Username: value }).then(function (result) {
            if (seq !== usernameSeq) { return; }
            status.className = 'status';
            if (result.Available) {
                status.className = 'status ok';
                status.textContent = '✓';
                box.classList.add('valid');
            } else if (result.Error && result.Error !== 'rate_limited') {
                status.className = 'status bad';
                status.textContent = '✕';
                box.dataset.serverError = result.Error;
                setError('username', result.Error);
            }
        }).catch(function () {
            if (seq === usernameSeq) { status.className = 'status'; }
        });
    }

    // --- Sugestie pentru greseli in adresa de e-mail ------------------------------------------

    function distance(a, b) {
        var prev = [], cur, i, j;
        for (j = 0; j <= b.length; j++) { prev[j] = j; }
        for (i = 1; i <= a.length; i++) {
            cur = [i];
            for (j = 1; j <= b.length; j++) {
                cur[j] = Math.min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1));
            }
            prev = cur;
        }
        return prev[b.length];
    }

    function suggestEmail() {
        var value = $('email').value.trim().toLowerCase();
        var at = value.lastIndexOf('@');
        var box = $('emailSuggestion');
        box.classList.add('hidden');
        if (at < 1) { return; }
        var domain = value.slice(at + 1);
        if (!domain || COMMON_DOMAINS.indexOf(domain) >= 0) { return; }
        var best = null, bestDistance = 3;
        COMMON_DOMAINS.forEach(function (d) {
            var dist = distance(domain, d);
            if (dist > 0 && dist < bestDistance) { best = d; bestDistance = dist; }
        });
        if (best) {
            $('emailSuggestionValue').textContent = value.slice(0, at + 1) + best;
            box.classList.remove('hidden');
        }
    }

    // --- Trimitere ----------------------------------------------------------------------------

    function setBusy(busy, note) {
        var button = $('submit');
        button.disabled = busy;
        button.querySelector('.busy').classList.toggle('hidden', !busy);
        $('submitNote').textContent = note || '';
    }

    function formError(code, values) {
        var box = $('formError');
        box.textContent = code ? t('errors.' + code, values) : '';
        box.classList.toggle('hidden', !code);
        if (code) { box.scrollIntoView({ behavior: 'smooth', block: 'center' }); }
    }

    function onSubmit(event) {
        event.preventDefault();
        formError(null);
        var ok = true;
        FIELDS.forEach(function (f) {
            touched[f] = true;
            if (!validateField(f)) { ok = false; }
        });
        if (!ok) {
            formError('fields');
            var first = document.querySelector('.field.invalid input, .field.invalid select');
            if (first) { first.focus(); }
            return;
        }

        setBusy(true, t('working'));
        var current = pow;
        Promise.all([current.promise, waitTurnstile(15000)]).then(function (values) {
            var body = {
                Token: current.token,
                Pow: values[0],
                Turnstile: values[1],
                Website: $('website').value,
                Username: $('username').value.trim().toLowerCase(),
                FirstName: $('firstName').value.trim(),
                LastName: $('lastName').value.trim(),
                Email: $('email').value.trim(),
                Country: $('country').value,
                Phone: phoneE164() || $('phone').value.trim(),
                Password: $('password').value,
                Pin: info.PinEnabled ? $('pin').value.trim() : '',
                InviteCode: $('inviteCode').value.trim(),
                Consent: $('consent').checked,
                Language: lang,
                Device: storedDevice(),
                Fingerprint: fingerprint(),
                EmbyUsers: embyUsers(),
                Automation: automationSigns(),
                Inputs: activity.inputs,
                Gestures: activity.gestures
            };
            return api('Submit', body);
        }).then(handleResult, function () {
            setBusy(false);
            formError('network');
        });
    }

    function handleResult(result) {
        setBusy(false);
        if (result.Outcome === 'Ok') {
            try { sessionStorage.removeItem('registration.draft'); } catch (e) { /* */ }
            showDone(result.Status);
            return;
        }

        resetTurnstile();
        if (result.Outcome === 'Invalid' && result.Error === 'fields') {
            Object.keys(result.Fields || {}).forEach(function (field) {
                if (field === 'username') {
                    document.querySelector('[data-field="username"]').dataset.serverError = result.Fields[field];
                }
                setError(field, result.Fields[field]);
            });
            formError('fields');
            var first = document.querySelector('.field.invalid input, .field.invalid select');
            if (first) { first.focus(); }
            return;
        }

        if (result.Outcome === 'Closed' && result.Error === 'locked') {
            loadInfo().then(startForm);
            return;
        }

        if (result.Outcome === 'Closed') {
            showClosed(result.Error);
            return;
        }

        formError(result.Error === 'server_error' ? 'server_error' : 'error');
        loadInfo().catch(function () { /* se reincearca la urmatoarea trimitere */ });
    }

    function showDone(status, confirmOutcome) {
        var icon = document.querySelector('.done-icon');
        icon.classList.remove('info');
        icon.textContent = '✓';
        var title, text, apps = false;
        if (confirmOutcome) {
            if (confirmOutcome === 'confirmed') {
                title = t('confirmedTitle');
                text = status === 'Automatic' ? t('confirmedAuto') : t('confirmed');
                apps = status === 'Automatic';
            } else if (confirmOutcome === 'already') {
                title = t('confirmAlreadyTitle');
                text = t('confirmAlready');
            } else {
                icon.classList.add('info');
                icon.textContent = '!';
                title = t('confirmBadTitle');
                text = confirmOutcome === 'rate_limited' ? t('confirmBusy') : t('confirmBad');
            }
        } else if (status === 'EmailPending') {
            icon.textContent = '✉';
            title = t('doneEmailTitle');
            text = t('doneEmail');
        } else if (status === 'Approved') {
            title = t('doneApprovedTitle');
            text = t('doneApproved');
            apps = true;
        } else {
            title = t('donePendingTitle');
            text = t('donePending');
        }
        $('doneTitle').textContent = title;
        $('doneText').textContent = text;
        $('apps').classList.toggle('hidden', !apps);
        var url = (info && info.ServerUrl) || location.origin;
        $('serverAddress').textContent = url;
        $('openEmby').href = url + '/web/index.html';
        show('done');
        $('doneTitle').focus && $('doneTitle').setAttribute('tabindex', '-1');
    }

    function showClosed(reason) {
        reason = ['closed', 'not_yet', 'ended'].indexOf(reason || 'closed') >= 0 ? (reason || 'closed') : 'unavailable';
        $('closedText').textContent = (reason !== 'unavailable' && info && info.ClosedMessage) || t(reason);
        var title = reason === 'unavailable' ? 'titleUnavailable' : 'closedTitle';
        var heading = document.querySelector('#closed h2');
        heading.setAttribute('data-t', title);
        heading.textContent = t(title);
        show('closed');
    }

    // Ciorna formularului (fara parole si PIN) supravietuieste unei reincarcari a paginii.
    function saveDraft() {
        try {
            var draft = {};
            ['username', 'firstName', 'lastName', 'email', 'country', 'phone', 'inviteCode'].forEach(function (f) { draft[f] = $(f).value; });
            sessionStorage.setItem('registration.draft', JSON.stringify(draft));
        } catch (e) { /* stocare blocata */ }
    }

    function restoreDraft() {
        try {
            var draft = JSON.parse(sessionStorage.getItem('registration.draft') || 'null');
            if (!draft) { return; }
            Object.keys(draft).forEach(function (f) { if ($(f) && draft[f]) { $(f).value = draft[f]; } });
            updatePhoneHelp();
        } catch (e) { /* ciorna invalida */ }
    }

    // --- Codul de acces ---------------------------------------------------------------------------

    function showLocked() {
        show('locked');
        $('code').value = '';
        $('code').focus({ preventScroll: true });
    }

    function onUnlock(event) {
        event.preventDefault();
        var code = $('code').value.toUpperCase().replace(/[\s-]/g, '');
        if (code.length < 4 || code.length > 12) {
            setError('code', 'code_length');
            return;
        }
        setError('code', null);
        var button = $('unlock');
        button.disabled = true;
        button.querySelector('.busy').classList.remove('hidden');
        api('Unlock', { Code: code, Device: storedDevice(), Users: embyUsers() }).then(function (result) {
            if (result.Ok) {
                return loadInfo().then(startForm);
            }
            if (result.Error === 'wrong_code') {
                setError('code', 'wrong_code');
                $('code').select();
            } else {
                showClosed('unavailable');
            }
        }, function () {
            setError('code', 'network');
        }).then(function () {
            button.disabled = false;
            button.querySelector('.busy').classList.add('hidden');
        });
    }

    function startForm() {
        applyTexts();
        if (!info.Open) {
            showClosed(info.ClosedReason);
            return;
        }
        if (info.Locked) {
            showLocked();
            return;
        }
        restoreDraft();
        loadTurnstile();
        show('form');
        $('username').focus({ preventScroll: true });
    }

    // --- Pornire ------------------------------------------------------------------------------

    function wire() {
        $('langToggle').addEventListener('click', function () {
            lang = lang === 'ro' ? 'en' : 'ro';
            try { localStorage.setItem('registration.lang', lang); } catch (e) { /* */ }
            applyTexts();
            FIELDS.forEach(function (f) { if (touched[f]) { validateField(f); } });
        });

        FIELDS.forEach(function (f) {
            var el = $(f);
            if (!el) { return; }
            el.addEventListener('blur', function () {
                // Un camp gol parasit nu e inca o greseala; eroarea „obligatoriu” apare la trimitere.
                if (el.type !== 'checkbox' && !el.value.trim() && !touched[f]) { return; }
                touched[f] = true;
                validateField(f);
            });
            el.addEventListener(el.type === 'checkbox' ? 'change' : 'input', function () {
                if (touched[f]) { validateField(f); }
                saveDraft();
            });
        });

        $('username').addEventListener('input', function () {
            var el = $('username');
            var lower = el.value.toLowerCase().replace(/\s/g, '');
            if (lower !== el.value) {
                var pos = el.selectionStart;
                el.value = lower;
                try { el.setSelectionRange(pos, pos); } catch (e) { /* */ }
            }
            clearTimeout(usernameTimer);
            usernameTimer = setTimeout(checkUsername, 500);
        });

        $('pin').addEventListener('input', function () { $('pin').value = $('pin').value.replace(/\D/g, '').slice(0, 4); });
        $('inviteCode').addEventListener('input', function () { $('inviteCode').value = $('inviteCode').value.toUpperCase(); });

        $('password').addEventListener('input', function () {
            var bar = $('meterBar');
            bar.className = 's' + strength($('password').value);
            if (!$('password').value) { bar.className = ''; }
        });

        $('country').addEventListener('change', function () {
            updatePhoneHelp();
            if (touched.phone) { validateField('phone'); }
            saveDraft();
        });

        $('phone').addEventListener('input', function () {
            // Un numar lipit cu prefix international alege singur tara.
            var value = $('phone').value.trim();
            if (value.charAt(0) === '+' || value.indexOf('00') === 0) {
                var digits = value.replace(/\D/g, '').replace(/^00/, '');
                var match = info.Countries.filter(function (c) { return digits.indexOf(c.Dial) === 0; })
                    .sort(function (a, b) { return b.Dial.length - a.Dial.length; })[0];
                if (match && match.Iso !== $('country').value && !(match.Dial === '1' && country() && country().Dial === '1')) {
                    $('country').value = match.Iso;
                    updatePhoneHelp();
                }
            }
        });

        $('emailSuggestionValue').addEventListener('click', function () {
            $('email').value = $('emailSuggestionValue').textContent;
            $('emailSuggestion').classList.add('hidden');
            validateField('email');
            saveDraft();
        });

        document.querySelectorAll('[data-toggle]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var input = $(btn.getAttribute('data-toggle'));
                var reveal = input.type === 'password';
                input.type = reveal ? 'text' : 'password';
                $('password2').type = input.type;
                btn.textContent = reveal ? t('hide') : t('show');
            });
        });

        $('form').addEventListener('submit', onSubmit);
        $('locked').addEventListener('submit', onUnlock);
        $('code').addEventListener('input', function () {
            var el = $('code');
            var clean = el.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 12);
            if (clean !== el.value) { el.value = clean; }
            setError('code', null);
        });
    }

    function confirmFromLink(token) {
        // Scoatem tokenul din bara de adrese (istoric, capturi de ecran).
        try { history.replaceState(null, '', location.pathname); } catch (e) { /* */ }
        api('ConfirmEmail', { Token: token }).then(function (result) {
            showDone(result.Mode === 'Automatic' ? 'Automatic' : 'Approval', result.Outcome || 'invalid');
        }, function () {
            showDone(null, 'invalid');
        });
    }

    function start() {
        wire();
        applyTexts();
        var confirm = new URLSearchParams(location.search).get('confirm');
        if (confirm) {
            api('Info').then(function (data) {
                info = data;
                applyTexts();
            }).catch(function () { /* doar numele serverului */ }).then(function () {
                confirmFromLink(confirm);
            });
            return;
        }

        loadInfo().then(startForm).catch(function () {
            $('closedText').textContent = t('errors.network');
            show('closed');
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
