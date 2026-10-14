var AdminJs = (function () {

    var ttimer = null;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Admin/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=/; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ad-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ad-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ad-open');
        }, 3000);
    }

    function openNav() {
        el('ad-nav').classList.add('ad-open');
        el('ad-scrim').classList.add('ad-open');
    }

    function closeNav() {
        el('ad-nav').classList.remove('ad-open');
        el('ad-scrim').classList.remove('ad-open');
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
        el('ad-modal').classList.add('ad-open');
    }

    function closeModal() {
        var m = el('ad-modal');
        if (m) {
            m.classList.remove('ad-open');
        }
    }

    function loaded() {
    }

    function undo() {
        if (confirm('Restore the version before your last save?')) {
            api('Undo', []);
        }
    }

    function reset() {
        if (confirm('Start over from the original demo site? Your changes will be replaced.')) {
            api('Reset', []);
        }
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
            var id = e.target && e.target.id ? e.target.id.replace('ad-', 'ad-e-') : '';
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
        undo: undo,
        reset: reset
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminJs.reveal);
} else {
    AdminJs.reveal();
}
