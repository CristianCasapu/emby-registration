// Butonul „Creeaza cont” pe ecranul de conectare al interfetei web Emby.
// Se incarca din index.html-ul Emby (tools/install-login-button.sh). Apare doar pe paginile
// de conectare si doar cat timp inregistrarea e deschisa in plugin.
(function () {
    'use strict';

    var base = new URL('../emby/Registration/', document.baseURI);
    var state = { open: false, checked: 0, pending: null };
    var timer = null;

    function english() {
        var lang = (document.documentElement.lang || navigator.language || 'ro').toLowerCase();
        return lang.indexOf('ro') !== 0;
    }

    function isOpen() {
        if (Date.now() - state.checked < 60000) {
            return Promise.resolve(state.open);
        }
        if (!state.pending) {
            state.pending = fetch(new URL('Status', base), { credentials: 'omit', cache: 'no-store' })
                .then(function (r) { return r.ok ? r.json() : { Enabled: false }; })
                .then(function (status) { state.open = !!status.Enabled; })
                .catch(function () { state.open = false; })
                .then(function () { state.checked = Date.now(); state.pending = null; return state.open; });
        }
        return state.pending;
    }

    function onLoginPage() {
        return /startup\/(manual)?login/i.test(location.hash + location.pathname);
    }

    function activeView() {
        var views = document.querySelectorAll('.view');
        for (var i = views.length - 1; i >= 0; i--) {
            if (!views[i].classList.contains('hide') && views[i].offsetParent !== null) {
                return views[i];
            }
        }
        return null;
    }

    function button() {
        var box = document.createElement('div');
        box.className = 'registrationSignup';
        box.style.cssText = 'display:flex;flex-direction:column;align-items:center;gap:.6em;margin:1.6em auto 1em;max-width:28em;width:100%;padding:0 1em;box-sizing:border-box;text-align:center;';
        var text = document.createElement('div');
        text.className = 'secondaryText';
        text.textContent = english() ? "Don't have an account?" : 'Nu ai cont?';
        var link = document.createElement('a');
        link.href = new URL('Page', base).href;
        link.className = 'raised button-submit emby-button block';
        link.style.cssText = 'display:flex;align-items:center;justify-content:center;gap:.5em;width:100%;box-sizing:border-box;text-decoration:none;padding:.9em 1em;border-radius:.3em;font-weight:600;';
        link.setAttribute('is', 'emby-linkbutton');
        var icon = document.createElement('i');
        icon.className = 'md-icon';
        icon.setAttribute('aria-hidden', 'true');
        icon.textContent = 'person_add';
        var label = document.createElement('span');
        label.textContent = english() ? 'Create an account' : 'Creează cont nou';
        link.appendChild(icon);
        link.appendChild(label);
        box.appendChild(text);
        box.appendChild(link);
        return box;
    }

    function update() {
        timer = null;
        if (!onLoginPage()) {
            return;
        }
        var view = activeView();
        if (!view || view.querySelector('.registrationSignup')) {
            return;
        }
        isOpen().then(function (open) {
            var target = activeView();
            if (!open || !target || target !== view || target.querySelector('.registrationSignup')) {
                return;
            }
            var slider = target.querySelector('.scrollSlider') || target;
            var disclaimer = slider.querySelector('.disclaimer');
            if (disclaimer) {
                disclaimer.parentNode.insertBefore(button(), disclaimer);
            } else {
                slider.appendChild(button());
            }
        });
    }

    function schedule() {
        if (!timer) {
            timer = setTimeout(update, 150);
        }
    }

    new MutationObserver(schedule).observe(document.documentElement, { childList: true, subtree: true });
    window.addEventListener('hashchange', schedule);
    window.addEventListener('popstate', schedule);
    schedule();
})();
