var AdminLoginJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('AdminLogin/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=/; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('al-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('al-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('al-open');
        }, 3000);
    }

    function openNav() {
        el('al-nav').classList.add('al-open');
        el('al-scrim').classList.add('al-open');
    }

    function closeNav() {
        el('al-nav').classList.remove('al-open');
        el('al-scrim').classList.remove('al-open');
    }

    function signIn() {
        call('SignIn', [
            { key: 'user', vlu: val('al-user') },
            { key: 'pass', vlu: el('al-pass').value },
            { key: 'sbx', vlu: getCookie('ab_sbx') }
        ]);
        return false;
    }

    function signedIn(tok, sbx, secs) {
        setCookie('ab_tok', tok, secs);
        if (sbx) {
            setCookie('ab_sbx', sbx, secs);
        }
        location.href = 'Admin';
    }

    function reveal() {
        document.addEventListener('input', function () {
            el('al-e-pass').textContent = '';
        });
    }

    return {
        reveal: reveal,
        signIn: signIn,
        signedIn: signedIn
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminLoginJs.reveal);
} else {
    AdminLoginJs.reveal();
}
