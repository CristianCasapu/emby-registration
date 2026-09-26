define(['baseView', 'loading', 'toast', 'emby-scroller'], function (BaseView, loading, toastModule) {
    'use strict';

    var pluginId = 'f94669d3-60d5-4470-b709-8eda287b7b0c';

    // Setarile simple, descrise o singura data. Valorile implicite (def) trebuie sa fie
    // aceleasi cu cele din PluginConfiguration.cs.
    var ACTIONS = [['Block', 'Blochează'], ['Flag', 'Semnalează'], ['Off', 'Ignoră']];

    var GROUPS = [
        {
            id: 'general', icon: 'toggle_on', title: 'Stare',
            fields: [
                { key: 'RegistrationOpen', type: 'bool', def: false, label: 'Înregistrare deschisă',
                    help: 'Oprit: pagina arată „Înregistrarea nu este deschisă” și nu primește cereri. Cererile deja primite rămân în tab-ul Cereri.' },
                { key: 'Mode', type: 'select', def: 'Approval', label: 'Cum se activează conturile',
                    options: [['Approval', 'Aprobare manuală (recomandat)'], ['InviteOnly', 'Doar cu cod de invitație + aprobare'], ['Automatic', 'Automat, fără aprobare']],
                    help: '<b>Aprobare manuală</b>: contul se creează dezactivat și devine activ când îl aprobi. <b>Invitație</b>: formularul cere un cod generat în tab-ul Invitații. <b>Automat</b>: contul e activ imediat (după confirmarea e-mailului, dacă e pornită).' },
                { key: 'OpenFrom', type: 'datetime', def: '', label: 'Deschis de la (opțional)', help: 'Gol: de acum.' },
                { key: 'OpenUntil', type: 'datetime', def: '', label: 'Deschis până la (opțional)', help: 'Gol: fără dată de închidere.' },
                { key: 'ServerDisplayName', type: 'text', def: '', label: 'Numele afișat pe pagină', help: 'Gol: numele serverului Emby.' },
                { key: 'PublicUrl', type: 'text', def: '', wide: true, label: 'Adresa publică a serverului', placeholder: 'https://exemplu.ro:2096',
                    help: 'Adresa prin care intră utilizatorii din internet. Se folosește în e-mailuri (linkul de confirmare, „Contul tău este gata”) și pe ecranul final. Obligatorie pentru confirmarea e-mailului.' },
                { key: 'ClosedMessage', type: 'textarea', def: '', wide: true, label: 'Mesaj când înregistrarea e închisă', help: 'Gol: mesajul standard.' }
            ]
        },
        {
            id: 'code', icon: 'password', title: 'Cod de acces',
            fields: [
                { key: 'RequireAccessCode', type: 'bool', def: false, label: 'Cere cod de acces',
                    help: 'Pagina de înregistrare arată întâi un câmp pentru cod (5 caractere: litere mari și cifre, fără cele care se confundă, ca 0/O sau 1/I/L). Formularul apare doar după codul corect.' },
                { key: 'FamilyCode', type: 'text', def: '', label: 'Cod de familie (static)', placeholder: 'ex. FAMIS',
                    help: 'Cod fix pentru familie, 4–12 litere sau cifre; nu se schimbă la folosire și merge de mai multe ori. Cu el nu se aplică regulile de adresă/rețea comună (familia e în aceeași casă); același dispozitiv și același telefon rămân interzise. Cererile trec tot prin aprobare și sunt marcate „cod de familie”. Gol = dezactivat.' },
                { key: 'MaxCodeAttempts', type: 'int', def: 2, min: 1, max: 10, unit: 'greșeli', label: 'Blochează după', help: 'Numărate separat pentru adresa IP și pentru dispozitiv.' },
                { key: 'CodeBlockHours', type: 'int', def: 24, min: 1, max: 720, unit: 'ore', label: 'Durata blocării' },
                { key: 'CodeUnlockMinutes', type: 'int', def: 120, min: 5, max: 1440, unit: 'minute', label: 'Formularul rămâne deschis', help: 'După codul corect, pe același dispozitiv (dacă reîncarcă pagina nu trebuie alt cod).' }
            ]
        },
        {
            id: 'limits', icon: 'speed', title: 'Limite',
            fields: [
                { key: 'MaxAccounts', type: 'int', def: 0, min: 0, max: 100000, unit: 'conturi', label: 'Conturi în total', help: 'Aprobate + în așteptare, create prin plugin. 0 = fără limită.' },
                { key: 'MaxPending', type: 'int', def: 50, min: 0, max: 10000, unit: 'cereri', label: 'Cereri în așteptare', help: 'Peste limită, formularul se închide temporar. 0 = fără limită.' },
                { key: 'MaxRequestsPerDay', type: 'int', def: 20, min: 0, max: 10000, unit: 'pe zi', label: 'Cereri noi pe zi', help: 'De la toți vizitatorii. 0 = fără limită.' },
                { key: 'PendingExpiryDays', type: 'int', def: 7, min: 1, max: 365, unit: 'zile', label: 'Cererile neaprobate expiră după', help: 'Contul dezactivat al unei cereri expirate se șterge, iar numele de utilizator devine din nou liber.' },
                { key: 'RetentionDays', type: 'int', def: 30, min: 1, max: 3650, unit: 'zile', label: 'Păstrează cererile respinse/expirate', help: 'Apoi se șterg definitiv, cu tot cu datele personale.' }
            ]
        },
        {
            id: 'fields', icon: 'edit_note', title: 'Câmpuri',
            desc: 'Obligatorii întotdeauna: nume de utilizator, prenume, nume, e-mail, telefon (format internațional) și parolă.',
            fields: [
                { key: 'PinEnabled', type: 'bool', def: true, label: 'PIN de profil (opțional pentru utilizator)',
                    help: 'PIN-ul de profil Emby (4 cifre): aplicațiile îl cer când utilizatorul revine pe un dispozitiv pe care e deja conectat, dacă activează PIN-ul pe acel dispozitiv. Emby 4.9 nu mai are PIN de conectare doar în rețeaua locală.' },
                { key: 'DefaultCountry', type: 'select', def: 'RO', label: 'Țara implicită pentru telefon', optionsFrom: 'countries' },
                { key: 'AllowedCountries', type: 'text', def: '', label: 'Doar numere din țările', placeholder: 'RO, MD',
                    help: 'Coduri ISO separate prin virgulă. Gol: toate țările din listă.' },
                { key: 'UsernameMinLength', type: 'int', def: 3, min: 2, max: 32, unit: 'caractere', label: 'Nume de utilizator: minim' },
                { key: 'UsernameMaxLength', type: 'int', def: 32, min: 3, max: 64, unit: 'caractere', label: 'Nume de utilizator: maxim' },
                { key: 'ReservedUsernames', type: 'textarea', def: '', label: 'Nume interzise',
                    help: 'Unul pe rând, pe lângă lista inclusă (admin, root, emby, support…). Numele care conțin „admin”, „emby”, „moderator” sau „support” sunt oricum interzise.' },
                { key: 'PasswordMinLength', type: 'int', def: 10, min: 8, max: 64, unit: 'caractere', label: 'Parolă: minim' },
                { key: 'CheckPwnedPasswords', type: 'bool', def: true, label: 'Refuză parolele apărute în scurgeri de date',
                    help: 'Verificare în Have I Been Pwned prin k-anonimitate: din server pleacă doar primele 5 caractere ale hash-ului SHA-1, niciodată parola. Dacă serviciul nu răspunde, cererea merge mai departe.' }
            ]
        },
        {
            id: 'privacy', icon: 'privacy_tip', title: 'Acord și confidențialitate',
            fields: [
                { key: 'RequireConsent', type: 'bool', def: true, label: 'Bifă de acord obligatorie', help: 'Utilizatorul confirmă că datele sunt păstrate pentru gestionarea contului; data acordului se salvează cu cererea.' },
                { key: 'PrivacyText', type: 'textarea', def: '', wide: true, label: 'Text de confidențialitate',
                    help: 'Afișat sub bifă, într-o secțiune care se deschide. Text simplu. Exemplu: ce date păstrezi, cât timp, cum pot cere ștergerea.' }
            ]
        },
        {
            id: 'access', icon: 'tune', title: 'Redare',
            fields: [
                { key: 'EnableLiveTv', type: 'bool', def: false, label: 'Acces la Live TV', help: 'Dacă folosești Headend cu „dezactivat pentru utilizatorii noi”, Headend îl poate retrage imediat după creare; la aprobare plugin-ul îl aplică din nou.' },
                { key: 'StreamLimit', type: 'int', def: 1, min: 0, max: 20, unit: 'redări', label: 'Redări simultane', help: '0 = fără limită.' },
                { key: 'RemoteBitrateLimitMbps', type: 'int', def: 0, min: 0, max: 1000, unit: 'Mbps', label: 'Bitrate maxim în afara rețelei', help: '0 = fără limită.' },
                { key: 'AccountExpiryDays', type: 'int', def: 0, min: 0, max: 3650, unit: 'zile', label: 'Cont de probă: dezactivat după', help: 'Numărate de la aprobare. 0 = contul nu expiră.' },
                { key: 'EnforcePolicy', type: 'bool', def: true, label: 'Paznic de politică', help: 'Retrage drepturile interzise dacă apar la un cont gestionat. Oprit: plugin-ul le aplică doar la creare.' }
            ]
        },
        {
            id: 'multi', icon: 'devices', title: 'Un cont per persoană',
            desc: 'Adresele IP se schimbă (majoritatea sunt dinamice), așa că se compară doar pe o perioadă. Un dispozitiv se recunoaște după un identificator păstrat în browser, după conturile Emby deja conectate în el și după amprenta browserului. „Semnalează” lasă cererea să treacă, dar o marchează în tab-ul Cereri.',
            fields: [
                { key: 'DuplicateWindowDays', type: 'int', def: 30, min: 1, max: 365, unit: 'zile', label: 'Ține minte adresele cererilor', help: 'O adresă IP sau o rețea folosită la o cerere nu mai poate fi folosită atâtea zile.' },
                { key: 'SameDeviceAction', type: 'select', def: 'Block', options: ACTIONS, label: 'Același dispozitiv', help: 'Identificatorul din browser, sau browserul e deja conectat cu un cont Emby.' },
                { key: 'SameIpAction', type: 'select', def: 'Block', options: ACTIONS, label: 'Aceeași adresă IP ca altă cerere' },
                { key: 'ExistingUserIpAction', type: 'select', def: 'Block', options: ACTIONS, label: 'Aceeași adresă IP ca un cont existent', help: 'Adresa unui dispozitiv cu care s-a conectat un cont Emby existent (inclusiv cele create manual).' },
                { key: 'ExistingUserIpDays', type: 'int', def: 14, min: 1, max: 365, unit: 'zile', label: 'Dispozitive active în ultimele' },
                { key: 'SameSubnetAction', type: 'select', def: 'Flag', options: ACTIONS, label: 'Aceeași rețea (/24, /64)', help: 'Vecini la același furnizor sau aceeași casă cu adresă schimbată.' },
                { key: 'FingerprintSubnetAction', type: 'select', def: 'Block', options: ACTIONS, label: 'Același browser și aceeași rețea' },
                { key: 'FingerprintAction', type: 'select', def: 'Flag', options: ACTIONS, label: 'Același browser, altă rețea', help: 'Telefoanele de același model au adesea aceeași amprentă: implicit doar semnalat.' },
                { key: 'SamePhoneAction', type: 'select', def: 'Block', options: ACTIONS, label: 'Același număr de telefon' }
            ]
        },
        {
            id: 'visitors', icon: 'travel_explore', title: 'Doar vizitatori legitimi',
            desc: 'Verificări făcute chiar la deschiderea paginii și la trimitere. Cloudflare WARP (1.1.1.1) nu e blocat, dar adresa lui e comună multor oameni, deci regulile pe adresă doar semnalează.',
            fields: [
                { key: 'BlockDatacenters', type: 'bool', def: true, label: 'Refuză centrele de date și VPN-urile comerciale', help: 'AWS, Google Cloud, Azure, DigitalOcean, Hetzner, OVH, M247, Datacamp etc. După furnizorul adresei (baza GeoLite2-ASN a plugin-ului Jurnal de acces).' },
                { key: 'BlockTor', type: 'bool', def: true, label: 'Refuză rețeaua Tor' },
                { key: 'RequireSameOrigin', type: 'bool', def: true, label: 'Doar din pagina de înregistrare', help: 'Cererile trebuie să vină din pagina servită de acest server (antetele Origin și Sec-Fetch-Site ale browserului); un script care trimite direct la API e refuzat.' },
                { key: 'BlockAutomation', type: 'bool', def: true, label: 'Refuză browserele automatizate', help: 'Browsere conduse de programe (navigator.webdriver, Chrome headless, Puppeteer, Playwright).' },
                { key: 'RequireInteraction', type: 'bool', def: true, label: 'Cere tastare sau atingeri reale', help: 'Formularul trebuie completat de un om: evenimente de tastatură sau atingere generate de utilizator, nu de un script.' },
                { key: 'AdaptiveProofOfWork', type: 'bool', def: true, label: 'Proof-of-work mai greu când vin multe cereri', help: '+1 bit peste 5 cereri pe oră, +2 peste 15 (de 4 ori mai greu).' },
                { key: 'AllowedVisitorCountries', type: 'text', def: '', label: 'Doar din țările', placeholder: 'RO, MD', help: 'Coduri ISO, după Cloudflare. Gol: toate țările. Rețeaua locală e mereu permisă.' },
                { key: 'AsnDatabasePath', type: 'text', def: '', wide: true, label: 'Baza GeoLite2-ASN', placeholder: '/var/lib/emby/plugins/AccessLog/geoip/GeoLite2-ASN.mmdb', help: 'Gol: cea descărcată de plugin-ul Jurnal de acces.' }
            ]
        },
        {
            id: 'turnstile', icon: 'verified_user', title: 'Cloudflare Turnstile',
            desc: 'Verificare invizibilă pentru majoritatea oamenilor. Chei gratuite din panoul Cloudflare → Turnstile → Add widget (domeniul serverului). Fără chei, rămân celelalte protecții.',
            fields: [
                { key: 'TurnstileSiteKey', type: 'text', def: '', label: 'Site key', placeholder: '0x4AAAAAAA…' },
                { key: 'TurnstileSecretKey', type: 'password', def: '', label: 'Secret key', placeholder: '0x4AAAAAAA…' }
            ],
            actions: '<button type="button" class="rg-btn btnTestTurnstile"><span class="md-icon">verified</span>Verifică cheile</button>'
        },
        {
            id: 'pow', icon: 'memory', title: 'Proof-of-work și capcane',
            desc: 'Browserul face un mic calcul cât se completează formularul; un robot care trimite mii de cereri plătește pentru fiecare. Capcana (un câmp ascuns) e mereu activă.',
            fields: [
                { key: 'ProofOfWorkEnabled', type: 'bool', def: true, label: 'Proof-of-work' },
                { key: 'ProofOfWorkBits', type: 'int', def: 18, min: 8, max: 26, unit: 'biți', label: 'Dificultate', help: '18 ≈ o secundă pe un telefon; fiecare bit în plus dublează timpul.' },
                { key: 'MinFillSeconds', type: 'int', def: 4, min: 0, max: 120, unit: 'secunde', label: 'Timp minim de completare', help: 'Trimiterile mai rapide sunt refuzate (roboții completează instant).' }
            ]
        },
        {
            id: 'rate', icon: 'timer', title: 'Limite de încercări',
            desc: 'Se numără toate trimiterile, reușite sau nu. Adresa vine de la Cloudflare; firewall-ul de margine face ca ea să nu poată fi falsificată.',
            fields: [
                { key: 'MaxPerIpPerHour', type: 'int', def: 5, min: 0, max: 1000, unit: 'pe oră', label: 'De la aceeași adresă IP', help: '0 = fără limită.' },
                { key: 'MaxPerSubnetPerDay', type: 'int', def: 20, min: 0, max: 10000, unit: 'pe zi', label: 'Din aceeași rețea (/24, /64)', help: '0 = fără limită.' },
                { key: 'MaxGlobalPerHour', type: 'int', def: 30, min: 0, max: 100000, unit: 'pe oră', label: 'În total', help: '0 = fără limită.' }
            ]
        },
        {
            id: 'blocks', icon: 'block', title: 'Liste de blocare',
            fields: [
                { key: 'BlockDisposableEmail', type: 'bool', def: true, label: 'Refuză adresele de e-mail temporare', help: 'Listă inclusă cu cele mai folosite servicii (mailinator, yopmail, 10minutemail…).' },
                { key: 'BlockedEmailDomains', type: 'textarea', def: '', label: 'Domenii de e-mail refuzate', help: 'Unul pe rând; se aplică și subdomeniilor.' },
                { key: 'BlockedIps', type: 'textarea', def: '', label: 'Adrese și rețele refuzate', help: 'Una pe rând: 203.0.113.7 sau 203.0.113.0/24 sau 2001:db8::/32.' }
            ]
        },
        {
            id: 'smtp', icon: 'mail', title: 'Server de e-mail (SMTP)',
            desc: 'Pentru e-mailurile către utilizatori și către tine. Gmail: smtp.gmail.com, port 587, parolă de aplicație (Cont Google → Securitate → Parole pentru aplicații).',
            fields: [
                { key: 'SmtpHost', type: 'text', def: '', label: 'Server', placeholder: 'smtp.gmail.com' },
                { key: 'SmtpPort', type: 'int', def: 587, min: 1, max: 65535, label: 'Port', help: '587 cu STARTTLS. Portul 465 (TLS implicit) nu este suportat.' },
                { key: 'SmtpStartTls', type: 'bool', def: true, label: 'STARTTLS' },
                { key: 'SmtpUser', type: 'text', def: '', label: 'Utilizator' },
                { key: 'SmtpPassword', type: 'password', def: '', label: 'Parolă' },
                { key: 'SmtpFrom', type: 'text', def: '', label: 'Expeditor', placeholder: 'Emby <adresa@gmail.com>' }
            ],
            actions: '<button type="button" class="rg-btn btnTestEmail"><span class="md-icon">send</span>Trimite e-mail de test</button>'
        },
        {
            id: 'userMail', icon: 'mark_email_read', title: 'E-mailuri către utilizatori',
            fields: [
                { key: 'RequireEmailConfirmation', type: 'bool', def: false, label: 'Confirmarea adresei de e-mail',
                    help: 'Pornit: utilizatorul primește un link; cererea ajunge în „În așteptare” abia după ce îl deschide. Cere SMTP și adresa publică a serverului (tab-ul General).' },
                { key: 'EmailConfirmationHours', type: 'int', def: 24, min: 1, max: 720, unit: 'ore', label: 'Linkul de confirmare e valabil' },
                { key: 'SendUserEmails', type: 'bool', def: true, label: 'Anunță utilizatorul', help: 'E-mail la cerere primită, aprobată sau respinsă.' }
            ]
        },
        {
            id: 'channels', icon: 'campaign', title: 'Notificări către tine',
            fields: [
                { key: 'NotifyActivityLog', type: 'bool', def: true, label: 'Jurnalul de activitate Emby', help: 'Apare în Tabloul de bord → Activitate și în aplicațiile de administrare.' },
                { key: 'NotifyEmail', type: 'bool', def: false, label: 'E-mail' },
                { key: 'NotifyEmailTo', type: 'text', def: '', label: 'Adresele tale', help: 'Separate prin virgulă.' },
                { key: 'NotifyTelegram', type: 'bool', def: false, label: 'Telegram' },
                { key: 'TelegramBotToken', type: 'password', def: '', label: 'Token bot', help: 'De la @BotFather. Trimite un mesaj botului, apoi află chat id-ul din https://api.telegram.org/bot&lt;token&gt;/getUpdates.' },
                { key: 'TelegramChatId', type: 'text', def: '', label: 'Chat id' },
                { key: 'NotifyBatchMinutes', type: 'int', def: 0, min: 0, max: 1440, unit: 'minute', label: 'Grupează e-mail/Telegram la', help: '0 = imediat. Altfel, un singur mesaj cu tot ce s-a strâns.' }
            ],
            actions: '<button type="button" class="rg-btn btnTestTelegram"><span class="md-icon">send</span>Mesaj Telegram de test</button>'
        },
        {
            id: 'events', icon: 'rule', title: 'Când te anunț',
            fields: [
                { key: 'NotifyOnNewRequest', type: 'bool', def: true, label: 'Cerere nouă' },
                { key: 'NotifyOnEmailConfirmed', type: 'bool', def: false, label: 'Adresă de e-mail confirmată' },
                { key: 'NotifyOnDecision', type: 'bool', def: false, label: 'Cerere aprobată sau respinsă, cont expirat' },
                { key: 'NotifyOnAbuse', type: 'bool', def: true, label: 'Limită de încercări atinsă', help: 'Cel mult o dată pe zi pentru aceeași rețea.' }
            ]
        },
        {
            id: 'updates', icon: 'settings', title: 'Setări',
            fields: [
                { key: 'AutoCheckUpdates', type: 'bool', def: true, label: 'Verifică zilnic', help: 'O notificare când apare o versiune nouă. Instalarea o pornești tu.' },
                { key: 'IncludePrereleases', type: 'bool', def: false, label: 'Include versiunile de test (pre-release)' },
                { key: 'GitHubRepository', type: 'text', def: 'CristianCasapu/emby-registration', label: 'Depozit GitHub', help: 'cont/nume. Fișierele descărcate trebuie să fie semnate cu cheia autorului, altfel sunt refuzate.' }
            ]
        },
        {
            id: 'uninstall', icon: 'delete', title: 'Dezinstalare',
            desc: 'Dezinstalare: Tablou de bord → Plugin-uri → Înregistrare → Dezinstalează, apoi repornește Emby. Conturile create prin plugin rămân conturi Emby obișnuite.',
            fields: [
                { key: 'DeleteDataOnUninstall', type: 'bool', def: false, label: 'La dezinstalare șterge și datele',
                    help: 'Cererile, invitațiile, statisticile și setările plugin-ului. Oprit: rămân în /var/lib/emby/plugins/Registration și pot fi șterse manual.' }
            ]
        }
    ];

    var SETTINGS_TABS = ['general', 'form', 'access', 'antibot', 'notify', 'updates', 'maintenance'];

    var STATUS = {
        EmailPending: ['E-mail neconfirmat', 'rg-badge-warn'],
        Pending: ['În așteptare', 'rg-badge-warn'],
        Approved: ['Aprobat', 'rg-badge-ok'],
        Rejected: ['Respins', 'rg-badge-danger'],
        Expired: ['Expirat', ''],
        Deleted: ['Cont șters', '']
    };

    var STATS = {
        requests: 'Cereri primite', approved: 'Aprobate', rejected: 'Respinse', invalid: 'Câmpuri greșite', duplicate_email: 'E-mail repetat',
        rate_limited: 'Limită de încercări', blocked_ip: 'IP blocat', bot_honeypot: 'Robot: capcană', bot_token: 'Robot: fără token',
        bot_token_reuse: 'Robot: token refolosit', bot_too_fast: 'Prea rapid', bot_pow: 'Robot: proof-of-work', bot_turnstile: 'Robot: Turnstile',
        bot_origin: 'Robot: nu din pagină', bot_locked: 'Fără cod de acces', code_used: 'Coduri folosite', family_code_used: 'Cod de familie folosit', code_wrong: 'Coduri greșite',
        code_block_ip: 'Adrese blocate (cod)', code_block_device: 'Dispozitive blocate (cod)', bot_automation: 'Browser automatizat', bot_interaction: 'Fără interacțiune reală',
        network_blocked: 'VPN / centru de date / Tor', country_blocked: 'Țară nepermisă',
        dup_same_device: 'Același dispozitiv', dup_signed_in: 'Deja conectat cu un cont', dup_same_ip: 'Aceeași adresă IP',
        dup_existing_ip: 'Adresa unui cont existent', dup_same_subnet: 'Aceeași rețea', dup_fingerprint_subnet: 'Același browser și rețea',
        dup_fingerprint: 'Același browser', dup_same_phone: 'Același telefon'
    };

    function toast(text) {
        var fn = typeof toastModule === 'function' ? toastModule : (toastModule && toastModule.default);
        if (fn) { fn(text); }
    }

    function escapeHtml(value) {
        var div = document.createElement('div');
        div.textContent = value == null ? '' : String(value);
        return div.innerHTML;
    }

    function formatDate(value) {
        if (!value) { return '—'; }
        var d = new Date(value);
        return isNaN(d.getTime()) ? '—' : d.toLocaleString('ro-RO', { dateStyle: 'short', timeStyle: 'short' });
    }

    function api(type, path, data) {
        var options = { type: type, url: ApiClient.getUrl(path, type === 'GET' ? (data || {}) : {}), dataType: 'json' };
        if (type !== 'GET' && data) {
            options.data = JSON.stringify(data);
            options.contentType = 'application/json';
        }
        return ApiClient.ajax(options);
    }

    function flag(code) {
        if (!code || code.length !== 2) { return ''; }
        return String.fromCodePoint(0x1F1E6 + code.charCodeAt(0) - 65, 0x1F1E6 + code.charCodeAt(1) - 65) + ' ';
    }

    function parseColor(text) {
        var m = /rgba?\(([\d.]+),\s*([\d.]+),\s*([\d.]+)(?:,\s*([\d.]+))?\)/.exec(text || '');
        return m ? { r: +m[1], g: +m[2], b: +m[3], a: m[4] == null ? 1 : +m[4] } : null;
    }

    function mix(a, b, amount) {
        return { r: Math.round(a.r + (b.r - a.r) * amount), g: Math.round(a.g + (b.g - a.g) * amount), b: Math.round(a.b + (b.b - a.b) * amount) };
    }

    function rgb(c, alpha) {
        return alpha == null ? 'rgb(' + c.r + ',' + c.g + ',' + c.b + ')' : 'rgba(' + c.r + ',' + c.g + ',' + c.b + ',' + alpha + ')';
    }

    // Culorile urmeaza tema Emby (inchisa sau deschisa) si accentul ei.
    function applyTheme(view) {
        var page = view.querySelector('.registrationPage');
        var text = parseColor(getComputedStyle(page.parentElement || page).color) || { r: 255, g: 255, b: 255 };
        var luminance = function (x) { return 0.299 * x.r + 0.587 * x.g + 0.114 * x.b; };
        var dark = luminance(text) > 128;
        var bg = dark ? { r: 24, g: 24, b: 24 } : { r: 250, g: 250, b: 250 };
        var probe = document.createElement('button');
        probe.className = 'raised button-submit emby-button';
        probe.style.cssText = 'position:absolute;visibility:hidden;';
        page.appendChild(probe);
        var accent = parseColor(getComputedStyle(probe).backgroundColor);
        page.removeChild(probe);
        if (!accent || accent.a < 0.5 || Math.abs(luminance(accent) - luminance(bg)) < 70) {
            accent = dark ? { r: 82, g: 181, b: 75 } : { r: 46, g: 125, b: 50 };
        }
        page.style.setProperty('--rg-text', rgb(text));
        page.style.setProperty('--rg-muted', rgb(text, 0.64));
        page.style.setProperty('--rg-bg', rgb(bg));
        page.style.setProperty('--rg-card', rgb(mix(bg, text, dark ? 0.045 : 0.025)));
        page.style.setProperty('--rg-raised', rgb(mix(bg, text, dark ? 0.09 : 0.055)));
        page.style.setProperty('--rg-head', rgb(mix(mix(bg, text, dark ? 0.12 : 0.08), accent, dark ? 0.28 : 0.18)));
        page.style.setProperty('--rg-line', rgb(text, dark ? 0.14 : 0.16));
        page.style.setProperty('--rg-accent', rgb(accent));
        page.style.setProperty('--rg-accent-soft', rgb(accent, dark ? 0.18 : 0.14));
    }

    // --- Setari --------------------------------------------------------------------------------

    function fieldHtml(field) {
        var help = field.help ? '<div class="rg-help">' + field.help + '</div>' : '';
        if (field.type === 'bool') {
            return '<label class="rg-switch rg-field-wide"><input type="checkbox" data-field="' + field.key + '"><span><b>' + escapeHtml(field.label) + '</b>' + help + '</span></label>';
        }
        var control;
        var placeholder = field.placeholder ? ' placeholder="' + escapeHtml(field.placeholder) + '"' : '';
        switch (field.type) {
            case 'int':
                control = '<div class="rg-unit"><input class="rg-input" type="number" data-field="' + field.key + '" min="' + field.min + '" max="' + field.max + '" step="1">' +
                    (field.unit ? '<span class="rg-muted">' + escapeHtml(field.unit) + '</span>' : '') + '</div>';
                break;
            case 'select':
                control = '<select class="rg-select" data-field="' + field.key + '">' + (field.options || []).map(function (o) {
                    return '<option value="' + escapeHtml(o[0]) + '">' + escapeHtml(o[1]) + '</option>';
                }).join('') + '</select>';
                break;
            case 'textarea':
                control = '<textarea class="rg-textarea" data-field="' + field.key + '" rows="3"' + placeholder + '></textarea>';
                break;
            case 'password':
                control = '<input class="rg-input" type="password" autocomplete="new-password" data-field="' + field.key + '"' + placeholder + '>';
                break;
            case 'datetime':
                control = '<input class="rg-input" type="datetime-local" data-field="' + field.key + '">';
                break;
            default:
                control = '<input class="rg-input" type="text" autocomplete="off" data-field="' + field.key + '"' + placeholder + '>';
        }
        return '<div class="rg-field' + (field.wide || field.type === 'textarea' ? ' rg-field-wide' : '') + '"><label>' + escapeHtml(field.label) + '</label>' + control + help + '</div>';
    }

    function renderGroups(view) {
        GROUPS.forEach(function (group) {
            var card = view.querySelector('[data-group="' + group.id + '"]');
            if (!card || card.childElementCount) { return; }
            var toggles = group.fields.filter(function (f) { return f.type === 'bool'; });
            var values = group.fields.filter(function (f) { return f.type !== 'bool'; });
            card.innerHTML = '<h3><span class="md-icon">' + group.icon + '</span>' + escapeHtml(group.title) + '</h3>' +
                (group.desc ? '<div class="rg-card-desc">' + escapeHtml(group.desc) + '</div>' : '') +
                (toggles.length ? '<div>' + toggles.map(fieldHtml).join('') + '</div>' : '') +
                (values.length ? '<div class="rg-fields">' + values.map(fieldHtml).join('') + '</div>' : '') +
                (group.actions ? '<div class="rg-toolbar" style="margin-top:.6em">' + group.actions + '</div>' : '');
        });
    }

    function allFields() {
        return GROUPS.reduce(function (list, g) { return list.concat(g.fields); }, []);
    }

    // datetime-local lucreaza in ora locala; in configuratie pastram ISO cu fus orar.
    function toLocalInput(iso) {
        if (!iso) { return ''; }
        var d = new Date(iso);
        if (isNaN(d.getTime())) { return ''; }
        var pad = function (n) { return String(n).padStart(2, '0'); };
        return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes());
    }

    function fromLocalInput(value) {
        if (!value) { return ''; }
        var d = new Date(value);
        return isNaN(d.getTime()) ? '' : d.toISOString();
    }

    function fillSettings(instance, config) {
        var view = instance.view;
        allFields().forEach(function (field) {
            var input = view.querySelector('[data-field="' + field.key + '"]');
            if (!input) { return; }
            var value = config[field.key];
            if (value == null) { value = field.def; }
            if (field.type === 'bool') {
                input.checked = !!value;
            } else if (field.type === 'datetime') {
                input.value = toLocalInput(value);
            } else {
                input.value = value;
            }
        });
        view.querySelector('.radLibAll').checked = !!config.AllLibraries;
        view.querySelector('.radLibSome').checked = !config.AllLibraries;
        instance.selectedLibraries = (config.Libraries || []).slice();
        renderLibraries(instance);
        updateWarnings(instance);
    }

    function collectSettings(instance, config) {
        var view = instance.view;
        allFields().forEach(function (field) {
            var input = view.querySelector('[data-field="' + field.key + '"]');
            if (!input) { return; }
            switch (field.type) {
                case 'bool':
                    config[field.key] = input.checked;
                    break;
                case 'int': {
                    var v = parseInt(input.value, 10);
                    if (isNaN(v)) { v = field.def; }
                    config[field.key] = Math.min(field.max, Math.max(field.min, v));
                    break;
                }
                case 'datetime':
                    config[field.key] = fromLocalInput(input.value);
                    break;
                default:
                    config[field.key] = input.value.trim();
            }
        });
        config.AllLibraries = view.querySelector('.radLibAll').checked;
        config.Libraries = Array.prototype.map.call(view.querySelectorAll('.libraryList input:checked'), function (i) { return i.value; });
        return config;
    }

    function renderLibraries(instance) {
        var box = instance.view.querySelector('.libraryList');
        var all = instance.view.querySelector('.radLibAll').checked;
        if (!instance.libraries) {
            box.innerHTML = '<span class="rg-muted">Se încarcă…</span>';
            return;
        }
        box.innerHTML = instance.libraries.map(function (lib) {
            var checked = all || instance.selectedLibraries.indexOf(lib.Id) >= 0;
            return '<label><input type="checkbox" value="' + escapeHtml(lib.Id) + '"' + (checked ? ' checked' : '') + (all ? ' disabled' : '') + '>' +
                '<span>' + escapeHtml(lib.Name) + (lib.CollectionType ? ' <span class="rg-muted">(' + escapeHtml(lib.CollectionType) + ')</span>' : '') + '</span></label>';
        }).join('') || '<span class="rg-muted">Serverul nu are biblioteci.</span>';
    }

    function updateWarnings(instance) {
        var view = instance.view;
        var confirm = view.querySelector('[data-field="RequireEmailConfirmation"]');
        var card = view.querySelector('[data-group="userMail"]');
        var warning = card.querySelector('.rg-warning');
        if (!warning) {
            warning = document.createElement('div');
            warning.className = 'rg-warning';
            card.appendChild(warning);
        }
        var missing = [];
        if (!view.querySelector('[data-field="SmtpHost"]').value.trim() || !view.querySelector('[data-field="SmtpFrom"]').value.trim()) { missing.push('serverul SMTP și expeditorul'); }
        if (!view.querySelector('[data-field="PublicUrl"]').value.trim()) { missing.push('adresa publică a serverului (tab-ul General)'); }
        warning.hidden = !(confirm.checked && missing.length);
        warning.textContent = 'Confirmarea e-mailului nu funcționează fără: ' + missing.join(' și ') + '. Până atunci cererile merg direct la aprobare.';
    }

    function loadSettings(instance) {
        return Promise.all([
            ApiClient.getPluginConfiguration(pluginId),
            api('GET', 'Registration/Admin/Libraries')
        ]).then(function (results) {
            instance.config = results[0];
            instance.libraries = results[1];
            fillSettings(instance, instance.config);
            instance.dirty = false;
            setSaveState(instance);
        });
    }

    function saveSettings(instance) {
        loading.show();
        return ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            return ApiClient.updatePluginConfiguration(pluginId, collectSettings(instance, config));
        }).then(function (result) {
            instance.dirty = false;
            setSaveState(instance);
            loadHero(instance);
            if (result && typeof Dashboard !== 'undefined' && Dashboard.processPluginConfigurationUpdateResult) {
                Dashboard.processPluginConfigurationUpdateResult(result);
            } else {
                toast('Setările au fost salvate.');
            }
        }).catch(function () {
            toast('Setările nu au putut fi salvate.');
        }).finally(function () {
            loading.hide();
        });
    }

    function setSaveState(instance) {
        instance.view.querySelector('.saveState').textContent = instance.dirty ? 'Modificări nesalvate' : '';
    }

    // --- Cereri --------------------------------------------------------------------------------

    function statusBadge(status) {
        var s = STATUS[status] || [status, ''];
        return '<span class="rg-badge ' + s[1] + '">' + escapeHtml(s[0]) + '</span>';
    }

    function matches(view, search) {
        if (!search) { return true; }
        var r = view.Record;
        return [r.Username, r.FirstName, r.LastName, r.Email, r.Phone, r.Ip, r.IpCountry, r.Reason, r.InviteCode]
            .join(' ').toLowerCase().indexOf(search) >= 0;
    }

    function person(r) {
        return '<b>' + escapeHtml((r.FirstName + ' ' + r.LastName).trim() || '—') + '</b>' +
            '<span class="rg-sub rg-mono">' + escapeHtml(r.Username) + (r.HasPin ? ' · PIN' : '') + '</span>';
    }

    function contact(r) {
        var confirmed = r.EmailConfirmedAt ? ' <span class="rg-badge rg-badge-ok" title="Confirmat ' + escapeHtml(formatDate(r.EmailConfirmedAt)) + '">confirmat</span>' : '';
        return (r.Email ? '<a href="mailto:' + escapeHtml(r.Email) + '">' + escapeHtml(r.Email) + '</a>' + confirmed : '—') +
            '<span class="rg-sub">' + (r.Phone ? '<a href="tel:' + escapeHtml(r.Phone) + '">' + escapeHtml(r.Phone) + '</a>' : '—') + '</span>';
    }

    function flags(r) {
        return (r.Flags || []).map(function (f) { return '<span class="rg-sub" style="color:var(--rg-warn)">⚠ ' + escapeHtml(f) + '</span>'; }).join('');
    }

    function origin(r) {
        var net = r.NetworkName ? '<span class="rg-sub" title="AS' + escapeHtml(r.Asn || '') + '">' + escapeHtml(r.NetworkName) + (r.NetworkKind === 'warp' ? ' (WARP)' : '') + '</span>' : '';
        var ua = r.UserAgent ? '<span class="rg-sub" title="' + escapeHtml(r.UserAgent) + '">' + escapeHtml(shortAgent(r.UserAgent)) + '</span>' : '';
        return (r.IpCountry ? flag(r.IpCountry) : '') + '<span class="rg-mono">' + escapeHtml(r.Ip || '—') + '</span>' + net + ua;
    }

    function shortAgent(ua) {
        var os = /Android/.test(ua) ? 'Android' : /iPhone|iPad/.test(ua) ? 'iOS' : /Windows/.test(ua) ? 'Windows' : /Mac OS/.test(ua) ? 'macOS' : /Linux/.test(ua) ? 'Linux' : '';
        var browser = /Edg\//.test(ua) ? 'Edge' : /Firefox\//.test(ua) ? 'Firefox' : /Chrome\//.test(ua) ? 'Chrome' : /Safari\//.test(ua) ? 'Safari' : '';
        return [browser, os].filter(Boolean).join(' / ') || ua.slice(0, 40);
    }

    function renderRequests(instance) {
        var view = instance.view;
        var data = instance.requests;
        if (!data) { return; }
        var search = view.querySelector('.txtRequestSearch').value.trim().toLowerCase();
        var sub = instance.sub || 'waiting';
        var table = view.querySelector('.requestTable');

        view.querySelector('.countWaiting').textContent = data.Waiting.length;
        view.querySelector('.countWaiting').classList.toggle('rg-count-hot', data.Waiting.length > 0);
        view.querySelector('.countProcessed').textContent = data.Processed.length;
        view.querySelector('.waitingBulk').hidden = sub !== 'waiting';

        var filterSelect = view.querySelector('.selProcessedFilter');
        filterSelect.hidden = sub !== 'processed';
        var currentFilter = filterSelect.value;
        filterSelect.innerHTML = '<option value="">Toate (' + data.Processed.length + ')</option>' + ['Approved', 'Rejected', 'Expired', 'Deleted'].map(function (s) {
            return '<option value="' + s + '">' + STATUS[s][0] + ' (' + (data.Counts[s] || 0) + ')</option>';
        }).join('');
        filterSelect.value = currentFilter;

        var rows;
        if (sub === 'waiting') {
            rows = data.Waiting.filter(function (v) { return matches(v, search); });
            if (!rows.length) {
                table.innerHTML = '<div class="rg-empty">' + (data.Waiting.length ? 'Nicio cerere nu se potrivește căutării.' : 'Nicio cerere în așteptare.') + '</div>';
                updateBulk(instance);
                return;
            }
            table.innerHTML = '<table class="rg-table"><thead><tr>' +
                '<th><input type="checkbox" class="chkAll" aria-label="Selectează tot"></th><th>Primită</th><th>Persoană</th><th>Contact</th><th>De la</th><th>Stare</th><th>Acțiuni</th>' +
                '</tr></thead><tbody>' + rows.map(function (v) {
                    var r = v.Record;
                    var selected = instance.selected.indexOf(r.Id) >= 0;
                    return '<tr data-id="' + escapeHtml(r.Id) + '"' + (selected ? ' class="rg-selected"' : '') + '>' +
                        '<td><input type="checkbox" class="chkRow" value="' + escapeHtml(r.Id) + '"' + (selected ? ' checked' : '') + ' aria-label="Selectează"></td>' +
                        '<td class="rg-nowrap">' + escapeHtml(formatDate(r.CreatedAt)) + (r.InviteCode ? '<span class="rg-sub rg-mono">' + escapeHtml(r.InviteCode) + '</span>' : '') + '</td>' +
                        '<td>' + person(r) + '</td><td>' + contact(r) + '</td><td>' + origin(r) + '</td>' +
                        '<td>' + statusBadge(r.Status) + (!v.UserExists ? '<span class="rg-sub rg-error">contul Emby lipsește</span>' : '') + flags(r) + '</td>' +
                        '<td><div class="rg-actions-cell">' +
                        '<button type="button" class="rg-btn rg-btn-small rg-btn-primary" data-action="approve"><span class="md-icon">check</span>Aprobă</button>' +
                        '<button type="button" class="rg-btn rg-btn-small rg-btn-danger" data-action="reject"><span class="md-icon">close</span>Respinge</button>' +
                        (r.Status === 'EmailPending' ? '<button type="button" class="rg-btn rg-btn-small" data-action="resend" title="Retrimite e-mailul de confirmare"><span class="md-icon">forward_to_inbox</span></button>' : '') +
                        '</div></td></tr>';
                }).join('') + '</tbody></table>';
        } else {
            var filter = filterSelect.value;
            rows = data.Processed.filter(function (v) { return (!filter || v.Record.Status === filter) && matches(v, search); });
            if (!rows.length) {
                table.innerHTML = '<div class="rg-empty">Nicio cerere procesată.</div>';
                return;
            }
            table.innerHTML = '<table class="rg-table"><thead><tr>' +
                '<th>Decizie</th><th>Persoană</th><th>Contact</th><th>De la</th><th>Stare</th><th>Acțiuni</th>' +
                '</tr></thead><tbody>' + rows.map(function (v) {
                    var r = v.Record;
                    var detail = r.Status === 'Approved'
                        ? (v.UserExists ? (v.UserDisabled ? '<span class="rg-sub rg-error">cont dezactivat</span>' : '') : '<span class="rg-sub rg-error">contul Emby lipsește</span>') +
                          (r.AccountExpiresAt ? '<span class="rg-sub">expiră ' + escapeHtml(formatDate(r.AccountExpiresAt)) + '</span>' : '') +
                          (!r.Managed ? '<span class="rg-sub">negestionat</span>' : '')
                        : (r.Reason ? '<span class="rg-sub">' + escapeHtml(r.Reason) + '</span>' : '');
                    return '<tr data-id="' + escapeHtml(r.Id) + '">' +
                        '<td class="rg-nowrap">' + escapeHtml(formatDate(r.DecidedAt)) + '<span class="rg-sub">' + escapeHtml(r.DecidedBy || '') + '</span><span class="rg-sub">cerută ' + escapeHtml(formatDate(r.CreatedAt)) + '</span></td>' +
                        '<td>' + person(r) + '</td><td>' + contact(r) + '</td><td>' + origin(r) + '</td>' +
                        '<td>' + statusBadge(r.Status) + detail + flags(r) + '</td>' +
                        '<td><div class="rg-actions-cell">' +
                        (r.Status === 'Approved' && v.UserExists ? '<button type="button" class="rg-btn rg-btn-small" data-action="managed" title="' + (r.Managed ? 'Scoate contul de sub paznicul de politică (îi poți da drepturi în plus din Emby)' : 'Pune contul înapoi sub paznicul de politică') + '"><span class="md-icon">' + (r.Managed ? 'lock_open' : 'lock') + '</span>' + (r.Managed ? 'Eliberează' : 'Gestionează') + '</button>' : '') +
                        (r.Status === 'Approved' && v.UserExists ? '<button type="button" class="rg-btn rg-btn-small" data-action="share" title="Trimite datele de acces"><span class="md-icon">share</span></button>' : '') +
                        '<button type="button" class="rg-btn rg-btn-small" data-action="export" title="Exportă datele (JSON)"><span class="md-icon">download</span></button>' +
                        '<button type="button" class="rg-btn rg-btn-small rg-btn-danger" data-action="delete" title="Șterge cererea"><span class="md-icon">delete</span></button>' +
                        '</div></td></tr>';
                }).join('') + '</tbody></table>';
        }
        updateBulk(instance);
    }

    function updateBulk(instance) {
        var view = instance.view;
        var waitingIds = (instance.requests ? instance.requests.Waiting : []).map(function (v) { return v.Record.Id; });
        instance.selected = instance.selected.filter(function (id) { return waitingIds.indexOf(id) >= 0; });
        var count = instance.selected.length;
        view.querySelector('.btnApproveSelected').disabled = !count;
        view.querySelector('.btnRejectSelected').disabled = !count;
        view.querySelector('.btnApproveSelected').lastChild.textContent = count ? 'Aprobă (' + count + ')' : 'Aprobă selectate';
        view.querySelector('.btnRejectSelected').lastChild.textContent = count ? 'Respinge (' + count + ')' : 'Respinge selectate';
    }

    function loadRequests(instance) {
        return api('GET', 'Registration/Admin/Requests').then(function (data) {
            instance.requests = data;
            renderRequests(instance);
            renderTabCount(instance);
        });
    }

    function renderTabCount(instance) {
        var count = instance.requests ? instance.requests.Waiting.length : 0;
        var badge = instance.view.querySelector('.tabCount');
        badge.textContent = count;
        badge.classList.toggle('rg-count-hot', count > 0);
    }

    function find(instance, id) {
        var all = instance.requests.Waiting.concat(instance.requests.Processed);
        for (var i = 0; i < all.length; i++) {
            if (all[i].Record.Id === id) { return all[i].Record; }
        }
        return null;
    }

    function reportResult(result, what) {
        if (result.Failed && result.Failed.length) {
            toast(what + ': ' + result.Done + ' reușite, ' + result.Failed.length + ' eșuate (' + result.Failed.join('; ') + ')');
        } else if (result.Ok === false) {
            toast('Eroare: ' + (result.Error || 'necunoscută'));
        } else {
            toast(what + (result.Done > 1 ? ': ' + result.Done : '') + '.');
        }
    }

    function approve(instance, ids) {
        var names = ids.map(function (id) { var r = find(instance, id); return r ? r.Username : id; });
        if (ids.length > 1 && !window.confirm('Aprobi ' + ids.length + ' cereri (' + names.join(', ') + ')?')) { return; }
        loading.show();
        api('POST', 'Registration/Admin/Approve', { Ids: ids }).then(function (result) {
            reportResult(result, ids.length > 1 ? 'Aprobate' : 'Aprobat ' + names[0]);
            instance.selected = [];
            return loadRequests(instance).then(function () { loadHero(instance); });
        }).finally(function () { loading.hide(); });
    }

    function reject(instance, ids) {
        var names = ids.map(function (id) { var r = find(instance, id); return r ? r.Username : id; });
        var reason = window.prompt('Respingi ' + (ids.length > 1 ? ids.length + ' cereri (' + names.join(', ') + ')' : names[0]) +
            '. Contul dezactivat se șterge.\n\nMotiv (opțional, apare în e-mailul către utilizator):', '');
        if (reason === null) { return; }
        loading.show();
        api('POST', 'Registration/Admin/Reject', { Ids: ids, Reason: reason, NotifyUser: true }).then(function (result) {
            reportResult(result, ids.length > 1 ? 'Respinse' : 'Respins ' + names[0]);
            instance.selected = [];
            return loadRequests(instance).then(function () { loadHero(instance); });
        }).finally(function () { loading.hide(); });
    }

    function onRequestAction(instance, button) {
        var id = button.closest('tr').getAttribute('data-id');
        var record = find(instance, id);
        var action = button.getAttribute('data-action');
        if (action === 'approve') {
            approve(instance, [id]);
        } else if (action === 'reject') {
            reject(instance, [id]);
        } else if (action === 'resend') {
            api('POST', 'Registration/Admin/ResendConfirmation', { Id: id }).then(function (r) {
                toast(r.Ok ? 'E-mailul de confirmare a fost retrimis.' : 'Eroare: ' + (r.Error === 'smtp_missing' ? 'SMTP sau adresa publică lipsesc' : r.Error));
            });
        } else if (action === 'managed') {
            api('POST', 'Registration/Admin/Managed', { Id: id, Managed: !record.Managed }).then(function () {
                toast(record.Managed ? 'Contul ' + record.Username + ' nu mai e gestionat: îi poți da drepturi în plus din profilul Emby.' : 'Contul ' + record.Username + ' este din nou gestionat.');
                loadRequests(instance);
            });
        } else if (action === 'share') {
            instance.shareUserId = record.UserId;
            showTab(instance, 'share');
        } else if (action === 'export') {
            download('Registration/Admin/Export', { Id: id }, 'cerere-' + record.Username + '.json', 'application/json');
        } else if (action === 'delete') {
            var deleteUser = false;
            if (record.Status === 'Approved') {
                if (!window.confirm('Ștergi datele cererii lui ' + record.Username + ' (nume, e-mail, telefon, IP)?')) { return; }
                deleteUser = window.confirm('Ștergi și contul Emby „' + record.Username + '”?\n\nOK = da, șterge contul. Anulează = păstrează contul, șterge doar datele cererii.');
            } else if (!window.confirm('Ștergi definitiv cererea lui ' + record.Username + '?')) {
                return;
            }
            api('POST', 'Registration/Admin/Delete', { Id: id, DeleteUser: deleteUser }).then(function (r) {
                reportResult(r, 'Șters');
                loadRequests(instance);
            });
        }
    }

    // Fisierul se cere cu antetele sesiunii (fara token in adresa), apoi se salveaza local.
    function download(path, query, name, type) {
        loading.show();
        ApiClient.ajax({ type: 'GET', url: ApiClient.getUrl(path, query || {}), dataType: 'text' }).then(function (result) {
            return result && typeof result.text === 'function' ? result.text() : result;
        }).then(function (text) {
            var link = document.createElement('a');
            link.href = URL.createObjectURL(new Blob([typeof text === 'string' ? text : JSON.stringify(text, null, 2)], { type: type + ';charset=utf-8' }));
            link.download = name;
            document.body.appendChild(link);
            link.click();
            setTimeout(function () { URL.revokeObjectURL(link.href); link.remove(); }, 1000);
        }).finally(function () { loading.hide(); });
    }

    // --- Antet, invitatii, statistici, actualizari --------------------------------------------------

    function publicLink() {
        var base = (instanceConfigUrl() || ApiClient.serverAddress()).replace(/\/+$/, '');
        return base + '/emby/Registration/Page';
    }

    var currentConfig = null;

    function instanceConfigUrl() {
        return currentConfig && currentConfig.PublicUrl ? currentConfig.PublicUrl.trim() : '';
    }

    function loadHero(instance) {
        return ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            currentConfig = config;
            var chips = [];
            chips.push(config.RegistrationOpen ? '<span class="rg-chip rg-chip-on">Deschisă</span>' : '<span class="rg-chip rg-chip-off">Închisă</span>');
            chips.push('<span class="rg-chip">' + ({ Approval: 'Aprobare manuală', InviteOnly: 'Doar cu invitație', Automatic: 'Automat' }[config.Mode] || config.Mode) + '</span>');
            if (config.TurnstileSiteKey && config.TurnstileSecretKey) { chips.push('<span class="rg-chip">Turnstile</span>'); }
            if (instance.requests) { chips.push('<span class="rg-chip">' + instance.requests.Waiting.length + ' în așteptare</span>'); }
            instance.view.querySelector('.heroChips').innerHTML = chips.join('');
            var link = publicLink();
            instance.view.querySelector('.publicLink').textContent = link;
            instance.view.querySelector('.openLink').href = link;
            var notice = instance.view.querySelector('.txtLoginNotice');
            if (!notice.value) {
                notice.value = 'Nu ai cont? Cere unul la ' + link;
            }
        });
    }

    function loadInvites(instance) {
        return api('GET', 'Registration/Admin/Invites').then(function (invites) {
            var now = Date.now();
            var box = instance.view.querySelector('.inviteTable');
            if (!invites.length) {
                box.innerHTML = '<div class="rg-empty">Niciun cod generat.</div>';
                return;
            }
            box.innerHTML = '<table class="rg-table"><thead><tr><th>Cod</th><th>Notă</th><th>Utilizări</th><th>Expiră</th><th>Stare</th><th></th></tr></thead><tbody>' +
                invites.map(function (i) {
                    var expired = i.ExpiresAt && new Date(i.ExpiresAt).getTime() < now;
                    var used = i.MaxUses > 0 && i.Uses >= i.MaxUses;
                    var state = i.Revoked ? '<span class="rg-badge rg-badge-danger">Revocat</span>' : expired ? '<span class="rg-badge">Expirat</span>' : used ? '<span class="rg-badge">Folosit</span>' : '<span class="rg-badge rg-badge-ok">Valabil</span>';
                    return '<tr data-code="' + escapeHtml(i.Code) + '"><td class="rg-mono rg-nowrap"><b>' + escapeHtml(i.Code) + '</b></td><td>' + escapeHtml(i.Note || '') + '</td>' +
                        '<td>' + i.Uses + ' / ' + (i.MaxUses > 0 ? i.MaxUses : '∞') + '</td><td class="rg-nowrap">' + escapeHtml(i.ExpiresAt ? formatDate(i.ExpiresAt) : 'niciodată') + '</td>' +
                        '<td>' + state + '</td><td><div class="rg-actions-cell">' +
                        '<button type="button" class="rg-btn rg-btn-small" data-invite="copy" title="Copiază codul"><span class="md-icon">content_copy</span></button>' +
                        (!i.Revoked ? '<button type="button" class="rg-btn rg-btn-small rg-btn-danger" data-invite="revoke">Revocă</button>' : '') +
                        '</div></td></tr>';
                }).join('') + '</tbody></table>';
        });
    }

    function loadStats(instance) {
        return api('GET', 'Registration/Admin/Stats', { Days: 30 }).then(function (days) {
            var totals = {};
            days.forEach(function (d) {
                Object.keys(d.Counters).forEach(function (k) { totals[k] = (totals[k] || 0) + d.Counters[k]; });
            });
            var keys = Object.keys(STATS).filter(function (k) { return totals[k]; });
            instance.view.querySelector('.statsBox').innerHTML = keys.length
                ? '<div class="rg-kv">' + keys.map(function (k) { return '<div>' + escapeHtml(STATS[k]) + '</div><div><b>' + totals[k] + '</b></div>'; }).join('') + '</div>'
                : '<div class="rg-muted">Nimic încă.</div>';
        });
    }

    function renderUpdate(instance, s) {
        var view = instance.view;
        var rows = [
            ['Instalată', s.CurrentVersion],
            ['Pe GitHub', s.LatestVersion ? s.LatestVersion + (s.PublishedAt ? ' (' + formatDate(s.PublishedAt) + ')' : '') : '—'],
            ['Ultima verificare', formatDate(s.LastCheck)]
        ];
        if (s.InstalledPendingRestart) { rows.push(['Pe disc, după repornire', s.InstalledPendingRestart]); }
        if (s.BackupVersion) { rows.push(['Versiunea anterioară păstrată', s.BackupVersion]); }
        view.querySelector('.updateInfo').innerHTML = rows.map(function (r) { return '<div>' + escapeHtml(r[0]) + '</div><div>' + escapeHtml(r[1]) + '</div>'; }).join('') +
            (s.ReleaseUrl ? '<div>Release</div><div><a href="' + escapeHtml(s.ReleaseUrl) + '" target="_blank" rel="noopener">' + escapeHtml(s.LatestTag || s.ReleaseUrl) + '</a></div>' : '');
        var error = view.querySelector('.updateError');
        error.hidden = !s.Error;
        error.textContent = s.Error || '';
        var notes = view.querySelector('.updateNotes');
        notes.hidden = !(s.UpdateAvailable && s.ReleaseNotes);
        notes.textContent = s.ReleaseNotes || '';
        view.querySelector('.btnInstallUpdate').hidden = !s.UpdateAvailable;
        view.querySelector('.btnInstallUpdate').lastChild.textContent = 'Actualizează la ' + (s.LatestVersion || '');
        view.querySelector('.btnRollback').hidden = !s.BackupVersion;
        view.querySelector('.btnRestart').hidden = !(s.PendingRestart || s.InstalledPendingRestart);
        view.querySelector('.updateCount').hidden = !s.UpdateAvailable;
    }

    function loadUpdate(instance, refresh) {
        return api('GET', 'Registration/Admin/Update', { Refresh: !!refresh }).then(function (s) { renderUpdate(instance, s); });
    }

    function restart(instance, force) {
        api('POST', 'Registration/Admin/Restart', { Force: !!force }).then(function (r) {
            if (r.Ok) {
                toast('Emby repornește…');
                return;
            }
            if (r.Error === 'playback_active' && window.confirm(r.ActivePlayback + ' redări sunt în desfășurare și se vor opri. Repornești totuși?')) {
                restart(instance, true);
            }
        });
    }

    // --- Trimite datele de acces ----------------------------------------------------------------

    var SHARE_TEXT = {
        ro: {
            title: 'Datele tale de acces Emby ({server})',
            address: 'Adresă server', port: 'Port', user: 'Utilizator', password: 'Parolă',
            chosen: 'cea aleasă la înregistrare',
            browser: 'În browser', apps: 'În aplicațiile Emby (telefon, tabletă, TV): „Adaugă server”, apoi adresa și portul de mai sus.',
            hidden: 'Contul nu apare în lista de pe ecranul de conectare: scrie numele de utilizator.',
            change: 'Poți schimba parola din Setări → Profil după prima conectare.',
            download: 'Aplicații', subject: 'Datele tale de acces Emby'
        },
        en: {
            title: 'Your Emby sign-in details ({server})',
            address: 'Server address', port: 'Port', user: 'Username', password: 'Password',
            chosen: 'the one you chose when registering',
            browser: 'In a browser', apps: 'In the Emby apps (phone, tablet, TV): "Add server", then the address and port above.',
            hidden: 'The account is not listed on the sign-in screen: type your username.',
            change: 'You can change the password in Settings → Profile after signing in.',
            download: 'Apps', subject: 'Your Emby sign-in details'
        }
    };

    function loadShareUsers(instance) {
        return api('GET', 'Registration/Admin/Users').then(function (users) {
            instance.shareUsers = users;
            var select = instance.view.querySelector('.selShareUser');
            var current = instance.shareUserId || select.value;
            select.innerHTML = users.map(function (u) {
                var label = u.Name + (u.FirstName ? ' — ' + (u.FirstName + ' ' + (u.LastName || '')).trim() : '') + (u.Disabled ? ' (dezactivat)' : '');
                return '<option value="' + escapeHtml(u.Id) + '">' + escapeHtml(label) + '</option>';
            }).join('');
            if (current && users.some(function (u) { return u.Id === current; })) { select.value = current; }
            instance.shareUserId = null;
            onShareUserChange(instance);
        });
    }

    function shareUser(instance) {
        var id = instance.view.querySelector('.selShareUser').value;
        return (instance.shareUsers || []).filter(function (u) { return u.Id === id; })[0];
    }

    function onShareUserChange(instance) {
        var view = instance.view;
        var u = shareUser(instance);
        view.querySelector('.shareResult').hidden = true;
        if (!u) {
            view.querySelector('.shareUserInfo').textContent = 'Nu există utilizatori (administratorii nu apar aici).';
            return;
        }
        var parts = [];
        if (u.FirstName) { parts.push((u.FirstName + ' ' + (u.LastName || '')).trim()); }
        if (u.Email) { parts.push(u.Email); }
        if (u.Phone) { parts.push(u.Phone); }
        if (!u.FromRegistration) { parts.push('cont creat în afara plugin-ului'); }
        if (u.Disabled) { parts.push('ATENȚIE: contul este dezactivat'); }
        view.querySelector('.shareUserInfo').textContent = parts.join(' · ');
        view.querySelector('.selShareLang').value = u.Language === 'en' ? 'en' : 'ro';
        view.querySelector('.txtShareEmail').value = u.Email || '';
        view.querySelector('.txtSharePhone').value = u.Phone || '';
        // Pentru un cont facut manual, utilizatorul nu are neaparat o parola aleasa de el.
        view.querySelector('input[name=sharePw][value=' + (u.FromRegistration ? 'keep' : 'generate') + ']').checked = true;
        view.querySelector('.txtSharePassword').hidden = true;
    }

    function buildShareText(info, lang) {
        var t = SHARE_TEXT[lang] || SHARE_TEXT.ro;
        var base = info.PublicUrl || ApiClient.serverAddress().replace(/\/emby\/?$/, '');
        var url;
        try { url = new URL(base); } catch (e) { url = new URL(location.origin); }
        var port = url.port || (url.protocol === 'https:' ? '443' : '80');
        var origin = url.protocol + '//' + url.host;
        var lines = [
            t.title.replace('{server}', info.ServerName),
            '',
            t.address + ': ' + url.protocol + '//' + url.hostname,
            t.port + ': ' + port,
            t.user + ': ' + info.Username,
            t.password + ': ' + (info.Password || t.chosen),
            '',
            t.browser + ': ' + origin + '/web/index.html',
            t.apps,
            t.hidden
        ];
        if (info.Password) { lines.push(t.change); }
        lines.push('', t.download + ': https://emby.media/download.html');
        return lines.join('\n');
    }

    function prepareShare(instance) {
        var view = instance.view;
        var u = shareUser(instance);
        if (!u) { return; }
        var mode = view.querySelector('input[name=sharePw]:checked').value;
        var password = view.querySelector('.txtSharePassword').value;
        if (mode !== 'keep' && !window.confirm('Parola contului „' + u.Name + '” se schimbă acum în Emby; parola veche nu mai funcționează. Continui?')) { return; }
        loading.show();
        api('POST', 'Registration/Admin/AccessInfo', { UserId: u.Id, PasswordMode: mode, Password: password }).then(function (info) {
            if (!info.Ok) {
                toast({ user_missing: 'Contul nu mai există.', admin_user: 'Datele unui administrator nu se trimit de aici.', password_short: 'Parola trebuie să aibă cel puțin 8 caractere.' }[info.Error] || ('Eroare: ' + info.Error));
                return;
            }
            instance.shareInfo = info;
            var lang = view.querySelector('.selShareLang').value;
            view.querySelector('.txtShareText').value = buildShareText(info, lang);
            var warnings = [];
            if (info.Password) { warnings.push('Parola apare în clar: oricine vede conversația o poate folosi. Trimite-o doar persoanei potrivite.'); }
            if (info.Disabled) { warnings.push('Contul este dezactivat: utilizatorul nu se poate conecta până nu îl activezi.'); }
            if (!info.PublicUrl) { warnings.push('Adresa publică nu e setată (tab-ul General); am folosit adresa din care ai deschis panoul.'); }
            var box = view.querySelector('.shareWarning');
            box.hidden = !warnings.length;
            box.textContent = warnings.join(' ');
            view.querySelector('[data-share=native]').hidden = !navigator.share;
            view.querySelector('.shareResult').hidden = false;
            view.querySelector('.shareResult').scrollIntoView({ behavior: 'smooth', block: 'start' });
            if (info.Password) { toast('Parola nouă a fost setată.'); }
        }).finally(function () { loading.hide(); });
    }

    function copyText(text) {
        if (navigator.clipboard && window.isSecureContext) {
            return navigator.clipboard.writeText(text);
        }
        var area = document.createElement('textarea');
        area.value = text;
        area.style.cssText = 'position:fixed;top:-1000px;opacity:0';
        document.body.appendChild(area);
        area.select();
        var ok = document.execCommand('copy');
        area.remove();
        return ok ? Promise.resolve() : Promise.reject();
    }

    function openExternal(url) {
        var win = window.open(url, '_blank', 'noopener');
        if (!win) { location.href = url; }
    }

    function onShare(instance, kind) {
        var view = instance.view;
        var text = view.querySelector('.txtShareText').value;
        var lang = view.querySelector('.selShareLang').value;
        var subject = (SHARE_TEXT[lang] || SHARE_TEXT.ro).subject;
        var phone = view.querySelector('.txtSharePhone').value.replace(/[^\d+]/g, '');
        var enc = encodeURIComponent(text);
        switch (kind) {
            case 'copy':
                copyText(text).then(function () { toast('Textul a fost copiat.'); }, function () { window.prompt('Copiază textul:', text); });
                break;
            case 'native':
                navigator.share({ title: subject, text: text }).catch(function () { /* anulat */ });
                break;
            case 'whatsapp':
                openExternal('https://wa.me/' + phone.replace(/^\+/, '') + '?text=' + enc);
                break;
            case 'telegram':
                openExternal('https://t.me/share/url?url=' + encodeURIComponent('https://emby.media/download.html') + '&text=' + enc);
                break;
            case 'sms':
                openExternal('sms:' + phone + '?&body=' + enc);
                break;
            case 'mailto':
                openExternal('mailto:' + encodeURIComponent(view.querySelector('.txtShareEmail').value.trim()) + '?subject=' + encodeURIComponent(subject) + '&body=' + enc);
                break;
            case 'smtp': {
                var to = view.querySelector('.txtShareEmail').value.trim();
                if (!to) { toast('Scrie adresa de e-mail.'); return; }
                loading.show();
                api('POST', 'Registration/Admin/SendAccessEmail', { To: to, Subject: subject, Text: text }).then(function (r) {
                    toast(r.Ok ? 'E-mailul a fost trimis la ' + to + '.' : 'Eroare SMTP: ' + r.Error);
                }).finally(function () { loading.hide(); });
                break;
            }
        }
    }

    // --- Codul de acces ---------------------------------------------------------------------

    function renderCode(instance, data) {
        var view = instance.view;
        instance.code = data.Code;
        view.querySelector('.codeBox').textContent = data.Code;
        var enabled = instance.config && instance.config.RequireAccessCode;
        view.querySelector('.codeState').textContent = (enabled ? 'Activ.' : 'Codul nu e cerut acum (pornește „Cere cod de acces” și salvează).') +
            ' Generat ' + formatDate(data.Created) + '. Coduri greșite în ultimele 24 de ore: ' + data.WrongLastDay + '.';
        view.querySelector('.codeHistory').innerHTML = data.History.length ? '<table class="rg-table"><thead><tr><th>Cod</th><th>Generat</th><th>Folosit</th><th>De la</th></tr></thead><tbody>' +
            data.History.map(function (c) {
                return '<tr><td class="rg-mono">' + escapeHtml(c.Code) + '</td><td class="rg-nowrap">' + escapeHtml(formatDate(c.CreatedAt)) + '</td><td class="rg-nowrap">' +
                    (c.UsedAt ? escapeHtml(formatDate(c.UsedAt)) : (c.Replaced ? 'înlocuit, nefolosit' : '—')) + '</td><td>' +
                    (c.UsedByCountry ? flag(c.UsedByCountry) : '') + escapeHtml(c.UsedByIp || '') + '</td></tr>';
            }).join('') + '</tbody></table>' : '<span class="rg-muted">Niciun cod folosit încă.</span>';
        var now = Date.now();
        view.querySelector('.blockTable').innerHTML = data.Blocks.length ? '<table class="rg-table"><thead><tr><th>Ce</th><th>Cine</th><th>Blocat</th><th>Până la</th><th></th></tr></thead><tbody>' +
            data.Blocks.map(function (b) {
                var active = new Date(b.Until).getTime() > now;
                return '<tr data-block="' + escapeHtml(b.Id) + '"><td>' + (b.Kind === 'ip' ? 'Adresă IP' : 'Dispozitiv') + '<span class="rg-sub rg-mono">' + escapeHtml(b.Value) + '</span></td>' +
                    '<td>' + escapeHtml(b.Label || '') + '<span class="rg-sub">' + b.Attempts + ' coduri greșite</span></td>' +
                    '<td class="rg-nowrap">' + escapeHtml(formatDate(b.CreatedAt)) + '</td>' +
                    '<td class="rg-nowrap">' + (active ? escapeHtml(formatDate(b.Until)) : '<span class="rg-muted">expirat / deblocat</span>') + '</td>' +
                    '<td>' + (active ? '<button type="button" class="rg-btn rg-btn-small" data-unblock="1"><span class="md-icon">lock_open</span>Deblochează</button>' : '') + '</td></tr>';
            }).join('') + '</tbody></table>' : '<div class="rg-empty">Nicio blocare.</div>';
    }

    function loadCode(instance) {
        return api('GET', 'Registration/Admin/AccessCode').then(function (data) { renderCode(instance, data); });
    }

    function codeMessage(instance) {
        return 'Cod de acces pentru un cont nou pe ' + (instance.config && instance.config.ServerDisplayName || 'serverul Emby') + ': ' + instance.code +
            '\nPagina de înregistrare: ' + publicLink() + '\nCodul se poate folosi o singură dată.';
    }

    // --- Tab-uri -------------------------------------------------------------------------------

    function showTab(instance, name) {
        instance.tab = name;
        var view = instance.view;
        view.querySelectorAll('[data-tab]').forEach(function (t) { t.setAttribute('aria-selected', String(t.getAttribute('data-tab') === name)); });
        view.querySelectorAll('[data-panel]').forEach(function (p) { p.hidden = p.getAttribute('data-panel') !== name; });
        view.querySelector('.rg-savebar').hidden = SETTINGS_TABS.indexOf(name) < 0;
        if (name === 'requests') {
            loadRequests(instance);
        } else if (name === 'invites') {
            loadInvites(instance);
        } else if (name === 'share') {
            loadShareUsers(instance);
        } else if (name === 'updates') {
            loadUpdate(instance, false);
        } else if (name === 'maintenance') {
            loadStats(instance);
        } else if (name === 'general') {
            loadCode(instance);
            api('GET', 'Registration/Admin/LoginButton').then(function (r) {
                var box = view.querySelector('.loginButtonState');
                box.textContent = r.Ok ? 'instalat' : 'neinstalat';
                box.className = 'loginButtonState ' + (r.Ok ? '' : 'rg-error');
            });
            api('GET', 'Registration/Admin/LoginNotice').then(function (r) {
                view.querySelector('.noticeState').textContent = r.Text ? 'Afișat acum: „' + r.Text + '”' : 'Nimic afișat de plugin.';
                if (r.Text) { view.querySelector('.txtLoginNotice').value = r.Text; }
            });
        }
    }

    function View(view, params) {
        BaseView.apply(this, arguments);
        var instance = this;
        instance.selected = [];
        instance.sub = 'waiting';
        renderGroups(view);

        var countrySelect = view.querySelector('[data-field="DefaultCountry"]');
        countrySelect.innerHTML = [['RO', 'România'], ['MD', 'Republica Moldova'], ['AT', 'Austria'], ['BE', 'Belgia'], ['BG', 'Bulgaria'], ['CA', 'Canada'], ['CH', 'Elveția'],
            ['CY', 'Cipru'], ['CZ', 'Cehia'], ['DE', 'Germania'], ['DK', 'Danemarca'], ['ES', 'Spania'], ['FR', 'Franța'], ['GB', 'Regatul Unit'], ['GR', 'Grecia'],
            ['HU', 'Ungaria'], ['IE', 'Irlanda'], ['IT', 'Italia'], ['NL', 'Țările de Jos'], ['NO', 'Norvegia'], ['PL', 'Polonia'], ['PT', 'Portugalia'], ['SE', 'Suedia'],
            ['TR', 'Turcia'], ['UA', 'Ucraina'], ['US', 'Statele Unite']].map(function (c) {
            return '<option value="' + c[0] + '">' + flag(c[0]) + c[1] + '</option>';
        }).join('');

        view.querySelector('.rg-tabs').addEventListener('click', function (e) {
            var tab = e.target.closest('[data-tab]');
            if (tab) { showTab(instance, tab.getAttribute('data-tab')); }
        });

        view.querySelector('.rg-subtabs').addEventListener('click', function (e) {
            var sub = e.target.closest('[data-sub]');
            if (!sub) { return; }
            instance.sub = sub.getAttribute('data-sub');
            view.querySelectorAll('[data-sub]').forEach(function (s) { s.setAttribute('aria-selected', String(s === sub)); });
            renderRequests(instance);
        });

        var searchTimer = null;
        view.querySelector('.txtRequestSearch').addEventListener('input', function () {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(function () { renderRequests(instance); }, 200);
        });
        view.querySelector('.selProcessedFilter').addEventListener('change', function () { renderRequests(instance); });
        view.querySelector('.btnRefresh').addEventListener('click', function () { loadRequests(instance); });
        view.querySelector('.btnCsv').addEventListener('click', function () {
            download('Registration/Admin/Requests/Csv', null, 'inregistrari-' + new Date().toISOString().slice(0, 10) + '.csv', 'text/csv');
        });

        var table = view.querySelector('.requestTable');
        table.addEventListener('click', function (e) {
            var button = e.target.closest('[data-action]');
            if (button) { onRequestAction(instance, button); }
        });
        table.addEventListener('change', function (e) {
            if (e.target.classList.contains('chkAll')) {
                var boxes = table.querySelectorAll('.chkRow');
                instance.selected = e.target.checked ? Array.prototype.map.call(boxes, function (b) { return b.value; }) : [];
                renderRequests(instance);
            } else if (e.target.classList.contains('chkRow')) {
                var id = e.target.value;
                instance.selected = instance.selected.filter(function (x) { return x !== id; });
                if (e.target.checked) { instance.selected.push(id); }
                e.target.closest('tr').classList.toggle('rg-selected', e.target.checked);
                updateBulk(instance);
            }
        });
        view.querySelector('.btnApproveSelected').addEventListener('click', function () { approve(instance, instance.selected.slice()); });
        view.querySelector('.btnRejectSelected').addEventListener('click', function () { reject(instance, instance.selected.slice()); });

        var form = view.querySelector('.settingsForm');
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            saveSettings(instance);
            return false;
        });
        form.addEventListener('input', function () {
            instance.dirty = true;
            setSaveState(instance);
            updateWarnings(instance);
        });
        form.addEventListener('change', function (e) {
            instance.dirty = true;
            setSaveState(instance);
            updateWarnings(instance);
            if (e.target.name === 'libMode') {
                instance.selectedLibraries = Array.prototype.map.call(view.querySelectorAll('.libraryList input:checked'), function (i) { return i.value; });
                if (e.target.value === 'some' && instance.config && instance.config.AllLibraries) {
                    instance.selectedLibraries = (instance.config.Libraries || []).slice();
                }
                renderLibraries(instance);
            }
        });

        view.querySelector('.btnCopyCode').addEventListener('click', function () {
            copyText(instance.code || '').then(function () { toast('Codul a fost copiat.'); }, function () { window.prompt('Codul:', instance.code); });
        });
        view.querySelector('.btnShareCode').addEventListener('click', function () {
            var text = codeMessage(instance);
            if (navigator.share) {
                navigator.share({ text: text }).catch(function () { /* anulat */ });
                return;
            }
            copyText(text).then(function () { toast('Mesajul cu codul a fost copiat.'); }, function () { window.prompt('Mesajul:', text); });
        });
        view.querySelector('.btnWhatsappCode').addEventListener('click', function () {
            openExternal('https://wa.me/?text=' + encodeURIComponent(codeMessage(instance)));
        });
        view.querySelector('.btnNewCode').addEventListener('click', function () {
            if (!window.confirm('Generezi alt cod? Codul ' + instance.code + ' nu va mai funcționa.')) { return; }
            api('POST', 'Registration/Admin/AccessCode/Replace').then(function (data) {
                renderCode(instance, data);
                toast('Cod nou: ' + data.Code);
            });
        });
        view.querySelector('.blockTable').addEventListener('click', function (e) {
            var button = e.target.closest('[data-unblock]');
            if (!button) { return; }
            var id = button.closest('tr').getAttribute('data-block');
            api('POST', 'Registration/Admin/Unblock', { Id: id }).then(function (r) {
                toast(r.Ok ? 'Deblocat.' : 'Eroare: ' + r.Error);
                loadCode(instance);
            });
        });
        view.querySelector('.btnTestTurnstile').addEventListener('click', function () {
            if (instance.dirty) { toast('Salvează întâi setările.'); return; }
            loading.show();
            api('POST', 'Registration/Admin/TestTurnstile').then(function (r) {
                toast(r.Ok ? r.Text : 'Turnstile: ' + r.Error);
            }).finally(function () { loading.hide(); });
        });

        view.querySelector('.btnCopyLink').addEventListener('click', function () {
            var text = view.querySelector('.publicLink').textContent;
            (navigator.clipboard ? navigator.clipboard.writeText(text) : Promise.reject()).then(function () {
                toast('Linkul a fost copiat.');
            }, function () {
                window.prompt('Copiază linkul:', text);
            });
        });

        view.querySelector('.btnSetNotice').addEventListener('click', function () {
            var text = view.querySelector('.txtLoginNotice').value.trim();
            api('POST', 'Registration/Admin/LoginNotice', { Text: text }).then(function () {
                toast('Textul apare pe ecranul de conectare.');
                showTab(instance, 'general');
            });
        });
        view.querySelector('.btnClearNotice').addEventListener('click', function () {
            api('POST', 'Registration/Admin/LoginNotice', { Text: '' }).then(function () {
                toast('Textul a fost scos de pe ecranul de conectare.');
                showTab(instance, 'general');
            });
        });

        view.querySelector('.btnTestEmail').addEventListener('click', function () {
            if (instance.dirty) { toast('Salvează întâi setările.'); return; }
            var to = window.prompt('Trimite e-mailul de test la adresa:', (currentConfig && currentConfig.NotifyEmailTo || '').split(',')[0].trim());
            if (!to) { return; }
            loading.show();
            api('POST', 'Registration/Admin/TestEmail', { To: to }).then(function (r) {
                toast(r.Ok ? 'E-mailul de test a fost trimis la ' + to + '.' : 'Eroare SMTP: ' + r.Error);
            }).finally(function () { loading.hide(); });
        });
        view.querySelector('.btnTestTelegram').addEventListener('click', function () {
            if (instance.dirty) { toast('Salvează întâi setările.'); return; }
            loading.show();
            api('POST', 'Registration/Admin/TestTelegram').then(function (r) {
                toast(r.Ok ? 'Mesajul de test a fost trimis.' : 'Eroare Telegram: ' + r.Error);
            }).finally(function () { loading.hide(); });
        });

        view.querySelector('.selShareUser').addEventListener('change', function () { onShareUserChange(instance); });
        view.querySelector('.selShareLang').addEventListener('change', function () {
            if (instance.shareInfo && !view.querySelector('.shareResult').hidden) {
                view.querySelector('.txtShareText').value = buildShareText(instance.shareInfo, view.querySelector('.selShareLang').value);
            }
        });
        view.querySelectorAll('input[name=sharePw]').forEach(function (radio) {
            radio.addEventListener('change', function () {
                view.querySelector('.txtSharePassword').hidden = radio.value !== 'set' || !radio.checked;
            });
        });
        view.querySelector('.btnPrepareShare').addEventListener('click', function () { prepareShare(instance); });
        view.querySelector('.shareResult').addEventListener('click', function (e) {
            var button = e.target.closest('[data-share]');
            if (button) { onShare(instance, button.getAttribute('data-share')); }
        });

        view.querySelector('.btnCreateInvite').addEventListener('click', function () {
            api('POST', 'Registration/Admin/Invites', {
                Note: view.querySelector('.txtInviteNote').value.trim(),
                MaxUses: parseInt(view.querySelector('.txtInviteUses').value, 10) || 0,
                ValidDays: parseInt(view.querySelector('.txtInviteDays').value, 10) || 0
            }).then(function (invite) {
                view.querySelector('.txtInviteNote').value = '';
                toast('Cod nou: ' + invite.Code);
                loadInvites(instance);
            });
        });
        view.querySelector('.inviteTable').addEventListener('click', function (e) {
            var button = e.target.closest('[data-invite]');
            if (!button) { return; }
            var code = button.closest('tr').getAttribute('data-code');
            if (button.getAttribute('data-invite') === 'copy') {
                (navigator.clipboard ? navigator.clipboard.writeText(code) : Promise.reject()).then(function () { toast('Codul a fost copiat.'); }, function () { window.prompt('Codul:', code); });
            } else if (window.confirm('Revoci codul ' + code + '?')) {
                api('POST', 'Registration/Admin/Invites/Revoke', { Code: code }).then(function () { loadInvites(instance); });
            }
        });

        view.querySelector('.btnCheckUpdate').addEventListener('click', function () {
            loading.show();
            loadUpdate(instance, true).finally(function () { loading.hide(); });
        });
        view.querySelector('.btnInstallUpdate').addEventListener('click', function () {
            if (!window.confirm('Descarc și instalez versiunea nouă? Se încarcă la repornirea Emby.')) { return; }
            loading.show();
            api('POST', 'Registration/Admin/Update/Install').then(function (r) {
                toast(r.Ok ? 'Versiunea nouă a fost instalată. Repornește Emby ca să se încarce.' : 'Actualizarea a eșuat: ' + r.Error);
                return loadUpdate(instance, false);
            }).finally(function () { loading.hide(); });
        });
        view.querySelector('.btnRollback').addEventListener('click', function () {
            if (!window.confirm('Revii la versiunea anterioară? Se încarcă la repornirea Emby.')) { return; }
            api('POST', 'Registration/Admin/Update/Rollback').then(function (r) {
                toast(r.Ok ? 'Versiunea anterioară a fost pusă la loc. Repornește Emby.' : 'Eroare: ' + r.Error);
                loadUpdate(instance, false);
            });
        });
        view.querySelector('.btnRestart').addEventListener('click', function () {
            if (window.confirm('Repornești Emby acum?')) { restart(instance, false); }
        });

        view.querySelector('.btnPurge').addEventListener('click', function () {
            var answer = window.prompt('Scrie STERGE ca să ștergi toate cererile, invitațiile și statisticile plugin-ului:');
            if (answer !== 'STERGE') { return; }
            api('POST', 'Registration/Admin/Purge', { Confirm: 'STERGE' }).then(function (r) {
                toast(r.Ok ? 'Datele plugin-ului au fost șterse.' : 'Eroare: ' + r.Error);
                loadStats(instance);
                loadRequests(instance);
            });
        });
    }

    Object.assign(View.prototype, BaseView.prototype);

    View.prototype.onResume = function (options) {
        BaseView.prototype.onResume.apply(this, arguments);
        var instance = this;
        applyTheme(instance.view);
        loadSettings(instance).then(function () {
            return loadRequests(instance);
        }).then(function () {
            loadHero(instance);
        });
        api('GET', 'Registration/Admin/Update').then(function (s) { renderUpdate(instance, s); });
        showTab(instance, instance.tab || 'requests');
    };

    return View;
});
