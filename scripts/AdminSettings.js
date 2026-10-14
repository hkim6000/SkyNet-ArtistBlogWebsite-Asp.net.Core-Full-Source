var AdminSettingsJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('AdminSettings/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=/; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('as-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('as-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('as-open');
        }, 3000);
    }

    function openNav() {
        el('as-nav').classList.add('as-open');
        el('as-scrim').classList.add('as-open');
    }

    function closeNav() {
        el('as-nav').classList.remove('as-open');
        el('as-scrim').classList.remove('as-open');
    }

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('ab_tok') });
        call(method, list);
    }

    function expired() {
        setCookie('ab_tok', '', 0);
        location.replace('AdminLogin');
    }

    function signOut() {
        api('SignOut', []);
    }

    function out() {
        setCookie('ab_tok', '', 0);
        location.href = 'AdminLogin';
    }

    function openModal() {
        el('as-modal').classList.add('as-open');
    }

    function closeModal() {
        var m = el('as-modal');
        if (m) {
            m.classList.remove('as-open');
        }
    }

    function loaded() {
        preview();
    }

    function preview() {
        var m = el('as-mock');
        if (!m) {
            return;
        }
        el('as-mock-name').textContent = val('as-name') || 'Your name';
        el('as-mock-tag').textContent = val('as-tagline');
        el('as-mock-btn').style.background = el('as-accent').value;
        var ins = document.querySelectorAll('.as-sr .as-su');
        for (var i = 0; i < ins.length; i++) {
            var k = ins[i].getAttribute('data-k');
            var on = el('as-v-' + k).checked;
            ins[i].closest('.as-sr').classList.toggle('as-off', !on);
            var ic = el('as-mi-' + k);
            if (ic) {
                ic.hidden = !on || ins[i].value.trim() === '';
            }
        }
        var dark = el('as-theme-dark').checked;
        m.classList.toggle('as-dk', dark);
    }

    function swatch(c) {
        el('as-accent').value = c;
        preview();
    }

    function save() {
        api('Save', [
            { key: 'name', vlu: val('as-name') },
            { key: 'tagline', vlu: val('as-tagline') },
            { key: 'accent', vlu: el('as-accent').value },
            { key: 'theme', vlu: el('as-theme-dark').checked ? 'dark' : 'light' },
            { key: 'email', vlu: val('as-email') },
            { key: 'social', vlu: JSON.stringify(links()) },
            { key: 'location', vlu: val('as-location') }
        ]);
        return false;
    }

    function links() {
        var ins = document.querySelectorAll('.as-sr .as-su');
        var out = [];
        for (var i = 0; i < ins.length; i++) {
            var k = ins[i].getAttribute('data-k');
            out.push({ key: k, url: ins[i].value.trim(), show: el('as-v-' + k).checked });
        }
        return out;
    }

    function hash() {
        api('Hash', [{ key: 'secret', vlu: el('as-secret').value }]);
    }

    function reveal() {
        if (!getCookie('ab_tok')) {
            location.replace('AdminLogin');
            return;
        }
        api('Load', []);
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
                closeModal();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('as-', 'as-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        signOut: signOut,
        out: out,
        expired: expired,
        openModal: openModal,
        closeModal: closeModal,
        loaded: loaded,
        preview: preview,
        swatch: swatch,
        save: save,
        hash: hash
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminSettingsJs.reveal);
} else {
    AdminSettingsJs.reveal();
}
